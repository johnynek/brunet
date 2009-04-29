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

  public class SocialConnectionManager {

    /**
     * The node which accepts peers based on certificates.
     */
    protected SocialNode _node;

    /*
     * The identity provider.
     */
    protected IProvider _provider;

    /**
     * The social network or relationship provider.
     */
    protected ISocialNetwork _network;

    /**
     * The list of unique friend ids.
     */
    protected List<string> _friends;

    /** 
     * The HTTP interface to manage socialvpn.
     */
    protected HttpInterface _http;

    /**
     * Timer thread.
     */
    protected Timer _timer_thread;

    /**
     * Constructor.
     * @param node the social node.
     * @param provider the identity provider.
     * @param network the social network.
     */
    public SocialConnectionManager(SocialNode node, IProvider provider,
                                   ISocialNetwork network) {
      _node = node;
      _provider = provider;
      _network = network;
      _friends = new List<string>();
      _http = new HttpInterface();
      _http.ProcessEvent += ProcessHandler;
      _http.Start();
      _timer_thread = new Timer(new TimerCallback(TimerHandler), null,
                                120000, 120000);
    }

    /**
     * Timer event handler.
     * @param obj the default object.
     */
    public void TimerHandler(Object obj) {
      _node.PublishCertificate();
      UpdateFriends();
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
            AddFriend(request["uids"]);;
            break;

          case "login":
            Login(request["user"], request["pass"]);
            UpdateFriends();
            break;

          case "load":
            LoadFromFiles();
            break;

          default:
            break;
        }
      }
      _http.StateXml = GetState();
    }

    private string GetState() {
      SocialState state = new SocialState();
      state.LocalUser = _node.LocalUser;
      state.Friends = new SocialUser[_node.Friends.Count];
      _node.Friends.Values.CopyTo(state.Friends, 0);
      return SocialUtils.ObjectToXml<SocialState>(state);
    }

    /**
     * Updates friends from social newtork.
     */
    public void UpdateFriends() {
      List<string> new_friends = _network.GetFriends();
      foreach(string uid in new_friends) {
        if(!_friends.Contains(uid)) {
          _friends.Add(uid);
        }
      }
      foreach(string uid in _friends) {
        AddFriend(uid);
      }
      _provider.StoreFingerprint();
    }

    public void AddFriends(string friendlist) {
      string[] friends = friendlist.Split('\n');
      foreach(string friend in friends) {
        AddFriend(friend);
      }
    }

    /**
     * Adds a friend based on user id.
     * @param uid the friend's user id.
     */
    public void AddFriend(string uid) {
      if(!_friends.Contains(uid)) _friends.Add(uid);
      List<string> fingerprints = _provider.GetFingerprints(uid);
      foreach(string fpr in fingerprints) {
        _node.AddDhtFriend(fpr);
      }
    }

    public bool Login(string username, string password) {
      return _provider.Login(username, password);
    }

    public void LoadFromFiles() {
      string[] files = null;
      try {
        files = Directory.GetFiles("certificates");
        foreach(string file in files) {
          byte[] cert_data = SocialUtils.ReadFileBytes(file);
          _node.AddCertificate(cert_data);
        }
      } catch (Exception e) { 
        ProtocolLog.Write(SocialLog.SVPNLog, e.Message);
        ProtocolLog.Write(SocialLog.SVPNLog, "Load from files failed");
      }
    }
  }

  /**
   * The interface for an identity provider.
   */
  public interface IProvider {

    bool Login(string username, string password);
    /**
     * Retrieves the fingerprints of a particular peer.
     */
    List<string> GetFingerprints(string uid);

    /**
     * Stores the fingerprint of a peer.
     */
    bool StoreFingerprint();
  }

  /**
   * The interface for a social network.
   */
  public interface ISocialNetwork {
    /**
     * Get a list of friends from the social network.
     */
    List<string> GetFriends();
  }

  public class SocialState {
    public SocialUser LocalUser;
    public SocialUser[] Friends;
  }

#if SVPN_NUNIT
  [TestFixture]
  public class SocialConnectionManagerTester {
    [Test]
    public void SocialConnectionManagerTest() {
      string[] files = null;
      try {
        files = Directory.GetFiles("certificates");
        foreach(string file in files) {
          Console.WriteLine("Directory file: " + file);
        }
      } catch {}
    }
  } 
#endif
}
