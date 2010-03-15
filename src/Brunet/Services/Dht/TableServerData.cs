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
using System.Collections.Generic;
using System.Collections;
using System.IO;

#if BRUNET_NUNIT
using NUnit.Framework;
using System.Security.Cryptography;
#endif

using Brunet.Util;

using Brunet.Symphony;
namespace Brunet.Services.Dht {
  /**
  <summary>This class separate the dht data store server (this) from the data
  store client (TableServer).<summary>
  <remarks><para>The data is stored in a hashtable with each key pointing to a
  LinkedList<Entry> containing all the values and other data associated with
  that key.  These are sorted by earliest expiration first, so that the
  operation of removing them is fast.  There is also a sorted list of keys for
  having quick search through all the keys on this node (used primarily for 
  disconnection transfer).</para>
  <para>
  Using the cache here is difficult because Brunet's AdrConverter does
  not support the data types stored inside the cache.  More importantly
  there is no point in caching files that take up only a kilobyte, if
  Brunet streaming becomes available and the Dht can store larger than
  one kilobyte values, this may be valuable to revisit.  In the meantime,
  don't renable it, unless you want to fix it.  It is broken!  The problem
  with this implementation is that it doesn't care what size the data in
  the cache is, this isn't the right way to implement it.
  <code>
  Brunet.Collections.Cache _data = new Brunet.Collections.Cache(2500);
  </code>
  </para></remarks>
  */
  #if BRUNET_NUNIT
  [TestFixture]
  #endif
  public class TableServerData {
    /**  <summary>Every 24 hours all entries in the table are checked to see if
    their lease had expired, this contains the last time that occurred.
    </summary> */
    DateTime last_clean = DateTime.UtcNow;
    /// <summary>A list of keys stored in the this dht.</summary>
    LinkedList<MemBlock> list_of_keys = new LinkedList<MemBlock>();
    /**  <summary>LinkedList of values stored in  a hashtable, indexed by key.
    </summary> */
    Hashtable _data = new Hashtable();
    /// <summary>The base directory for uncached entries.</summary>
    protected string _base_dir;
    /// <summary>The total amount of key:value pairs stored.</summary>
    public int Count { get { return count; } }
    /// <summary>The total amount of key:value pairs stored.</summary>
    protected int count = 0;
    /// <summary>The time in seconds between cleanups.</summary>
    public readonly int TimeBetweenCleanup;

    /**
    <summary>Creates a new data store for the dht.</summary>
    <param name="node">For uncache data, we use the node to alert us to delete
    the entries as well the address to define a path to store them.</param>
    */
    public TableServerData(Node node) {
      node.DepartureEvent += this.CleanUp;
      // Caching is not supported at this time.
//      _data.EvictionEvent += this.Brunet.Collections.CacheEviction;
//      _data.MissEvent += this.Brunet.Collections.CacheMiss;
      _base_dir = Path.Combine("data", node.Address.ToString().Substring(12));
      CleanUp(null, null);
      TimeBetweenCleanup = 60*60*24;
    }

    /**
    <summary>This adds an entry and should only be called if no such entry
    exists, as it does not look to see if a duplicate entry already exists.
    This creates a new LinkedList if this is the first entry for the specific
    key and stores it in the _data hashtable.  This increments count.</summary>
    <remarks>Because data is stored by non-decreasing end time, we must place
    this at the correct position, which by starting at the last entry is
    right after the first entry that has a shorter end time.</remarks>
    <param name="entry">The data to store.</param>
    */

    public void AddEntry(Entry entry) {
      CheckEntries();
      LinkedList<Entry> data = (LinkedList<Entry>) _data[entry.Key];
      if(data == null) {
        list_of_keys.AddLast(entry.Key);
        data = new LinkedList<Entry>();
        _data[entry.Key] = data;
      }
      LinkedListNode<Entry> ent = data.Last;
      while(ent != null) {
        if(entry.EndTime > ent.Value.EndTime) {
          data.AddAfter(ent, entry);
          break;
        }
        ent = ent.Previous;
      }
      if(ent == null) {
        data.AddFirst(entry);
      }
      count++;
    }

