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
using Brunet.Security;
using Ipop;
using Ipop.ManagedNode;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  /**
   * SocialNode Class. Extends the RpcIpopNode to support adding friends based
   * on X509 certificates.
   */
  public class SocialNode : ManagedIpopNode {

    /**
     * The current version of SocialVPN.
     */
    public const string VERSION = "SVPN_0.3.0";

    /**
     * The suffix for the DNS names.
     */
    public const string DNSSUFFIX = ".ipop";

    /**
     * The local certificate file name.
     */
    public const string CERTFILENAME = "local.cert";

    /**
     * The path for the state file.
     */
    public const string STATEPATH = "state.xml";

    /**
     * The DHT TTL.
     */
    public const int DHTTTL = 3600;

    /**
     * Dictionary of friends indexed by alias.
     */
    protected readonly Dictionary<string, SocialUser> _friends;

    /**
     * The mapping of aliases to friends.
     */
    protected readonly Dictionary<string, string> _aliases;

    /**
     * The mapping of address to dht keys.
     */
    protected readonly Dictionary<string, string> _addr_to_key;

    /**
     * The certificate directory path.
     */
    protected readonly string _cert_dir;

    /**
     * The local user.
     */
    protected readonly SocialUser _local_user;

    /**
     * The local user certificate.
     */
    protected readonly Certificate _local_cert;

    /**
     * The base64 string representation of local certificate.
     */
    protected readonly string _local_cert_b64;

    /**
     * The identity provider and the social network.
     */
    protected readonly SocialNetworkProvider _snp;

    /**
     * The connection manager.
     */
    protected readonly SocialConnectionManager _scm;

    /**
     * The Rpc handler for socialvpn RPC functions.
     */
    protected readonly SocialRpcHandler _srh;

    /**
     * The main blocking queue used for message passing between threads.
     */
    protected readonly BlockingQueue _queue;

    /**
     * The http port.
     */
    protected readonly string _http_port;

    /**
     * Keeps track of published certificate.
     */
    protected bool _cert_published;

    /**
     * Access for published certificate status.
     */
    public bool CertPublished { get { return _cert_published; } }

    /**
     * Constructor.
     * @param brunetConfig configuration file for Brunet P2P library.
     * @param ipopConfig configuration file for IP over P2P app.
     */
    public SocialNode(NodeConfig brunetConfig, IpopConfig ipopConfig, 
                      string certDir, string port, string global_access) : 
                      base(brunetConfig, ipopConfig) {
      _friends = new Dictionary<string, SocialUser>();
      _aliases = new Dictionary<string, string>();
      _addr_to_key = new Dictionary<string, string>();
      _cert_dir = certDir;
      _http_port = port;
      string cert_path = Path.Combine(certDir, CERTFILENAME);
      _local_cert = new Certificate(SocialUtils.ReadFileBytes(cert_path));
      _local_user = new SocialUser(_local_cert);
      _local_cert_b64 = Convert.ToBase64String(_local_cert.X509.RawData);
      _bso.CertificateHandler.AddCACertificate(_local_cert.X509);
      _bso.CertificateHandler.AddSignedCertificate(_local_cert.X509);
      _snp = new SocialNetworkProvider(this.Dht, _local_user, 
                                       _local_cert.X509.RawData);
      _queue = new BlockingQueue();
      _srh = new SocialRpcHandler(_node, _local_user, _friends, _queue);
      _scm = new SocialConnectionManager(this, _snp, _srh, port, _queue);
      _cert_published = false;
      _node.ConnectionTable.ConnectionEvent += ConnectHandler;
      _node.HeartBeatEvent += _scm.HeartBeatHandler;
      Shutdown.OnExit += _scm.Stop;
      _local_user.IP = _marad.LocalIP;
      CreateAlias(_local_user);
      _marad.MapLocalDNS(_local_user.Alias);
      _scm.SetGlobalAccess(global_access);
      LoadCertificates();
    }

    /**
     * Create a unique alias for a user resource.
     * @param user the object representing the user.
     */
    protected virtual void CreateAlias(SocialUser friend) {
      string user = friend.Uid.Split('@')[0];
      string alias = (friend.PCID + "." + user + DNSSUFFIX).ToLower();
      int counter = 1;
      // If alias already exists, remove old friend with alias
      while(_aliases.ContainsKey(alias)) {
        alias = (friend.PCID + counter + "." + user + DNSSUFFIX).ToLower();
        counter++;
      }
      _aliases[alias] = friend.DhtKey;
      friend.Alias = alias;
    }

    /**
     * The connect handler keeps track of when a friend address is added
     * to the connection table.
     * @param obj the connection object containing address of new connection.
     * @param eargs the event arguments.
     */
    public void ConnectHandler(Object obj, EventArgs eargs) {
      Connection new_conn = ((ConnectionEventArgs)eargs).Connection;
      string address = new_conn.Address.ToString();
      if(_addr_to_key.ContainsKey(address)) {
        ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                            String.Format("CONNECT HANDLER: {0} {1} {2}",
                            DateTime.Now.TimeOfDay, _addr_to_key[address],
                            address));
      }
    }

    /**
     * Add local certificate to the DHT.
     */
    public void PublishCertificate() {
      byte[] key_bytes = Encoding.UTF8.GetBytes(_local_user.DhtKey);
      MemBlock keyb = MemBlock.Reference(key_bytes);
      MemBlock valueb = MemBlock.Reference(_local_cert.X509.RawData);

      Channel q = new Channel();
      q.CloseAfterEnqueue();
      q.CloseEvent += delegate(Object o, EventArgs eargs) {
        try {
          bool success = (bool) (q.Dequeue());
          if(success) {
            _cert_published = true;
            ProtocolLog.WriteIf(SocialLog.SVPNLog,
                                String.Format("PUBLISH CERT SUCCESS: {0} {1}",
                                DateTime.Now.TimeOfDay, _local_user.DhtKey));
          }
        } catch (Exception e) {
          ProtocolLog.WriteIf(SocialLog.SVPNLog,e.Message);
          ProtocolLog.WriteIf(SocialLog.SVPNLog,
                              String.Format("PUBLISH CERT FAILURE: {0} {1}", 
                              DateTime.Now.TimeOfDay, _local_user.DhtKey));
        }
      };
      this.Dht.AsyncPut(keyb, valueb, DHTTTL, q);
    }

    /**
     * Add a friend to socialvpn from an X509 certificate.
     * @param certData the X509 certificate as a byte array.
     * @param access determines to give user network access.
     */
    public void AddCertificate(byte[] certData, bool access) {
      Certificate cert = new Certificate(certData);
      SocialUser friend = new SocialUser(cert);

      // Verification on the certificate by email and fingerprint
      if(friend.DhtKey == _local_user.DhtKey || 
         _friends.ContainsKey(friend.DhtKey)) {
        ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                            String.Format("ADD CERT KEY FOUND: {0} {1}",
                            DateTime.Now.TimeOfDay, friend.DhtKey));
      }
      else if(_snp.ValidateCertificate(certData)) {
        CreateAlias(friend);
        SocialUtils.SaveCertificate(cert, _cert_dir);
        _bso.CertificateHandler.AddCACertificate(cert.X509);
        _friends.Add(friend.DhtKey, friend);
        _addr_to_key.Add(friend.Address, friend.DhtKey);
        AddFriend(friend);
        _srh.PingFriend(friend);

        // Block access
        if(!access) {
          RemoveFriend(friend);
        }

        ProtocolLog.WriteIf(SocialLog.SVPNLog,
                            String.Format("ADD CERT KEY SUCCESS: {0} {1} {2}",
                            DateTime.Now.TimeOfDay, friend.DhtKey,
                            friend.Address));
      }
      else {
        ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                            String.Format("ADD CERT KEY INVALID: {0} {1} {2}",
                            DateTime.Now.TimeOfDay, friend.DhtKey,
                            friend.Address));
      }
    }

    /**
     * Add friend by retreiving certificate from DHT.
     * @param key the DHT key for friend's certificate.
     * @param access determines to give user network access.
     */
    public void AddDhtFriend(string key, bool access) {
      if(key != _local_user.DhtKey && !_friends.ContainsKey(key) &&
         key.Length >= 45 ) {
        ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                            String.Format("ADD DHT FETCH: {0} {1}", 
                            DateTime.Now.TimeOfDay, key));
        Channel q = new Channel();
        q.CloseAfterEnqueue();
        q.CloseEvent += delegate(Object o, EventArgs eargs) {
          try {
            Hashtable result = (Hashtable) q.Dequeue();
            byte[] certData = (byte[]) result["value"];
            string tmp_key = SocialUser.DHTPREFIX + 
                             SocialUtils.GetSHA256(certData);
            if(tmp_key == key) {
              ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                                  String.Format("ADD DHT SUCCESS: {0} {1}",
                                  DateTime.Now.TimeOfDay, key));
              if(access) {
                _queue.Enqueue(new QueueItem(
                               QueueItem.Actions.AddCertTrue, certData));
              }
              else {
                _queue.Enqueue(new QueueItem(
                               QueueItem.Actions.AddCertFalse, certData));
              }
            }
          } catch (Exception e) {
            ProtocolLog.WriteIf(SocialLog.SVPNLog,e.Message);
            ProtocolLog.WriteIf(SocialLog.SVPNLog,
                                String.Format("ADD DHT FAILURE: {0} {1}", 
                                DateTime.Now.TimeOfDay, key));
          }
        };
        byte[] key_bytes = Encoding.UTF8.GetBytes(key);
        MemBlock keyb = MemBlock.Reference(key_bytes);
        this.Dht.AsyncGet(keyb, q);
      }
    }

    /*
     * Add a friend from socialvpn.
     * @param fpr the friend's fingerprint to be added.
     */
    public void AddFriend(string fpr) {
      if(_friends.ContainsKey(fpr)) {
        AddFriend(_friends[fpr]);
      }
    }

    /*
     * Removes a friend from socialvpn.
     * @param fpr the friend's fingerprint to be removed.
     */
    public void RemoveFriend(string fpr) {
      if(_friends.ContainsKey(fpr)) {
        RemoveFriend(_friends[fpr]);
      }
    }

    /*
     * Deletes a friend from socialvpn.
     * @param fpr the friend's fingerprint to be removed.
     */
    public void DeleteFriend(string fpr) {
      if(_friends.ContainsKey(fpr)) {
        DeleteFriend(_friends[fpr]);
      }
    }

    /*
     * Add a friend from socialvpn.
     * @param friend the friend to be added.
     */
    protected void AddFriend(SocialUser friend) {
      Address addr = AddressParser.Parse(friend.Address);
      friend.IP = _marad.RegisterMapping(friend.Alias, addr);
      _node.ManagedCO.AddAddress(addr);
      friend.Access = SocialUser.AccessTypes.Allow.ToString();
      _srh.PingFriend(friend);
      GetState(true);
    }

    /**
     * Removes (block access) a friend from socialvpn.
     * @param friend the friend to be removed.
     */
    protected void RemoveFriend(SocialUser friend) {
      Address addr = AddressParser.Parse(friend.Address);
      _node.ManagedCO.RemoveAddress(addr);
      _marad.UnregisterMapping(friend.Alias);
      friend.Access = SocialUser.AccessTypes.Block.ToString();
      GetState(true);
    }

    /**
     * Delete (erase) a friend from socialvpn.
     * @param friend the friend to be deleted.
     */
    protected void DeleteFriend(SocialUser friend) {
      RemoveFriend(friend);
      _friends.Remove(friend.DhtKey);
      _addr_to_key.Remove(friend.Address);
      _aliases.Remove(friend.Alias);
      SocialUtils.DeleteCertificate(friend.Address, _cert_dir);
      GetState(true);
    }

    /**
     * Loads certificates from the file system.
     */
    protected void LoadCertificates() {
      string[] cert_files = null;
      try {
        cert_files = System.IO.Directory.GetFiles(_cert_dir);
        SocialState state = Utils.ReadConfig<SocialState>(STATEPATH);
        foreach(string cert_file in cert_files) {
          byte[] cert_data = SocialUtils.ReadFileBytes(cert_file);
          AddCertificate(cert_data, true);
        }
        SetState(state);
      } catch (Exception e) {
        ProtocolLog.WriteIf(SocialLog.SVPNLog, e.Message);
        ProtocolLog.WriteIf(SocialLog.SVPNLog, "LOAD CERTIFICATES FAILURE");
      }
    }

    /**
     * Restores the previous state from the file system.
     * @param state the social state to set.
     */
    public void SetState(SocialState state) {
      try {
        foreach(SocialUser friend in state.Friends) {
          if(friend.Access == SocialUser.AccessTypes.Block.ToString()) {
            DeleteFriend(friend.DhtKey);
          }
        }
      } catch(Exception e) { 
        ProtocolLog.WriteIf(SocialLog.SVPNLog, e.Message);
        ProtocolLog.WriteIf(SocialLog.SVPNLog, "SET STATE FAILURE");
      }
    }

    /**
     * Generates an XML string representing state of the system.
     * @return a string represential the state.
     */
    public string GetState(bool write_to_file) {
      SocialState state = new SocialState();
      state.Certificate = _local_cert_b64;
      state.LocalUser = _local_user;
      //state.Deletes = _deletes;
      state.Friends = new SocialUser[_friends.Count];
      _friends.Values.CopyTo(state.Friends, 0);
      if(write_to_file) {
        Utils.WriteConfig(STATEPATH, state);
      }
      return SocialUtils.ObjectToXml<SocialState>(state);
    }

    /**
     * The main function, starting point for the program.
     */
    public static new void Main(string[] args) {

      NodeConfig node_config = null;
      IpopConfig ipop_config = null;
      string port, global_access;

      if(args.Length < 4) {
        try {
          node_config = Utils.ReadConfig<NodeConfig>("brunet.config");
          ipop_config = Utils.ReadConfig<IpopConfig>("ipop.config");
          port = "58888";
          global_access = "on";
        } catch {
          Console.WriteLine("usage: SocialVPN.exe <brunet.config path> " + 
                             "<ipop.config path> <http port> " +
                             "<global_access(on/off)>" + 
                             "[email] [pcid] [\"name\"]");
          return;
        }
      }
      else {
        node_config = Utils.ReadConfig<NodeConfig>(args[0]);
        ipop_config = Utils.ReadConfig<IpopConfig>(args[1]);
        port = args[2];
        global_access = args[3];
      }

      if(!System.IO.Directory.Exists(node_config.Security.CertificatePath)) {
        string name, uid, pcid, version, country;
        if(args.Length >= 7) {
          uid = args[4];
          pcid = args[5];
          name = args[6];
        }
        else {
          Console.WriteLine("usage (on first run): SocialVPN.exe " +
                            "<brunet.config path> <ipop.config path>" +
                            "<http port> <global_access(on/off)> " +
                            "<email> <pcid> <\"name\">");
          return;
        }

        country = "Undefined";
        version = VERSION;
        node_config.NodeAddress = Utils.GenerateAHAddress().ToString();
        Utils.WriteConfig(args[0], node_config);
        SocialUtils.CreateCertificate(uid, name, pcid, version, country,
                                      node_config.NodeAddress, 
                                      node_config.Security.CertificatePath,
                                      node_config.Security.KeyPath);
      }

      SocialNode node = new SocialNode(node_config, ipop_config, 
                                       node_config.Security.CertificatePath,
                                       port, global_access);
      node.Run();
    }
  }
}
