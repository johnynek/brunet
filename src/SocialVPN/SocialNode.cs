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
using Brunet.Security;
using Brunet.Applications;
using Ipop;
using Ipop.ManagedNode;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  public enum AccessTypes {
    Allow,
    Block
  }

  public class SocialNode : ManagedIpopNode {

    public class SocialState {

      public SocialUser LocalUser;

      public SocialUser[] Friends;

      public string[] BlockedFriends;
    }

    public const string DNSSUFFIX = "ipop";

    public const string STATEPATH = "state.xml";

    protected readonly Dictionary<string, SocialUser> _friends;

    protected readonly List<string> _bfriends;

    protected readonly SocialUser _local_user;

    protected readonly object _sync;

    private string _status;

    public SocialUser LocalUser {
      get { return _local_user; }
    }

    public Node RpcNode {
      get { return _node; }
    }

    public SocialNode(NodeConfig brunetConfig, IpopConfig ipopConfig,
                      string certificate) : base(brunetConfig, ipopConfig) {
      _friends = new Dictionary<string, SocialUser>();
      _bfriends = new List<string>();
      _sync = new object();
      _status = StatusTypes.Offline.ToString();
      _local_user = new SocialUser();
      _local_user.Certificate = certificate;
      _local_user.IP = _marad.LocalIP;
      _marad.AddDnsMapping(_local_user.Alias, _local_user.IP, true);
      _bso.CertificateHandler.AddCACertificate(_local_user.GetCert().X509);
      _bso.CertificateHandler.AddSignedCertificate(_local_user.GetCert().X509);
      LoadState();
    }

    protected SocialUser AddFriend(string certb64, string uid, string ip) {
      SocialUser user = new SocialUser();
      user.Certificate = certb64;
      user.Status = StatusTypes.Offline.ToString();

      if (user.Uid.ToLower() != uid.ToLower()) {
        throw new Exception("Uids do not match");
      }

      foreach (SocialUser tmp_user in GetFriends()) {
        if (tmp_user.Address == user.Address) {
          throw new Exception("Address already exists");
        }
      }

      if (_friends.ContainsKey(user.Alias)) {
        RemoveFriend(user.Alias);
      }

      Address addr = AddressParser.Parse(user.Address);
      _bso.CertificateHandler.AddCACertificate(user.GetCert().X509);
      _node.ManagedCO.AddAddress(addr);
      if (!_bfriends.Contains(uid)) {
        user.IP = _marad.AddIPMapping(ip, addr);
        _marad.AddDnsMapping(user.Alias, user.IP, true);
        user.Access = AccessTypes.Allow.ToString();
      }
      else {
        user.Access = AccessTypes.Block.ToString();
      }

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
          _marad.RemoveDnsMapping(user.Alias, true);
          string access = AccessTypes.Block.ToString();
          UpdateFriend(user.Alias, user.IP, user.Time, access, user.Status);
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
          user.IP = _marad.AddIPMapping(user.IP, addr);
          _marad.AddDnsMapping(user.Alias, user.IP, true);
          string access = AccessTypes.Allow.ToString();
          UpdateFriend(user.Alias, user.IP, user.Time, access, user.Status);
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
      UpdateTime();
      SocialState state = new SocialState();
      state.LocalUser = _local_user.ChangedCopy(_local_user.IP, 
        String.Empty, String.Empty, _status);
      state.Friends = new SocialUser[_friends.Count];
      state.BlockedFriends = new string[_bfriends.Count];
      _friends.Values.CopyTo(state.Friends, 0);
      _bfriends.CopyTo(state.BlockedFriends, 0);
      if(write_to_file) {
        Utils.WriteConfig(STATEPATH, state);
      }
      return SocialUtils.ObjectToXml<SocialState>(state);
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
        int i = 0;
        foreach(SocialUser user in _friends.Values) {
          friends[i] = user.ExactCopy();
          i++;
        }
      }
      return friends;
    }

    public void UpdateFriend(string alias, string ip, string time, 
      string access, string status) {
      SocialUser user;
      if(_friends.TryGetValue(alias, out user)) {
        SocialUser new_user = user.ChangedCopy(ip, time, access, status);
        lock(_sync) {
          _friends.Remove(new_user.Alias);
          _friends.Add(new_user.Alias, new_user);
        }
      }
      else {
        throw new Exception("Could not get value to update");
      }
    }

    public void UpdateTime() {
      foreach(SocialUser user in GetFriends()) {
        Address addr = AddressParser.Parse(user.Address);
        if(_node.ConnectionTable.Contains(ConnectionType.Structured,addr)) {
          string time = DateTime.Now.ToString();
          string status = StatusTypes.Online.ToString();
          UpdateFriend(user.Alias, user.IP, time, user.Access, status);
        }
        else if(user.Status == StatusTypes.Online.ToString()) {
          string status = StatusTypes.Relay.ToString();
          UpdateFriend(user.Alias, user.IP, user.Time, user.Access, status);
        }
      }
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
          _status = request["status"];
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
      string certb64 = Convert.ToBase64String(certData);
      social_config = Utils.ReadConfig<SocialConfig>("social.config");
      node_config = Utils.ReadConfig<NodeConfig>(social_config.BrunetConfig);
      ipop_config = Utils.ReadConfig<IpopConfig>(social_config.IpopConfig);

      SocialNode node = new SocialNode(node_config, ipop_config, certb64);
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
