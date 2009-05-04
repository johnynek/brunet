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

  public class SocialRpcHandler : IRpcHandler {

    public const char DELIM = '-';

    public event EventHandler SyncEvent;

    protected readonly StructuredNode _node;

    protected readonly RpcManager _rpc;

    protected readonly SocialUser _local_user;

    protected readonly Dictionary<string, SocialUser> _friends;

    public SocialRpcHandler(StructuredNode node, SocialUser localUser,
                           Dictionary<string, SocialUser> friends) {
      _node = node;
      _rpc = node.Rpc;
      _rpc.AddHandler("SocialVPN", this);
      _local_user = localUser;
      _friends = friends;
    }

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

    protected void FireSync(object obj) {
      EventHandler sync_event = SyncEvent;
      if(sync_event != null) {
        sync_event(obj, EventArgs.Empty);
      }
    }

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
