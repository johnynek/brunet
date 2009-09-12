/*
Copyright (C) 2009  Kyungyong Lee <kyungyonglee@ufl.edu>, University of Florida

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
using Brunet.Util;

namespace Brunet.DistributedServices {
  /// <summary>Provides RpcProxyHandler service, 
  /// which reinserts dht entry before its ttl expires</summary>
  public class RpcDhtProxy : IRpcHandler {
    public static readonly int RETRY_TIMEOUT = 30000;
    protected RpcManager _rpc;
    protected static IDht _dht;
    protected Dictionary<MemBlock, Dictionary<MemBlock, Entry>> _entries;
    protected object _sync;


    /// <summary>Initiates a RpcProxyHandler instance. It uses reflection for rpc call.
    /// Thus, it does not have to inherit IRpcHanler.This instance keeps Entry
    /// to keep track of key, value, and ttl</summary>
    /// <param name="node">node which is currently connected.</param>
    /// <param name="dht">IDht instance</param>
    public RpcDhtProxy(IDht dht, Node node)
    {
      _entries = new Dictionary<MemBlock, Dictionary<MemBlock, Entry>>();
      _rpc = node.Rpc;
      _dht = dht;
      _sync = new Object();
      _rpc.AddHandler("RpcDhtProxy", this);
    }

    public void HandleRpc(ISender caller, string method, IList arguments, object request_state)
    {
      object result = null;
      MemBlock key;
      MemBlock value;
      int ttl;
        
      try {
        switch(method) {
          case "Register": 
            key = MemBlock.Reference((byte[]) arguments[0]);
            value = MemBlock.Reference((byte[]) arguments[1]);
            ttl = (int) arguments[2];
            result = Register(key, value, ttl);
            break;
          case "Unregister":
            key = MemBlock.Reference((byte[]) arguments[0]);
            value = MemBlock.Reference((byte[]) arguments[1]);
            result = Unregister(key, value);
            break;
          case "ListEntries":
            result = ListEntries();
            break;
          default:
            throw new Exception("Invalid method");
        }
      }
      catch (Exception e) {
        result = new AdrException(-32602, e);
      }
      _rpc.SendResult(request_state, result);
    }

    /// <summary>This is a RpcDhtProxy rpc call entry, which can be called using "RpcDhtProxy.Register"
    /// Register the entry to Entry. If the key,value pair does not 
    /// exist in _entries, it creates the pair in the list. Otherwise, it updates the ttl.
    /// After inserting the entry, this module try to register the key, value pair to neighbor node.</summary>
    /// <param name="key">dht entry key to insert</param>
    /// <param name="value">dht entry value to insert</param>
    /// <param name="ttl">dht entry ttl to insert</param>
    public bool Register(MemBlock key, MemBlock value, int ttl)
    {
      Entry entry = null;
      lock(_sync) {
        Dictionary<MemBlock, Entry> key_entries = null;
        if(!_entries.TryGetValue(key, out key_entries)) {
          key_entries = new Dictionary<MemBlock, Entry>();
          _entries[key] = key_entries;
        }

        if(key_entries.ContainsKey(value)) {
          key_entries[value].Timer.Stop();
        }

        entry = new Entry(key, value, ttl);
        key_entries[value] = entry;
      }

      if(entry != null) {
        entry.Timer = new SimpleTimer(EntryCallback, entry, 0, RETRY_TIMEOUT);
        entry.Timer.Start();
      }
      return true;
    }

    ///<summary>Unregister the proxy entry. Removes the entry from Dictionary and set
    /// the stop the timer to disable future reference</summary>
    public bool Unregister(MemBlock key, MemBlock value)
    {
      lock(_sync) {
        if(_entries.ContainsKey(key)) {
          if(_entries[key].ContainsKey(value)) {
            _entries[key][value].Timer.Stop();
            if(_entries[key].Count == 1) {
              _entries.Remove(key);
            } else {
              _entries[key].Remove(value);
            }
          }
        }
      }
      return true;
    }
    
    ///<summary>Returns all stored values in a list.</summary>
    public object ListEntries()
    {
      ArrayList all_entries = new ArrayList();
      lock(_sync) {
        foreach(KeyValuePair<MemBlock, Dictionary<MemBlock, Entry>> key_entries in _entries) {
          foreach(KeyValuePair<MemBlock, Entry> values in key_entries.Value) {
            all_entries.Add((Hashtable) values.Value);
          }
        }
      }
      return all_entries;
    }

    ///<summary> If half of ttl time passed, this event handler is called. AlarmEventHandler calls
    ///"DhtClient.Put" command to insert the entry to other nodes. It restarts the timer.
    /// If error occurs during ASyncPut, it retries after 30 seconds</summary>
    /// <param name="o">Entry which initiates ttl time expire event</param>
    public void EntryCallback(object o)
    {
      Entry entry = o as Entry;
      Channel returns = new Channel(1);
      returns.CloseEvent += delegate(object obj, EventArgs eargs) {
        bool success = false;
        try {
          success = (bool) returns.Dequeue();
        } catch {
          success = false;
        }

        if(success && !entry.Working) {
          entry.Timer.Stop();
          entry.Working = true;
          int time = entry.Ttl * 1000 / 2;
          entry.Timer = new SimpleTimer(EntryCallback, entry, time, time);
          entry.Timer.Start();
        } else if(!success && entry.Working) {
          entry.Timer.Stop();
          entry.Working = false;
          entry.Timer = new SimpleTimer(EntryCallback, entry, RETRY_TIMEOUT, RETRY_TIMEOUT);
          entry.Timer.Start();
        }
      };

      _dht.AsyncPut(entry.Key, entry.Value, entry.Ttl, returns);
    }

    ///<summary> This class keeps Entry, whose element is key, value, ttl, and
    ///brunet timer.  Each instance has its own timer, so we don't need to
    ///order or track each proxy entry to notice the oldest entry.   Each timer
    ///initiates ttl expiration. While ttl expiration, it calls EntryCallback.
    ///</summary>
    public class Entry {
      public readonly MemBlock Key;
      public readonly MemBlock Value;
      public readonly int Ttl;
      public SimpleTimer Timer;
      public bool Working;

      public Entry(MemBlock key, MemBlock value, int ttl)
      {
        Key = key;
        Value = value;
        Ttl = ttl;
        Working = false;
      }

      public static explicit operator Hashtable(Entry e)
      {
        Hashtable ht = new Hashtable(2);
        ht["Key"] = e.Key;
        ht["Value"] = e.Value;
        return ht;
      }
    }
  }
}
