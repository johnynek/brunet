/*
Copyright (C) 2009 Pierre St Juste <ptony82@ufl.edu>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

using Brunet;
using Brunet.Applications;
using Brunet.DistributedServices;
using Ipop.RpcNode;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  /**
   * SocialNode Class. Extends the RpcIpopNode to support adding friends based
   * on X509 certificates.
   */
  public class SocialNode : RpcIpopNode {

    /**
     * Dictionary of friends indexed by alias.
     */
    protected Dictionary<string, SocialUser> _friends;

    /**
     * The local user.
     */
    protected SocialUser _local_user;

    /**
     * The local user certificate.
     */
    protected Certificate _local_cert;

    /**
     * The identity provider and the social network.
     */
    protected SocialNetworkProvider _snp;

    /**
     * The connection manager.
     */
    protected SocialConnectionManager _scm;

    /**
     * Dictionary representing friends in the system.
     */
    public Dictionary<string, SocialUser> Friends { get { return _friends; } }

    /**
     * The local user object.
     */
    public SocialUser LocalUser { get { return _local_user; } }

    /**
     * Constructor.
     * @param brunetConfig configuration file for Brunet P2P library.
     * @param ipopConfig configuration file for IP over P2P app.
     */
    public SocialNode(string brunetConfig, string ipopConfig) :
      base(brunetConfig, ipopConfig) {
      _friends = new Dictionary<string, SocialUser>();
      string cert_path = Path.Combine("certificates", "lc.cert");
      _local_cert = new Certificate(SocialUtils.ReadFileBytes(cert_path));
      _local_user = new SocialUser(_local_cert);
      _bso.CertificateHandler.AddCACertificate(_local_cert.X509);
      _bso.CertificateHandler.AddSignedCertificate(_local_cert.X509);
      _snp = new SocialNetworkProvider(this.Dht, _local_user);
      _scm = new SocialConnectionManager(this, _snp, _snp);
      _local_user.Alias = "localhost";
    }

    /**
     * Create a unique alias for a user resource.
     * @param uid the user unique identifier.
     * @param pcid the pc identifier.
     * @return a unique user alias used for DNS naming.
     */
    protected virtual string CreateAlias(string uid, string pcid) {
      uid = uid.Replace('@', '.');
      string alias = (pcid + "." + uid + ".ipop").ToLower();
      int counter = 1;
      while(_friends.ContainsKey(alias)) {
        alias = (pcid + counter + "." + uid + ".ipop").ToLower();
        counter++;
      }
      return alias;
    }

    /**
     * Add local certificate to the DHT.
     */
    public void PublishCertificate() {
      byte[] key_bytes = Encoding.UTF8.GetBytes(_local_user.DhtKey);
      MemBlock keyb = MemBlock.Reference(key_bytes);
      MemBlock value = MemBlock.Reference(_local_cert.X509.RawData);

      Channel q = new Channel();
      q.CloseAfterEnqueue();
      q.CloseEvent += delegate(Object o, EventArgs eargs) {
        try {
          bool success = (bool) (q.Dequeue());
          if(success) {
            ProtocolLog.Write(SocialLog.SVPNLog,"Dht Put successful");
          }
        } catch (Exception e) {
          ProtocolLog.Write(SocialLog.SVPNLog,e.Message);
          ProtocolLog.Write(SocialLog.SVPNLog,"Dht Put failed");
        }
      };
      this.Dht.AsPut(keyb, value, 3600, q);

      ProtocolLog.Write(SocialLog.SVPNLog,"DHT key: " + _local_user.DhtKey);
    }

    /**
     * Add a friend to socialvpn from an X509 certificate.
     * @param certData the X509 certificate as a byte array.
     */
    public void AddCertificate(byte[] certData) {
      Certificate cert = new Certificate(certData);
      SocialUser friend = new SocialUser(cert);

      if(friend.DhtKey == _local_user.DhtKey || 
         _friends.ContainsKey(friend.DhtKey)) {
        return;
      }

      friend.Alias = CreateAlias(friend.Uid, friend.PCID);
      friend.Access = SocialUser.AccessTypes.Unapproved.ToString();

      // Save certificate to file system
      SocialUtils.SaveCertificate(cert);

      // Add certificates to handler
      _bso.CertificateHandler.AddCACertificate(cert.X509);
      _bso.CertificateHandler.AddSignedCertificate(cert.X509);

      // Add friend to list
      _friends.Add(friend.DhtKey, friend);

      // Temporary
      AddFriend(friend);

      ProtocolLog.Write(SocialLog.SVPNLog,cert.ToString());
      ProtocolLog.Write(SocialLog.SVPNLog,"Friend Info: " + friend.IP + 
                        " " + friend.Alias);
    }

    /**
     * Add friend by retreiving certificate from DHT.
     * @param key the DHT key for friend's certificate.
     */
    public void AddDhtFriend(string key) {
      // Do not retreive current user's key
      if(key == _local_user.DhtKey || _friends.ContainsKey(key)) {
        return;
      }

      Channel q = new Channel();
      q.CloseAfterEnqueue();
      q.CloseEvent += delegate(Object o, EventArgs eargs) {
        try {
          DhtGetResult dgr = (DhtGetResult) q.Dequeue();
          byte[] certData = dgr.value;
          string[] parts = key.Split(':');
          string fingerprint = parts[2];
          // Only add if fingerprints match
          if(fingerprint == SocialUtils.GetMD5(certData)) {
            AddCertificate(certData);
          }
          else {
            ProtocolLog.Write(SocialLog.SVPNLog, "Fingerprint mismatch: " +
                              key);
          }
        } catch (Exception e) {
          ProtocolLog.Write(SocialLog.SVPNLog,e.Message);
          ProtocolLog.Write(SocialLog.SVPNLog,"Certificate not found: " + 
                            key);
        }
      };
      this.Dht.AsGet(key, q);
    }

    /*
     * Add a friend from socialvpn.
     * @param friend the friend to be added.
     */
    public void AddFriend(SocialUser friend) {
      Address addr = AddressParser.Parse(friend.Address);
      friend.IP = _rarad.RegisterMapping(friend.Alias, addr);
      _node.ManagedCO.AddAddress(addr);
      friend.Access = SocialUser.AccessTypes.Approved.ToString();
    }

    /*
     * Removes a friend from socialvpn.
     * @param friend the friend to be removed.
     */
    public void RemoveFriend(SocialUser friend) {
      Address addr = AddressParser.Parse(friend.Address);
      _node.ManagedCO.RemoveAddress(addr);
      _rarad.UnregisterMapping(friend.Alias);
      friend.Access = SocialUser.AccessTypes.Denied.ToString();
    }

    public static new void Main(string[] args) {
      SocialUtils.BrunetConfig = args[0];
      NodeConfig config = Utils.ReadConfig<NodeConfig>(args[0]);
      if(!System.IO.File.Exists(config.Security.KeyPath)) {
        Console.Write("Enter Name (First Last): ");
        string name = Console.ReadLine();
        Console.Write("Enter Email Address: ");
        string uid = Console.ReadLine();
        Console.Write("Enter a name for this PC: ");
        string pcid = Console.ReadLine();
        string version = "SVPN_0.3.0";
        string country = "US";

        SocialUtils.CreateCertificate(uid, name, pcid, version, country);
      }

      SocialNode node = new SocialNode(args[0], args[1]);
      node.Run();
    }
  }
}
