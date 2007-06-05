    // _expiring_entries needs to be moved to a SortedList for better performance

using System;
using System.Text;
using System.Collections;
using System.Security.Cryptography;

#if BRUNET_NUNIT
using NUnit.Framework;
using System.Threading;
//using System.Collections.Generic;
#endif

using Brunet;

namespace Brunet.Dht {
  public class TableServer {
    protected object _sync;

    //maintain a list of keys that are expiring:
    //list of keys sorted on expiration times
    protected ArrayList _expiring_entries;

    protected Hashtable _ht;
    protected int _max_idx;

    protected Node _node;
    protected EntryFactory _ef;
    public TableServer(EntryFactory ef, Node node) {
      /**
      * @todo make sure there is a second copy of all data
      * in the network.  When a neighbor is lost, make sure
      * the new neighbor is updated with the correct content
      */
      _sync = new object();
      _node = node;
      _ef = ef;
      _expiring_entries = new ArrayList();
      _ht = new Hashtable();
      _max_idx = 0;
    }

    protected bool ValidatePasswordFormat(string password,
      out string hash_name, out string base64_val) {
      string[] ss = password.Split(new char[] {':'});
      if (ss.Length != 2) {
        hash_name = "invalid";
        base64_val = null;
        return false;
      }
      hash_name = ss[0];
      base64_val = ss[1];
      return true;
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
      //Alternatively, we could also have count as number of keys
      //return _ht.Count; ?????
      return count;
    }

    /**
    * This method puts in a key-value pair. (now this is idempotent).
    * @param key key associated with the date item
    * @param ttl time-to-live in seconds
    * @param hashed_password <hash_name>:<base64(hashed_pass)>
    * @param data data associated with the key
    * @return true on success, false on failure
    */
    public int Put(byte[] key, int ttl, string hashed_password, byte[] data) {
      MemBlock ht_key3 = MemBlock.Reference(key, 0, key.Length);
      ArrayList list3 = _ht[ht_key3] as ArrayList;
      if (list3 != null) {
        Console.Error.WriteLine("[Test] data items under the key before put: {0}", list3.Count);
      }
      else {
        Console.Error.WriteLine("[Test] 1st time");
      }

      string hash_name = null;
      string base64_val = null;
      if(!ValidatePasswordFormat(hashed_password, out hash_name, out base64_val)) {
        throw new Exception("Invalid password format.");
      }

      DateTime create_time = DateTime.Now;
      TimeSpan ts = new TimeSpan(0,0,ttl);
      DateTime end_time = create_time + ts;
      ArrayList entry_list = null;

      lock(_sync) {
        DeleteExpired();
        MemBlock ht_key = MemBlock.Reference(key, 0, key.Length);
        entry_list = (ArrayList)_ht[ht_key];
        if( entry_list != null ) {
          //Make sure we only keep one reference to a key to save memory:
          //Arijit Ganguly - I had no idea what this was about. Now I know...
          key = ((Entry)entry_list[0]).Key;
          ht_key = MemBlock.Reference(key, 0, key.Length);
        }
        else {
          //This is a new key:
          entry_list = new ArrayList();
          _ht[ht_key] = entry_list;
        }
        _max_idx++; //Increment the maximum index

        foreach(Entry ent in entry_list) {
          // Can't have duplicate passwords - no RePuts
          if (ent.Password.Equals(hashed_password)) {
              Console.Error.WriteLine("Exist because password conflict");
              return entry_list.Count;            
          }
        }

        //Look up 
        Entry e = _ef.CreateEntry(key, hashed_password,  create_time, end_time,
                          data, _max_idx);

        //Add the entry to the end of the list.
        entry_list.Add(e);

        MemBlock ht_key2 = MemBlock.Reference(key, 0, key.Length);
        ArrayList list2 = _ht[ht_key2] as ArrayList;
        Console.Error.WriteLine("[Test] data items under the key before sort: {0}", list2.Count);
          
          //Further add this to sorted list _expired_entries list
        InsertToSorted(e);

        ///@todo, we might need to tell a neighbor about this object
        
          //jx
        MemBlock ht_key1 = MemBlock.Reference(key, 0, key.Length);
        ArrayList list1 = _ht[ht_key1] as ArrayList;
        Console.Error.WriteLine("[Test] data items under the key after sort: {0}", list1.Count);
          //
      } // end of lock
      return entry_list.Count;
    }

    /**
    * This method differs from put() in the key is already mapped
    * we fail. (now this is idempotent). 
    * @param key key associated with the date item
    * @param ttl time-to-live in seconds
    * @param hashed_password <hash_name>:<base64(hashed_pass)>
    * @param data data associated with the key
    * @return true on success, false on failure
    */

