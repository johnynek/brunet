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
using System.Threading;
using System.IO;

using Brunet;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  /**
   * This class is in charge of making connections between friends. It
   * manages the social networking backends as well as the identity providers
   * of the system.
   */
  public class SocialConnectionManager {

    /**
     * The start time for the timer thread (30 secs).
     */
    private const int STARTTIME = 30000;

    /**
     * The interval time for the timer thread (5 mins).
     */
    private const int INTERVALTIME = 300000;

    /**
     * The node which accepts peers based on certificates.
     */
    protected readonly SocialNode _snode;

    /*
     * The identity provider.
     */
    protected readonly IProvider _provider;

    /**
     * The social network or relationship provider.
     */
    protected readonly ISocialNetwork _network;

    /**
     * The list of unique friend ids.
     */
    protected readonly List<string> _friendlist;

    /** 
     * The HTTP interface to manage socialvpn.
     */
    protected readonly HttpInterface _http;

    /**
     * The handles RPC for socialvpn.
     */
    protected readonly SocialRpcHandler _srh;

    /**
     * Timer thread.
     */
    protected readonly Timer _timer_thread;

    /**
     * Dictionary of friends indexed by alias.
     */
    protected readonly Dictionary<string, SocialUser> _friends;

    /**
     * Location of the certificates directory.
     */
    protected readonly string _cert_dir;

    /**
     * Constructor.
     * @param node the social node.
     * @param provider the identity provider.
     * @param network the social network.
     * @param port the port number for the HTTP interface.
     * @param srh the social rpc handler.
     */
    public SocialConnectionManager(SocialNode node, IProvider provider,
                                   ISocialNetwork network, string port,
                                   Dictionary<string, SocialUser> friends,
                                   SocialRpcHandler srh, string certDir) {
      _snode = node;
      _provider = provider;
      _network = network;
      _friendlist = new List<string>();
      _friends = friends;
      _http = new HttpInterface(port);
      _http.ProcessEvent += ProcessHandler;
      _http.Start();
      _srh = srh;
      _srh.SyncEvent += SyncHandler;
      _cert_dir = certDir;
      _timer_thread = new Timer(new TimerCallback(TimerHandler), null,
                                STARTTIME, INTERVALTIME);
    }

    /**
     * Timer event handler.
     * @param obj the default object.
     */
    public void TimerHandler(Object obj) {
      // Load certificates from the file system
      LoadCertificates(_cert_dir);

      try {
        UpdateFriends();
        _provider.StoreFingerprint();
        _snode.PublishCertificate();
        _timer_thread.Change(INTERVALTIME, INTERVALTIME);
      } catch (Exception e) {
        if(e.Message.StartsWith("Dht")) {
          // Only change time on Dht not activitated
          _timer_thread.Change(STARTTIME, STARTTIME);
        }
        ProtocolLog.Write(SocialLog.SVPNLog, e.Message);
        ProtocolLog.Write(SocialLog.SVPNLog, "TIMER HANDLER FAILURE " +
                          DateTime.Now.ToString());
      }
    }

    /**
     * Process event handler.
     * @param obj the default object.
     * @param eargs the event arguments.
     */
    public void ProcessHandler(Object obj, EventArgs eargs) {
      Dictionary<string, string> request = (Dictionary<string,string>) obj;
      if(request.ContainsKey("m")) {
        switch(request["m"]) {
          case "add":
            AddFriends(request["uids"]);;
            break;

          case "addfpr":
            AddFingerprints(request["fprs"]);
            break;

          case "addcert":
            AddCertificate(request["cert"]);
            break;
            
          case "allow":
            AllowFriends(request["fprs"]);
            break;

          case "block":
            BlockFriends(request["fprs"]);
            break;

          case "login":
            Login(request["id"], request["user"], request["pass"]);
            UpdateFriends();
            break;

          default:
            break;
        }
      }
      request["response"] = _snode.GetState();
    }

    /**
     * Sync event handler.
     * @param obj the default object.
     * @param eargs the event arguments.
     */
    public void SyncHandler(Object obj, EventArgs eargs) {
      string dht_key = (string) obj;
      string[] parts = dht_key.Split(':');
      string uid = parts[1];

      // Makes sure sync request came from a friend
      if(!_friendlist.Contains(uid)) {
        UpdateFriendUids();  
      }
      
      if(_friendlist.Contains(uid)) {
        AddFriends(new string[] {uid});
      }
    }

    /**
     * Updates friends and adds to socialvpn.
     */
    protected void UpdateFriends() {
      UpdateFriendUids();
      AddFriends(_friendlist.ToArray());
    }

    /**
     * Updates friend uids from social newtork.
     */
    protected void UpdateFriendUids() {
      List<string> new_friends = _network.GetFriends();
      foreach(string uid in new_friends) {
        if(!_friendlist.Contains(uid)) {
          _friendlist.Add(uid);
        }
      }
    }

    /**
     * Adds a list of fingerprints seperated by newline.
     * @param fprlist a list of fingerprints.
     */
    protected void AddFingerprints(string fprlist) {
      string[] fprs = fprlist.Split('\n');
      foreach(string fpr in fprs) {
        _snode.AddDhtFriend(fpr);
      }
    }

    /**
     * Adds a certificate to the socialvpn system.
     * @param certString a base64 encoding string representing certificate.
     */
    protected void AddCertificate(string certString) {
      certString = certString.Replace("\n", "");
      byte[] certData = Convert.FromBase64String(certString);
      _snode.AddCertificate(certData);
    }

    /**
     * Loads certificates from the file system.
     * @param certdir the directory to load certificates from.
     */
    protected void LoadCertificates(string certDir) {
      string[] cert_files = null;
      try {
        cert_files = System.IO.Directory.GetFiles(certDir);
      } catch (Exception e) {
        ProtocolLog.Write(SocialLog.SVPNLog, e.Message);
        ProtocolLog.Write(SocialLog.SVPNLog, "LOAD CERTIFICATES FAILURE");
      }
      foreach(string cert_file in cert_files) {
        byte[] cert_data = SocialUtils.ReadFileBytes(cert_file);
        _snode.AddCertificate(cert_data);
      }
    }

    /**
     * Allow a list of fingerprints seperated by newline.
     * @param fprlist a list of fingerprints.
     */
    protected void AllowFriends(string fprlist) {
      string[] fprs = fprlist.Split('\n');
      foreach(string fpr in fprs) {
        _snode.AddFriend(_friends[fpr]);
      }
    }

    /**
     * Block a list of fingerprints seperated by newline.
     * @param fprlist a list of fingerprints.
     */
    protected void BlockFriends(string fprlist) {
      string[] fprs = fprlist.Split('\n');
      foreach(string fpr in fprs) {
        _snode.RemoveFriend(_friends[fpr]);
      }
    }

    /**
     * Adds a list of friends seperated by newline.
     * @param friendlist a list of friends unique identifiers.
     */
    protected void AddFriends(string friendlist) {
      string[] friends = friendlist.Split('\n');
      foreach(string friend in friends) {
        if(!_friendlist.Contains(friend)) {
          _friendlist.Add(friend);
        }
      }
      AddFriends(friends);
    }

    /**
     * Adds a list of friend based on user id.
     * @param uids the list of friend's user id.
     */
    protected void AddFriends(string[] uids) {
      List<string> fingerprints = _provider.GetFingerprints(uids);
      foreach(string fpr in fingerprints) {
        _snode.AddDhtFriend(fpr);
      }
      List<byte[]> certificates = _provider.GetCertificates(uids);
      foreach(byte[] cert in certificates) {
        _snode.AddCertificate(cert);
      }
    }

    /**
     * Logins into a identity provider backend.
     * @param username the username.
     * @param password the password.
     */
    protected bool Login(string id, string username, string password) {
      return _provider.Login(id, username, password);
    }
  }

#if SVPN_NUNIT
  [TestFixture]
  public class SocialConnectionManagerTester {
    [Test]
    public void SocialConnectionManagerTest() {
      Assert.AreEqual("test", "test");
    }
  } 
#endif
}
