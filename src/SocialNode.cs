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
   * on X509 certificates
   */
  public class SocialNode : RpcIpopNode {

    /**
     * Dictionary of friends indexed by alias.
     */
    private Dictionary<string, SocialUser> _friends;

    /**
     * The local user.
     */
    private SocialUser _local_user;

    /**
     * The local user certificate.
     */
    private Certificate _local_cert;

    /**
     * The collection of friends
     */
    public ICollection Friends { get { return _friends.Values; } }
    /**
     * Constructor.
     * @param brunetConfig configuration file for Brunet P2P library.
     * @param ipopConfig configuration file for IP over P2P app.
     */
    public SocialNode(string brunetConfig, string ipopConfig) :
      base(brunetConfig, ipopConfig) {
      _friends = new Dictionary<string, SocialUser>();
      NodeConfig config = 
        Utils.ReadConfig<NodeConfig>(SocialUtils.BrunetConfig);
      string lc_path = Path.Combine(config.Security.CertificatePath,
                                    "lc.cert");
      _local_cert = new Certificate(SocialUtils.ReadFileBytes(lc_path));
      _local_user = new SocialUser(_local_cert);
      Init();
    }

    /**
     * Add local certificates to security handler and stores to DHT
     */
    public void Init() {
      _bso.CertificateHandler.AddCertificate("lc.cert");
      _bso.CertificateHandler.AddCertificate("ca.cert");
      DhtPublishCert();
    }

    /**
     * Add a friend to socialvpn from an X509 certificate.
     * @param certData the X509 certificate as a byte array.
     * @param fingerprint the X509 certificate fingerprint.
     */
    public void AddFriend(byte[] certData, string fingerprint) {
      Certificate cert = new Certificate(certData);
      SocialUser friend = new SocialUser(cert);
      Address addr = AddressParser.Parse(friend.Address);
      friend.Alias = SocialUtils.CreateAlias(friend.Uid, friend.PCID);

      // Save certificate to file system
      SocialUtils.SaveCertificate(cert);

      // Pass certificates to security handler through paths
      string lc_path = "lc" + friend.Address.Substring(12) + ".cert";
      string ca_path = "ca" + friend.Address.Substring(12) + ".cert";
      _bso.CertificateHandler.AddCertificate(lc_path);
      _bso.CertificateHandler.AddCertificate(ca_path);

      // Add friend to the network
      friend.IP = _rarad.RegisterMapping(friend.Alias, addr);

      // Add friend to list
      _friends.Add(friend.Alias, friend);
    }

    /**
     * Publishes the local certificate on the dht.
     */
    public void DhtPublishCert() {
      string key = "svpn:" + _local_user.Uid + ":" + _local_user.Fingerprint;
      Channel q = new Channel();
      q.CloseAfterEnqueue();
      q.CloseEvent += delegate(Object o, EventArgs eargs) {
        try {
          bool success = (bool) (q.Dequeue());
          if(success) {
            Console.WriteLine("Dht Put successful");
          }
        } catch (Exception e) {
          Console.WriteLine(e);
          Console.WriteLine("Dht Put failed");
        }
      };
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      MemBlock value = MemBlock.Reference(_local_cert.X509.RawData);
      this.Dht.AsPut(keyb, value, 3600, q);
    }

    /**
     * Add friend through the friend id and fingerprint.
     * @param uid the friend's uid.
     * @param fingerprint the certificate fingerprint.
     */
    public void AddDhtFriend(string uid, string fingerprint) {
      string key = "svpn:" + uid + ":" + fingerprint;
      Channel q = new Channel();
      q.CloseAfterEnqueue();
      q.CloseEvent += delegate(Object o, EventArgs eargs) {
        try {
          DhtGetResult dgr = (DhtGetResult) q.Dequeue();
          byte[] certData = dgr.value;
          AddFriend(certData, fingerprint);
        } catch (Exception e) {
          Console.WriteLine(e);
          Console.WriteLine("Certificate not found for: " + key);
        }
      };
      this.Dht.AsGet(key, q);
    }

    /*
     * Removes a friend from socialvpn.
     * @param friend the friend to be removed.
     */
    public void RemoveFriend(SocialUser friend) {
      _rarad.UnregisterMapping(friend.Alias);
    }
  }
}
