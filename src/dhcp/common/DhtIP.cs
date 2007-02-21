#define DHCP_DEBUG

using System;
using System.IO;
using System.Text;
using System.Collections;

using System.Security.Cryptography;

using Brunet;
using Brunet.Dht;

namespace Ipop {
  public class DhtIP {
    public static void ReleaseIP(FDht _dht, string ipop_namespace, string ip, string password) {
      if (password == null) {
        Console.WriteLine("No password provided for ReleaseIP.");
        return;
      }
      BlockingQueue [] queues = _dht.DeleteF(Encoding.UTF8.GetBytes("dhcp:ip:" + ip), password);
      //just make the call and proceed
      BlockingQueue.ParallelFetch(queues, 0);
    }
    private class DhcpQuorum {
      //hold replies for each queue
      private Hashtable _ht;

      //minimim replies in each queue
      private int _min_replies_per_queue;

      //minimum number of satisfactory queues for majority
      private int _min_majority;

      public DhcpQuorum(int min_replies_per_queue, int min_majority) {
        _min_replies_per_queue = min_replies_per_queue;
        _min_majority = min_majority;
        _ht = new Hashtable();
#if DHCP_DEBUG
        Console.WriteLine("Created a Dhcp quorum, min_replies_per_queue: {0}, min_majority: {1}", _min_replies_per_queue,
                          _min_majority);
#endif

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
      public bool Check() {
#if DHCP_DEBUG
        Console.WriteLine("Checking if the quorum is satisfied");
#endif
        int remaining = _min_majority;
        lock(this) {
          //now test if constraint is met
          foreach (BlockingQueue q in _ht.Keys) {
#if DHCP_DEBUG
            Console.WriteLine("Analysing a queue."); 
#endif
            ArrayList q_result = (ArrayList) _ht[q];
            if (q_result.Count < _min_replies_per_queue) {
#if DHCP_DEBUG
              Console.WriteLine("Fewer results: {0} ({1} expercted).", 
                                q_result.Count, _min_replies_per_queue);
#endif
              continue;
            }

            bool success = true;
            foreach (RpcResult rpc_result in q_result) {
              try {
                bool result = (bool) rpc_result.Result;
#if DHCP_DEBUG
                Console.WriteLine("Result for acquire: {0}", result);
#endif
                continue;
              } catch(AdrException e) {
#if DHCP_DEBUG
                Console.WriteLine(e);
                Console.WriteLine(e.Message);
#endif
                success = false;
                continue;
              }
            }
            if (success) {
#if DHCP_DEBUG
              Console.WriteLine("Sufficient results.");
#endif
              remaining--;
            }
          }
          if (remaining <= 0) {
#if DHCP_DEBUG
            Console.WriteLine("quorum satisfied, remaining: {0} (return true).", remaining);
#endif
            return true;
          } else {
#if DHCP_DEBUG
            Console.WriteLine("quorum not yet satisfied, remaining: {0} (return false).", remaining);
#endif
            return false;
          }
        }
      }
    }

    private static DhcpQuorum _quorum;
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
      bool done = _quorum.Check();
      if (done) {
        //signal the waiting thread
        _re.Set();
      }
    }


    public static bool GetIP(FDht _dht, string ipop_namespace, string ip,
      int leasetime, byte [] brunet_id, ref string password) {
      //get a password into a form that is usable for DHT operations
      byte[] bin_password = null;
      bool valid = false;
      if (password != null) {
      //test validity of current password
        string[] ss = password.Split(new char[] {':'});
        if (ss.Length != 2) {
          Console.WriteLine("Invalid password for GetIP (will generate a new one).");
        }
        else {
          bin_password = Convert.FromBase64String(ss[1]);
          valid = true;
        }
      }
      if (password == null || !valid) {
        bin_password = new byte[10];
        Random _rand = new Random();
        _rand.NextBytes(bin_password);
        password = "SHA1:" + Convert.ToBase64String(bin_password);
      }

      HashAlgorithm algo = new SHA1CryptoServiceProvider();
      byte[] sha1_pass = algo.ComputeHash(bin_password);
      string hashed_password = "SHA1:" + Convert.ToBase64String(sha1_pass);

      //now generate a dht_key for the IPOP namespace and IP address combination
      string str_key = "dhcp:ip:" + ip;
      byte[] dht_key = Encoding.UTF8.GetBytes(str_key);
#if DHCP_DEBUG
      Console.WriteLine("attempting to acquire: {0} at time: {1}", ip, DateTime.Now);
      Console.WriteLine("Invoking recreate() on: {0}", str_key);
#endif
      int min_replies_per_queue = 2;
      int min_majority = _dht.Degree/2 + 1;
      //int min_majority = _dht.Degree;
      _quorum = new DhcpQuorum(min_replies_per_queue, min_majority);
      BlockingQueue [] queues = _dht.RecreateF(dht_key, leasetime, hashed_password, brunet_id);
      for (int i = 0; i < queues.Length; i++) {
        Console.WriteLine("queue: {0} is at position: {1}", queues[i].GetHashCode(), i);
        queues[i].EnqueueEvent += new EventHandler(EnqueueHandler);
        //also dequeue if something is already in there
        EnqueueHandler(queues[i], null);
      }

      //wait for upto 60 seconds
      bool got_set = _re.WaitOne(60000, false);
      //we should not close all queues, and cancel their events
      for (int i = 0; i < queues.Length; i++) {
        queues[i].EnqueueEvent -= new EventHandler(EnqueueHandler);
        queues[i].Close();
        _quorum = null;
      }

      _re.Reset();
      if (got_set) {
        //set quorom to null
#if DHCP_DEBUG
        Console.WriteLine("successfully acquired ip: {0} at time: {1}",ip, DateTime.Now);
#endif
        return true;
      }
#if DHCP_DEBUG
      Console.WriteLine("failed to acquire ip: {0} at time: {1}",ip, DateTime.Now);
#endif
      return false;
    }
  }
}
