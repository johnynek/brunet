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
    Hashtable list_of_keys = new Hashtable();
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

    public void AddEntry(Entry ent) {
      CheckEntries();
      ArrayList data = (ArrayList) _data[ent.Key];
      if(data == null) {
        list_of_keys.Add(ent.Key, true);
        data = new ArrayList();
        _data[ent.Key] = data;
      }

      int index = 0;
      for(index = 0; index < data.Count; index++) {
        Entry entry = (Entry) data[index];
        if(entry.EndTime > ent.EndTime) {
          break;
        }
      }
      data.Insert(index, ent);
      count++;
    }

    /* When we have a cache eviction, we must write it to disk, we take
    * each entry, convert it explicitly into a hashtable, and then use adr
    * to create a stream and write it to disk
    */
    public void CacheEviction(Object o, EventArgs args) {
      Cache.EvictionArgs eargs = (Cache.EvictionArgs) args;
      MemBlock key = (MemBlock) eargs.Key;
      if(eargs.Value != null && ((ArrayList) eargs.Value).Count > 0) {
        Hashtable data = (Hashtable) eargs.Value;
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
          _data[key] = (Hashtable) AdrConverter.Deserialize(fs);
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
    entry are expired, otherwise we they must be deleted during an operation
    on the table regarding a specific key, or we would be constantly
    churning through an enormous list
    */
    public void CheckEntries() {
      DateTime now = DateTime.UtcNow;
      if(now - last_clean < TimeSpan.FromHours(24)) {
        return;
      }
      // Otherwise its time to do some cleaning!
      last_clean = now;
      Hashtable keys_to_delete = new Hashtable();
      foreach(MemBlock key in list_of_keys.Keys) {
        if(DeleteExpired(key) == 0) {
          keys_to_delete.Add(key, true);
        }
      }
      foreach(MemBlock key in keys_to_delete.Keys) {
        list_of_keys.Remove(key);
      }
    }

    /* Deletes any of the expired entries for a specific key, we execute this
    prior to any Dht operations involving the key in question
    */
    public int DeleteExpired(MemBlock key) {
      ArrayList data = (ArrayList) _data[key];
      if(data == null) {
        return 0;
      }
      int del_count = 0;
      DateTime now = DateTime.UtcNow;
      foreach(Entry ent in data) {
        if (ent.EndTime > now) {
          break;
        }
        del_count++;
      }
      if (del_count > 0) {
        data.RemoveRange(0, del_count);
      }
      count -= del_count;
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


    /* This is very broken now, we will need to manually update count for it
    * to work properly
    */
    public int GetCount() {
      return -1;
    }

    // This gets us an ArrayList of entries based upon the key
    public ArrayList GetEntries(MemBlock key) {
      CheckEntries();
      return (ArrayList) _data[key];
    }

    public LinkedList<MemBlock> GetKeysBetween(AHAddress add1, AHAddress add2) {
      LinkedList<MemBlock> keys = new LinkedList<MemBlock>();
      if(add1.IsRightOf(add2)) {
        foreach(MemBlock key in list_of_keys.Keys) {
          AHAddress key_addr = new AHAddress(key);
          if(key_addr.IsBetweenFromLeft(add1, add2)) {
            keys.AddLast(key);
          }
        }
      }
      else {
        foreach(MemBlock key in list_of_keys.Keys) {
          AHAddress key_addr = new AHAddress(key);
          if(key_addr.IsBetweenFromRight(add1, add2)) {
            keys.AddLast(key);
          }
        }
      }
      return keys;
    }

    public IEnumerable GetKeys() {
      CheckEntries();
      return (IEnumerable) list_of_keys.Keys;
    }

    /* Sometimes our put succeeds, but our recursive fails, this method gets
    * called to fix the mess
    */
    public void RemoveEntries(MemBlock key) {
      ArrayList data = (ArrayList) _data[key];
      count -= data.Count;
      if(data != null) {
        data.Clear();
      }
    }

    /* Sometimes our put succeeds, but our recursive fails, this method gets
     * called to fix the mess
     */
    public void RemoveEntry(MemBlock key, MemBlock value) {
      ArrayList data = (ArrayList) _data[key];
      if(data != null) {
        int index = 0;
        for(index = 0; index < data.Count; index++) {
          if(value.Equals(key)) {
            data.Remove(value);
            count--;
            break;
          }
        }
      }
    }

    public void UpdateEntry(MemBlock key, MemBlock value, DateTime end_time) {
      CheckEntries();
      ArrayList data = (ArrayList) _data[key];
      if(data != null) {
        Entry ent = null;
        for(int index = 0; index < data.Count; index++) {
          if(value.Equals(key)) {
            ent = (Entry) data[index];
            data.RemoveAt(index);
            AddEntry(ent);
            break;
          }
        }
      }
    }
  }
}
