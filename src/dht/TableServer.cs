/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 University of Florida

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
using System.Text;
using System.Collections;
using System.Reflection;
using System.Security.Cryptography;

#if BRUNET_NUNIT
using NUnit.Framework;
using System.Threading;
using System.Collections.Generic;
#endif

using Brunet;

namespace Brunet.Dht {
  public class TableServer : IRpcHandler {
    protected object _sync;

    /* Why on earth does the SortedList only allow sorting based upon keys?
     * I should really implement a more general SortedList, but we want this 
     * working asap...
     */
    protected TableServerData _data;
    protected Node _node;
    private RpcManager _rpc;

    public TableServer(Node node, RpcManager rpc) {
      _sync = new object();
      _node = node;
      _rpc = rpc;
      _data = new TableServerData(_node);
    }

    /* This is very broken now, we will need to manually update count for it
    * to work properly
    */
    public int GetCount() {
      lock(_sync) {
        _data.DeleteExpired();
        return _data.GetCount();
      }
    }


//  We implement IRpcHandler to help with Puts, so we must have this method to process
//  new Rpc commands on this object
    public void HandleRpc(ISender caller, string method, IList args, object rs) {
      // We have special case for the puts since they are done asynchronously
      if(method == "Put") {
        try {
          MemBlock key = (byte[]) args[0];
          MemBlock value = (byte[]) args[1];
          int ttl = (int) args[2];
          bool unique = (bool) args[3];
          Put(key, value, ttl, unique, rs);
        }
        catch (Exception e) {
          object result = new AdrException(-32602, e);
          _rpc.SendResult(rs, result);
        }
      }
      else {
        // Everybody else just uses the generic synchronous style
        object result = null;
        try {
          Type type = this.GetType();
          MethodInfo mi = type.GetMethod(method);
          object[] arg_array = new object[ args.Count ];
          args.CopyTo(arg_array, 0);
          result = mi.Invoke(this, arg_array);
        }
        catch(Exception e) {
          result = new AdrException(-32602, e);
        }
        _rpc.SendResult(rs, result);
      }
    }

    /**
     * This method is called by a Dht client to place data into the Dht
     * @param key key associated with the date item
     * @param key key associated with the date item
     * @param data data associated with the key
     * @param ttl time-to-live in seconds
     * @param unique determines whether or not this is a put or a create
     * @return true on success, thrown exception on failure
    */

    /* First we try locally and then remotely, they should both except if 
     * failure, so if we get rv == true + exception, we were successful
     * but remote wasn't, so we remove locally
     */

        // Here we receive the results of our put follow ups, for simplicity, we
        // have both the local Put and the remote PutHandler return the results
        // via the blockingqueue.  If it fails, we remove it locally, if the item
        // was never created it shouldn't matter.


    public void Put(MemBlock key, MemBlock value, int ttl, bool unique, object rs) {
      object result = null;
      try {
        PutHandler(key, value, ttl, unique);
        BlockingQueue remote_put = new BlockingQueue();
        remote_put.EnqueueEvent += delegate(Object o, EventArgs eargs) {
          result = null;
          try {
            bool timedout;
            result = remote_put.Dequeue(0, out timedout);
            RpcResult rpcResult = (RpcResult) result;
            result = rpcResult.Result;
            if(result.GetType() != typeof(bool)) {
              throw new Exception("Incompatible return value.");
            }
          }
          catch (Exception e) {
            result = new AdrException(-32602, e);
            _data.RemoveEntry(key, value);
          }

          remote_put.Close();
          _rpc.SendResult(rs, result);
        };


        Address key_address = new AHAddress(key);
        ISender s = null;
        // We need to forward this to the appropriate node!
        if(((AHAddress)_node.Address).IsLeftOf((AHAddress) key_address)) {
          Connection con = _node.ConnectionTable.GetRightStructuredNeighborOf((AHAddress) _node.Address);
          s = con.Edge;
        }
        else {
          Connection con = _node.ConnectionTable.GetLeftStructuredNeighborOf((AHAddress) _node.Address);
          s = con.Edge;
        }
        _rpc.Invoke(s, remote_put, "dht.PutHandler", key, value, ttl, unique);
      }
      catch (Exception e) {
        result = new AdrException(-32602, e);
        _rpc.SendResult(rs, result);
      }
    }

    /**
     * This method puts in a key-value pair at this node
     * @param key key associated with the date item
     * @param data data associated with the key
     * @param ttl time-to-live in seconds
     * @return true on success, thrown exception on failure
     */

