using Brunet;
using Brunet.Dht;
using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Net;
using System.Security.Cryptography;

namespace Ipop {
  public class DhtOp {
    public FDht dht;

    public DhtOp(FDht dht) {
      this.dht = dht;
    }

    /* Returns a password if it works or NULL if it didn't */
    public string Create(byte[] key, byte[] value, string password, int ttl) {
      password = GeneratePassword(password);
      string hashed_password = GetHashedPassword(password);

      int min_replies_per_queue = 2;
      int min_majority = this.dht.Degree/2 + 1;
      //int min_majority = this.dht.Degree;
      BooleanQuorum _quorum = new BooleanQuorum(min_replies_per_queue, min_majority);
      System.Threading.AutoResetEvent _re = new System.Threading.AutoResetEvent(false);

      BlockingQueue [] queues = this.dht.CreateF(key, ttl, hashed_password, value);

      EventHandler EnqueueHandler = delegate(object o, EventArgs args) {
        BlockingQueue q = (BlockingQueue) o;
        try {
          while(true) {
            bool timedout; 
            object res = q.Dequeue(0, out timedout);
            if (timedout) {
              break;
            }
          //add this result to the quorom
            _quorum.Add(q, res);
          }
        }
        catch(InvalidOperationException) {
        }
        bool done = _quorum.CheckFinished();
        if (done) {
        //signal the waiting thread
          _re.Set();
        }
      };

      foreach(BlockingQueue queue in queues) {
        queue.EnqueueEvent += new EventHandler(EnqueueHandler);
        //also dequeue if something is already in there
        EnqueueHandler(queue, null);
      }

      //wait for upto 60 seconds
      bool got_set = _re.WaitOne(60000, false);
      bool success = (_quorum.Result == BooleanQuorum.State.Success);
      //we should not close all queues, and cancel their events

      foreach(BlockingQueue queue in queues) {
        queue.EnqueueEvent -= new EventHandler(EnqueueHandler);
        queue.Close();
        _quorum = null;
      }

      _re.Reset();
      if (got_set && success) {
        return "SHA1:" + password;
      }
      return null;
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

        ArrayList [] results = BlockingQueue.ParallelFetchWithTimeout(q, 1000);

        tokens = new byte[this.dht.Degree][];
        ArrayList result = null;
        for (int i = 0; i < results.Length; i++ ) {
          ArrayList q_replies = results[i];
          //investigating individual results
          foreach (RpcResult rpc_replies in q_replies) {
            try{
              ArrayList rpc_result = (ArrayList) rpc_replies.Result;
              if (rpc_result == null || rpc_result.Count < 3) {
                continue;
              }
              result = rpc_result;
              ArrayList values = (ArrayList) result[0];
              int local_remaining = (int) result[1];
              if(local_remaining > remaining) {
                remaining = local_remaining;
              }

              tokens[i] = (byte[]) result[2];

              foreach (Hashtable ht in values) {
                DhtGetResult dgr = new DhtGetResult(ht);
                if(!allValues.Contains(dgr)) {
                  allValues.Add(dgr);
                }
              }
            }
            catch (Exception e) {
              Console.WriteLine(e);
              return null;
            }
          }
        }
      }

      return (DhtGetResult []) allValues.ToArray(typeof(DhtGetResult));
    }

    public DhtGetResult[] Get(string key) {
      byte[] keyb = GetHashedKey(key);
      return Get(keyb);
    }

    public string Put(byte[] key, byte[] value, string password, int ttl) {
      password = GeneratePassword(password);
      string hashed_password = GetHashedPassword(password);

      BlockingQueue[] q = this.dht.PutF(key, ttl, hashed_password, value);
      RpcResult res = q[0].Dequeue() as RpcResult;
      foreach(BlockingQueue queue in q) {
        queue.Close();
      }
      return "SHA1:" + password;
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

    public string GeneratePassword(string password) {
      if(password == null) {
        byte[] bin_password = new byte[10];
        RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        rng.GetBytes(bin_password);
        password = Convert.ToBase64String(bin_password);
      }
      else if (password != null) {
        Console.WriteLine("b" + password.Length);
      //test validity of current password
        string[] ss = password.Split(new char[] {':'});
        if (ss.Length == 2 && ss[0] == "SHA1") {
          Console.WriteLine("d" + password.Length);
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

    private class BooleanQuorum {
      public enum State {
        Success = 0,
        Failure = 1, 
        NoResult = 2,
      }
      private State _result;
      public State Result {
        get {
          return _result;
        }
      }
      //hold replies for each queue
      private Hashtable _ht;
      //minimim replies in each queue
      private int _min_replies_per_queue;
      //minimum number of satisfactory queues for majority
      private int _min_majority;

      public BooleanQuorum(int min_replies_per_queue, int min_majority) {
        _min_replies_per_queue = min_replies_per_queue;
        _min_majority = min_majority;
        _ht = new Hashtable();
        _result = State.NoResult;
      }
      //returns a true when certain constraint is satisfied
      public void Add(BlockingQueue q, object reply) {
        lock(this) {
          //we managed to read something out
          if(!_ht.ContainsKey(q)) {
            _ht[q] = new ArrayList();
          }
          ArrayList x = (ArrayList) _ht[q];
          x.Add(reply);
        }
      }

      public bool CheckFinished() {
        lock(this) {
          int true_count = 0, false_count = 0, disagree_count = 0;
          //now check if we have a majority
          foreach (BlockingQueue q in _ht.Keys) {
            ArrayList x = (ArrayList) _ht[q];
            int success = 0;
            int failure = 0;
            foreach (RpcResult rpc_result in x) {
              try {
                bool result = (bool) rpc_result.Result;
                success++;
                continue;
              }
              catch(AdrException) {
                failure++;
                continue;
              }
            }
            //now we see if there has been a consensus
            if (success >= _min_replies_per_queue  && failure == 0) {
              true_count++;
            } else if (failure >= _min_replies_per_queue && success == 0) {
              false_count++;
            } else if (failure > 0 && success > 0) {
              disagree_count++;
            }
          }
          if (true_count == _min_majority) {
            _result = State.Success;
            return true;
          }
          else if (false_count == _min_majority) {
            _result = State.Failure;
            return true;
          }
          else if (disagree_count == _min_majority) {
            _result = State.NoResult;
            return true;
          }
          else {
            return false;
          }
        }
      }
    }
  }
}
