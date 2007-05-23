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

      BlockingQueue [] queues = dht.RecreateF(keyb, ttl, hashed_password, valueb);

      for (int i = 0; i < queues.Length; i++) {
        Console.Error.WriteLine("queue: {0} is at position: {1}", queues[i].GetHashCode(), i);
        queues[i].EnqueueEvent += new EventHandler(EnqueueHandler);
        //also dequeue if something is already in there
        EnqueueHandler(queues[i], null);
      }

      //wait for upto 60 seconds
      bool got_set = _re.WaitOne(60000, false);
      bool success = (_quorum.Result == BooleanQuorum.State.Success);
      //we should not close all queues, and cancel their events

      for (int i = 0; i < queues.Length; i++) {
        queues[i].EnqueueEvent -= new EventHandler(EnqueueHandler);
        queues[i].Close();
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

    public static void Delete(string key, string password, FDht dht) {
      BlockingQueue [] queues = dht.DeleteF(Encoding.UTF8.GetBytes(key), password);
      //just make the call and proceed
      BlockingQueue.ParallelFetch(queues, 0);
    }

    public static Hashtable[] Get(string key, FDht dht) {
      byte[] utf8_key = Encoding.UTF8.GetBytes(key);
      BlockingQueue[] q = dht.GetF(utf8_key, 1000, null);

      RpcResult res = q[0].Dequeue() as RpcResult;
      ArrayList result = res.Result as ArrayList;
      if (result == null || result.Count < 3) {
        return null;
      }
      ArrayList values = (ArrayList) result[0];
      Hashtable [] return_values = new Hashtable[values.Count];
      for (int i = 0; i < values.Count; i++) {
        Hashtable ht = (Hashtable) values[i];
        return_values[i] = new Hashtable();
        return_values[i].Add("age", ht["age"]);
        return_values[i].Add("value", ht["data"]);
        return_values[i].Add("value_string", Encoding.UTF8.GetString((byte []) ht["data"]));
      }
      return return_values;
    }

    public static string Put(string key, string value, string password, int ttl, FDht dht) {
      byte[] utf8_key = Encoding.UTF8.GetBytes(key);
      byte[] utf8_data = Encoding.UTF8.GetBytes(value);

      password = GeneratePassword(password);
      string hashed_password = GetHashedPassword(password);

      BlockingQueue[] q = dht.PutF(utf8_key, ttl, hashed_password, utf8_data);
      RpcResult res = q[0].Dequeue() as RpcResult;
      for (int i = 0; i < q.Length; i++) {
        q[i].Close();
      }
      return "SHA1:" + password;
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
#if DHCP_DEBUG
        Console.Error.WriteLine("Creating a dhcp quorum, min_replies_per_queue: {0}, min_majority: {1}", min_replies_per_queue,
                          min_majority);
#endif

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
#if DHCP_DEBUG
        Console.Error.WriteLine("Checking if the quorum is complete.");
#endif
        lock(this) {
          int true_count = 0, false_count = 0, disagree_count = 0;
          //now check if we have a majority
          foreach (BlockingQueue q in _ht.Keys) {
#if DHCP_DEBUG
            Console.Error.WriteLine("Analysing a queue.");
#endif
            ArrayList x = (ArrayList) _ht[q];
            if (x.Count < _min_replies_per_queue) {
#if DHCP_DEBUG
              Console.Error.WriteLine("Incorrect number of  results: {0} ({1} expected).", 
                              x.Count, _min_replies_per_queue);
#endif
            }
            //in case we have sufficient results
            int success = 0;
            int failure = 0;
            foreach (RpcResult rpc_result in x) {
              try {
                bool result = (bool) rpc_result.Result;
#if DHCP_DEBUG
                Console.Error.WriteLine("Result for acquire: {0}", result);
#endif
                success++;
                continue;
#if DHCP_DEBUG
              } catch(AdrException e) {

                Console.Error.WriteLine(e);
                Console.Error.WriteLine(e.Message);
#else
              } catch(AdrException) {
#endif
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
#if DHCP_DEBUG
            Console.Error.WriteLine("quorum has succeeded, true: {0}, false: {1}, disagree: {2}.", 
                              true_count, false_count, disagree_count);
#endif
            _result = State.Success;
            return true;
          } else if (false_count == _min_majority) {
#if DHCP_DEBUG
            Console.Error.WriteLine("quorum has failed, true: {0}, false: {1}, disagree: {2}.", 
                              true_count, false_count, disagree_count);
#endif      
            _result = State.Failure;
            return true;
          } else if (disagree_count == _min_majority) {
#if DHCP_DEBUG
            Console.Error.WriteLine("quorum has disagreed, true: {0}, false: {1}, disagree: {2}.", 
                              true_count, false_count, disagree_count);
#endif      
            _result = State.NoResult;
            return true;
          } else {
#if DHCP_DEBUG
            Console.Error.WriteLine("quorum not yet complete, true: {0}, false: {1}, disagree: {2}.", 
                              true_count, false_count, disagree_count);
#endif
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
