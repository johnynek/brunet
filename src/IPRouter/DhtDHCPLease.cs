using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Security.Cryptography;
using Brunet;
using Brunet.Dht;
using System.Diagnostics;

namespace Ipop {
  public class DhtDHCPLeaseParam: DHCPLeaseParam {
    byte[] _preferred_ip;
    public byte[] PreferredIP {
      get {
        return _preferred_ip;
      }
    }
    string _brunet_id;
    public string BrunetId {
      get {
        return _brunet_id;
      }
    }

    public DhtDHCPLeaseParam(byte[] preferred_ip, string brunet_id) {
      _preferred_ip = preferred_ip;
      _brunet_id = brunet_id;
    }
  }

  public class DhtDHCPLease: DHCPLease {
    protected Dht _dht;
    protected Random _rand;
    protected DateTime _last_assigned_instant;
    protected DHCPLeaseResponse _last_assigned_lease;

    public DhtDHCPLease(Dht dht, IPOPNamespace config):base(config) {
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
      if (_last_assigned_lease != null && t_span.TotalSeconds < 0.10*leasetime) {
        return _last_assigned_lease;
      }
      byte[] preferred_ip = dht_param.PreferredIP;
      byte[] new_ip = ReAllocateIPAddress(preferred_ip, dht_param.BrunetId);
      if (new_ip == null) {
        return null;
      }
      DHCPLeaseResponse leaseReturn = new DHCPLeaseResponse();
      leaseReturn.ip = new_ip;
      leaseReturn.netmask = netmask;
      leaseReturn.leasetime = leasetimeb;

      _last_assigned_lease = leaseReturn;
      _last_assigned_instant = DateTime.Now; 
      return leaseReturn;
    }

    private byte[] ReAllocateIPAddress (byte[] preferred_ip, string brunet_id) {
      int max_attempts = 1, max_renew_attempts = 2;
      byte[] guessed_ip = null;
      bool renew_attempt = false;

      if (preferred_ip[0] == 0 && preferred_ip[1] == 0 && 
          preferred_ip[2] == 0 && preferred_ip[3] == 0) {
        //we should make a guess
        guessed_ip = GuessIPAddress();
      }
      else {
        renew_attempt = true;
        guessed_ip = preferred_ip;
        max_attempts++;
      }

      while (true) {
        do {
          string guessed_ip_str = guessed_ip[0].ToString();
          for (int k = 1; k < guessed_ip.Length; k++) {
            guessed_ip_str += "." + guessed_ip[k].ToString();
          }

          string key = "dhcp:ipop_namespace:" + namespace_value + ":ip:" + guessed_ip_str;
          bool res = false;
          try {
            res = _dht.Create(key, brunet_id, leasetime);
          }
          catch {
            res = false;
          }
          if(res) {
            Console.Error.WriteLine("Got " + key + " successfully");
            _dht.Put(brunet_id, key + "|" + DateTime.Now.Ticks, leasetime);
            return guessed_ip;
          }
        } while(max_renew_attempts-- > 0 && renew_attempt);
        if (--max_attempts > 0) {
          //guess a new IP address
          guessed_ip = GuessIPAddress();
        }
        else {
          break;
        }
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
          Debug.WriteLine("Invalid IPOP namespace IP range: lower > upper");
        }
      }
      if (!ValidIP(guessed_ip)) {
        guessed_ip = GuessIPAddress();
      }
      return guessed_ip;
    }
  }
}
