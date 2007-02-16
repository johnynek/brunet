using System;
using System.Text;
using System.Collections;
using System.Security.Cryptography;

using Brunet;

#if DHT_LOG
using log4net;
using log4net.Config;
#endif

namespace Brunet.Dht {


public class TableServer {
#if DHT_LOG
    private static readonly log4net.ILog _log =
    log4net.LogManager.GetLogger(System.Reflection.MethodBase.
				 GetCurrentMethod().DeclaringType);
#endif
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

  protected  bool ValidatePasswordFormat(string password, 
					 out string hash_name,
					 out string base64_val) {
    
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
      //delete keys that have expired
      DeleteExpired();
#if DHT_DEBUG
      Console.WriteLine("[DhtServer: {0}] Cleaned up expired entries.", _node.Address);
#endif
      foreach (Object val in _ht.Values) 
      {
	ArrayList entry_list = (ArrayList) val;
	count += entry_list.Count;
      }
    }
    //Alternatively, we could also have count as number of keys
    //return _ht.Count;

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
#if DHT_LOG
    _log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::RequestPut::::" + 
    	       + Base32.Encode(key));
#endif

#if DHT_DEBUG
    Console.WriteLine("[DhtServer: {0}]: Put() on key: {1}", _node.Address, Base32.Encode(key));
#endif

    string hash_name = null;
    string base64_val = null;
    if (!ValidatePasswordFormat(hashed_password, out hash_name, 
				out base64_val)) {
      throw new Exception("Invalid password format.");
    }
    
    DateTime create_time = DateTime.Now;
    TimeSpan ts = new TimeSpan(0,0,ttl);
    DateTime end_time = create_time + ts;
   
    lock(_sync) {
      //delete all keys that have expired
      DeleteExpired();
#if DHT_DEBUG
      Console.WriteLine("[DhtServer: {0}] Cleaned up expired entries.", _node.Address);
#endif
      MemBlock ht_key = MemBlock.Reference(key, 0, key.Length);
      ArrayList entry_list = (ArrayList)_ht[ht_key];
      if( entry_list != null ) {
        //Make sure we only keep one reference to a key to save memory:
	//Arijit Ganguly - I had no idea what this was about. Now I know...
#if DHT_DEBUG
	Console.WriteLine("[DhtServer: {0}]: Key exists.", _node.Address);
#endif
        key = ((Entry)entry_list[0]).Key;
	ht_key = MemBlock.Reference(key, 0, key.Length);
      }
      else {
        //This is a new key:
        entry_list = new ArrayList();
	//added the new key to hashtable
#if DHT_DEBUG
	Console.WriteLine("[DhtServer: {0}]: Key doesn't exist. Created new entry_list.", _node.Address);
#endif
	_ht[ht_key] = entry_list;
      }
      _max_idx++; //Increment the maximum index

      foreach(Entry ent in entry_list) {
	if (ent.Password.Equals(hashed_password)) {
#if DHT_DEBUG
	  Console.WriteLine("[DhtServer: {0}]: Attempting to duplicate. (No put).", _node.Address);
#endif
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

#if DHT_LOG
      _log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::SuccessPut::::" + 
		+ Base32.Encode(key));
#endif     
      ///@todo, we might need to tell a neighbor about this object
      return entry_list.Count;
    }
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
  
  public bool Create(byte[] key, int ttl, string hashed_password, byte[] data) 
  {
#if DHT_LOG
    _log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::RequestCreate::::" + 
	       + Base32.Encode(key));
#endif
#if DHT_DEBUG
    Console.WriteLine("[DhtServer: {0}]: Create() on key: {1}.", 
		      _node.Address, Base32.Encode(key));
#endif
    string hash_name = null;
    string base64_val = null;
    if (!ValidatePasswordFormat(hashed_password, out hash_name,
				out base64_val)) {
      throw new Exception("Invalid password format.");
    }
    DateTime create_time = DateTime.Now;
    TimeSpan ts = new TimeSpan(0,0,ttl);
    DateTime end_time = create_time + ts;
    
    lock(_sync) {
      //delete all keys that have expired
      DeleteExpired();
#if DHT_DEBUG
      Console.WriteLine("[DhtServer: {0}] Cleaned up expired entries.", _node.Address);
#endif
      MemBlock ht_key = MemBlock.Reference(key, 0, key.Length);
      ArrayList entry_list = (ArrayList)_ht[ht_key];
      if( entry_list != null ) {
#if DHT_DEBUG
	Console.WriteLine("[DhtServer: {0}]: Key exists (check for password and value match).", _node.Address);
#endif
	bool match = false;
	foreach(Entry e in entry_list) {
	  if (!e.Password.Equals(hashed_password)) {
	    continue;
	  }
	  MemBlock arg_data = MemBlock.Reference(data, 0, data.Length);
	  MemBlock e_data = MemBlock.Reference(e.Data, 0, e.Data.Length);
	  if (!e_data.Equals(arg_data)) {
	    continue;
	  }
	  match = true;
	}
	if (!match) {
#if DHT_DEBUG
	  Console.WriteLine("[DhtServer: {0}]: No duplication allowed. Key exists.", _node.Address);
#endif
	  //we already have the key mapped to something.
	  throw new Exception("Attempting to duplicate the key. Entry exists.");
	}
      } else {
	//This is a new key:
	entry_list = new ArrayList();
	_ht[ht_key] = entry_list;
#if DHT_DEBUG
	Console.WriteLine("[DhtServer:{0}]: Key doesn't exist. Created new entry_list.", _node.Address);
#endif      
	_max_idx++; //Increment the maximum index
	//Look up 
	Entry new_e = _ef.CreateEntry(key, hashed_password,  create_time, end_time,
				      data, _max_idx);
	//Add the entry to the end of the list.
	entry_list.Add(new_e);
	//Further add the entry to the sorted list _expired_entries
	InsertToSorted(new_e);
      }
#if DHT_LOG
      _log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::SuccessCreate::::" + 
		 + Base32.Encode(key));
#endif
      return true;
    } //release the lock
  }
  /**
   * This method allows renewing the lifetime of an existing key-value
   * which has a password and value match. (equivalent to a create if the key already does not exist).
   * Can be invoked multiple times now.
   * @param key key associated with the date item
   * @param ttl time-to-live in seconds
   * @param hashed_password <hash_name>:<base64(hashed_pass)> 
   * @return true on success, false on failure
   */
  public bool Recreate(byte[] key, int ttl, string hashed_password, byte[] data)
  {
#if DHT_LOG
    _log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::RequestRecreate::::" + 
	       + Base32.Encode(key));
