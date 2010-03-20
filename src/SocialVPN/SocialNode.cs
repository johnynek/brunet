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
using System.Collections.Generic;

using Brunet;
using Brunet.Util;
using Brunet.Applications;
using Brunet.Security;
using Ipop;
using Ipop.ManagedNode;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  /**
   * This class defines the social state of the system.
   */
  public class SocialState {

    /**
     * The local user.
     */
    public SocialUser LocalUser;

    /**
     * The list of friends.
     */
    public SocialUser[] Friends;

    /**
     * The list of blocked friends.
     */
    public string[] BlockedFriends;

  }

  public enum StatusTypes {
    Online,
    Offline,
    Failed,
    Blocked
  }

  public class SocialNode : ManagedIpopNode {

    public const string DNSSUFFIX = "ipop";

    public const string STATEPATH = "state.xml";

    protected readonly Dictionary<string, SocialUser> _friends;

    protected readonly List<string> _bfriends;

    protected readonly SocialUser _local_user;

    protected readonly object _sync;

    public SocialUser LocalUser {
      get { return _local_user; }
    }

    public Node RpcNode {
      get { return _node; }
    }

    public SocialNode(NodeConfig brunetConfig, IpopConfig ipopConfig,
                      byte[] certData) : base(brunetConfig, ipopConfig) {
      _friends = new Dictionary<string, SocialUser>();
      _bfriends = new List<string>();
      _sync = new object();
      Certificate local_cert = new Certificate(certData);
      _local_user = new SocialUser(local_cert);
      _local_user.IP = _marad.LocalIP;
      _local_user.Alias = CreateAlias(_local_user);
      _marad.AddDnsMapping(_local_user.Alias, _local_user.IP, true);
      _bso.CertificateHandler.AddCACertificate(local_cert.X509);
      _bso.CertificateHandler.AddSignedCertificate(local_cert.X509);
      LoadState();
    }

    protected static string CreateAlias(SocialUser user) {
      char[] delims = new char[] {'@','.'};
      string[] parts = user.Uid.Split(delims);
      string alias = String.Empty;
      for(int i = 0; i < parts.Length-1; i++) {
        alias += parts[i] + ".";
      }
      alias = (user.PCID + "." + alias + DNSSUFFIX).ToLower();
      return alias;
    }

    protected void UpdateStatus() {
      foreach(SocialUser user in GetFriends()) {
        Address addr = AddressParser.Parse(user.Address);
        if(_node.ConnectionTable.Contains(ConnectionType.Structured, 
           addr) && user.Status != StatusTypes.Blocked.ToString()) {
          lock (_sync) {
            SocialUser tmp_user;
            if(_friends.TryGetValue(user.Alias, out tmp_user)) {
              tmp_user.Status = StatusTypes.Online.ToString();
            }
          }
        }
        else if (user.Status != StatusTypes.Blocked.ToString()) {
          lock (_sync) {
            SocialUser tmp_user;
            if(_friends.TryGetValue(user.Alias, out tmp_user)) {
              tmp_user.Status = StatusTypes.Offline.ToString();
            }
          }
        }
      }
    }

    protected SocialUser AddFriend(string certb64, string uid, string ip) {
      byte[] certData = Convert.FromBase64String(certb64);
      return AddFriend(certData, uid, ip);
    }

    protected SocialUser AddFriend(byte[] certData, string uid, string ip) {
      if (_bfriends.Contains(uid)) {
        throw new Exception("Uid blocked, cannot add");
      }

      Certificate cert = new Certificate(certData);
      SocialUser user = new SocialUser(cert);
      user.Alias = CreateAlias(user);

      // Uids have to match
      if (user.Uid.ToLower() != uid.ToLower()) return null;

      // Only add new addresses
      foreach (SocialUser tmp_user in GetFriends()) {
        if (tmp_user.Address == user.Address) return null;
      }

      // If old is mapping is found, remove
      if (_friends.ContainsKey(user.Alias)) {
        RemoveFriend(user.Alias);
      }

      Address addr = AddressParser.Parse(user.Address);
      _bso.CertificateHandler.AddCACertificate(cert.X509);
      _node.ManagedCO.AddAddress(addr);
      user.IP = _marad.AddIPMapping(ip, addr);
      _marad.AddDnsMapping(user.Alias, user.IP, true);

      lock (_sync) {
        _friends.Add(user.Alias, user);
      }
      GetState(true);
      return user;
    }

    protected void RemoveFriend(string alias) {
      if (!_friends.ContainsKey(alias)) {
        throw new Exception("Alias not found");
      }

      SocialUser user = _friends[alias];
      Address addr = AddressParser.Parse(user.Address);
      _node.ManagedCO.RemoveAddress(addr);
      _marad.RemoveIPMapping(user.IP);
      _marad.RemoveDnsMapping(user.Alias, true);

      lock (_sync) {
        _friends.Remove(user.Alias);
      }
      GetState(true);
    }

    protected void Block(string uid) {
      if (_bfriends.Contains(uid)) {
        throw new Exception("Uid already blocked");
      }

      foreach(SocialUser user in GetFriends()) {
        if (user.Uid == uid) {
          _marad.RemoveIPMapping(user.IP);
          lock (_sync) {
            SocialUser tmp_user;
            if(_friends.TryGetValue(user.Alias, out tmp_user)) {
              tmp_user.Status = StatusTypes.Blocked.ToString();
            }
          }
        }
      }

      lock (_sync) {
        _bfriends.Add(uid);
      }
      GetState(true);
    }

    protected void Unblock(string uid) {
      foreach(SocialUser user in GetFriends()) {
        if (user.Uid == uid) {
          Address addr = AddressParser.Parse(user.Address);
          _marad.AddIPMapping(user.IP, addr);
          lock (_sync) {
            SocialUser tmp_user;
            if(_friends.TryGetValue(user.Alias, out tmp_user)) {
              tmp_user.Status = StatusTypes.Offline.ToString();
            }
          }
        }
      }

      lock (_sync) {
        _bfriends.Remove(uid);
      }
      GetState(true);
    }

    public void AddDnsMapping(string alias, string ip) {
      _marad.AddDnsMapping(alias, ip, false);
    }

    protected void RemoveDnsMapping(string alias) {
      _marad.RemoveDnsMapping(alias, false);
    }

    protected string GetState(bool write_to_file) {
      UpdateStatus();
      SocialState state = new SocialState();
      state.LocalUser = _local_user;
      state.Friends = new SocialUser[_friends.Count];
      state.BlockedFriends = new string[_bfriends.Count];
      _friends.Values.CopyTo(state.Friends, 0);
      _bfriends.CopyTo(state.BlockedFriends, 0);
      if(write_to_file) {
        Utils.WriteConfig(STATEPATH, state);
      }
      return SocialUtils.ObjectToXml1<SocialState>(state);
    }

    protected void LoadState() {
      try {
        SocialState state = Utils.ReadConfig<SocialState>(STATEPATH);
        foreach (string user in state.BlockedFriends) {
          lock (_sync) {
            _bfriends.Add(user);
          }
        }
        foreach (SocialUser user in state.Friends) {
          AddFriend(user.Certificate, user.Uid, user.IP);
        }
      }
      catch (Exception e) {
        Console.WriteLine(e);
      }
    }

    public SocialUser[] GetFriends() {
      SocialUser[] friends;
      lock(_sync) {
        friends = new SocialUser[_friends.Count];
        _friends.Values.CopyTo(friends, 0);
      }
      return friends;
    }

    public void ProcessHandler(Object obj, EventArgs eargs) {
      Dictionary <string, string> request = (Dictionary<string, string>)obj;
      string method = String.Empty;
      request.TryGetValue("m", out method);

      switch(method) {
        case "add":
          AddFriend(request["cert"], request["uid"], null);
          break;

        case "addip":
          AddFriend(request["cert"], request["uid"], request["ip"]);
          break;

        case "remove":
          RemoveFriend(request["alias"]);
          break;

        case "block":
          Block(request["uid"]);
          break;

        case "unblock":
          Unblock(request["uid"]);
          break;

        case "getcert":
          request["response"] = _local_user.Certificate;
          break;

        case "updatestat":
          _local_user.Status = request["status"];
          break;

        default:
          break;
      }
      if (!request.ContainsKey("response")) {
        request["response"] = GetState(false);
      }
    }

    public static new SocialNode CreateNode() {
      SocialConfig social_config;
      NodeConfig node_config;
      IpopConfig ipop_config;

      byte[] certData = SocialUtils.ReadFileBytes("local.cert");
      social_config = Utils.ReadConfig<SocialConfig>("social.config");
      node_config = Utils.ReadConfig<NodeConfig>(social_config.BrunetConfig);
      ipop_config = Utils.ReadConfig<IpopConfig>(social_config.IpopConfig);

      SocialNode node = new SocialNode(node_config, ipop_config, certData);
      HttpInterface http_ui = new HttpInterface(social_config.HttpPort);
      SocialDnsManager dns_manager = new SocialDnsManager(node);
      JabberNetwork jabber = new JabberNetwork(social_config.JabberHost,
                                               social_config.JabberPort);

      http_ui.ProcessEvent += node.ProcessHandler;
      http_ui.ProcessEvent += jabber.ProcessHandler;
      http_ui.ProcessEvent += dns_manager.ProcessHandler;
      jabber.ProcessEvent += node.ProcessHandler;

      node.Shutdown.OnExit += http_ui.Stop;
      node.Shutdown.OnExit += jabber.Logout;

      if (social_config.AutoLogin) {
        jabber.Login(social_config.JabberID, social_config.JabberPass);
      }
      http_ui.Start();
      return node;
    }

    public static void Main(string[] args) {
      
      SocialNode node = SocialNode.CreateNode();
      node.Run();
    }
  }

#if SVPN_NUNIT
  [TestFixture]
  public class SocialNodeTester {
    [Test]
    public void SocialNodeTest() {
      Certificate cert = SocialUtils.CreateCertificate("alice@facebook.com",
        "Alice Wonderland", "pc", "version", "country", "address123", null);
    }
  }
#endif
}
