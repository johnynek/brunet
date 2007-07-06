using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;

using Brunet;

namespace Brunet.Dht {
  /* This is meant to separate some logic from the TableServer, there are many
   * ways to implement this, so I figured it would be best to offer an 
   * abstracted view
   */
  public class TableServerData {
    DateTime last_clean = DateTime.UtcNow;
    LinkedList<MemBlock> list_of_keys = new LinkedList<MemBlock>();
    Cache _data = new Cache(2500);
    protected string _base_dir;
    public int Count { get { return count; } }
    private int count = 0;

    public TableServerData(Node _node) {
      _node.DepartureEvent += this.CleanUp;
      _data.EvictionEvent += this.CacheEviction;
      _data.MissEvent += this.CacheMiss;
      _base_dir = Path.Combine("data", _node.Address.ToString().Substring(12));
      CleanUp();
    }

    /* This is a quick little method to add an entry, since the basic Put
    * mechanism will not be changing and this may, it is separate
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
        if(ent.Value.EndTime < entry.EndTime) {
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

    /* When we have a cache eviction, we must write it to disk, we take
    * each entry, convert it explicitly into a hashtable, and then use adr
    * to create a stream and write it to disk
    */
    public void CacheEviction(Object o, EventArgs args) {
      Cache.EvictionArgs eargs = (Cache.EvictionArgs) args;
      MemBlock key = (MemBlock) eargs.Key;
      if(eargs.Value != null && ((LinkedList<LinkedList<Entry>>) eargs.Value).Count > 0) {
        LinkedList<LinkedList<Entry>> data = (LinkedList<LinkedList<Entry>>) eargs.Value;
        string path = GeneratePath(key);
        using (FileStream fs = File.Open(path, FileMode.Create)) {
          AdrConverter.Serialize(data, fs);
        }
      }
    }

    /* When we have a cache miss, we should try to load the data from disk,
    * if we are successful, we should also delete that file from the disk
    */
    public void CacheMiss(Object o, EventArgs args) {
      Cache.MissArgs margs = (Cache.MissArgs) args;
      MemBlock key = (MemBlock) margs.Key;
      string path = GeneratePath(key);
      if(File.Exists(path)) {
        using (FileStream fs = File.Open(path, FileMode.Open)) {
          _data[key] = (LinkedList<Entry>) AdrConverter.Deserialize(fs);
        }
        File.Delete(path);
      }
    }

    //  Called to clean up the disk data left behind by the dht
    private void CleanUp() {
      if(Directory.Exists(_base_dir)) {
        Directory.Delete(_base_dir, true);
      }
    }

    public void CleanUp(Object o, EventArgs args) {
      this.CleanUp();
    }

    /* Deletes any of the expired entries, where all entries in the individual
     * entry are expired, otherwise we they must be deleted during an operation
     * on the table regarding a specific key, or we would be constantly
     * churning through an enormous list
     */
    public void CheckEntries() {
      DateTime now = DateTime.UtcNow;
      if(now - last_clean < TimeSpan.FromHours(24)) {
        return;
      }
      // Otherwise its time to do some cleaning!
      last_clean = now;
      LinkedListNode<MemBlock> current = list_of_keys.First;
      while(current != null) {
        LinkedListNode<MemBlock> next = current.Next;
        if(DeleteExpired(current.Value) == 0) {
          list_of_keys.Remove(current);
        }
        current = next;
      }
    }

    /* Deletes any of the expired entries for a specific key, we execute this
     * prior to any Dht operations involving the key in question
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
      return data.Count;
    }

    // Generates the file system path for a specific key
    public string GeneratePath(MemBlock key) {
      string[] l = new string[5];
      for (int j = 0; j < 4; j++) {
        l[j] = string.Empty;
      }

      l[0] = _base_dir;
      l[1] = key[0].ToString();
      l[2] = key[1].ToString();
      l[3] = key[2].ToString();

      for (int i = 3; i < 19; i++) {
        l[4] += key[i].ToString();
      }

      string path = String.Join(Path.DirectorySeparatorChar.ToString(), l);
      if(!Directory.Exists(path)) {
        Directory.CreateDirectory(path);
      }
      return Path.Combine(path, key[19].ToString());
    }

    // This gets us an ArrayList of entries based upon the key
    public LinkedList<Entry> GetEntries(MemBlock key) {
      CheckEntries();
      return (LinkedList<Entry>) _data[key];
    }

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

    public LinkedList<MemBlock> GetKeys() {
      CheckEntries();
      return list_of_keys;
    }

    /* Sometimes our put succeeds, but our recursive fails, this method gets
    * called to fix the mess
    */
    public void RemoveEntries(MemBlock key) {
      LinkedList<Entry> data = (LinkedList<Entry>) _data[key];
      if(data != null) {
        count -= data.Count;
        data.Clear();
      }
    }

    /* Sometimes our put succeeds, but our recursive fails, this method gets
     * called to fix the mess
     */
    public void RemoveEntry(MemBlock key, MemBlock value) {
      LinkedList<Entry> data = (LinkedList<Entry>) _data[key];
      if(data != null) {
        LinkedListNode<Entry> current = data.First;
        while(current != null) {
          if (current.Value.Value.Equals(value)) {
            data.Remove(current);
            break;
          }
          current = current.Next;
        }
        count--;
      }
    }

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
  }
}