#endif

#if DHT_DEBUG
    Console.WriteLine("[DhtServer: {0}]: Recreate() on key: {1}.", _node.Address, Base32.Encode(key));
#endif    

    string hash_name = null;
    string base64_val = null;
    if (!ValidatePasswordFormat(hashed_password, out hash_name, 
				out base64_val)) {
      throw new Exception("Invalid password format.");
    }
    DateTime create_time = DateTime.Now;
    TimeSpan ts = new TimeSpan(0,0,ttl);
    DateTime end_time = create_time + ts;
    
    lock(_sync) {
      //delete all keys that have expired
      DeleteExpired();
#if DHT_DEBUG
      Console.WriteLine("[DhtServer: {0}] Cleaned up expired entries.", _node.Address);
#endif
      MemBlock ht_key = MemBlock.Reference(key, 0, key.Length);
      ArrayList entry_list = (ArrayList)_ht[ht_key];
      if( entry_list != null ) {
#if DHT_DEBUG
	Console.WriteLine("[DhtServer: {0}]: Key exists (check for password and value match).", _node.Address);
#endif
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

	Entry new_e = _ef.CreateEntry(to_renew.Key, hashed_password, to_renew.CreatedTime, end_time,
				      data, to_renew.Index);
	//add the new entry
	entry_list.Add(new_e);
	InsertToSorted(new_e);
	
      } else {     
	//This is a new key, just a regular Create()
	entry_list = new ArrayList();
	_ht[ht_key] = entry_list;
#if DHT_DEBUG
	Console.WriteLine("[DhtServer:{0}]: Key doesn't exist. Created new entry_list.", _node.Address);
#endif      
	_max_idx++; //Increment the maximum index
	//Look up 
	Entry e = _ef.CreateEntry(key, hashed_password,  create_time, end_time,
				  data, _max_idx);
	//Add the entry to the end of the list.
	entry_list.Add(e);
	//Further add the entry to the sorted list _expired_entries
	InsertToSorted(e);
      }
#if DHT_LOG
      _log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::SuccessRenew::::" + 
		 + Base32.Encode(key));
#endif
      return true;	
    }//end of lock
  }
      

  public IList Get(byte[] key, int maxbytes, byte[] token)
  {
#if DHT_LOG
    _log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::RequestGet::::" + 
	       + Base32.Encode(key));
