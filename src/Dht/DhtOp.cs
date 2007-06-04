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
/* Returns a password if it works or NULL if it didn't */
    public static string Create(string key, byte [] valueb, string password, int ttl, FDht dht) {
      byte[] keyb = Encoding.UTF8.GetBytes(key);

      password = GeneratePassword(password);
      string hashed_password = GetHashedPassword(password);

      int min_replies_per_queue = 2;
      int min_majority = dht.Degree/2 + 1;
      //int min_majority = dht.Degree;
      _quorum = new BooleanQuorum(min_replies_per_queue, min_majority);

      BlockingQueue [] queues = dht.CreateF(keyb, ttl, hashed_password, valueb);

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

    public static string Create(string key, string value, string password, int ttl, FDht dht) {
      byte[] valueb = Encoding.UTF8.GetBytes(value);
      return Create(key, valueb, password, ttl, dht);
    }

    // This method could be heavily parallelized
    public static DhtGetResult[] Get(string key, FDht dht) {
      byte[] utf8_key = Encoding.UTF8.GetBytes(key);
      ArrayList allValues = new ArrayList();
      int remaining = -1;
      ArrayList tokens = new ArrayList();

      while(remaining != 0) {
        remaining = -1;
        BlockingQueue[] q = null;
        if(tokens.Count == 0) {
          q = dht.GetF(utf8_key, 1000, null);
        }
        else {
          q = dht.GetF(utf8_key, 1000, (byte [][]) tokens.ToArray(typeof(byte[])));
        }

        ArrayList [] results = BlockingQueue.ParallelFetchWithTimeout(q, 1000);

        tokens.Clear();
        ArrayList result = null;
        foreach (ArrayList q_replies in results) {
          foreach (RpcResult rpc_replies in q_replies) {
          //investigating individual results
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
              tokens.Add((byte[]) result[2]);

              foreach (Hashtable ht in values) {
                DhtGetResult dgr = new DhtGetResult(ht);
                if(!allValues.Contains(dgr)) {
                  allValues.Add(dgr);
                }
              }
            }
            catch (Exception) {
              return null;
            }
          }
        }
      }

      if(allValues.Count == 0) {
        return null;
      }

      return (DhtGetResult []) allValues.ToArray(typeof(DhtGetResult));
    }

    public static string Put(string key, byte[] value, string password, int ttl, FDht dht) {
      byte[] utf8_key = Encoding.UTF8.GetBytes(key);

      password = GeneratePassword(password);
      string hashed_password = GetHashedPassword(password);

      BlockingQueue[] q = dht.PutF(utf8_key, ttl, hashed_password, value);
      RpcResult res = q[0].Dequeue() as RpcResult;
      foreach(BlockingQueue queue in q) {
        queue.Close();
      }
      return "SHA1:" + password;
    }

    public static string Put(string key, string value, string password, int ttl, FDht dht) {
      byte[] valueb = Encoding.UTF8.GetBytes(value);
      return Put(key, valueb, password, ttl, dht);
    }

    public static string GeneratePassword(string password) {
      if(password == null) {
        byte[] bin_password = new byte[10];
        Random _rand = new Random();
        _rand.NextBytes(bin_password);
        password = Convert.ToBase64String(bin_password);
      }
      else if (password != null) {
      //test validity of current password
        string[] ss = password.Split(new char[] {':'});
        if (ss.Length == 2 && ss[0] == "SHA1") {
          password = ss[1];
        }
      }
      return password;
    }

    public static string GetHashedPassword(string password) {
      byte[] bin_password = Convert.FromBase64String(password);
      HashAlgorithm algo = new SHA1CryptoServiceProvider();
      byte[] sha1_pass = algo.ComputeHash(bin_password);
      return "SHA1:" + Convert.ToBase64String(sha1_pass);
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

    private static BooleanQuorum _quorum;
    private static System.Threading.AutoResetEvent _re = new System.Threading.AutoResetEvent(false);

    private static void EnqueueHandler(object o, EventArgs args) {
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
    }
  }
}
