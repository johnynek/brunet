/*
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.IO;
using System.Text;
using System.Collections;
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
    public bool debug = false;
    public int Count { get { return _data.Count; } }
    public const int MAX_BYTES = 1024;
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
      object result = null;
      try {
        if(method.Equals("Put")) {
          MemBlock key = (byte[]) args[0];
          MemBlock value = (byte[]) args[1];
          int ttl = (int) args[2];
          bool unique = (bool) args[3];
          Put(key, value, ttl, unique, rs);
          return;
        }
        else if(method.Equals("PutHandler")) {
          MemBlock key = (byte[]) args[0];
          MemBlock value = (byte[]) args[1];
          int ttl = (int) args[2];
          bool unique = (bool) args[3];
          result = PutHandler(key, value, ttl, unique);
        }
        else if(method.Equals("Get")) {
          MemBlock key = (byte[]) args[0];
          // Hack for backwards compatibility, supports forwards too
          int token_pos = args.Count - 1;
          if(args[token_pos] == null) {
           result = Get(key, null);
          }
          else {
            result = Get(key, (byte[]) args[token_pos]);
          }
        }
        else if(method.Equals("Dump")) {
          lock(_sync) {
            result = _data.Dump();
          }
        }
        else if(method.Equals("Count")) {
          result = Count;
        }
        else {
          throw new Exception("Dht.Exception:  Invalid method");
        }
      }
      catch (Exception e) {
        result = new AdrException(-32602, e);
      }
      _rpc.SendResult(rs, result);
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
        // via the Channel.  If it fails, we remove it locally, if the item
        // was never created it shouldn't matter.


    public bool Put(MemBlock key, MemBlock value, int ttl, bool unique, object rs) {
      if(value.Length > MAX_BYTES) {
        throw new Exception(String.Format(
          "Dht only supports storing data smaller than {0} bytes.", MAX_BYTES));
      }
      PutHandler(key, value, ttl, unique);
      Channel remote_put = new Channel();
      remote_put.CloseAfterEnqueue();
      remote_put.EnqueueEvent += delegate(Object o, EventArgs eargs) {
        object result = false;
        try {
          result = remote_put.Dequeue();
          RpcResult rpcResult = (RpcResult) result;
          result = rpcResult.Result;
          if(result.GetType() != typeof(bool)) {
            throw new Exception("Incompatible return value.");
          }
          else if(!(bool) result) {
            throw new Exception("Unknown error!");
          }
        }
        catch (Exception e) {
          lock(_sync) {
            _data.RemoveEntry(key, value);
          }
          result = new AdrException(-32602, e);
        }
        _rpc.SendResult(rs, result);
      };

      try {
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
      catch (Exception) {
        lock(_sync) {
          _data.RemoveEntry(key, value);
        }
        throw;
      }
      return true;
    }

    /**
     * This method puts in a key-value pair at this node
     * @param key key associated with the date item
     * @param data data associated with the key
     * @param ttl time-to-live in seconds
     * @return true on success, thrown exception on failure
     */

    public bool PutHandler(MemBlock key, MemBlock value, int ttl, bool unique) {
      DateTime create_time = DateTime.UtcNow;
      DateTime end_time = create_time.AddSeconds(ttl);

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

    public IList Get(MemBlock key, byte[] token) {
      int seen_start_idx = 0;
      int seen_end_idx = 0;
      if( token != null ) {
        using(MemoryStream ms = new MemoryStream(token)) {
          int[] bounds = (int[])AdrConverter.Deserialize(ms);
          seen_start_idx = bounds[0];
          seen_end_idx = bounds[1];
          seen_start_idx = seen_end_idx + 1;
        }
      }

      int consumed_bytes = 0;
      Entry[] data = null;

      lock(_sync ) {
        _data.DeleteExpired(key);
        LinkedList<Entry> ll_data = _data.GetEntries(key);

        // Keys exist!
        if( ll_data != null ) {
          data = new Entry[ll_data.Count];
          ll_data.CopyTo(data, 0);
        }
      }

      ArrayList result = null;

      if(data != null) {
        result = new ArrayList();
        ArrayList values = new ArrayList();
        int remaining_items = 0;
        byte[] next_token = null;

        seen_end_idx = data.Length - 1;
        for(int i = seen_start_idx; i < data.Length; i++) {
          Entry e = (Entry) data[i];
          if(e.Value.Length + consumed_bytes <= MAX_BYTES) {
            int age = (int) (DateTime.UtcNow - e.CreateTime).TotalSeconds;
            int ttl = (int) (e.EndTime - DateTime.UtcNow).TotalSeconds;
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

        //Token creation
        int[] new_bounds = new int[2];
        new_bounds[0] = seen_start_idx;
        new_bounds[1] = seen_end_idx;
        using(MemoryStream ms = new System.IO.MemoryStream()) {
          AdrConverter.Serialize(new_bounds, ms);
          next_token = ms.ToArray();
        }
        result.Add(values);
        result.Add(remaining_items);
        result.Add(next_token);
      }
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
              _left_transfer_state = new TransferState(lc, this);
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
              _right_transfer_state = new TransferState(rc, this);
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
      protected object _sync = new object();
      protected const int MAX_PARALLEL_TRANSFERS = 10;
      private volatile bool _interrupted = false;
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
      public TransferState(Connection con, TableServer ts) {
        this._ts = ts;
        this._con = con;
        // Get all keys between me and my new neighbor
        LinkedList<MemBlock> keys;
        lock(_ts._sync) {
          keys = _ts._data.GetKeysBetween((AHAddress) _ts._node.Address,
                                      (AHAddress) _con.Address);
        }
        if(Dht.DhtLog.Enabled) {
          ProtocolLog.Write(Dht.DhtLog, String.Format(
                            "Starting transfer from {0} to {1}", 
                            _ts._node.Address, _con.Address));
        }
        int total_entries = 0;
        /* Get all values for those keys, we copy so that we don't worry about
         * changes to the dht during this interaction.  This is only a pointer
         * copy and since we let the OS deal with removing the contents of an
         * entry, we don't need to make copies of the actual entry.
         */
        foreach(MemBlock key in keys) {
          Entry[] entries;
          lock(_ts._sync) {
            LinkedList<Entry> llentries = _ts._data.GetEntries(key);
            if(llentries == null) {
              continue;
            }
            entries = new Entry[llentries.Count];
            total_entries += llentries.Count;
            llentries.CopyTo(entries, 0);
          }
          key_entries.AddLast(entries);
        }
        if(Dht.DhtLog.Enabled) {
          ProtocolLog.Write(Dht.DhtLog, String.Format(
                            "Total keys: {0}, total entries: {1}.", 
                            key_entries.Count, total_entries));
        }
        _entry_enumerator = GetEntryEnumerator();

        /* Here we generate another list of keys that we would like to 
         * this is done here, so that we can lock up the _entry_enumerator
         * only during this stage and not during the RpcManager.Invoke
         */
        LinkedList<Entry> local_entries = new LinkedList<Entry>();
        for(int i = 0; i < MAX_PARALLEL_TRANSFERS && _entry_enumerator.MoveNext(); i++) {
          local_entries.AddLast((Entry) _entry_enumerator.Current);
        }

        foreach(Entry ent in local_entries) {
          Channel queue = new Channel();
          queue.CloseAfterEnqueue();
          queue.CloseEvent += this.NextTransfer;
          int ttl = (int) (ent.EndTime - DateTime.UtcNow).TotalSeconds;
          try {
            _ts._rpc.Invoke(_con.Edge, queue, "dht.PutHandler", ent.Key, ent.Value, ttl, false);
          }
          catch {
            if(_con.Edge.IsClosed) {
              _interrupted = true;
              Done();
              break;
            }
          }
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
        Channel queue = (Channel) o;
        queue.CloseEvent -= this.NextTransfer;
        /* No point in dequeueing, if we've been interrupted, we most likely
         * will get an exception!
         */
        if(_interrupted) {
          return;
        }
        try {
          queue.Dequeue();
        }
        catch (Exception){
          if(_con.Edge.IsClosed) {
            _interrupted = true;
            Done();
            return;
          }
        }

        Entry ent = null;
        try {
          lock(_sync) {
            if(_entry_enumerator.MoveNext()) {
              ent = (Entry) _entry_enumerator.Current;
            }
          }
        }
        catch{}
        if(ent != null) {
          queue = new Channel();
          queue.CloseAfterEnqueue();
          queue.CloseEvent += this.NextTransfer;
          int ttl = (int) (ent.EndTime - DateTime.UtcNow).TotalSeconds;
          try {
            _ts._rpc.Invoke(_con.Edge, queue, "dht.PutHandler", ent.Key, ent.Value, ttl, false);
          }
          catch {
            if(_con.Edge.IsClosed) {
              _interrupted = true;
            }
          }
        }
        else {
          Done();
          if(Dht.DhtLog.Enabled) {
            ProtocolLog.Write(Dht.DhtLog, String.Format(
                              "Successfully complete transfer from {0} to {1}",
                              _ts._node.Address, _con.Address));
          }
        }
      }

      public void Interrupt() {
        _interrupted = true;
        Done();
      }

      private void Done() {
        key_entries.Clear();
      }
    }
  }
}
