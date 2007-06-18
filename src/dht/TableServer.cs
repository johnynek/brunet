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

    //maintain a list of keys that are expiring:
    //list of keys sorted on expiration times
    protected ArrayList _expiring_entries;

    protected Hashtable _ht;
    protected int _max_idx;

    protected Node _node;
    protected EntryFactory _ef;
    private RpcManager _rpc;
    private Hashtable _put_table= new Hashtable();

    public TableServer(EntryFactory ef, Node node, RpcManager rpc) {
      _sync = new object();
      _node = node;
      _ef = ef;
      _expiring_entries = new ArrayList();
      _ht = new Hashtable();
      _max_idx = 0;
      _rpc = rpc;
    }

//  We implement IRpcHandler to help with Puts, so we must have this method to process
//  new Rpc commands on this object
    public void HandleRpc(ISender caller, string method, IList args, object rs) {
      // We have special case for the puts since they are done asynchronously
      if(method == "Put") {
        BlockingQueue queue = new BlockingQueue();
        try {
          MemBlock key = (byte[]) args[0];
          MemBlock value = (byte[]) args[1];
          int ttl = (int) args[2];
          bool unique = (bool) args[3];
          // Puts are started by Put, but finished by the HandlePutResult
          queue.EnqueueEvent += this.HandlePutResult;
          Hashtable new_put = new Hashtable();
          new_put["key"] = key;
          new_put["value"] = value;
          new_put["request_state"] = rs;
          lock(_put_table) {
            _put_table[queue] = new_put;
          }
          this.Put(key, value, ttl, unique, queue);
        }
        catch (Exception e) {
          queue.EnqueueEvent -= this.HandlePutResult;
          queue.Close();
          lock(_put_table) {
            _put_table.Remove(queue);
          }
          AdrException ae = new AdrException(-32602, e);
          _rpc.SendResult(rs, ae);
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

    // Here we receive the results of our put follow ups, for simplicity, we
    // have both the local Put and the remote PutHandler return the results
    // via the blockingqueue.  If it fails, we remove it locally, if the item
    // was never created it shouldn't matter.
    public void HandlePutResult(Object o, EventArgs args) {
      BlockingQueue queue = (BlockingQueue) o;
      Hashtable finalize_put = (Hashtable) _put_table[queue];
      lock(_put_table) {
        _put_table.Remove(queue);
      }

      object result = null;
      bool bresult = false;
      try {
        bool timedout;
        result = queue.Dequeue(0, out timedout);
        RpcResult rpcResult = (RpcResult) result;
        result = rpcResult.Result;
        try {
          bresult = (bool) result;
        }
        catch(Exception e) {
          bresult = false;
          result = new AdrException(-32602, e);
        }
      }
      catch (Exception e) {
        try {
          result = new AdrException(-32602, (Exception) result);
        }
        catch {
          result = new AdrException(-32602, e);
        }
      }

      // Looks like we didn't succeed, thus time to remove
      Object request_state = finalize_put["request_state"];
      if(!bresult) {
        MemBlock key = (MemBlock) finalize_put["key"];
        MemBlock value = (MemBlock) finalize_put["value"];
        this.RemoveEntry(key, value);
      }

      queue.EnqueueEvent -= this.HandlePutResult;
      queue.Close();
      _rpc.SendResult(request_state, result);
    }

    public int GetCount() {
      int count = 0;
      lock(_sync) {
        DeleteExpired();
        foreach (Object val in _ht.Values) 
        {
          ArrayList entry_list = (ArrayList) val;
          count += entry_list.Count;
        }
      }
      return count;
    }

    protected void UpdateEntry(ArrayList entry_list, Entry ent, DateTime end_time) {
      entry_list.Remove(ent);
      DeleteFromSorted(ent);

      Entry new_e = _ef.CreateEntry(ent.Key, ent.CreatedTime, end_time, 
                                    ent.Data, ent.Index);
      entry_list.Add(new_e);
      InsertToSorted(new_e);
    }

    protected void RemoveEntry(MemBlock key, MemBlock data) {
      ArrayList entry_list = (ArrayList)_ht[key];
      foreach(Entry ent in entry_list) {
        if(ent.Data.Equals(data)) {
          entry_list.Remove(ent);
          DeleteFromSorted(ent);
          ent.Delete();
          break;
        }
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

    public void Put(MemBlock key, MemBlock data, int ttl, bool unique, BlockingQueue remote_put) {
      try {
        PutHandler(key, data, ttl, unique);
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
        _rpc.Invoke(s, remote_put, "dht.PutHandler", key, data, ttl, unique);
      }
      catch (Exception e) {
        // We failed :(, we don't need to worry about the Invoke's results
        remote_put.Enqueue(e);
        remote_put.Close();
      }
    }

    /**
     * This method puts in a key-value pair at this node
     * @param key key associated with the date item
     * @param data data associated with the key
     * @param ttl time-to-live in seconds
     * @return true on success, thrown exception on failure
     */

    public bool PutHandler(byte[] keyb, byte[] datab, int ttl, bool unique) {
      MemBlock key = MemBlock.Reference(keyb);
      MemBlock data = MemBlock.Reference(datab);

      DateTime create_time = DateTime.UtcNow;
      TimeSpan ts = new TimeSpan(0,0,ttl);
      DateTime end_time = create_time + ts;
      ArrayList entry_list = null;

      lock(_sync) {
        DeleteExpired();
        entry_list = (ArrayList)_ht[key];
        if( entry_list != null ) {
          key = ((Entry)entry_list[0]).Key;
          foreach(Entry ent in entry_list) {
            if(ent.Data.Equals(data)) {
              if(end_time > ent.EndTime) {
                UpdateEntry(entry_list, ent, end_time);
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
          entry_list = new ArrayList();
          _ht[key] = entry_list;
        }

        // This is either a new key or a new value (put only)
        _max_idx++;
        // Create a new entry
        Entry e = _ef.CreateEntry(key, create_time, end_time, data, _max_idx);

        entry_list.Add(e);
        InsertToSorted(e);
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
      int seen_start_idx = -1;
      int seen_end_idx = -1;
      if( token != null ) {
        int[] bounds = (int[])AdrConverter.Deserialize(new System.IO.MemoryStream(token));
        seen_start_idx = bounds[0];
        seen_end_idx = bounds[1];
      }

      int consumed_bytes = 0;

      ArrayList result = new ArrayList();
      ArrayList values = new ArrayList();
      int remaining_items = 0;
      byte[] next_token = null;

      lock(_sync ) {
        DeleteExpired();
        ArrayList entry_list = (ArrayList)_ht[key];

        int seen = 0;
        // Keys exist!
        if( entry_list != null ) {
          int max_index = seen_end_idx;
          foreach(Entry e in entry_list) {
            // Have we seen this and do we have enough space for it?
            if(e.Index > seen_end_idx) {
              if (e.Data.Length + consumed_bytes <= maxbytes) {
                TimeSpan age = DateTime.UtcNow - e.CreatedTime;
                int age_i = (int)age.TotalSeconds;
                consumed_bytes += e.Data.Length;
                Hashtable item = new Hashtable();
                item["age"] = age_i;
                item["value"] = e.Data;
                values.Add(item);
                if (e.Index > max_index) {
                  max_index= e.Index;
                }
              }
              else {
                break;
              }
            }
            seen++;
          }
          seen_end_idx = max_index;
          remaining_items = entry_list.Count - seen;
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

    /** The method gets rid of keys that have expired. 
    *  (Assuming that _expiring_entries is sorted).
    */
    protected void DeleteExpired() {
      int del_count = 0;
      DateTime now = DateTime.UtcNow;
      foreach(Entry e in _expiring_entries) {
        DateTime end_time = e.EndTime;
        // These should be sorted so we will break once we find an end_time greater than now
        if (end_time > now) {
          break;
        }
        // Expired entry, must delete it
        ArrayList entry_list = (ArrayList) _ht[e.Key];
        if (entry_list == null) {
          Console.Error.WriteLine("Fatal error missing key during DeleteExpired()");
          continue;
        }
        entry_list.Remove(e);
        e.Delete();
        if (entry_list.Count == 0) {
          _ht.Remove(e.Key);
        }
        del_count++;
      }
      if (del_count > 0) {
        _expiring_entries.RemoveRange(0, del_count);
      }
    }

    /** Add to _expiring entries. */
    protected void InsertToSorted(Entry new_entry) {
      int idx = 0;
      foreach(Entry e in _expiring_entries) {
        if (new_entry.EndTime < e.EndTime) {
          break;
        }
        idx++;
      }
      _expiring_entries.Insert(idx, new_entry);
    }

    /** we further need a way to get rid of entries that are deleted.*/
    protected void DeleteFromSorted(Entry e) {
      _expiring_entries.Remove(e);
    }

    /** Methods not exposed by DHT but available only within DHT. */

    /** Not RPC related methods. */
    /** Invoked by local DHT object. */
    public ArrayList GetValues(MemBlock key) {
      lock(_sync) {
        ArrayList entry_list = (ArrayList)_ht[key];
        return entry_list;
      }
    }

    /** Get all the keys to left of some address.
    *  Note that this depends on whether the ring is stored clockwise or
    *  anti-clockwise, we assume clockwise!
    *  
    */
    public Hashtable GetKeysToLeft(AHAddress us, AHAddress within) {
/*      if(us == null)
//        Console.WriteLine("us went null");
      if(within == null)
//        Console.WriteLine("within went null");*/
      lock(_sync) {
        Hashtable key_list = new Hashtable();
        foreach (MemBlock key in _ht.Keys) {
            AHAddress target = new AHAddress(key);
            if (target.IsBetweenFromLeft(us, within)) {
              //this is a relevant key
              //we want to share it
              ArrayList entry_list = (ArrayList)_ht[key];
              key_list[key] = entry_list.Clone();
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
/*      if(us == null)
        Console.WriteLine("rus went null");
      if(within == null)
        Console.WriteLine("rwithin went null");*/
      lock(_sync) {
        Hashtable key_list = new Hashtable();
        foreach (MemBlock key in _ht.Keys) {
          AHAddress target = new AHAddress(key);
          if (target.IsBetweenFromRight(us, within)) {
            //this is a relevant key
            //we want to share it
            ArrayList entry_list = (ArrayList) _ht[key];
            key_list[key] = entry_list.Clone();
          }
        }
        return key_list;
      }
    }

    //Note: This is critical method, and allows dropping complete range of keys.
    public void AdminDelete(Hashtable key_list) {
      lock(_sync ) { 
        //delete keys that have expired
        DeleteExpired();
        foreach (MemBlock ht_key in key_list.Keys) {
          //all the values to get rid
          ArrayList entry_list = (ArrayList) _ht[ht_key];
          //essentially delete all the values for that key
          if (entry_list != null) {
            foreach(Entry e in entry_list) {
              DeleteFromSorted(e);
            }
          }
          _ht.Remove(ht_key);
        }
      }
    }

  //Note: Another critical method, dumps the Hashtable data (for debugging only!)
    public Hashtable GetAll() {
      lock(_sync ) { 
        DeleteExpired();
        Hashtable rt = new Hashtable();
        foreach (MemBlock key in _ht.Keys) {
          ArrayList entry_list = (ArrayList) _ht[key];
          rt[key] = entry_list.Clone();
        }
        return rt;
      }
    }
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class TableServerTest
  {

    #region PrivateUtilMethods
    private enum TestMode {
      Threads,SingleThread,ThreadPool
    }

    /**
     * Use Disk as default media
     */
    public static TableServer GetServer() {
      return TableServerTest.GetServer(EntryFactory.Media.Disk);
    }

    public static TableServer GetServer(EntryFactory.Media media) {
      AHAddress addr = new AHAddress(new RNGCryptoServiceProvider());
      Node brunetNode = new StructuredNode(addr);
      EntryFactory factory = EntryFactory.GetInstance(brunetNode, media);
      TableServer ret = new TableServer(factory, brunetNode);
      return ret;
    }

    public static string GenHashedPasswd() {
      byte[] bin_passwd = new byte[100];

      RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
      provider.GetBytes(bin_passwd);
      return "SHA1:" + Convert.ToBase64String(bin_passwd);
    }

    public static byte[] GenRandomKey() {
      byte[] key = new byte[160];
      RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
      provider.GetBytes(key);
      return key;
    }

    /// <summary>
    /// A class which added some feature to Entry for testing. Some methods could be added to Entry
    /// </summary>
    public class Entry4Test : Entry, IComparable
    {
      public int TTL {
        get {
          TimeSpan diff = this._end_time - this._create_time;          
          return (int)diff.TotalSeconds;
        }
      }

      /// <summary>
      /// Ctor with Random password, random index
      /// </summary>
      public Entry4Test(byte[] key, byte[] data, int ttl)
        : base(key, TableServerTest.GenHashedPasswd(), 
               DateTime.Now, DateTime.Now + new TimeSpan(0,0,ttl), data, new Random().Next()) {
      }

      /// <summary>
      /// Ctor with Random password, random index and random data
      /// </summary>
      public Entry4Test(byte[] key, int ttl)
        : this(key,null,ttl) {
        byte[] d = new byte[100];
        new Random().NextBytes(d);
        string data = Encoding.UTF8.GetString(d) + ":" + Encoding.UTF8.GetString(key);
        this._data = Encoding.UTF8.GetBytes(data);
      }

      /// <summary>
      /// Used for Sorting. Since the idx is assigned with a random number, the sort operation for the entry list is actually to randomize it.
      /// Compare the idx field. This is also true for the idx in Entry because it is unique increasing.
      /// </summary>
      /// <param name="obj"></param>
      /// <returns></returns>
      public int CompareTo(object obj) {
        if (obj == null) {
          throw new ArgumentNullException();
        }
        Entry4Test en = obj as Entry4Test;
        if (en == null) {
          throw new ArgumentException("Not the same type");
        }
        return this._idx.CompareTo(en._idx);
      }

      /// <summary>
      /// This method only compares the idx to decide whether equal
      /// </summary>
      public override bool Equals(object obj) {
        if (obj == null) {
          throw new ArgumentNullException();
        }
        Entry4Test en = obj as Entry4Test;
        if (en == null) {
          throw new ArgumentException("Not the same type");
        }
        return this._idx.Equals(en._idx);
      }
    }

    /**
     * Multitheading using Threads directly and Disk by default
     */
    private void TestPutConcurrently(int keyNum, int minDataNumPerKey, int maxDataNumPerKey) {
      this.TestPut(keyNum, minDataNumPerKey, maxDataNumPerKey, TestMode.Threads);
    }

    private void TestPut(int keyNum, int minDataNumPerKey, int maxDataNumPerKey, TestMode testType) {
      this.TestPut(keyNum, minDataNumPerKey, maxDataNumPerKey, testType, EntryFactory.Media.Disk);
    }

    /// <summary>
    /// Put and compare with data prepared in a single Thread
    /// </summary>
    /// <param name="keyNum">number of keys</param>
    /// <param name="minDataNumPerKey">min: should be at least 1</param>
    /// <param name="maxDataNumPerKey">max: should >= min</param>
    /// <param name="testType">testType: Threads(default), ThreadPool and SingleThread</param>
    private void TestPut(int keyNum, int minDataNumPerKey, int maxDataNumPerKey,
                         TestMode testType, EntryFactory.Media media) {
      Random rnd = new Random();
      int entryCount = 0;
      /**
      * key: key in table server
      * value: number of data items to be put in ht
      */
      Hashtable ht = new Hashtable();
      ArrayList lpasswd = new ArrayList();
      //List<Entry4Test> opList = new List<Entry4Test>(); //operation list
      List<Entry4Test> opList = new List<Entry4Test>();
      for (int i = 0; i < keyNum; i++) {
        byte[] key = TableServerTest.GenRandomKey();
        MemBlock b_key = MemBlock.Reference(key, 0, key.Length);
        //Random number at least 1
        int num = rnd.Next(minDataNumPerKey, maxDataNumPerKey);
        List<Entry4Test> list = new List<Entry4Test>();
        for (int j = 0; j < num; j++) {
          //values of a key
          int ttl = new Random().Next(100, 300);          
          Entry4Test e = new Entry4Test(key,ttl);
          list.Add(e);
          opList.Add(e);
        }
        ht.Add(b_key, list);
        entryCount += num;
      }

      int putCount = 0;
      //sort by id, since id is randomly generated, the put sequence is random      
      opList.Sort();      
      //Data prepared BEFORE any concurrent operations. Begin test            

      ArrayList threads = new ArrayList();

      TableServer server = TableServerTest.GetServer(media);
      foreach (Entry4Test entry in opList) {
        byte[] key = entry.Key;
        string strData = Encoding.UTF8.GetString(entry.Data);
        int ttl = entry.TTL;

        string passwd = entry.Password;
        //state used in PutProc
        ArrayList state = new ArrayList();
        state.Add(key);
        state.Add(strData);
        state.Add(ttl);
        state.Add(server);
        state.Add(passwd);
        //.Net 1.0 can't pass state to threads, so we make a little object
        PutState ps = new PutState(state);
        switch (testType) {
          case TestMode.ThreadPool:
            ThreadPool.QueueUserWorkItem(new WaitCallback(ps.PutProc), state);
            break;
          case TestMode.SingleThread:            
            ps.PutProc();            
            break;
          case TestMode.Threads:
          default:
            Thread t = new Thread(new ThreadStart(ps.PutProc));
            threads.Add(t);
            t.Start();
            break;
        }
      }

      foreach (Thread t in threads) {
        t.Join();
      }

      /* Verify the result using single threaded Get() */      
      int k = 1;
      foreach (MemBlock b_key in ht.Keys) {
        byte[] key = new byte[b_key.Length];
        b_key.CopyTo(key, 0);
        //maxbytes is set to 10000
        IList result = server.Get(key, 10000, null);
        IList actual_values = result[0] as ArrayList;

        List<Entry4Test> expected_items = ht[b_key] as List<Entry4Test>;
        Assert.AreEqual(expected_items.Count, actual_values.Count, string.Format("Data item count failed in the {0}th key checked", k));
        //check values
        List<MemBlock> expected_values = new List<MemBlock>();
        foreach (Entry4Test it in expected_items) {
          expected_values.Add(MemBlock.Reference(it.Data));
        }
        foreach (Hashtable actual_val in actual_values) {
          Assert.IsTrue(expected_values.Contains(MemBlock.Reference((byte[])actual_val["value"])),"Incorrect value");
        }
        k++;
      }
      Assert.AreEqual(entryCount, server.GetCount(), "Quantity of total entries wrong");
    }


    protected class PutState
    {
      protected ArrayList _lstate;
      protected Random rnd;
      public PutState(ArrayList state) {
        _lstate = state;
      }
      /*
       * The Put action called concurrently or by a single thread. 5 Params
       */
      public void PutProc(object state) {
        ArrayList lstate = (ArrayList)state;
        Assert.AreEqual(5, lstate.Count);
        byte[] key = (byte[])lstate[0];
        string strData = (string)lstate[1];
        int ttl = (int)lstate[2];
        TableServer server = (TableServer)lstate[3];
        string passwd = lstate[4] as string;

        server.Put(key, ttl, passwd, Encoding.UTF8.GetBytes(strData));
      }

      public void PutProc() {
        PutProc(_lstate);
      }
    }

    #endregion

    #region PutConurrently
    /// <summary>
    /// TestPutConcurrentlyX_Y_Z: When min==max, DataNumPerKey=min
    /// X: keyNum
    /// Y: minDataNumPerKey
    /// Z: maxDataNumPerKey
    /// </summary>
    [Test]
    public void TestPutConcurrently100_1_1() {
      this.TestPutConcurrently(100, 1, 1);
    }

    [Test]
    public void TestPutConcurrently1_3_3() {
      this.TestPutConcurrently(1, 3, 3);
    }

    [Test]
    public void TestPutConcurrently100_2_2() {
      this.TestPutConcurrently(100, 2, 2);
    }

    [Test]
    public void TestPutConcurrently10_1_20() {
      this.TestPutConcurrently(10, 1, 20);
    }

    [Test]
    public void TestPutConcurrently50_1_50() {
      this.TestPutConcurrently(50, 1, 50);
    }

    #endregion

    #region PutInSingleThread
    /*  Single threaded tests  */
    [Test]
    public void TestPutInSingleThread100_1_1() {
      this.TestPut(100, 1, 1, TestMode.SingleThread);
    }

    [Test]
    public void TestPutInSingleThread1_3_3() {
      this.TestPut(1, 3, 3, TestMode.SingleThread);
    }

    [Test]
    public void TestPutInSingleThread100_2_2() {
      this.TestPut(100, 2, 2, TestMode.SingleThread);
    }

    [Test]
    public void TestPutInSingleThread100_10_10() {
      this.TestPut(100, 10, 10, TestMode.SingleThread);
    }

    [Test]
    public void TestPutInSingleThread1000_1_20() {
      this.TestPut(1000, 1, 20, TestMode.SingleThread);
    }

    [Test]
    public void TestPutInSingleThread50_10_10() {
      this.TestPut(50, 10, 10, TestMode.SingleThread);
    }

    [Test]
    public void TestPutInSingleThreadMemory50_10_10() {
      this.TestPut(50, 10, 10, TestMode.SingleThread, EntryFactory.Media.Memory);
    }

    #endregion

    #region PutConcurrentlyUsingMemOption
    /* multithreaded using memory option  */
    [Test]
    public void TestPutConcurrentlyMemory100_1_1() {
      this.TestPut(100, 1, 1, TestMode.Threads, EntryFactory.Media.Memory);
    }

    [Test]
    public void TestPutConcurrentlyMemory1_3_3() {
      this.TestPut(1, 3, 3, TestMode.Threads, EntryFactory.Media.Memory);
    }

    [Test]
    public void TestPutConcurrentlyMemory2_2_2() {
      this.TestPut(2, 2, 2, TestMode.Threads, EntryFactory.Media.Memory);
    }

    [Test]
    public void TestPutConcurrentlyMemory50_10_10() {
      this.TestPut(50, 10, 10, TestMode.Threads, EntryFactory.Media.Memory);
    }

    [Test]
    public void TestPutConcurrentlyMemory100_10_10() {
      this.TestPut(100, 10, 10, TestMode.Threads, EntryFactory.Media.Memory);
    }
    #endregion
  }
#endif
}
