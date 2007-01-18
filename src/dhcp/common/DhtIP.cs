using System;
using System.IO;
using System.Text;
using System.Collections;

using System.Security.Cryptography;

using Brunet;
using Brunet.Dht;

namespace Ipop {
  public class DhtIP {
    public static bool GetIP(FDht _dht, string dht_key, string old_password, int leasetime, byte [] brunet_id, out string new_password) {
      //Generate a new password
      HashAlgorithm algo = new SHA1CryptoServiceProvider();
      byte[] bin_password = new byte[10];
      Random _rand = new Random();
      _rand.NextBytes(bin_password);
      new_password = "SHA1:" + Convert.ToBase64String(bin_password);
      byte[] sha1_pass = algo.ComputeHash(bin_password);
      string new_hashed_password = "SHA1:" + Convert.ToBase64String(sha1_pass);

      if (old_password == null)
        old_password = new_password;

      int max_results_per_queue = 2;
      int min_majority = 3;
      byte[] dht_key_bytes = Encoding.UTF8.GetBytes(dht_key);
      BlockingQueue [] queues = null;
      try {
        queues = _dht.RecreateF(dht_key_bytes, old_password, leasetime, new_hashed_password, brunet_id);
      }
      catch (Exception) { 
        System.Console.WriteLine("Dht not enabled yet....");
        return false;
      }

      ArrayList []results = null;
      try {
        results = BlockingQueue.ParallelFetchWithTimeout(queues, 5000);
      }
      catch (Exception) {
        System.Console.WriteLine("Dht error....");
        return false;
      }

      //this method will return as soon as we have results available
      for (int i = 0; i < results.Length; i++) {
        bool success = false;
        ArrayList q_result = results[i];
        if (q_result.Count < max_results_per_queue) {
          continue;
        }
        foreach (RpcResult rpc_result in q_result) {
          try {
            if((success = (bool) rpc_result.Result) == true)
              break;
            }
          catch(Exception) {
            success = false;
          }
        }
        if (success) {
          min_majority--;
        }
      }
      if (min_majority > 0) {
      //we have not been able to acquire a majority, delete all keys
        try {
          queues = _dht.DeleteF(dht_key_bytes, new_password);
          BlockingQueue.ParallelFetch(queues, 0);
        }
        catch (Exception) {
          System.Console.WriteLine("How'd we get HERE?!?");
        }
        System.Console.WriteLine("Unsuccessful " + dht_key);
        return false;
      }
      System.Console.WriteLine("Successful " + dht_key);
      return true;
    }
  }
}