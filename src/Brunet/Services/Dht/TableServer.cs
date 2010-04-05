/*
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using Brunet.Util;
using Brunet.Concurrent;
using Brunet.Connections;

using Brunet.Messaging;
using Brunet.Symphony;
namespace Brunet.Services.Dht {
  /**
  <summary>The TableServer provides the Dht server end point.</summary>
  <remarks>Besides providing entry points for dht operations such as Get, Put,
  and Create; it also contains the logic necessary to transfer keys when there
  is churn in the system, this is implemented in ConnectionHandler,
  DepartureHandler, and TransferState.</remarks>
  */
  public class TableServer : IRpcHandler {
    /// <summary>Used to lock the TableServerData.</summary>
    protected readonly Object _sync;
    /// <summary>Used to lock connection and transfer state.</summary>
    protected readonly Object _transfer_sync;
    /// <summary>The data store for this dht server</summary>
    protected readonly TableServerData _data;
    /// <summary>The node the dht is serving from.</summary>
    protected readonly Node _node;
    /// <summary>Our right neighbors address.</summary>
    protected Address _right_addr = null;
    /// <summary>Our left neighbors address.</summary>
    protected Address _left_addr = null;
    /// <summary>The current transfer state to our right.</summary>
    protected TransferState _right_transfer_state = null;
    /// <summary>The current transfer state to our left.</summary>
    protected TransferState _left_transfer_state = null;
    /**  <summary>Do not allow dht operations until
    StructuredConnectionOverlord is connected.</summary>*/
    protected bool _online = false;
    /// <summary>Total count of key:value pairs stored locally.</summary>
    public int Count { get { return _data.Count; } }
    /// <summary>Maximum size for all values stored here.</summary>
    public const int MAX_BYTES = 1024;
    /// <summary>The RpcManager the dht is serving from.</summary>
    protected readonly RpcManager _rpc;

    /**
    <summary>Creates a new TableServer object and registers it to the "dht"
    handler in the node's RpcManager.</summary>
    <param name="node">The node the dht is to serve from.</param>
    */
    public TableServer(Node node) {
      _sync = new Object();
      _transfer_sync = new Object();

      _node = node;
      _rpc = node.Rpc;

      _data = new TableServerData(_node);
      lock(_transfer_sync) {
        node.ConnectionTable.ConnectionEvent += this.ConnectionHandler;
        node.ConnectionTable.DisconnectionEvent += this.ConnectionHandler;
        _node.StateChangeEvent += StateChangeHandler;
      }

      _rpc.AddHandler("dht", this);
    }

    /**
    <summary>This provides faster translation for Rpc methods as well as allows
    for Asynchronous Rpc calls which are required for Puts and Creates.
    </summary>
    <param name="caller">The ISender who made the request.</param>
    <param name="method">The method requested.</param>
    <param name="args">A list of arguments to pass to the method.</param>
    <param name="rs">The return state sent back to the RpcManager so that it
    knows who to return the result to.</param>
    <exception cref="Brunet::DistributedServices::Dht::Exception">Thrown when
    there the method is not Put, PutHandler, Get, Dump, or Count</exception>
    */
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
    <summary>Called by a Dht client to store data here, this supports both Puts
    and Creates by using the unique parameter.</summary>
    <remarks>Puts will store the value no matter what, Creates will only store
    the value if they are the first ones to store data on that key.  This is
    the first part of a Put operation.  This calls PutHandler on itself and
    the neighbor nearest to the key, which actually places the data into the
    store.  The result is returned to the client upon completion of the call
    to the neighbor, if that fails the data is removed locally and an exception
    is sent to the client indicating failure.</remarks>
    <param name="key">The index to store the data at.</param>
    <param name="value">Data to store at the key.</param>
    <param name="ttl">Dht lease time in seconds</param>
    <param name="unique">True if this should perform a create, false otherwise.
    </param>
    <param name="rs">The return state sent back to the RpcManager so that it
    knows who to return the result to.</param>
    <returns>True on success, thrown exception on failure</returns>
    <exception cref="Exception">Data is too large, unresolved remote issues,
    or the create is no successful</exception>
    */

    public bool Put(MemBlock key, MemBlock value, int ttl, bool unique, object rs) {
      if(value.Length > MAX_BYTES) {
        throw new Exception(String.Format(
          "Dht only supports storing data smaller than {0} bytes.", MAX_BYTES));
      }
      PutHandler(key, value, ttl, unique);
      Channel remote_put = new Channel();
      remote_put.CloseAfterEnqueue();
      remote_put.CloseEvent += delegate(Object o, EventArgs eargs) {
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
        var structs =
        _node.ConnectionTable.GetConnections(ConnectionType.Structured);
        // We need to forward this to the appropriate node!
        if(((AHAddress)_node.Address).IsLeftOf((AHAddress) key_address)) {
          var con = structs.GetRightNeighborOf(_node.Address);
          s = con.Edge;
        }
        else {
          var con = structs.GetLeftNeighborOf(_node.Address);
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
    <summary>Attempts to store the key:value pair into this server.</summary>
    <remarks>First the dht deletes any expired entries stored at the key,
    second it retrieves the entries from the data store.  If it is empty it
    creates a new entry and returns.  Otherwise, it looks for the value in
    the list and updates the lease time.  If there is no entry for that
    key:value pair it either adds it in the case of a put or throws an
    exception if it is a create.</remarks>
    <param name="key">The index to store the data at.</param>
    <param name="value">Data to store at the key.</param>
    <param name="ttl">Dht lease time in seconds</param>
    <param name="unique">True if this should perform a create, false otherwise.
    </param>
    <returns>True on success, thrown exception on failure</returns>
    <exception cref="Exception">Data is too large, unresolved remote issues,
    or the create is no successful</exception>
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
    <summary>Retrieves data from the Dht.</summary>
    <remarks>First old entries for the key are deleted from the dht, second a
    look up is performed, and finally using the token a range of data is
    selectively returned.</remarks>
    <param name="key">The index used to look up.</summary>
    <param name="token">Contains the data necessary to do follow up look ups
    if all the data stored in a key is to big for MAX_BYTES.</param>
    <returns>IList of hashtables containing the results.  Compatible with
    DhtGetResult.</returns>
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

    /// <summary>If we are or were connected in the right place, we accept DHT
    /// messages otherwise we ignore them.</summary>
    /// <param name="n">The node for this event.</param>
    /// <param name="state">The new state.</param>
    protected void StateChangeHandler(Node n, Node.ConnectionState state) {
      lock(_sync) {
        if(state == Node.ConnectionState.Leaving) {
          _online = false;
          DepartureHandler();
        } else if(state == Node.ConnectionState.Disconnected ||
            state == Node.ConnectionState.Offline) {
          _online = false;
        } else if(state == Node.ConnectionState.Connected) {
          _online = true;
        }
      }
    }

    /**
    <summary>This is called whenever there is a disconnect or a connect, the
    idea is to determine if there is a new left or right node, if there is and
    here is a pre-existing transfer, we must interupt it, and start a new
    transfer.</summary>
    <remarks>The possible scenarios where this would be active:
     - no change on left
     - new left node with no previous node (from disc or new node)
     - left disconnect and new left ready
     - left disconnect and no one ready
     - no change on right
     - new right node with no previous node (from disc or new node)
     - right disconnect and new right ready
     - right disconnect and no one ready
    </remarks>
    <param name="o">Unimportant</param>
    <param name="eargs">Contains the ConnectionEventArgs, which lets us know
    if this was a Structured Connection change and if it is, we should check
    the state of the system to see if we have a new left or right neighbor.
    </param>
    */

    protected void ConnectionHandler(object o, EventArgs eargs) {
      if(!_online) {
        return;
      }

      ConnectionEventArgs cargs = eargs as ConnectionEventArgs;
      Connection old_con = cargs.Connection;
      //first make sure that it is a new StructuredConnection
      if (old_con.MainType != ConnectionType.Structured) {
        return;
      }
      lock(_transfer_sync) {
        if(!_online) {
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

    /// <summary>Called when Node.Disconnect is triggered stopping
    /// transfer states and preventing any more transfers. </summary>
    /// <param name="o">Unimportant.</param>
    /// <param name="eargs">Unimportant.</param>
    protected void DepartureHandler() {
      lock(_transfer_sync) {
        if(_right_transfer_state != null) {
          _right_transfer_state.Interrupt();
          _right_transfer_state = null;
        }
        if(_left_transfer_state != null) {
          _left_transfer_state.Interrupt();
          _left_transfer_state = null;
        }
      }
    }

      /* Since there is support for parallel transfers, the methods for 
    * inserting the first n versus the follow up puts are different,
    * consider it an optimization.  The foreach loop goes through all the
    * keys in the local ht, if it finds one that should be transferred, it
    * goes through all the values for that key.  Once it reaches max 
    * parallel transfers, it is done.
      */
    /**
    <summary>This contains all the logic used to do the actual transfers.  This
    does MAX_PARALLEL_TRANSFERS parallel transfer to speed up the transferring
    of data to the new neighbor.  When a transfer is done, the next data to
    transfer is transferred until they all are done.  The method Interrupt() is
    an asynchronous call to stop transferring, it prevents new transfers from
    starting but allows previously started ones to continue on.
    </remarks>
    */
    protected class TransferState {
      /// <summary>Lock for the enumeration of data to transfer</summary>
      protected Object _sync = new Object();
      /// <summary>The maximum amount of transfers to make in parallel.</summary>
      protected const int MAX_PARALLEL_TRANSFERS = 10;
      /// <summary>Set when no more transfers should be made.</summary>
      protected volatile bool _interrupted = false;
      /// <summary>A linkedlist of Entry arrays containing data to transfer.</summary>
      LinkedList<Entry[]> key_entries = new LinkedList<Entry[]>();
      /// <summary>An enumerator for key_entries.</summary>
      protected IEnumerator _entry_enumerator;
      /// <summary>The connection to the neighbor we're sending the data to.</summary>
      Brunet.Connections.Connection _con;
      /// <summary>The tableserver we're providing the transfer for.</summary>
      TableServer _ts;

      /**
      <summary>Begins a new transfer state to the neighbor connected via con.
      </summary>
      <param name="con">The connection to the neigbhor we will be transferring
      data to.</param>
      <param name="ts">The table server we're providing the transfer for.  C#
      does not allow sub-class objects to have access to their parent objects
      member variables, so we pass it in like this.</param>
      <remarks>
      Step 1:

      Get all the keys between me and my new neighbor.

      Step 2:

      Get all values for those keys, we copy so that we don't worry about
      changes to the dht during this interaction.  This is only a pointer
      copy and since we let the OS deal with removing the contents of an
      entry, we don't need to make copies of the actual entry.

      Step 3:

      Generate another list of keys of up to max parallel transfers and begin
      transferring, that way we do not need to lock access to the entry
      enumerator until non-constructor puts.

      Step 4:

      End constructor, results from puts, cause the next entry to be sent.
      */
      public TransferState(Brunet.Connections.Connection con, TableServer ts) {
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

      /**
      <summary>Returns an enumerator for key_entries.</summary>
      <returns>The enumerator for key_entries.</returns>
      */
      protected IEnumerator GetEntryEnumerator() {
        foreach(Entry[] entries in key_entries) {
          foreach(Entry entry in entries) {
            yield return entry;
          }
        }
      }

      /**
      <summary>This is called by all completed transfers.  It checks to see if
      there is another value to transfer, transfers it if there is.  Otherwise
      it calls Done.</summary>
      <param name="o">The Channel where the result of the previous transfer is
      stored.</param>
      <param name="eargs">Null</param>
      */
      protected void NextTransfer(Object o, EventArgs eargs) {
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

        /* An exception could be thrown if Done is called in another thread or
        there are no more entries available. */
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

      /**
      <summary>An asyncronous interrupt that prevents future transfers, but
      does not stop current transfers.  Calls done.</summary>
      */
      public void Interrupt() {
        _interrupted = true;
        Done();
      }

      /**
      <summary>Used to clear out the key_entries when done to assist in
      garbage collection.</summary>
      */
      protected void Done() {
        key_entries.Clear();
      }
    }
  }
}
