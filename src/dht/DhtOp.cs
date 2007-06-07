using Brunet;
using Brunet.Dht;
using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Net;
using System.Security.Cryptography;

namespace Brunet.Dht {
  public class DhtOp {
    public FDht dht;

    public DhtOp(FDht dht) {
      this.dht = dht;
    }

    public static readonly int DELAY = 2000;

    /* Returns a password if it works or NULL if it didn't */
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

    // This method could be heavily parallelized
    public DhtGetResult[] Get(byte[] key) {
      ArrayList allValues = new ArrayList();
      int remaining = -1;
      byte [][]tokens = null;

      while(remaining != 0) {
        remaining = -1;
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

        while(true) {
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
              DhtGetResult dgr = new DhtGetResult(ht);
              if(!allValues.Contains(dgr)) {
                allValues.Add(dgr);
              }
            }
          }
          catch (Exception) {;} // Treat this as receiving nothing
        }
        foreach(BlockingQueue queue in q) {
          queue.Close();
        }
      }

      return (DhtGetResult []) allValues.ToArray(typeof(DhtGetResult));
    }

    public DhtGetResult[] Get(string key) {
      byte[] keyb = GetHashedKey(key);
      return Get(keyb);
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

    /* Since the Puts and Creates are the same from the client side, we merge them into a
       single put that if unique is true, it is a create, otherwise a put */

    public string Put(byte[] key, byte[] value, string password, int ttl, bool unique) {
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
      int pcount = 0, ncount = 0, majority = this.dht.Degree / 2 + 1;
      ArrayList allQueues = new ArrayList();
      allQueues.AddRange(q);

      DateTime start = DateTime.UtcNow;

      while(pcount <= majority || ncount < majority) {
        TimeSpan ts_timeleft = DateTime.UtcNow - start;
        int time_diff = ts_timeleft.Milliseconds;
        int time_left = (DELAY - time_diff > 0) ? DELAY - time_diff : 0;

        int idx = BlockingQueue.Select(allQueues, time_left);
        bool result = false;
        if(idx == -1) {
          break;
        }

        if(!((BlockingQueue) allQueues[idx]).Closed) {
          try {
            RpcResult rpc_reply = (RpcResult) ((BlockingQueue) allQueues[idx]).Dequeue();
            result = (bool) rpc_reply.Result;
          }
          catch(Exception) {;} // Treat this as receiving a negative
        }

        if(result == true) {
          pcount++;
        }
        else {
          ncount++;
        }
        allQueues.RemoveAt(idx);
      }

      if(pcount >= majority) {
        rv = "SHA1:" + password;
      }

      foreach(BlockingQueue queue in q) {
        queue.Close();
      }
      return rv;
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