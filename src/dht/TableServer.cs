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
using System.Collections.Generic;

#if BRUNET_NUNIT
using NUnit.Framework;
using System.Threading;
#endif

using Brunet;

namespace Brunet.Dht {
  public class TableServer : IRpcHandler {
    protected object _sync, _transfer_sync;

    /* Why on earth does the SortedList only allow sorting based upon keys?
     * I should really implement a more general SortedList, but we want this 
     * working asap...
     */
    private TableServerData _data;
    private Node _node;
    protected Address _right_addr = null, _left_addr = null;
    protected TransferState _right_transfer_state = null, _left_transfer_state = null;
    protected bool _dhtactivated = false, disconnected = false;
    public bool Activated { get { return _dhtactivated; } }
    public int Count { get { return _data.Count; } }
    private RpcManager _rpc;

    public TableServer(Node node, RpcManager rpc) {
      _sync = new object();
      _node = node;
      _rpc = rpc;
      _data = new TableServerData(_node);
      _transfer_sync = new object();
      lock(_transfer_sync) {
        node.ConnectionTable.ConnectionEvent += this.ConnectionHandler;
        node.ConnectionTable.DisconnectionEvent += this.ConnectionHandler;
        node.ConnectionTable.StatusChangedEvent += this.StatusChangedHandler;
        node.DepartureEvent += this.DepartureHandler;
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
        _data.DeleteExpired(key);
        LinkedList<Entry> data = _data.GetEntries(key);
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
        _data.DeleteExpired(key);
        LinkedList<Entry> ll_data = _data.GetEntries(key);
        Entry[] data = new Entry[ll_data.Count];
        ll_data.CopyTo(data, 0);

        // Keys exist!
        if( data != null ) {
          seen_end_idx = data.Length - 1;
          for(int i = seen_start_idx; i < data.Length; i++) {
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
          remaining_items = data.Length - (seen_end_idx + 1);
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

    /* This method checks to see if the node is connected and activates
     * the Dht if it is.
     */
    protected void StatusChangedHandler(object contab, EventArgs eargs) {
      if(!_dhtactivated && _node.IsConnected) {
            _dhtactivated = true;
      }
    }

    /* This is called whenever there is a disconnect or a connect, the idea
     * is to determine if there is a new left or right node, if there is and
     * there is a pre-existing transfer, we must interuppt it, and start a new
     * transfer
     */

    private void ConnectionHandler(object o, EventArgs eargs) {
      if(disconnected) {
        return;
      }

      ConnectionEventArgs cargs = eargs as ConnectionEventArgs;
      Connection old_con = cargs.Connection;
      //first make sure that it is a new StructuredConnection
      if (old_con.MainType != ConnectionType.Structured) {
        return;
      }
      lock(_transfer_sync) {
        if(disconnected) {
          return;
        }
        ConnectionTable tab = _node.ConnectionTable;
        Connection lc = null, rc = null;
        try {
          lc = tab.GetLeftStructuredNeighborOf((AHAddress) _node.Address);
        }
        catch(Exception) {}
        try {
          rc = tab.GetRightStructuredNeighborOf((AHAddress) _node.Address);
        }
        catch(Exception) {}

        /* Cases
         * no change on left
         * new left node with no previous node (from disc or new node)
         * left disconnect and new left ready
         * left disconnect and no one ready
         * no change on right
         * new right node with no previous node (from disc or new node)
         * right disconnect and new right ready
         * right disconnect and no one ready
         */
        if(lc != null) {
          if(lc.Address != _left_addr) {
            if(_left_transfer_state != null) {
              _left_transfer_state.Interrupt();
              _left_transfer_state = null;
            }
            _left_addr = lc.Address;
            if(Count > 0) {
              _left_transfer_state = new TransferState(true, this);
            }
          }
        }
        else if(_left_addr != null) {
          if(_left_transfer_state != null) {
            _left_transfer_state.Interrupt();
            _left_transfer_state = null;
          }
          _left_addr = null;
        }

        if(rc != null) {
          if(rc.Address != _right_addr) {
            if(_right_transfer_state != null) {
              _right_transfer_state.Interrupt();
              _right_transfer_state = null;
            }
            _right_addr = rc.Address;
            if(Count > 0) {
              _right_transfer_state = new TransferState(false, this);
            }
          }
        }
        else if(_right_addr != null) {
          if(_right_transfer_state != null) {
            _right_transfer_state.Interrupt();
            _right_transfer_state = null;
          }
          _right_addr = null;
        }
      }
    }

    private void DepartureHandler(Object o, EventArgs eargs) {
      lock(_transfer_sync) {
        if(_right_transfer_state != null) {
          _right_transfer_state.Interrupt();
          _right_transfer_state = null;
        }
        if(_left_transfer_state != null) {
          _left_transfer_state.Interrupt();
          _left_transfer_state = null;
        }
        this.disconnected = true;
      }
    }

    // This contains all the logic used to do the actual transfers
    protected class TransferState {
      protected const int MAX_PARALLEL_TRANSFERS = 10;
      private object _interrupted = false;
      LinkedList<Entry[]> key_entries = new LinkedList<Entry[]>();
      private IEnumerator _entry_enumerator;
      Connection _con;
      TableServer _ts;

      /* Since there is support for parallel transfers, the methods for 
       * inserting the first n versus the follow up puts are different,
       * consider it an optimization.  The foreach loop goes through all the
       * keys in the local ht, if it finds one that should be transferred, it
       * goes through all the values for that key.  Once it reaches max 
       * parallel transfers, it is done.
       */
      public TransferState(bool left, TableServer ts) {
        this._ts = ts;
        ConnectionTable tab = _ts._node.ConnectionTable;
        if(left) {
          try {
            _con = tab.GetLeftStructuredNeighborOf((AHAddress) _ts._node.Address);
          }
          catch(Exception) {}
        }
        else {
          try {
            _con = tab.GetRightStructuredNeighborOf((AHAddress) _ts._node.Address);
          }
          catch(Exception) {}
        }
        if(_con == null) {
          return;
        }
        LinkedList<MemBlock> keys =
            _ts._data.GetKeysBetween((AHAddress) _ts._node.Address, 
                                      (AHAddress) _con.Address);
        foreach(MemBlock key in keys) {
          Entry[] entries = new Entry[_ts._data.GetEntries(key).Count];
          _ts._data.GetEntries(key).CopyTo(entries, 0);
          key_entries.AddLast(entries);
        }
        _entry_enumerator = GetEntryEnumerator();

        int count = 0;
        LinkedList<Entry> local_entries = new LinkedList<Entry>();
        lock(_entry_enumerator) {
          while(_entry_enumerator.MoveNext() && count++ == MAX_PARALLEL_TRANSFERS) {
            local_entries.AddLast((Entry) _entry_enumerator.Current);
          }
        }
        foreach(Entry ent in local_entries) {
          BlockingQueue queue = new BlockingQueue();
          queue.EnqueueEvent += this.NextTransfer;
          queue.CloseEvent += this.NextTransfer;
          int ttl = (int) (ent.EndTime - DateTime.UtcNow).TotalSeconds;
          _ts._rpc.Invoke(_con.Edge, queue, "dht.PutHandler", ent.Key, ent.Value, ttl, false);
        }
      }

      private IEnumerator GetEntryEnumerator() {
        foreach(Entry[] entries in key_entries) {
          foreach(Entry entry in entries) {
            yield return entry;
          }
        }
      }

      /* This determines if there is a new value to be transferred or if the
       * transfer is complete
       */
      private void NextTransfer(Object o, EventArgs eargs) {
        BlockingQueue queue = (BlockingQueue) o;
        queue.EnqueueEvent -= this.NextTransfer;
        queue.CloseEvent -= this.NextTransfer;
        try {
          queue.Dequeue();
        }
        catch (Exception e){
          Console.Error.WriteLine("Maybe the timeouts are too low....\n" + e);
        }
        if((bool) _interrupted) {
          return;
        }
        Entry ent = null;
        lock(_entry_enumerator) {
          if(_entry_enumerator.MoveNext()) {
            ent = (Entry) _entry_enumerator.Current;
          }
        }
        if(ent != null) {
          queue = new BlockingQueue();
          queue.EnqueueEvent += this.NextTransfer;
          queue.CloseEvent += this.NextTransfer;
          int ttl = (int) (ent.EndTime - DateTime.UtcNow).TotalSeconds;
          _ts._rpc.Invoke(_con.Edge, queue, "dht.PutHandler", ent.Key, ent.Value, ttl, false);
        }
      }

      public void Interrupt() {
        lock(_interrupted) {
          _interrupted = true;
        }
      }
    }
  }
}
