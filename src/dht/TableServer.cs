// _expiring_entries needs to be moved to a SortedList for better performance

using System;
using System.Text;
using System.Collections;
using System.Security.Cryptography;

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
            return entry_list.Count;
          }
        }

        //Look up 
        Entry e = _ef.CreateEntry(key, hashed_password,  create_time, end_time,
                          data, _max_idx);

        //Add the entry to the end of the list.
        entry_list.Add(e);
        //Further add this to sorted list _expired_entries list
        InsertToSorted(e);

        ///@todo, we might need to tell a neighbor about this object
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
}