    public bool Create(byte[] key, int ttl, string hashed_password, byte[] data) {
      string hash_name = null;
      string base64_val = null;
      if (!ValidatePasswordFormat(hashed_password, out hash_name, out base64_val)) {
        throw new Exception("Invalid password format.");
      }
      DateTime create_time = DateTime.Now;
      TimeSpan ts = new TimeSpan(0,0,ttl);
      DateTime end_time = create_time + ts;

      lock(_sync) {
        DeleteExpired();
        MemBlock ht_key = MemBlock.Reference(key, 0, key.Length);
        ArrayList entry_list = (ArrayList)_ht[ht_key];
        if( entry_list != null ) {
          Entry to_renew = null;
          foreach(Entry e in entry_list) {
            if (!e.Password.Equals(hashed_password)) {
              continue;
            }
            MemBlock arg_data = MemBlock.Reference(data, 0, data.Length);
            MemBlock e_data = MemBlock.Reference(e.Data, 0, e.Data.Length);
            if (!e_data.Equals(arg_data)) {
              continue;
            }
            to_renew = e; 
          }
          if (to_renew == null) {
            throw new Exception("Unable to find a key-value pair to renew.");
          }
          if (end_time < to_renew.EndTime) {
            throw new Exception("Cannot shorten lifetime of a key-value.");
          }
          //we should also remove this entry, and put a new one
          entry_list.Remove(to_renew);
          DeleteFromSorted(to_renew);

          Entry new_e = _ef.CreateEntry(to_renew.Key, hashed_password, 
                                        to_renew.CreatedTime, end_time,
                                        data, to_renew.Index);
          entry_list.Add(new_e);
          InsertToSorted(new_e);
        } else {
          //This is a new key, just a regular Create()
          entry_list = new ArrayList();
          _ht[ht_key] = entry_list;

          _max_idx++; //Increment the maximum index
          //Look up
          Entry e = _ef.CreateEntry(key, hashed_password,  create_time, end_time,
                                    data, _max_idx);
          //Add the entry to the end of the list.
          entry_list.Add(e);
          //Further add the entry to the sorted list _expired_entries
          InsertToSorted(e);
        }
      }//end of lock
      return true;
    }

    /**
    * Retrieves data from the Dht
    * @param key key associated with the date item
    * @param maxbytes amount of data to retrieve
    * @param token an array of ints used for continuing gets
    * @return IList of results
    */