    /**
    <summary>Disk caching is unsupported at this time.</summary>
    */
    /* When we have a cache eviction, we must write it to disk, we take
    * each entry, convert it explicitly into a hashtable, and then use adr
    * to create a stream and write it to disk
    */
    public void CacheEviction(Object o, EventArgs args) {
      Brunet.Collections.Cache.EvictionArgs eargs = (Brunet.Collections.Cache.EvictionArgs) args;
      MemBlock key = (MemBlock) eargs.Key;
      if(Dht.DhtLog.Enabled) {
        ProtocolLog.Write(Dht.DhtLog, String.Format(
          "Evicted out of cache {0}, entries in dht {1}, entries in cache {2}",
           (new BigInteger(key)).ToString(16), Count, _data.Count));
      }
      if(eargs.Value != null && ((LinkedList<Entry>) eargs.Value).Count > 0) {
        LinkedList<Entry> data = (LinkedList<Entry>) eargs.Value;
        // AdrConverter doesn't support LinkedLists
        Entry[] entries = new Entry[data.Count];
        data.CopyTo(entries, 0);
        Hashtable[] ht_entries = new Hashtable[entries.Length];
        int index = 0;
        foreach(Entry entry in entries) {
          ht_entries[index++] = (Hashtable) entry;
        }

        string dir_path, filename;
        string file_path = GeneratePath(key, out dir_path, out filename);
        if(!Directory.Exists(dir_path)) {
          Directory.CreateDirectory(dir_path);
        }
        using (FileStream fs = File.Open(file_path, FileMode.Create)) {
          AdrConverter.Serialize(ht_entries, fs);
        }
      }
    }

    /**
    <summary>Disk caching is unsupported at this time.</summary>
    */
    /* When we have a cache miss, we should try to load the data from disk,
    * if we are successful, we should also delete that file from the disk
    */
    public void CacheMiss(Object o, EventArgs args) {
      Brunet.Collections.Cache.MissArgs margs = (Brunet.Collections.Cache.MissArgs) args;
      MemBlock key = (MemBlock) margs.Key;
      string path = GeneratePath(key);
      if(File.Exists(path)) {
        using (FileStream fs = File.Open(path, FileMode.Open)) {
          ArrayList ht_entries = (ArrayList) AdrConverter.Deserialize(fs);
          Entry[] entries = new Entry[ht_entries.Count];
          int index = 0;
          foreach(Hashtable entry in ht_entries) {
            entries[index++] = (Entry) entry;
          }
          _data[key] = new LinkedList<Entry>(entries);
        }
        File.Delete(path);
      }
    }

    /**
    <summary>Called to clean up the disk data left behind by the dht</summary>
    <param name="o">Unused.</param>
    <param name="args">Unused.</param>
    <returns></returns>
    */
    public void CleanUp(Object o, EventArgs args) {
      if(Directory.Exists(_base_dir)) {
        Directory.Delete(_base_dir, true);
      }
    }

    /**
    <summary>Deletes any of the expired entries by traversing the entire data
    store.  This is done only once every 24 hours to reduce heavy memory access
    due to short lived unused entries.</summary>
    */
    public void CheckEntries() {
      DateTime now = DateTime.UtcNow;
      if(now - last_clean < TimeSpan.FromSeconds(TimeBetweenCleanup)) {
        return;
      }
      // Otherwise its time to do some cleaning!
      last_clean = now;
      LinkedListNode<MemBlock> current = list_of_keys.First;
      while(current != null) {
        LinkedListNode<MemBlock> next = current.Next;
        DeleteExpired(current.Value);
        current = next;
      }
    }

    /**
    <summary>Deletes all expired entries for the specified key.  For each entry
    deleted, count is decremented.  This should be called before accessing the
    data stored in this table.</summary>
    <param name="key">The index to check for expired entries.</param>
    <returns>The amount of entries deleted.</returns>
    */

    public int DeleteExpired(MemBlock key) {
      LinkedList<Entry> data = (LinkedList<Entry>) _data[key];
      if(data == null) {
        return 0;
      }
      DateTime now = DateTime.UtcNow;
      LinkedListNode<Entry> current = data.First;
      while(current != null) {
        if (current.Value.EndTime > now) {
          break;
        }
        LinkedListNode<Entry> next = current.Next;
        data.Remove(current);
        current = next;
        count--;
      }
      int lcount = data.Count;
      if(data.Count == 0) {
        list_of_keys.Remove(key);
        _data.Remove(key);
      }
      return lcount;
    }

