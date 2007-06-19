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
    List<DateTime> _end_time_expiring_entries =  new List<DateTime>();
    Dictionary<int, MemBlock> _memblock_expiring_entries = new Dictionary<int, MemBlock>();
    Dictionary<MemBlock, int> _int_expiring_entries = new Dictionary<MemBlock, int>();
    Cache _data = new Cache(2500);
    protected string _base_dir;

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
      ArrayList data = (ArrayList) _data[ent.Key];
      if(data == null) {
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
      if(index == data.Count - 1) {
        ExpiredEntriesUpdate(ent.Key, ent.EndTime);
      }
    }

    /* When we have a cache eviction, we must write it to disk, we take
    * each entry, convert it explicitly into a hashtable, and then use adr
    * to create a stream and write it to disk
    */
    public void CacheEviction(Object o, EventArgs args) {
      Cache.EvictionArgs eargs = (Cache.EvictionArgs) args;
      MemBlock key = (MemBlock) eargs.Key;
      if(eargs.Value != null || ((ArrayList) eargs.Value).Count > 0) {
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
    public void DeleteExpired() {
      DateTime now = DateTime.UtcNow;
      for(int i = 0; i < _end_time_expiring_entries.Count; i++) {
        if(_end_time_expiring_entries[i] > now) {
          break;
        }
        MemBlock key = _memblock_expiring_entries[i];

        ArrayList data = (ArrayList) _data[key];
        data.Clear();

        _memblock_expiring_entries.Remove(i);
        _int_expiring_entries.Remove(key);
        _end_time_expiring_entries.RemoveAt(i);
      }
    }

    /* Deletes any of the expired entries for a specific key, we execute this
    prior to any Dht operations involving the key in question
    */
    public void DeleteExpired(MemBlock key) {
      ArrayList data = (ArrayList) _data[key];
      if(data == null) {
        return;
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
    }

    /* This will add, update, or remove an Entry from the ExpiredEntries list.
     * The idea is that if end_time is null, we don't have a new key to insert,
     * and instead it is getting removed.  So we check to see if there are
     * other entries, if there are, we move them into the ExpiredEntries, if
     * not, we're all done
     */
    public void ExpiredEntriesUpdate(MemBlock key, DateTime end_time) {
      int pos;
      if(_int_expiring_entries.TryGetValue(key, out pos)) {
        _memblock_expiring_entries.Remove(pos);
        _end_time_expiring_entries.RemoveAt(pos);
      }
      if(end_time.Equals(DateTime.MinValue)) {
        ArrayList data = (ArrayList) _data[key];
        if(data != null && data.Count > 0) {
          end_time = ((Entry) data[data.Count - 1]).EndTime;
        }
      }
      if(!end_time.Equals(DateTime.MinValue)) {
        int index = _end_time_expiring_entries.BinarySearch(end_time);
        if(index < 0) {
          index = ~index;
        }
        _end_time_expiring_entries.Insert(index, end_time);
        _int_expiring_entries[key] = index;
        _memblock_expiring_entries[index] = key;
      }
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
      DeleteExpired();
      return -1;
    }

    // This gets us an ArrayList of entries based upon the key
    public ArrayList GetEntries(MemBlock key) {
      return (ArrayList) _data[key];
    }

    public IEnumerable GetKeys() {
      return _memblock_expiring_entries.Values;
    }

    /* Sometimes our put succeeds, but our recursive fails, this method gets
    * called to fix the mess
    */
    public void RemoveEntries(MemBlock key) {
      ArrayList data = (ArrayList) _data[key];
      if(data != null) {
        data.Clear();
        ExpiredEntriesUpdate(key, DateTime.MinValue);
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
            if(index == data.Count - 1) {
              ExpiredEntriesUpdate(key, DateTime.MinValue);
            }
            break;
          }
        }
      }
    }

    public void UpdateEntry(MemBlock key, MemBlock value, DateTime end_time) {
      ArrayList data = (ArrayList) _data[key];
      if(data != null) {
        int index = 0;
        for(index = 0; index < data.Count; index++) {
          if(value.Equals(key)) {
            ((Entry) data[index]).EndTime = end_time;
            if(index == data.Count - 1) {
              ExpiredEntriesUpdate(key, DateTime.MinValue);
            }
            break;
          }
        }
      }
    }
  }
}
