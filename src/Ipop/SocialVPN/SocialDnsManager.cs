/*
Copyright (C) 2010 Pierre St Juste <ptony82@ufl.edu>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Brunet;
using Brunet.Util;
using Brunet.Concurrent;
using Brunet.Messaging;
using Brunet.Symphony;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace Ipop.SocialVPN {

  public class SocialDnsManager : IRpcHandler {

    protected readonly Dictionary<string, DnsMapping> _mappings;

    protected readonly List<DnsMapping> _tmappings;

    protected readonly SocialNode _node;

    protected readonly SocialUser _local_user;

    private int _beat_counter;

    public SocialDnsManager(SocialNode node) {
      _mappings = new Dictionary<string, DnsMapping>();
      _tmappings = new List<DnsMapping>();
      _node = node;
      _node.RpcNode.Rpc.AddHandler("SocialDNS", this);
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
          request["response"] = SearchLocalCache(request["query"]);
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
      _node.RpcNode.Rpc.SendResult(req_state, result);
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
      _node.RpcNode.Rpc.Invoke(sender, q, method, _local_user.Address, query);
    }

    protected void PingFriends() {
      SocialUser[] friends = _node.GetFriends();
      foreach(SocialUser friend in friends) {
        if(friend.Time == String.Empty) {
          string time = DateTime.Now.ToString();
          _node.UpdateFriend(friend.Alias, friend.IP, time, friend.Access,
            friend.Status);
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
      _mappings.Add(mapping.Alias, mapping);
      _node.AddDnsMapping(mapping.Alias, mapping.IP);
      return true;
    }

    protected bool SearchMapping(string address, string pattern) {
      foreach(string alias in _mappings.Keys) {
        if(Regex.IsMatch(alias, pattern, RegexOptions.IgnoreCase)) {
          DnsMapping mapping = _mappings[alias];
          string method = "AddTmpMapping";
          SendRpcMessage(address, method, mapping.ToString());
        }
      }
      return true;
    }

    protected bool AddTmpMapping(string address, string mapping) {
      DnsMapping new_mapping = DnsMapping.Create(mapping);
      new_mapping.Referrer = address;
      return AddTmpMapping(new_mapping);
    }

    protected bool AddTmpMapping(DnsMapping mapping) {
      SocialUser[] friends = _node.GetFriends();
      foreach(SocialUser friend in friends) {
        if(friend.Address == mapping.Address) {
          mapping.IP = friend.IP;
          break;
        }
      }
      foreach (DnsMapping tmapping in _tmappings) {
        if (mapping.Equals(tmapping)) {
          tmapping.IP = mapping.IP;   //Updates ip
          return true;
        }
      }
      _tmappings.Add(mapping);
      return true;
    }

    protected void ClearResults() {
      _tmappings.Clear();
    }

    protected string SearchLocalCache(string pattern) {
      List<DnsMapping> searchlist = new List<DnsMapping>();
      foreach(DnsMapping mapping in _tmappings) {
        if(Regex.IsMatch(mapping.Alias, pattern, RegexOptions.IgnoreCase)) {
          bool mapping_found = false;
          foreach(DnsMapping tmp_mapping in searchlist) {
            if(tmp_mapping.WeakEquals(mapping)) {
              tmp_mapping.Rating++;
              mapping_found = true;
              break;
            }
          }
          if(!mapping_found) {
            searchlist.Add(DnsMapping.Create(mapping));
          }
        }
      }
      return GetState(searchlist);
    }

    protected string GetState() {
      return SearchLocalCache("");
    }

    protected string GetState(List<DnsMapping> tmappings) {
      DnsState state = new DnsState();
      state.Mappings = new DnsMapping[_mappings.Count];
      _mappings.Values.CopyTo(state.Mappings, 0);
      tmappings.Sort(new MappingComparer());
      state.TmpMappings = tmappings.ToArray();
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
      IP = "0.0.0.0";
      CheckAlias();
    }

    private void CheckAlias() {
      if(!Alias.EndsWith("." + SocialNode.DNSSUFFIX)) {
        Alias = Alias + "." + SocialNode.DNSSUFFIX;
      }
    }

    public static DnsMapping Create(string mapping) {
      string[] parts = mapping.Split(DnsMapping.DELIM);
      return new DnsMapping(parts[0], parts[1], parts[2]);
    }

    public static DnsMapping Create(DnsMapping mapping) {
      DnsMapping new_mapping = DnsMapping.Create(mapping.ToString());
      new_mapping.IP = mapping.IP;
      return new_mapping;
    }

    public bool WeakEquals(DnsMapping mapping) {
      return (mapping.Alias == Alias && mapping.Address == Address);
    }

    public bool Equals(DnsMapping mapping) {
      return (mapping.Alias == Alias && mapping.Address == Address
        && mapping.Referrer == Referrer);
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
      int val = y.Rating - x.Rating;
      if(val == 0) {
        return String.Compare(x.Alias, y.Alias);
      }
      else {
        return val;
      }
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
