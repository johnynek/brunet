using Brunet;
using Brunet.Dht;
using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Net;
using System.Security.Cryptography;
using System.Threading;

namespace Brunet.Dht {
  public class DhtOp {
    public FDht dht;

    public DhtOp(FDht dht) {
      this.dht = dht;
      this.MAJORITY = this.dht.Degree / 2 + 1;
    }

    // I guess with Async methods we can be more generous - after all,
    // if this fails - we're probably screwed anyway
    public static readonly int DELAY = 60000;
    private readonly int MAJORITY;

    /** Below are all the Create methods, they rely on a unique put *
      * this returns the password or null if it did not work        */

    public BlockingQueue AsCreate(byte[] key, byte[] value, string password, int ttl) {
      return AsPut(key, value, password, ttl, true);
    }

    public BlockingQueue AsCreate(string key, byte[] value, string password, int ttl) {
      byte[] keyb = GetHashedKey(key);
      return AsCreate(keyb, value, password, ttl);
    }

    public BlockingQueue AsCreate(string key, string value, string password, int ttl) {
      byte[] keyb = GetHashedKey(key);
      byte[] valueb = Encoding.UTF8.GetBytes(value);
      return AsCreate(keyb, valueb, password, ttl);
    }

    public string Create(byte[] key, byte[] value, string password, int ttl) {
      return Put(key, value, password, ttl, true);
    }

    public string Create(string key, byte[] value, string password, int ttl) {
      byte[] keyb = GetHashedKey(key);
      return Create(keyb, value, password, ttl);
    }

    public string Create(string key, string value, string password, int ttl) {
      byte[] keyb = GetHashedKey(key);
      byte[] valueb = Encoding.UTF8.GetBytes(value);
      return Create(keyb, valueb, password, ttl);
    }

    /** Below are all the Get methods */

    public BlockingQueue AsGet(string key) {
      byte[] keyb = GetHashedKey(key);
      return AsGet(keyb);
    }

    public BlockingQueue AsGet(byte[] key) {
      BlockingQueue queue = new BlockingQueue();
      object []data = new object[2];
      data[0] = key;
      data[1] = queue;
      ThreadPool.QueueUserWorkItem(new WaitCallback(Get), data);
      return queue;
    }

    public DhtGetResult[] Get(string key) {
      byte[] keyb = GetHashedKey(key);
      return Get(keyb);
    }

    public DhtGetResult[] Get(byte[] key) {
      BlockingQueue queue = AsGet(key);
      ArrayList allValues = new ArrayList();
      while(true) {
        // Still a chance for Dequeue to execute on an empty closed queue 
        // so we'll do this instead.
        try {
          DhtGetResult dgr = (DhtGetResult) queue.Dequeue();
          allValues.Add(dgr);
        }
        catch (Exception) {
          break;
        }
      }
      return (DhtGetResult []) allValues.ToArray(typeof(DhtGetResult));
    }

    /**  This is the get that does all the work, it is meant to be
     *   run as a thread */
    public void Get(object data) {
      object []data_array = (object[]) data;
      byte[] key = (byte[]) data_array[0];
      BlockingQueue allValues = (BlockingQueue) data_array[1];
      Hashtable allValuesCount = new Hashtable();
      int remaining = 0;
      byte [][]tokens = null;

      do {
        remaining = 0;
        BlockingQueue[] q = null;
        if(tokens == null) {
          q = this.dht.GetF(key, 1000, null);
        }
        else {
          q = this.dht.GetF(key, 1000, tokens);
        }

        ArrayList allQueues = new ArrayList();
        ArrayList queueMapping = new ArrayList();
        for(int i = 0; i < this.dht.Degree; i++) {
          queueMapping.Add(i);
        }
        allQueues.AddRange(q);
        tokens = new byte[this.dht.Degree][];

        DateTime start = DateTime.UtcNow;

        while(allQueues.Count > 0) {
          TimeSpan ts_timeleft = DateTime.UtcNow - start;
          int time_diff = ts_timeleft.Milliseconds;
          int time_left = (DELAY - time_diff > 0) ? DELAY - time_diff : 0;

          int idx = BlockingQueue.Select(allQueues, time_left);
          if(idx == -1) {
            break;
          }
          allQueues.RemoveAt(idx);
          int real_idx = (int) queueMapping[idx];
          queueMapping.RemoveAt(idx);
          idx = real_idx;

          if(q[idx].Closed) {
            continue;
          }
          try {
            RpcResult rpc_reply = (RpcResult) q[idx].Dequeue();
            ArrayList result = (ArrayList) rpc_reply.Result;
            //Result may be corrupted
            if (result == null || result.Count < 3) {
              continue;
            }
            ArrayList values = (ArrayList) result[0];
            int local_remaining = (int) result[1];
            if(local_remaining > remaining) {
              remaining = local_remaining;
            }

            tokens[idx] = (byte[]) result[2];
            foreach (Hashtable ht in values) {
              MemBlock mbVal = MemBlock.Reference((byte[])ht["value"]);
              if(!allValuesCount.Contains(mbVal)) {
                allValuesCount[mbVal] = 1;
              }
              else {
                int count = ((int) allValuesCount[mbVal]) + 1;
                allValuesCount[mbVal] = count;
                if(count == MAJORITY) {
                  allValues.Enqueue(new DhtGetResult(ht));
                }
              }
            }
          }
          catch (Exception) {;} // Treat this as receiving nothing
        }

        foreach(BlockingQueue queue in q) {
          queue.Close();
        }
      } while(remaining != 0);
      allValues.Close();
    }

