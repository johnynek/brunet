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

using Brunet;
using Brunet.DistributedServices;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  /**
   * This class implements all of the functions of the SocialVPN RPC
   * method. RPC mechanism is the main messaging mechanism between
   * SocialVPN nodes.
   */
  public class SocialRpcHandler : IRpcHandler {

    /**
     * The delimiter.
     */
    public const char DELIM = '-';

    /**
     * Sync event or function pointer.
     */
    public event EventHandler SyncEvent;

    /**
     * The P2P node.
     */
    protected readonly StructuredNode _node;

    /**
     * The Rpc manager.
     */
    protected readonly RpcManager _rpc;

    /**
     * The SocialUser object for local user.
     */
    protected readonly SocialUser _local_user;

    /**
     * The list of friends.
     */
    protected readonly Dictionary<string, SocialUser> _friends;

    /**
     * Constructor.
     * @param node the p2p node.
     * @param localUser the local user object.
     * @param friends the list of friends.
     */
    public SocialRpcHandler(StructuredNode node, SocialUser localUser,
                           Dictionary<string, SocialUser> friends) {
      _node = node;
      _rpc = node.Rpc;
      _rpc.AddHandler("SocialVPN", this);
      _local_user = localUser;
      _friends = friends;
    }

    /**
     * Handles incoming RPC calls.
     * @param caller object containing return path to caller.
     * @param method the object that is called.
     * @param arguments the arguments for the object.
     * @param req_state state object of the request.
     */
    public void HandleRpc(ISender caller, string method, IList arguments,
                          object req_state) {
      object result = null;
      try {
        switch (method) {
          case "FriendPing":
            result = FriendPingHandler((string)arguments[0]);
            break;

          default:
            result = new InvalidOperationException("Invalid Method");
            break;
        }
      } catch (Exception e) {
        result = e;
        ProtocolLog.Write(SocialLog.SVPNLog, e.Message);
        ProtocolLog.Write(SocialLog.SVPNLog, "RPC HANDLER FAILURE: " + 
                          method);
      }
      _rpc.SendResult(req_state, result);
    }

    /** 
     * Fires the sync event
     * @param obj object passes to sync event function.
     */
    protected void FireSync(object obj) {
      EventHandler sync_event = SyncEvent;
      if(sync_event != null) {
        sync_event(obj, EventArgs.Empty);
      }
    }

    /**
     * Pings a friend over the P2P network to see if online.
     * @param friend the friend to ping.
     */
    public void PingFriend(SocialUser friend) {
      if(friend.Time != SocialUser.TIMEDEFAULT) {
        DateTime past = DateTime.Parse(friend.Time);
        TimeSpan last_checked = DateTime.Now - past;
        if(last_checked.Minutes < 5) {
          return;
        };
      }
      FriendPing(friend.Address);
    }

    /**
     * Handles a ping request from a friend.
     * @param dhtkey the identifier for the friend making the request.
     * @return the respond sent back online/offline.
     */
    protected string FriendPingHandler(string dhtKey) {
      string response = "offline";

      if(_friends.ContainsKey(dhtKey)) { 
        if(_friends[dhtKey].Access == 
           SocialUser.AccessTypes.Allow.ToString()) {
          _friends[dhtKey].Time = DateTime.Now.ToString();
          response = "online";
        }
      }
      else {
        FireSync(dhtKey);
      }
      return _local_user.DhtKey + DELIM + response;
    }

    /**
     * Makes the ping request to a friend.
     * @param address the address of the friend.
     */
    protected void FriendPing(string address) {
      Address addr = AddressParser.Parse(address);
      Channel q = new Channel();
      q.CloseAfterEnqueue();
      q.CloseEvent += delegate(object obj, EventArgs eargs) {
        try {
          RpcResult res = (RpcResult) q.Dequeue();
          string result = (string) res.Result;
          string[] parts = result.Split(DELIM);
          string dht_key = parts[0];
          string response = parts[1];
          if(response == "online") {
            SocialUser friend = _friends[dht_key];
            friend.Time = DateTime.Now.ToString();
          }
          ProtocolLog.Write(SocialLog.SVPNLog, "PING FRIEND REPLY: " +
                            result);
        } catch(Exception e) {
          ProtocolLog.Write(SocialLog.SVPNLog, e.Message);
          ProtocolLog.Write(SocialLog.SVPNLog, "PING FRIEND FAILURE: " +
                            address);
        }
      };
      ISender sender = new AHExactSender(_node, addr);
      _rpc.Invoke(sender, q, "SocialVPN.FriendPing", _local_user.DhtKey);
    }
  }
#if SVPN_NUNIT
  [TestFixture]
  public class SocialRpcHandlerTester {
    [Test]
    public void SocialRpcHandlerTest() {
      Assert.AreEqual("test", "test");
    }
  } 
#endif
}