    public bool PutHandler(byte[] keyb, byte[] valueb, int ttl, bool unique) {
      MemBlock key = MemBlock.Reference(keyb);
      MemBlock value = MemBlock.Reference(valueb);

      DateTime create_time = DateTime.UtcNow;
      TimeSpan ts = new TimeSpan(0,0,ttl);
      DateTime end_time = create_time + ts;

      lock(_sync) {
        _data.DeleteExpired();
        _data.DeleteExpired(key);
        ArrayList data = _data.GetEntries(key);
        if(data != null) {
          foreach(Entry ent in data) {
            if(ent.Value.Equals(value)) {
              if(end_time > ent.EndTime) {
                _data.UpdateEntry(ent.Key, ent.Value, end_time);
              }
              return true;
            }
          }
          // If this is a create we didn't find an previous entry, so failure, else add it
          if(unique) {
            throw new Exception("ENTRY_ALREADY_EXISTS");
          }
        }
        else {
          //This is a new key
          data = new ArrayList();
        }

        // This is either a new key or a new value (put only)
        Entry e = new Entry(key, value, create_time, end_time);
        _data.AddEntry(e);
      } // end of lock
      return true;
    }

    /**
    * Retrieves data from the Dht
    * @param key key associated with the date item
    * @param maxbytes amount of data to retrieve
    * @param token an array of ints used for continuing gets
    * @return IList of results
    */

    public IList Get(byte[] keyb, int maxbytes, byte[] token) {
      MemBlock key = MemBlock.Reference(keyb);
      int seen_start_idx = 0;
      int seen_end_idx = 0;
      if( token != null ) {
        int[] bounds = (int[])AdrConverter.Deserialize(new System.IO.MemoryStream(token));
        seen_start_idx = bounds[0];
        seen_end_idx = bounds[1];
        seen_start_idx = seen_end_idx + 1;
      }

      int consumed_bytes = 0;

      ArrayList result = new ArrayList();
      ArrayList values = new ArrayList();
      int remaining_items = 0;
      byte[] next_token = null;

      lock(_sync ) {
        _data.DeleteExpired();
        _data.DeleteExpired(key);
        ArrayList data = _data.GetEntries(key);

        // Keys exist!
        if( data != null ) {
          seen_end_idx = data.Count - 1;
          for(int i = seen_start_idx; i < data.Count; i++) {
            Entry e = (Entry) data[i];
            if (e.Value.Length + consumed_bytes <= maxbytes) {
              int age = (int) (DateTime.UtcNow - e.CreateTime).TotalSeconds;
              int ttl = (int) (e.EndTime - e.CreateTime).TotalSeconds;
              consumed_bytes += e.Value.Length;
              Hashtable item = new Hashtable();
              item["age"] = age;
              item["value"] = (byte[])e.Value;
              item["ttl"] = ttl;
              values.Add(item);
            }
            else {
              seen_end_idx = i - 1;
              break;
            }
          }
          remaining_items = data.Count - (seen_end_idx + 1);
        }
      }//End of lock
      //we have added new item: update the token
      int[] new_bounds = new int[2];
      new_bounds[0] = seen_start_idx;
      new_bounds[1] = seen_end_idx;
      //new_bounds has to be converted to a new token
      System.IO.MemoryStream ms = new System.IO.MemoryStream();
      AdrConverter.Serialize(new_bounds, ms);
      next_token = ms.ToArray();
      result.Add(values);
      result.Add(remaining_items);
      result.Add(next_token);
      return result;
    }

    /** protected methods. */

    /** Get all the keys to left of some address.
    *  Note that this depends on whether the ring is stored clockwise or
    *  anti-clockwise, we assume clockwise!
    *  
    */
    public Hashtable GetKeysToLeft(AHAddress us, AHAddress within) {
      lock(_sync) {
        _data.DeleteExpired();
        Hashtable key_list = new Hashtable();
        foreach (MemBlock key in _data.GetKeys()) {
            AHAddress target = new AHAddress(key);
            if (target.IsBetweenFromLeft(us, within)) {
              _data.DeleteExpired(key);
              ArrayList data = _data.GetEntries(key);
              if(data != null) {
                key_list[key] = data.Clone();
              }
            }
        }
        return key_list;
      }
    }

    /** Get all the keys to right of some address.
    *  Note that this depends on whether the ring is stored clockwise or
    *  anti-clockwise, we assume clockwise!
    */

    public Hashtable GetKeysToRight(AHAddress us, AHAddress within) {
      lock(_sync) {
        _data.DeleteExpired();
        Hashtable key_list = new Hashtable();
        foreach (MemBlock key in _data.GetKeys()) {
          AHAddress target = new AHAddress(key);
          if (target.IsBetweenFromRight(us, within)) {
            _data.DeleteExpired(key);
            ArrayList data = _data.GetEntries(key);
            if(data != null) {
              key_list[key] = data.Clone();
            }
          }
        }
        return key_list;
      }
    }

    //Note: This is critical method, and allows dropping complete range of keys.
    public void AdminDelete(Hashtable key_list) {
      lock(_sync ) {
        //delete keys that have expired
        _data.DeleteExpired();
        foreach (MemBlock key in key_list.Keys) {
          _data.RemoveEntries(key);
        }
      }
    }
  }
}
