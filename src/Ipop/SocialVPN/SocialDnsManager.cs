/*
Copyright (C) 2010 Pierre St Juste <ptony82@ufl.edu>, University of Florida

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
using System.Text.RegularExpressions;

using Brunet;
using Brunet.DistributedServices;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace Ipop.SocialVPN {

  public class SocialDnsManager : IRpcHandler {

    protected readonly Dictionary<string, DnsMapping> _mappings;

    protected readonly List<DnsMapping> _tmappings;

    protected readonly SocialNode _node;

    protected readonly SocialUser _local_user;

    protected int _beat_counter;

    public SocialDnsManager(SocialNode node) {
      _mappings = new Dictionary<string, DnsMapping>();
      _tmappings = new List<DnsMapping>();
      _node = node;
      _node.Rpc.AddHandler("SocialDNS", this);
      _local_user = _node.LocalUser;
      _beat_counter = 0;
      _node.RpcNode.HeartBeatEvent += HeartBeatHandler;
    }

    public void HeartBeatHandler(object obj, EventArgs eargs) {
      if(_beat_counter % 120 == 0) {
        PingFriends();
      }
      _beat_counter++;
    }

    public void ProcessHandler(Object obj, EventArgs eargs) {
      Dictionary <string, string> request = (Dictionary<string, string>)obj;
      string method = String.Empty;
      if (request.ContainsKey("m")) {
        method = request["m"];
      }

      switch(method) {
        case "sdns.lookup":
          SearchFriends(request["query"]);
          request["response"] = GetState();
          break;

        case "sdns.addmapping":
          AddMapping(request["mapping"]);
          request["response"] = GetState();
          break;

        case "sdns.getstate":
          request["response"] = GetState();
          break;

        default:
          break;
      }
    }

    public void HandleRpc(ISender caller, string method, IList args,
                          object req_state) {
      object result = null;
      try {
        switch(method) {
          case "SearchMapping":
            result = SearchMapping((string)args[0], (string)args[1]);
            break;

          case "AddTmpMapping":
            result = AddTmpMapping((string)args[0], (string)args[1]);
            break;

          case "Ping":
            result = HandlePing((string)args[0], (string)args[1]);
            break;

          default:
            result = new InvalidOperationException("Invalid Method");
            break;
        }
      } catch (Exception e) {
        result = e;
        ProtocolLog.WriteIf(SocialLog.SVPNLog, e.Message);
        ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                            String.Format("RPC HANDLER FAILURE: {0} {1}" + 
                            DateTime.Now.TimeOfDay, args[0]));
      }
      _node.Rpc.SendResult(req_state, result);
    }

    protected void SendRpcMessage(string address, string method, 
      string query) {
      method = "SocialDNS." + method;
      Address addr = AddressParser.Parse(address);
      Channel q = new Channel();
      q.CloseAfterEnqueue();
      q.CloseEvent += delegate(object obj, EventArgs eargs) {
        try {
          RpcResult res = (RpcResult) q.Dequeue();
          // Result is true if it got there with no problem
          bool result = (bool) res.Result;
          if(result) {
            ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                          String.Format("RPC REPLY {3}: {0} {1} {2}",
                          DateTime.Now.TimeOfDay, address, result, method));
          }
        } catch(Exception e) {
          ProtocolLog.WriteIf(SocialLog.SVPNLog, e.Message);
          ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                         String.Format("RPC FAILURE {3}: {0} {1} {2}",
                         DateTime.Now.TimeOfDay, address, query, method));
        }
      };
      ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                      String.Format("RPC REQUEST {3}: {0} {1} {2}",
                      DateTime.Now.TimeOfDay, address, query, method));

      ISender sender = new AHExactSender(_node.RpcNode, addr);
      _node.Rpc.Invoke(sender, q, method, _local_user.Address, query);
    }

    protected void PingFriends() {
      SocialUser[] friends = _node.GetFriends();
      foreach(SocialUser friend in friends) {
        if(friend.Time == String.Empty) {
          string time = DateTime.Now.ToString();
          string status = StatusTypes.Offline.ToString();
          _node.UpdateFriend(friend.Alias, friend.IP, time, friend.Access,
            status);
        }
        else if(friend.Access != AccessTypes.Block.ToString()) {
          DateTime old_time = DateTime.Parse(friend.Time);
          TimeSpan interval = DateTime.Now - old_time;
          if(interval.Minutes >= 1) {
            string status = StatusTypes.Offline.ToString();
            _node.UpdateFriend(friend.Alias, friend.IP, friend.Time, 
              friend.Access, status);
          }
        }
        string method = "Ping";
        SendRpcMessage(friend.Address, method, "request");
      }
    }

    protected bool HandlePing(string address, string message) {
      SocialUser[] friends = _node.GetFriends();
      foreach(SocialUser friend in friends) {
        if(friend.Address == address && 
          friend.Access != AccessTypes.Block.ToString()) {
          string status = StatusTypes.Online.ToString();
          string time = DateTime.Now.ToString();
          _node.UpdateFriend(friend.Alias, friend.IP, time, friend.Access,
            status);
          if(message == "request") {
            string method = "Ping";
            SendRpcMessage(address, method, "reply");
          }
          return true;
        }
      }
      return false;
    }

    protected void SearchFriends(string query) {
      Console.WriteLine("calling search friends " + query);
      SocialUser[] friends = _node.GetFriends();
      foreach(SocialUser friend in friends) {
        if(friend.Access != AccessTypes.Block.ToString()) {
          string method = "SearchMapping";
          SendRpcMessage(friend.Address, method, query);
        }
      }
    }

    protected bool AddMapping(string mapping) {
      string[] parts = mapping.Split(DnsMapping.DELIM);
      if(parts.Length < 3) {
        mapping = parts[0] + DnsMapping.DELIM + _local_user.Address + 
          DnsMapping.DELIM + _local_user.Uid;
      }
      DnsMapping tmp = DnsMapping.Create(mapping);
      if(parts.Length == 4) {
        tmp.IP = parts[3];
      }
      return AddMapping(tmp);
    }

    protected bool AddMapping(DnsMapping mapping) {
      Console.WriteLine("Adding " + mapping);
      _mappings.Add(mapping.Alias, mapping);
      _node.AddDnsMapping(mapping.Alias, mapping.IP);
      return true;
    }

    protected bool SearchMapping(string address, string pattern) {
      Console.WriteLine("Searching for pattern " + pattern);
      foreach(string alias in _mappings.Keys) {
        if(Regex.IsMatch(alias, pattern, RegexOptions.IgnoreCase)) {
          DnsMapping mapping = _mappings[alias];
          mapping.Referrer = _local_user.Address;
          string method = "AddTmpMapping";
          SendRpcMessage(address, method, mapping.ToString());
          Console.WriteLine("Found match " + mapping.ToString());
        }
      }
      return true;
    }

    protected bool AddTmpMapping(string address, string mapping) {
      return AddTmpMapping(DnsMapping.Create(mapping));
    }

    protected bool AddTmpMapping(DnsMapping mapping) {
      Console.WriteLine("Adding tmp " + mapping);
      SocialUser[] friends = _node.GetFriends();
      foreach(SocialUser friend in friends) {
        if(friend.Address == mapping.Address) {
          mapping.IP = friend.IP;
          break;
        }
      }
      foreach (DnsMapping tmapping in _tmappings) {
        if (mapping.Equals(tmapping)) {
          Console.WriteLine("Incrementing rating");
          tmapping.Rating++;
          return true;
        }
      }
      _tmappings.Add(mapping);
      return true;
    }

    protected void ClearResults() {
      _tmappings.Clear();
    }

    protected string GetState() {
      DnsState state = new DnsState();
      state.Mappings = new DnsMapping[_mappings.Count];
      _mappings.Values.CopyTo(state.Mappings, 0);
      _tmappings.Sort(new MappingComparer());
      state.TmpMappings = _tmappings.ToArray();
      return SocialUtils.ObjectToXml<DnsState>(state);
    }
  }

  public class DnsMapping {

    public const char DELIM = '=';
    public const string MISS = "miss";
    public string Alias;
    public string Address;
    public string IP;
    public string Source;
    public string Referrer;
    public int Rating;

    public DnsMapping() {}

    public DnsMapping(string alias, string address, string source) {
      Alias = alias;
      Address = address;
      Source = source;
      Rating = 1;
    }

    public static DnsMapping Create(string mapping) {
      Console.WriteLine("creating " + mapping);
      string[] parts = mapping.Split(DnsMapping.DELIM);
      return new DnsMapping(parts[0], parts[1], parts[2]);
    }

    public bool Equals(DnsMapping mapping) {
      return (mapping.Alias == Alias && mapping.Address == Address);
    }

    public override string ToString() {
      return Alias + DELIM + Address + DELIM + Source + DELIM + Rating;
    }
  }

  public class DnsState {
    public DnsMapping[] Mappings;
    public DnsMapping[] TmpMappings;
  }

  public class MappingComparer : IComparer<DnsMapping> {
    public int Compare(DnsMapping x, DnsMapping y) {
      return y.Rating - x.Rating;
    }
  }



#if SVPN_NUNIT
  [TestFixture]
  public class SocialDnsManagerTester {
    [Test]
    public void SocialDnsManagerTest() {
      Assert.AreEqual("test", "test");
    }
  } 
#endif
}