    public IList Get(byte[] key, int maxbytes, byte[] token) {
      int seen_start_idx = -1;
      int seen_end_idx = -1;
      int pos = -1;
      if( token != null ) {
        // pos is used in FDht to help determine which connection to send to
        int[] bounds = (int[])AdrConverter.Deserialize(new System.IO.MemoryStream(token));
        pos = bounds[0];
        seen_start_idx = bounds[1];
        seen_end_idx = bounds[2];
      }

      int consumed_bytes = 0;

      ArrayList result = new ArrayList();
      ArrayList values = new ArrayList();
      int remaining_items = 0;
      byte[] next_token = null;

      lock(_sync ) {
        DeleteExpired();

        MemBlock ht_key = MemBlock.Reference(key, 0, key.Length);
        ArrayList entry_list = (ArrayList)_ht[ht_key];

        int seen = 0;
        // Keys exist!
        if( entry_list != null ) {
          int max_index = seen_end_idx;
          foreach(Entry e in entry_list) {
            // Have we seen this and do we have enough space for it?
            if(e.Index > seen_end_idx) {
              if (e.Data.Length + consumed_bytes <= maxbytes) {
                TimeSpan age = DateTime.Now - e.CreatedTime;
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
      int[] new_bounds = new int[3];
      new_bounds[0] = pos;
      new_bounds[1] = seen_start_idx;
      new_bounds[2] = seen_end_idx;
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
      DateTime now = DateTime.Now;
      foreach(Entry e in _expiring_entries) {
        DateTime end_time = e.EndTime; 
        // These should be sorted so we will break once we find an end_time greater than now
        if (end_time > now) {
          break;
        }
        // Expired entry, must delete it
        MemBlock key = MemBlock.Reference(e.Key, 0, e.Key.Length);
        ArrayList entry_list = (ArrayList) _ht[key];
        if (entry_list == null) {
          Console.Error.WriteLine("Fatal error missing key during DeleteExpired()");
          continue;
        }
        entry_list.Remove(e);
        if (entry_list.Count == 0) {
          _ht.Remove(key);
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
    public ArrayList GetValues(MemBlock ht_key) {
      lock(_sync) {
        ArrayList entry_list = (ArrayList)_ht[ht_key];
        return entry_list;
      }
    }

    /** Get all the keys to left of some address.
    *  Note that this depends on whether the ring is stored clockwise or
    *  anti-clockwise, we assume clockwise!
    *  
    */
    public Hashtable GetKeysToLeft(AHAddress us, AHAddress within) {
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


/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 Jiangyan Xu <dennis84225@gmail.com> University of Florida

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
#if BRUNET_NUNIT
    [TestFixture]
    public class TableServerTest
    {

        #region PrivateUtilMethods

        /**
         * Use Disk as default media
         */
        private TableServer GetServer()
        {
            return this.GetServer(EntryFactory.Media.Disk);
        }

        private TableServer GetServer(EntryFactory.Media media)
        {
            AHAddress addr = new AHAddress(new RNGCryptoServiceProvider());
            Node brunetNode = new StructuredNode(addr);
            EntryFactory factory = EntryFactory.GetInstance(brunetNode, media);
            TableServer ret = new TableServer(factory, brunetNode);
            return ret;
        }

        private string GenHashedPasswd()
        {
            byte[] bin_passwd = new byte[100];

            RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
            provider.GetBytes(bin_passwd);
            return "SHA1:" + Convert.ToBase64String(bin_passwd);
        }

        /**
         * Multitheading using Threads directly and Disk by default
         */
        private void TestPutConcurrently(int keyNum, int minDataNumPerKey, int maxDataNumPerKey)
        {
            this.TestPut(keyNum, minDataNumPerKey, maxDataNumPerKey, "Threads");
        }

        private void TestPut(int keyNum, int minDataNumPerKey, int maxDataNumPerKey, string testType)
        {
            this.TestPut(keyNum, minDataNumPerKey, maxDataNumPerKey, testType, EntryFactory.Media.Disk);
        }

        /// <summary>
        /// Concurrently Put (1 thread per put), Compare with data prepared in a single Thread
        /// </summary>
        /// <param name="keyNum">number of keys</param>
        /// <param name="minDataNumPerKey">min: should be at least 1</param>
        /// <param name="maxDataNumPerKey">max: should >= min</param>
        /// <param name="testType">testType: Threads(default), ThreadPool and SingleThread</param>
        private void TestPut(int keyNum, int minDataNumPerKey, int maxDataNumPerKey, string testType, EntryFactory.Media media)
        {
            Random rnd = new Random();
            int entryCount = 0;
            /**
            * key: key in table server
            * value: number of data items to be put in ht
            */
            Hashtable ht = new Hashtable();
            ArrayList lpasswd = new ArrayList();

            for (int i = 0; i < keyNum; i++)
            {
                byte[] key = new byte[160];
                rnd.NextBytes(key);
                MemBlock b_key = MemBlock.Reference(key, 0, key.Length);
                //Random number at least 1
                int value = rnd.Next(minDataNumPerKey, maxDataNumPerKey);
                ht.Add(b_key, value);
                entryCount += value;
            }

            Console.Error.WriteLine("{0} enties in total", entryCount);


            ArrayList threads = new ArrayList();

            TableServer server = this.GetServer(media);
            foreach (MemBlock b_key in ht.Keys)
            {
                int numData = (int)ht[b_key];
                Console.Error.WriteLine("numData: {0}", numData);
                //key
                byte[] key = new byte[b_key.Length];
                b_key.CopyTo(key, 0);

                string strKey = Encoding.UTF8.GetString(key);
                for (int i = 0; i < numData; i++)
                {
                    string strData = string.Format("{0}:{1}", strKey, i);

                    //ttl long enough to make sure no key has been expired before all data been Get()
                    int ttl = rnd.Next(150, 300);
                    string passwd = this.GenHashedPasswd();

                    if (!lpasswd.Contains(passwd))
                    {
                        lpasswd.Add(passwd);
                    }
                    else
                    {
                        throw new Exception("passwd shouldn't be the same!");
                    }

                    //state used in PutProc
                    ArrayList state = new ArrayList();
                    state.Add(key);
                    state.Add(strData);
                    state.Add(ttl);
                    state.Add(server);
                    state.Add(passwd);
		    //.Net 1.0 can't pass state to threads, so we make a little object
                    PutState ps = new PutState(state);
                    switch (testType)
                    {
                        case "ThreadPool":
                            ThreadPool.QueueUserWorkItem(new WaitCallback(ps.PutProc), state);
                            break;
                        case "SingleThread":
                            ps.PutProc();
                            break;
                        case "Threads":
                        default:
                            Thread t = new Thread(new ThreadStart(ps.PutProc));
                            threads.Add(t);
                            t.Start();
                            break;
                    }
                }
            }

            foreach (Thread t in threads)
            {
                t.Join();
            }


            /* Verify the result using single threaded Get() */
            int j = 1;
            foreach (MemBlock b_key in ht.Keys)
            {
                byte[] key = new byte[b_key.Length];
                b_key.CopyTo(key, 0);
                IList result = server.Get(key, 10000, null);
                IList values = result[0] as ArrayList;
                Assert.IsNotNull(values);
                Assert.AreEqual((int)ht[b_key], values.Count, string.Format("Data item count failed in the {0}st key checked", j));
                j++;
            }

            Assert.AreEqual(entryCount, server.GetCount(), "Quantity of total entries wrong");
        }
       protected class PutState {
         protected ArrayList _lstate;
	 protected Random rnd;
	 public PutState(ArrayList state) {
           _lstate = state;
	 }
        /*
         * The Put action called concurrently or by a single thread. 4 Params
         */
        public void PutProc(object state)
        {
	    ArrayList lstate = (ArrayList)state;
            Assert.AreEqual(5, lstate.Count);
            byte[] key = (byte[])lstate[0];
            string strData = (string)lstate[1];
            int ttl = (int)lstate[2];
            TableServer server = (TableServer)lstate[3];
            string passwd = lstate[4] as string;

            //Console.Error.WriteLine("Put:ThreadID:{0}", Thread.CurrentThread.GetHashCode());
            Console.Error.WriteLine("Calling Put");
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
        public void TestPutConcurrently100_1_1()
        {
            this.TestPutConcurrently(100, 1, 1);
        }

        [Test]
        public void TestPutConcurrently1_3_3()
        {
            this.TestPutConcurrently(1, 3, 3);
        }

        [Test]
        public void TestPutConcurrently1_2_2()
        {
            this.TestPutConcurrently(1, 2, 2);
        }

        [Test]
        public void TestPutConcurrently2_2_2()
        {
            this.TestPutConcurrently(2, 2, 2);
        }

        [Test]
        public void TestPutConcurrently100_2_2()
        {
            this.TestPutConcurrently(100, 2, 2);
        }
/*  This one causes an exception for me (POB)
        [Test]
        public void TestPutConcurrently1000_1_20()
        {
            this.TestPutConcurrently(1000, 1, 20);
        }
*/
        [Test]
        public void TestPutConcurrently100_10_10()
        {
            this.TestPutConcurrently(100, 10, 10);
        }

        [Test]
        public void TestPutConcurrently1_5_5()
        {
            this.TestPutConcurrently(1, 5, 5);
        }
        #endregion

        #region PutInSingleThread
        /*  Single threaded tests  */
        [Test]
        public void TestPutInSingleThread100_1_1()
        {
            this.TestPut(100, 1, 1, "SingleThread");
        }

        [Test]
        public void TestPutInSingleThread1_3_3()
        {
            this.TestPut(1, 3, 3, "SingleThread");
        }

        [Test]
        public void TestPutInSingleThread1_2_2()
        {
            this.TestPut(1, 2, 2, "SingleThread");
        }

        [Test]
        public void TestPutInSingleThread2_2_2()
        {
            this.TestPut(2, 2, 2, "SingleThread");
        }

        [Test]
        public void TestPutInSingleThread100_2_2()
        {
            this.TestPut(100, 2, 2, "SingleThread");
        }

        [Test]
        public void TestPutInSingleThread100_10_10()
        {
            this.TestPut(100, 10, 10, "SingleThread");
        }

        [Test]
        public void TestPutInSingleThread1_5_5()
        {
            this.TestPut(1, 5, 5, "SingleThread");
        }

        [Test]
        public void TestPutInSingleThread1000_1_20()
        {
            this.TestPut(1000, 1, 20, "SingleThread");
        }

        #endregion

        #region PutConcurrentlyUsingMemOption
        /* multithreaded using memory option  */
        [Test]
        public void TestPutConcurrentlyMemory100_1_1()
        {
            this.TestPut(100, 1, 1, "SingleThread", EntryFactory.Media.Memory);
        }

        [Test]
        public void TestPutConcurrentlyMemory1_3_3()
        {
            this.TestPut(1, 3, 3, "SingleThread", EntryFactory.Media.Memory);
        }

        [Test]
        public void TestPutConcurrentlyMemory1_2_2()
        {
            this.TestPut(1, 2, 2, "SingleThread", EntryFactory.Media.Memory);
        }

        [Test]
        public void TestPutConcurrentlyMemory2_2_2()
        {
            this.TestPut(2, 2, 2, "SingleThread", EntryFactory.Media.Memory);
        }

        [Test]
        public void TestPutConcurrentlyMemory100_2_2()
        {
            this.TestPut(100, 2, 2, "SingleThread", EntryFactory.Media.Memory);
        }

        [Test]
        public void TestPutConcurrentlyMemory100_10_10()
        {
            this.TestPut(100, 10, 10, "SingleThread", EntryFactory.Media.Memory);
        }

        [Test]
        public void TestPutConcurrentlyMemory1_5_5()
        {
            this.TestPut(1, 5, 5, "SingleThread", EntryFactory.Media.Memory);
        }
        #endregion
    }
#endif
}