#endif

#if DHT_DEBUG
    Console.WriteLine("[DhtServer: {0}]: Get() on key: {1}", _node.Address, Base32.Encode(key));
#endif

    int seen_start_idx = -1;
    int seen_end_idx = -1;
    if( token != null ) {
      
      //This is a continuing get...
      //This should be an array of ints:
      int[] bounds = (int[])AdrConverter.Deserialize(new System.IO.MemoryStream(token));
      seen_start_idx = bounds[0];
      seen_end_idx = bounds[1];
#if DHT_DEBUG
      Console.WriteLine("[DhtServer: {0}]: seen_start_idx: {1}", _node.Address,  seen_start_idx);
      Console.WriteLine("[DhtServer: {0}]: seen_end_idx: {1}", _node.Address, seen_end_idx);
#endif
    } else {
#if DHT_DEBUG
      Console.WriteLine("[DhtServer: {0}]: null token.", _node.Address);
#endif  
    }
    int consumed_bytes = 0;
    
    ArrayList result = new ArrayList();
    ArrayList values = new ArrayList();
    int remaining_items = 0;
    byte[] next_token = null;
    
    lock(_sync ) { 
    //delete keys that have expired
    DeleteExpired();
#if DHT_DEBUG
      Console.WriteLine("[DhtServer: {0}] Cleaned up expired entries.", _node.Address);
#endif      

    MemBlock ht_key = MemBlock.Reference(key, 0, key.Length);  
    ArrayList entry_list = (ArrayList)_ht[ht_key];

    int seen = 0; //Number we have already seen for this key
    if( entry_list != null ) {
#if DHT_DEBUG
      Console.WriteLine("[DhtServer: {0}]: Key exists. Browing the entry_list.", _node.Address);
#endif

      int max_index = seen_end_idx;
      foreach(Entry e in entry_list) {
        if( e.Index > seen_end_idx ) { 
          //We may add this one:
          if( e.Data.Length + consumed_bytes <= maxbytes ) {
            //Lets add it
            TimeSpan age = DateTime.Now - e.CreatedTime;
            int age_i = (int)age.TotalSeconds;
            consumed_bytes += e.Data.Length;
            Hashtable item = new Hashtable();
            item["age"] = age_i;
            item["data"] = e.Data;
            values.Add(item);
#if DHT_DEBUG
	    Console.WriteLine("[DhtServer: {0}]: Added value to results.", _node.Address);
#endif
	    if (e.Index > max_index) {
	      max_index= e.Index;
	    }
          }
	    else {
            //We are all full up
	      break;
          }
        }
        else {
          //This is one we have seen, don't count it in the
          //count of seen items;
          seen++;
        }
      }
      seen_end_idx = max_index;
      /*
       * Now compute how many items remain:
       * We have either already seen them, sending them now, or not yet seen them.
       */


#if DHT_DEBUG
      Console.WriteLine("[DhtServer: {0}]: # Total entries: {1}", _node.Address, entry_list.Count);
      Console.WriteLine("[DhtServer: {0}]: # Seen: {1}", _node.Address,seen);
      Console.WriteLine("[DhtServer: {0}]: # Values returned: {1}", _node.Address, values.Count);
#endif

      remaining_items = entry_list.Count - seen - values.Count;
    }
    else {
#if DHT_DEBUG
      Console.WriteLine("[DhtServer: {0}]: Key doesn't exist.", _node.Address);
#endif

      //Don't know about this key
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
  /**
   *  delete a key from the Table
   *  @param password <hash_algo>:<base64(plain_text_password)>
   *  @throws exception if invalid password
   */
  public void Delete(byte[] key, string password)
  {
#if DHT_LOG
    _log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::RequestDelete::::" + 
	       + Base32.Encode(key));
#endif

#if DHT_DEBUG
    Console.WriteLine("[DhtServer: {0}]: Delete() on key: {1}.", _node.Address, Base32.Encode(key));
#endif    
    string hash_name = null;
    string base64_pass = null;
    if (!ValidatePasswordFormat(password, out hash_name, 
				out base64_pass)) {
      throw new Exception("Invalid password format.");
    }
    HashAlgorithm algo = null;
    if (hash_name.Equals("SHA1")) {
      algo = new SHA1CryptoServiceProvider();
    } else if  (hash_name.Equals("MD5")) {
      algo = new MD5CryptoServiceProvider();
    }
    
    byte[] bin_pass = Convert.FromBase64String(base64_pass);
    byte [] sha1_hash = algo.ComputeHash(bin_pass);
    string base64_hash = Convert.ToBase64String(sha1_hash);
    string stored_pass =  hash_name + ":" + base64_hash;
    
    
    lock(_sync ) { 
      //delete keys that have expired
      DeleteExpired();  
      
      MemBlock ht_key = MemBlock.Reference(key, 0, key.Length);
      ArrayList entry_list = (ArrayList)_ht[ht_key];
      bool found = false;
      if (entry_list != null) {
#if DHT_DEBUG
	Console.WriteLine("[DhtServer: {0}]: Key exists. Browing the entry_list.", _node.Address);
#endif
	ArrayList to_delete = new ArrayList();

	//we will ony delete the entry which corresponds to the 
	//password provided
	//we therefore have to verify the password
	foreach(Entry e in entry_list) {
	  if (e.Password.Equals(stored_pass)) {
#if DHT_DEBUG
	    Console.WriteLine("[DhtServer: {0}]: Found a key to delete.", _node.Address);
#endif
	    found = true;
	    //we have found a key to delete
	    to_delete.Add(e);
	  }
	}
	foreach (Entry e in to_delete) {
	  entry_list.Remove(e);
	  //further remove the entry from the sorted list
	  DeleteFromSorted(e);  
	}
	//in case that the entry_list has shrunk to size 0, make it null
	if (entry_list.Count == 0) {
	  _ht.Remove(ht_key);
	}
#if DHT_LOG
	_log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::SuccessDelete::::" + 
		   + Base32.Encode(key));
#endif
      } else {
#if DHT_DEBUG
	Console.WriteLine("[DhtServer: {0}]: Key doesn't exist.", _node.Address);
	return;
#endif
      }
      if (!found) {
	//raise an error
#if DHT_DEBUG
	Console.WriteLine("[DhtServer: {0}]: Incorrect password", _node.Address);
#endif
	throw new Exception("Access control violation on key. Incorrect password");	
      }
    }
  }
  /** protected methods. */

  /** The method gets rid of keys that have expired. 
   *  (Assuming that _expiring_entries is sorted).
   */
  protected void DeleteExpired() {
    //scan through the list and remove entries 
    //whose expiration times have elapsed
#if DHT_DEBUG
    Console.WriteLine("[DhtServer: {0}] Getting rid of expired entries.", _node.Address);
#endif
    int del_count = 0;
    DateTime now = DateTime.Now;
    foreach(Entry e in _expiring_entries) {
      DateTime end_time = e.EndTime; 
      //Console.WriteLine("analysing an entry now: {0}, endtime: {1}", now, end_time);
      //first entry that hasn't expired, rest all should stay
      if (end_time > now) 
      {
	//Console.WriteLine("entry not expired (break)");
	break;
      }
      //we certainly are lookin at an entry that has expired
      //get rid of this entry
      //Console.WriteLine("entry has expired: {0}", e.Key);
      MemBlock key = MemBlock.Reference(e.Key, 0, e.Key.Length);
      ArrayList entry_list = (ArrayList) _ht[key];
      if (entry_list == null) {
	Console.WriteLine("This is fatal, the expired key is not recorded in hashtable.");
      }
      //remove this from the entry list
      entry_list.Remove(e);
      if (entry_list.Count == 0) {
	_ht.Remove(key);
      }
      del_count++;
    }
#if DHT_DEBUG
    Console.WriteLine("[DhtServer: {0}] {1} entries stand expired.", _node.Address,del_count);
#endif
    if (del_count > 0) {
      _expiring_entries.RemoveRange(0, del_count);
#if DHT_DEBUG
      Console.WriteLine("[DhtServer: {0}] {1} entries deleted.", _node.Address,del_count);
#endif
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
#if DHT_DEBUG
    Console.WriteLine("[DhtServer: {0}] New entry ranks: {1} in sorted array.",_node.Address,
		      idx);
#endif
    _expiring_entries.Insert(idx, new_entry);
  }
  /** we further need a way to get rid of entries that are deleted.*/
  protected void DeleteFromSorted(Entry e) {
#if DHT_DEBUG
    Console.WriteLine("[DhtServer: {0}] Removing an entry from sorted list. ", _node.Address);
#endif
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
   *  
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
#if DHT_LOG
	_log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::AdminDelete::::" + 
		   + Base32.Encode(k));
#endif
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
  
