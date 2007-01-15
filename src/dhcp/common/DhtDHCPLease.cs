using System;
using System.IO;
using System.Text;
using System.Collections;

using System.Security.Cryptography;

using Brunet; 
using Brunet.Dht;

namespace Ipop {
  public class DhtDHCPLeaseParam: DHCPLeaseParam {
    byte[] _preferred_ip;
    public byte[] PreferredIP {
      get {
        return _preferred_ip;
      }
    }
    string _stored_password;
    public string StoredPassword {
      get {
        return _stored_password;
      }
      set {
        _stored_password = value;
      }
    }
    byte[] _brunet_id;
    public byte[] BrunetId {
      get {
        return _brunet_id;
      }
    }

    public DhtDHCPLeaseParam(byte[] preferred_ip, string stored_password, byte [] brunet_id) {
      _preferred_ip = preferred_ip;
      _stored_password = stored_password;
      _brunet_id = brunet_id;
    }
  }

  public class DhtDHCPLease: DHCPLease {
    protected FDht _dht;
    protected Random _rand;
    protected DateTime _last_assigned_instant;
    protected DHCPLeaseResponse _last_assigned_lease;

    public DhtDHCPLease(FDht dht, IPOPNamespace config):base(config) {
      _dht = dht;
      _rand = new Random();
      _last_assigned_lease = null;
    }

    public override DHCPLeaseResponse GetLease(DHCPLeaseParam param) {
      DhtDHCPLeaseParam dht_param = param as DhtDHCPLeaseParam;
      if (dht_param == null) {
        return null;
      }
      TimeSpan t_span = DateTime.Now - _last_assigned_instant;
      if (_last_assigned_lease != null && t_span.TotalSeconds < 0.5*leasetime) {
        return _last_assigned_lease;
      }

      byte[] preferred_ip = dht_param.PreferredIP;
      if (preferred_ip[0] == 0 && preferred_ip[1] == 0 && 
          preferred_ip[2] == 0 && preferred_ip[3] == 0) {
        //we should make a guess
        preferred_ip = GuessIPAddress();
      }
      DHCPLeaseResponse leaseReturn = new DHCPLeaseResponse();
      string new_password; 
      byte[] new_ip = ReAllocateIPAddress(preferred_ip, dht_param.BrunetId, dht_param.StoredPassword, out new_password);
      leaseReturn.ip = new_ip;
      leaseReturn.netmask = netmask;
      leaseReturn.password = new_password;
      leaseReturn.leasetime = leasetimeb;

      _last_assigned_lease = leaseReturn;
      _last_assigned_instant = DateTime.Now; 
      return leaseReturn;
    }

    private byte[] ReAllocateIPAddress (byte[] preferred_ip, byte[] brunet_id, string old_password, out string new_password) {
      int max_attempts = 10, max_renew_attempts = 3;
      bool renew_attempt = false;
      HashAlgorithm algo = new SHA1CryptoServiceProvider();
      byte[] bin_password = new byte[10];
      _rand = new Random();
      _rand.NextBytes(bin_password);

      new_password = "SHA1:" + Convert.ToBase64String(bin_password);
      byte[] sha1_pass = algo.ComputeHash(bin_password);
      string new_hashed_password = "SHA1:" + Convert.ToBase64String(sha1_pass);

      if (old_password == null)
        old_password = new_password;
      else
        renew_attempt = true;

      byte[] guessed_ip = preferred_ip;

      while (true) {
        try {
          string guessed_ip_str = "";
          for (int k = 0; k < guessed_ip.Length-1; k++) {
            guessed_ip_str += (guessed_ip[k] + ".");
          }
          guessed_ip_str += guessed_ip[guessed_ip.Length - 1];
          string str_key = "dhcp:ip:" + guessed_ip_str;
          byte[] dht_key = Encoding.UTF8.GetBytes(str_key);

          do {
            BlockingQueue [] queues = _dht.RecreateF(dht_key, old_password, leasetime, new_hashed_password, brunet_id);

            int max_results_per_queue = 2;
            int min_majority = 3;

            ArrayList []results = BlockingQueue.ParallelFetchWithTimeout(queues, 5000);

            //this method will return as soon as we have results available
            for (int i = 0; i < results.Length; i++) {
              bool success = true;
              ArrayList q_result = results[i];
              if (q_result.Count < max_results_per_queue) {
                continue;
              }
              foreach (RpcResult rpc_result in q_result) {
                try {
                  if((success = (bool) rpc_result.Result) == true)
                    break;
                } catch(Exception) {
                  success = false;
                }
              }
              if (success) {
                min_majority--;
              }
            }
            if (min_majority > 0) {
              //we have not been able to acquire a majority, delete all keys
              queues = _dht.DeleteF(dht_key, new_password);
              BlockingQueue.ParallelFetch(queues, 1);//1 reply is sufficient
            }
            else {
              return guessed_ip;
            }
          } while(max_renew_attempts-- > 0 && renew_attempt);
          if (max_attempts > 0) {
            //guess a new IP address
            guessed_ip = GuessIPAddress();
            continue;
          }
          break;
        }
        catch(Exception) { System.Threading.Thread.Sleep(10000); }
      }
      return null;
    }

    private byte[] GuessIPAddress() {
      byte[] guessed_ip = new byte[4];
      bool smaller = true;
      for (int k = 0; k < guessed_ip.Length; k++) {
        if (lower[k] == upper[k]) {
          guessed_ip[k] = lower[k];
          smaller = false;
        }
        if (lower[k] < upper[k]) {
          guessed_ip[k] = (byte) _rand.Next((int) lower[k], (int) upper[k] + 1);
          continue;
        } 
        if (smaller && lower[k] > upper[k])
        {
          int max_offset = 255  - (int) lower[k] + (int) upper[k] + 2;
          int offset = _rand.Next(0, max_offset);
          guessed_ip[k] = (byte) (((int) lower[k] + offset)%255);
        }
        if (!smaller && lower[k] > upper[k]) {
          Console.Error.WriteLine("Invalid IPOP namespace IP range: lower > upper");
        }
      }
      if (!ValidIP(guessed_ip)) {
        guessed_ip = GuessIPAddress();
      }
      return guessed_ip;
    }
  }
}