    /** Below are all the Put methods, they use a non-unique put */

    public BlockingQueue AsPut(byte[] key, byte[] value, string password, int ttl) {
      return AsPut(key, value, password, ttl, false);
    }

    public BlockingQueue AsPut(string key, byte[] value, string password, int ttl) {
      byte[] keyb = GetHashedKey(key);
      return AsPut(keyb, value, password, ttl);
    }

    public BlockingQueue AsPut(string key, string value, string password, int ttl) {
      byte[] keyb = GetHashedKey(key);
      byte[] valueb = Encoding.UTF8.GetBytes(value);
      return AsPut(keyb, valueb, password, ttl);
    }

    public string Put(byte[] key, byte[] value, string password, int ttl) {
      return Put(key, value, password, ttl, false);
    }

    public string Put(string key, byte[] value, string password, int ttl) {
      byte[] keyb = GetHashedKey(key);
      return Put(keyb, value, password, ttl);
    }

    public string Put(string key, string value, string password, int ttl) {
      byte[] keyb = GetHashedKey(key);
      byte[] valueb = Encoding.UTF8.GetBytes(value);
      return Put(keyb, valueb, password, ttl);
    }

    /** Since the Puts and Creates are the same from the client side, we merge them into a
       single put that if unique is true, it is a create, otherwise a put */

    public BlockingQueue AsPut(byte[] key, byte[] value, string password, int ttl, bool unique) {
      BlockingQueue queue = new BlockingQueue();
      object []data = new object[6];
      data[0] = key;
      data[1] = value;
      data[2] = password;
      data[3] = ttl;
      data[4] = unique;
      data[5] = queue;
      ThreadPool.QueueUserWorkItem(new WaitCallback(Put), data);
      return queue;
    }

    public string Put(byte[] key, byte[] value, string password, int ttl, bool unique) {
      BlockingQueue queue = new BlockingQueue();
      object []data = new object[6];
      data[0] = key;
      data[1] = value;
      data[2] = password;
      data[3] = ttl;
      data[4] = unique;
      data[5] = queue;
      Put(data);
      return (string) queue.Dequeue();
    }


    public void Put(object data) {
      object[] data_array = (object[]) data;
      byte[] key = (byte[]) data_array[0];
      byte[] value = (byte[]) data_array[1];
      string password = (string) data_array[2];
      int ttl = (int) data_array[3];
      bool unique = (bool) data_array[4];
      BlockingQueue queue = (BlockingQueue) data_array[5];

      password = GeneratePassword(password);
      string hashed_password = GetHashedPassword(password);
      string rv = null;

      BlockingQueue[] q = null;
      if(unique) {
        q = this.dht.CreateF(key, ttl, hashed_password, value);
      }
      else {
        q = this.dht.PutF(key, ttl, hashed_password, value);
      }
      int pcount = 0, ncount = 0;
      ArrayList allQueues = new ArrayList();
      allQueues.AddRange(q);

      DateTime start = DateTime.UtcNow;

      while(pcount < MAJORITY && ncount < MAJORITY - 1) {
        TimeSpan ts_timeleft = DateTime.UtcNow - start;
        int time_diff = ts_timeleft.Milliseconds;
        int time_left = (DELAY - time_diff > 0) ? DELAY - time_diff : 0;

        int idx = BlockingQueue.Select(allQueues, time_left);
        int result = 1000;
        if(idx == -1) {
          break;
        }

        if(!((BlockingQueue) allQueues[idx]).Closed) {
          try {
            RpcResult rpc_reply = (RpcResult) ((BlockingQueue) allQueues[idx]).Dequeue();
            result = (int) rpc_reply.Result;
          }
          catch(Exception) {;} // Treat this as receiving a negative
        }

        if(result == 0) {
          pcount++;
        }
        else {
          ncount++;
        }
        allQueues.RemoveAt(idx);
      }

      if(pcount >= MAJORITY) {
        rv = "SHA1:" + password;
      }

      foreach(BlockingQueue qclose in q) {
        qclose.Close();
      }
      queue.Enqueue(rv);
      queue.Close();
    }

    public string GeneratePassword(string password) {
      if(password == null) {
        byte[] bin_password = new byte[10];
        RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        rng.GetBytes(bin_password);
        password = Convert.ToBase64String(bin_password);
      }
      else if (password != null) {
      //test validity of current password
        string[] ss = password.Split(new char[] {':'});
        if (ss.Length == 2 && ss[0] == "SHA1") {
          password = ss[1];
        }
        // must be a user input password
        else {
          int diff = (4 - (password.Length % 4)) % 4;
          password = password.PadRight(diff + password.Length, '0');
        }
      }
      return password;
    }

    public string GetHashedPassword(string password) {
      byte[] bin_password = Convert.FromBase64String(password);
      HashAlgorithm algo = new SHA1CryptoServiceProvider();
      byte[] sha1_pass = algo.ComputeHash(bin_password);
      return "SHA1:" + Convert.ToBase64String(sha1_pass);
    }

    public byte[] GetHashedKey(string key) {
      byte[] keyb = Encoding.UTF8.GetBytes(key);
      HashAlgorithm algo = new SHA1CryptoServiceProvider();
      return algo.ComputeHash(keyb);
    }
  }
}