    /**
    <summary>Generates a path given a key.</summary>
    <param name="key">The key to generate a path for.</param>
    <returns>The path for the key.</returns>
    */
    public string GeneratePath(MemBlock key) {
      string dir_path, filename;
      return GeneratePath(key, out dir_path, out filename);
    }

    /**
    <summary>Generates a path given a key.</summary>
    <param name="key">The key to generate a path for.</param>
    <param name="path">Returns the directory portion of the path.</param>
    <param name="filename">Returns the filename portion of the path.</param>
    <returns>The path for the key.</returns>
    */
    public string GeneratePath(MemBlock key, out string path, out string filename) {
      if(Address.MemSize < 5) {
        throw new Exception("Address.MemSize must be greater than or equal to 5.");
      }

      string[] l = new string[5];
      for (int j = 0; j < 4; j++) {
        l[j] = string.Empty;
      }

      l[0] = _base_dir;
      l[1] = key[0].ToString();
      l[2] = key[1].ToString();
      l[3] = key[2].ToString();

      for (int i = 3; i < Address.MemSize - 2; i++) {
        l[4] += key[i].ToString();
      }

      path = String.Join(Path.DirectorySeparatorChar.ToString(), l);
      filename = key[Address.MemSize - 1].ToString();
      return Path.Combine(path, filename);
    }

    /**
    <summary>Retrieves the entries for the specified key.</summary>
    <returns>The entries for the specified key.</returns>
    */
    public LinkedList<Entry> GetEntries(MemBlock key) {
      DeleteExpired(key);
      CheckEntries();
      return (LinkedList<Entry>) _data[key];
    }

    /**
    <summary>Returns a list of keys stored at this node that exist between the
    two addresses.  Such keys returned are the storest path between the two
    addresses.</summary>
    <param name="add1">One of the address end points.</param>
    <param name="add2">Another of the address end points.</param>
    <returns>A LinkedList of key entries between add1 and add2</returns>
    */
    public LinkedList<MemBlock> GetKeysBetween(AHAddress add1, AHAddress add2) {
      LinkedList<MemBlock> keys = new LinkedList<MemBlock>();
      if(add1.IsRightOf(add2)) {
        foreach(MemBlock key in list_of_keys) {
          AHAddress key_addr = new AHAddress(key);
          if(key_addr.IsBetweenFromLeft(add1, add2)) {
            keys.AddLast(key);
          }
        }
      }
      else {
        foreach(MemBlock key in list_of_keys) {
          AHAddress key_addr = new AHAddress(key);
          if(key_addr.IsBetweenFromRight(add1, add2)) {
            keys.AddLast(key);
          }
        }
      }
      return keys;
    }

    /**
    <summary>Returns the list of keys.</summary>
    <returns>A list of keys stored at this node.</summary>
    */
    public LinkedList<MemBlock> GetKeys() {
      CheckEntries();
      return list_of_keys;
    }

    /**
    <summary>This removes an entry from the TableServerData, the current dht
    does not support deletes, but if the second stage of a put (the remote 
    PutHandler) fails, the entry needs to be deleted from this node.</summary>
    <param name="key">The index the data is stored at.</param>
    <param name="value">The data to remove.</param>
    */
    public void RemoveEntry(MemBlock key, MemBlock value) {
      LinkedList<Entry> data = (LinkedList<Entry>) _data[key];
      if(data != null) {
        LinkedListNode<Entry> current = data.First;
        while(current != null) {
          if (current.Value.Value.Equals(value)) {
            data.Remove(current);
            count--;
            break;
          }
          current = current.Next;
        }
        if(data.Count == 0) {
          _data.Remove(key);
        }
      }
    }

