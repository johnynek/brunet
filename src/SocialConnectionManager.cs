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
    protected readonly SocialNode _node;

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
    protected readonly List<string> _friends;

    /** 
     * The HTTP interface to manage socialvpn.
     */
    protected readonly HttpInterface _http;

    /**
     * Timer thread.
     */
    protected readonly Timer _timer_thread;

    /**
     * Constructor.
     * @param node the social node.
     * @param provider the identity provider.
     * @param network the social network.
     * @param port the port number for the HTTP interface.
     */
    public SocialConnectionManager(SocialNode node, IProvider provider,
                                   ISocialNetwork network, string port) {
      _node = node;
      _provider = provider;
      _network = network;
      _friends = new List<string>();
      _http = new HttpInterface(port);
      _http.ProcessEvent += ProcessHandler;
      _http.Start();
      _timer_thread = new Timer(new TimerCallback(TimerHandler), null,
                                30000, 1800000);
    }

    /**
     * Timer event handler.
     * @param obj the default object.
     */
    public void TimerHandler(Object obj) {
      try {
        _node.PublishCertificate();
        UpdateFriends();
        _timer_thread.Change(30000, 1800000);
      } catch (Exception e) {
        _timer_thread.Change(30000, 30000);
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
            AddFriend(request["uids"]);;
            break;

          case "login":
            Login(request["user"], request["pass"]);
            UpdateFriends();
            break;

          default:
            break;
        }
      }
      request["response"] = GetState();
    }

    /**
     * Generates an XML string representing state of the system.
     */
    protected string GetState() {
      SocialState state = new SocialState();
      state.LocalUser = _node.LocalUser;
      state.Friends = new SocialUser[_node.Friends.Count];
      _node.Friends.Values.CopyTo(state.Friends, 0);
      return SocialUtils.ObjectToXml<SocialState>(state);
    }

    /**
     * Updates friends from social newtork.
     */
    protected void UpdateFriends() {
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

    /**
     * Adds a list of friends seperated by newline.
     */
    protected void AddFriends(string friendlist) {
      string[] friends = friendlist.Split('\n');
      foreach(string friend in friends) {
        AddFriend(friend);
      }
    }

    /**
     * Adds a friend based on user id.
     * @param uid the friend's user id.
     */
    protected void AddFriend(string uid) {
      if(!_friends.Contains(uid)) _friends.Add(uid);
      List<string> fingerprints = _provider.GetFingerprints(uid);
      foreach(string fpr in fingerprints) {
        _node.AddDhtFriend(fpr);
      }
    }

    protected bool Login(string username, string password) {
      return _provider.Login(username, password);
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
