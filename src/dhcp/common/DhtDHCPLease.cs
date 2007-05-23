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
      if (_last_assigned_lease != null && t_span.TotalSeconds < 0.10*leasetime) {
        return _last_assigned_lease;
      }
      byte[] preferred_ip = dht_param.PreferredIP;
      if (preferred_ip[0] == 0 && preferred_ip[1] == 0 && 
          preferred_ip[2] == 0 && preferred_ip[3] == 0) {
        //we should make a guess
        preferred_ip = GuessIPAddress();
      }
      string password = dht_param.StoredPassword;
      byte[] new_ip = ReAllocateIPAddress(preferred_ip, dht_param.BrunetId, ref password);
      if (new_ip == null) {
        return null;
      }
      DHCPLeaseResponse leaseReturn = new DHCPLeaseResponse();
      leaseReturn.ip = new_ip;
      leaseReturn.netmask = netmask;
      leaseReturn.password = password;
      leaseReturn.leasetime = leasetimeb;

      _last_assigned_lease = leaseReturn;
      _last_assigned_instant = DateTime.Now; 
      return leaseReturn;
    }

    private byte[] ReAllocateIPAddress (byte[] preferred_ip, byte[] brunet_id, ref string password) {
      int max_attempts = 1, max_renew_attempts = 2;
      bool renew_attempt = false;
      byte[] guessed_ip = null;

      if (password != null) {
        renew_attempt = true;
        guessed_ip = preferred_ip;
        max_attempts++;
      }
      else
        guessed_ip = GuessIPAddress();

      while (true) {
        do {
          string guessed_ip_str = "";
          for (int k = 0; k < guessed_ip.Length-1; k++) {
            guessed_ip_str += (guessed_ip[k] + ".");
          }
          guessed_ip_str += guessed_ip[guessed_ip.Length - 1];

          if(DhtIP.GetIP(_dht, namespace_value, guessed_ip_str, leasetime, brunet_id, ref password))
            return guessed_ip;
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