    /**
    <summary>This should be called if an entry already exists as it will find
    the entry and update its lease time.  If an entry does not exist nothing
    happens.</summary>
    <param name="key">The index to store the value.</param>
    <param name="value">The data to store.</param>
    <param name="end_time">The lease time for the data.</param>
    */
    public void UpdateEntry(MemBlock key, MemBlock value, DateTime end_time) {
      CheckEntries();
      LinkedList<Entry> data = (LinkedList<Entry>) _data[key];
      if(data != null) {
        Entry entry = null;
        LinkedListNode<Entry> current = data.First;
        while(current != null) {
          if (current.Value.Value.Equals(value)) {
            entry = current.Value;
            data.Remove(current);
            break;
          }
          current = current.Next;
        }
        if(entry != null) {
          count--;
          entry.EndTime = end_time;
          AddEntry(entry);
        }
      }
    }

    /**
    <summary>Converts all the entries into Adr compatible types so that they
    can be sent over BrunetRpc and XmlRpc</summary>
    */
    public ArrayList Dump() {
      ArrayList entries = new ArrayList(list_of_keys.Count);
      foreach(MemBlock key in list_of_keys) {
        LinkedList<Entry> data = (LinkedList<Entry>) _data[key];
        ArrayList lentries = new ArrayList(data.Count);
        foreach(Entry entry in data) {
          lentries.Add((Hashtable) entry);
        }
        entries.Add(lentries);
      }
      return entries;
    }

#if BRUNET_NUNIT
    //Needed for nunit testing
    public TableServerData(String dir) {
      _base_dir = Path.Combine("Data", dir);
      TimeBetweenCleanup = 5;
    }

    // Needed for nunit to work
    public TableServerData() {}

    // Basic tests for Add, Update, and Remove
    [Test]
    public void Test0() {
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      TableServerData tsd = new TableServerData("0");
      byte[] key = new byte[20];
      rng.GetBytes(key);
      DateTime now = DateTime.UtcNow;
      Entry ent = new Entry(key, key, now, now.AddSeconds(100));
      tsd.AddEntry(ent);
      LinkedList<Entry> entries = tsd.GetEntries(key);
      Assert.AreEqual(1, entries.Count, "Count after add");
      Assert.AreEqual(ent, entries.First.Value, "Entries are equal");
      tsd.UpdateEntry(ent.Key, ent.Value, now.AddSeconds(200));
      entries = tsd.GetEntries(key);
      Assert.AreEqual(1, entries.Count, "Count after update");
      Assert.AreEqual(ent, entries.First.Value, "Entries are equal");
      tsd.RemoveEntry(ent.Key, ent.Value);
      entries = tsd.GetEntries(key);
      Assert.AreEqual(tsd.Count, 0, "Count after remove");
      Assert.AreEqual(null, entries, "Entry after remove");
    }

    /*
    This tests multiple puts on the same keys, updates on all 6 of the
    not_expire2 and 1 of the to_expire.  So in short, this tests everything,
    but GetEntriesBetween.and GetKeys.
    */
    [Test]
    public void Test1() {
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      TableServerData tsd = new TableServerData("0");
      Entry[] not_expired = new Entry[12];
      Entry[] to_expire = new Entry[12];
      DateTime now = DateTime.UtcNow;
      DateTime live = now.AddSeconds(120);
      DateTime expire = now.AddSeconds(5);

      for(int i = 0; i < 4; i++) {
        byte[] key = new byte[20];
        rng.GetBytes(key);
        for(int j = 0; j < 3; j++) {
          byte[] value = new byte[20];
          rng.GetBytes(value);
          Entry ent = new Entry(key, value, now, expire);
          to_expire[i * 3 + j] = ent;
          tsd.AddEntry(ent);
          value = new byte[20];
          rng.GetBytes(value);
          ent = new Entry(key, value, now, live);
          not_expired[i * 3 + j] = ent;
          tsd.AddEntry(ent);
          Assert.IsFalse(not_expired[i * 3 + j].Equals(to_expire[i * 3 + j]), 
                         String.Format("{0}: not_expired == to_expire.", i * 3 + j));
        }
      }

      for(int i = 0; i < 4; i++) {
        LinkedList<Entry> entries = tsd.GetEntries(not_expired[i * 3].Key);
        for(int j = 0; j < 3; j++) {
          Assert.IsTrue(entries.Contains(not_expired[i * 3 + j]), "step 0: not_expired " + (i * 3 + j));
          Assert.IsTrue(entries.Contains(to_expire[i * 3 + j]), "step 0: to_expire " + (i * 3 + j));
        }
      }

      for(int i = 0; i < 4; i++) {
        for(int j = 0; j < 3; j++) {
          int pos = i * 3 + j;
          if(pos % 2 == 0) {
            Entry ent = not_expired[pos];
            tsd.UpdateEntry(ent.Key, ent.Value, now.AddSeconds(160));
          }
        }
      }

      Entry entry = to_expire[11];
      tsd.UpdateEntry(entry.Key, entry.Value, now.AddSeconds(160));

      for(int i = 0; i < 4; i++) {
        LinkedList<Entry> entries = tsd.GetEntries(not_expired[i * 3].Key);
        for(int j = 0; j < 3; j++) {
          Assert.IsTrue(entries.Contains(not_expired[i * 3 + j]), "step 1: not_expired " + (i * 3 + j));
          Assert.IsTrue(entries.Contains(to_expire[i * 3 + j]), "step 1: to_expire " + (i * 3 + j));
        }
      }

      while(DateTime.UtcNow < expire.AddSeconds(1)) {
        for(int i = 0; i < 50000000; i++) {
          int k = i % 5;
         k += 6;
        }
      }
      for(int i = 0; i < 3; i++) {
        LinkedList<Entry> entries = tsd.GetEntries(not_expired[i * 3].Key);
        for(int j = 0; j < 3; j++) {
          Assert.IsTrue(entries.Contains(not_expired[i * 3 + j]), "step 2: not_expired " + (i * 3 + j));
          Assert.IsFalse(entries.Contains(to_expire[i * 3 + j]), "step 2: to_expire " + (i * 3 + j));
        }
      }
      Assert.AreEqual(13, tsd.Count, "Entries we didn't check are removed by CheckEntries.");
    }

    /*
    This tests GetKeysBetween both with add1 < add2 and add2 < add1 and then
    checks for the existence of all keys via GetKeys.
    */
    [Test]
    public void Test2() {
      TableServerData tsd = new TableServerData("0");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      MemBlock[] addresses = new MemBlock[100];
      byte[] value = new byte[20];
      rng.GetBytes(value);
      DateTime now = DateTime.UtcNow;
      DateTime lease_end = now.AddMinutes(1);
      for(int i = 0; i < addresses.Length; i++) {
        addresses[i] = (new AHAddress(rng)).ToMemBlock();
        tsd.AddEntry(new Entry(addresses[i], value, now, lease_end));
      }

      AHAddress start = new AHAddress(rng);
      AHAddress end = new AHAddress(rng);
      LinkedList<MemBlock> keys_se = tsd.GetKeysBetween(start, end);
      LinkedList<MemBlock> keys_es = tsd.GetKeysBetween(end, start);
      String output = " - " +start + ":" + end;
      if(start.IsLeftOf(end)) {
        foreach(MemBlock address in addresses) {
          AHAddress addr = new AHAddress(address);
          if(addr.IsLeftOf(end) && addr.IsRightOf(start)) {
            Assert.IsTrue(keys_se.Contains(address), addr + " in lse" + output);
            Assert.IsTrue(keys_es.Contains(address), addr + " in les" + output);
          }
          else {
            Assert.IsFalse(keys_se.Contains(address), addr + " out lse" + output);
            Assert.IsFalse(keys_es.Contains(address), addr + " out les" + output);
          }
        }
      }
      else {
        foreach(MemBlock address in addresses) {
          AHAddress addr = new AHAddress(address);
          if(addr.IsLeftOf(start) && addr.IsRightOf(end)) {
            Assert.IsTrue(keys_se.Contains(address), addr + " in rse" + output);
            Assert.IsTrue(keys_es.Contains(address), addr + " in res" + output);
          }
          else {
            Assert.IsFalse(keys_se.Contains(address), addr + " out rse" + output);
            Assert.IsFalse(keys_es.Contains(address), addr + " out res" + output);
          }
        }
      }

      LinkedList<MemBlock> keys = tsd.GetKeys();
      foreach(MemBlock addr in addresses) {
        Assert.IsTrue(keys.Contains(addr), "keys does not contain: " + (new AHAddress(addr)));
      }
    }
#endif
  }
}
