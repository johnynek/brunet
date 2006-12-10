#define DHCP_DEBUG
using System;
using System.IO;
using System.Text;
using System.Collections;

using System.Security.Cryptography;

using Brunet; 
using Brunet.Dht;
namespace Ipop {
  abstract public class DHCPLeaseParam {
  }
  public class SoapDHCPLeaseParam: DHCPLeaseParam {
    byte[] _hwaddr;
    public byte[] HwAddr {
      get {
	return _hwaddr;
      }
    }
    public SoapDHCPLeaseParam(byte[] hwaddr) {
      _hwaddr = hwaddr;
    }
  }
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
  
  public struct Lease {
    public byte [] ip;
    public byte [] hwaddr;
    public DateTime expiration;
  }

  public class DHCPLeaseResponse {
    public byte [] ip;
    public byte [] netmask;
    public byte [] leasetime;
    public string password;
  }
  
  abstract public class DHCPLease {
    protected int size, index, leasetime;
    protected long logsize;
    protected string namespace_value;
    protected byte [] netmask;
    protected byte [] lower;
    protected byte [] upper;
    protected byte [] leasetimeb;
    protected byte [][] reservedIP;
    protected byte [][] reservedMask;


    public DHCPLease(IPOPNamespace config) {
      leasetime = 1000; //config.leasetime;
      leasetimeb = new byte[]{((byte) ((leasetime >> 24))),
        ((byte) ((leasetime >> 16))),
        ((byte) ((leasetime >> 8))),
        ((byte) (leasetime))};
      namespace_value = config.value;
      logsize = config.LogSize * 1024; /* Bytes */
      lower = DHCPCommon.StringToBytes(config.pool.lower, '.');
      upper = DHCPCommon.StringToBytes(config.pool.upper, '.');
      netmask = DHCPCommon.StringToBytes(config.netmask, '.');

      if(config.reserved != null) {
        reservedIP = new byte[config.reserved.value.Length + 1][];
        reservedMask = new byte[config.reserved.value.Length + 1][];
        for(int i = 1; i < config.reserved.value.Length + 1; i++) {
          reservedIP[i] = DHCPCommon.StringToBytes(
            config.reserved.value[i-1].ip, '.');
          reservedMask[i] = DHCPCommon.StringToBytes(
            config.reserved.value[i-1].mask, '.');
        }
      }
      else {
        reservedIP = new byte[1][];
        reservedMask = new byte[1][];
      }
      reservedIP[0] = new byte[4];

      for(int i = 0; i < 3; i++)
        reservedIP[0][i] = (byte) (lower[i] & netmask[i]);
      reservedIP[0][3] = 1;
      reservedMask[0] = new byte[4] {255, 255, 255, 255};
    }
    protected bool ValidIP(byte [] ip) {
      /* No 255 or 0 in ip[3]] */
      if(ip[3] == 255 || ip[3] == 0)
        return false;
      /* Check range */
      for(int i = 0; i < ip.Length; i++)
        if(ip[i] < lower[i] || ip[i] > upper[i])
          return false;
      /* Check Reserved */
      for(int i = 0; i < reservedIP.Length; i++) {
        for(int j = 0; j < reservedIP[i].Length; j++) {
          if((ip[j] & reservedMask[i][j]) != 
            (reservedIP[i][j] & reservedMask[i][j]))
            break;
          if(j == reservedIP[i].Length - 1)
            return false;
        }
      }
      return true;
    }
   protected byte [] IncrementIP(byte [] ip) {
      if(ip[3] == 0) {
        ip[3] = 1;
      }
      else if(ip[3] == 254 || ip[3] == upper[3]) {
        ip[3] = lower[3];
        if(ip[2] < upper[2])
          ip[2]++;
        else {
          ip[2] = lower[2];
          if(ip[1] < upper[1])
            ip[1]++;
          else {
            ip[1] = lower[1];
            if(ip[0] < upper[0])
              ip[0]++;
            else {
              ip[0] = lower[0];
              this.size = this.index;
              this.index = 0;
            }
          }
        }
      }
      else {
        ip[3]++;
      }

      if(!ValidIP(ip))
        ip = IncrementIP(ip);

      return ip;
    }

    abstract public DHCPLeaseResponse GetLease(DHCPLeaseParam param);
  }
  public class SoapDHCPLease: DHCPLease {
    protected ArrayList LeaseIPs;
    protected ArrayList LeaseHWAddrs;
    protected ArrayList LeaseExpirations;
    protected object LeaseLock;

    public SoapDHCPLease(IPOPNamespace config):base(config) {
      this.index = 0;
      this.size = 0;
      LeaseIPs = new ArrayList();
      LeaseHWAddrs = new ArrayList();
      LeaseExpirations = new ArrayList();
      LeaseLock = new object();
      if(!this.ReadLog()) {
        System.Console.WriteLine("Error can't read log files!\nShutting down...");
      }     
    }
    public override DHCPLeaseResponse GetLease(DHCPLeaseParam param) {
      SoapDHCPLeaseParam soap_param = param as SoapDHCPLeaseParam;
      if (soap_param == null) {
	return null;
      }
      byte[] hwaddr = soap_param.HwAddr;
      bool success = true;
      byte []ip = new byte[4] {0,0,0,0};
      lock(LeaseLock)
      {
        int index = CheckForPreviousLease(hwaddr);
        if(index == -1)
          index = GetNextAvailableIP(hwaddr);
        if(index >= 0) {
          ip = (byte []) LeaseIPs[index];
          LeaseHWAddrs[index] = hwaddr;
          LeaseExpirations[index] = DateTime.Now.AddSeconds(leasetime);
          success = UpdateLog(index);
        }
      }
      DHCPLeaseResponse leaseReturn;
      if(success)
      {
        leaseReturn = new DHCPLeaseResponse();
        leaseReturn.ip = ip;
        leaseReturn.netmask = netmask;
        leaseReturn.leasetime = leasetimeb;
      }
      else
      {
        /* This effectively nullifies any dhcp requests that occur when */
        /* there are some faults occuring */
        index--;
        LeaseExpirations[index] = 0;
        leaseReturn = null;
      }
      return leaseReturn;
    }

    public int CheckForPreviousLease(byte [] hwaddr) {
      for (int i = 0; i < LeaseHWAddrs.Count; i++) {
        for (int j = 0; j < hwaddr.Length; j++) {
          if (hwaddr[j] != ((byte []) LeaseHWAddrs[i])[j])
            break;
          else if(j == hwaddr.Length - 1)
            return i;
        }
      }
      return -1;
    }

/*  We no longer acknowledge requests for specific IPs
    public int CheckRequestedIP(byte [] ip) {
      if(!ValidIP(ip))
        return -1;
      int start = 0, end = leaselist.Count;
      int index = leaselist.Count / 2, ip_check;
      int ip_key = keygen(ip), count = 0, term = (int)
        Math.Ceiling(Math.Log((double) end));

      if(leaselist.Count == 0)
        return -1;

      while(count != term) {
        ip_check = keygen(((Lease)leaselist.GetByIndex(index)).ip);
        if(ip_key == ip_check)
          return index;
        else if(ip_key > ip_check) {
          start = index;
          index = (index + end) / 2;
        }
        else {
          end = index;
          index = (start + index) / 2;
        }
        count++;
      }
      return -1;
    }*/

    public int GetNextAvailableIP(byte [] hwaddr) {
      int temp = this.index, count = LeaseIPs.Count;
      DateTime now = DateTime.Now;
      byte [] ip = null;

      if(this.size == 0) {
        if(count == 0) {
          ip = lower;
          if(!ValidIP(ip))
            ip = IncrementIP(lower);
        }
        else {
          ip = IncrementIP((byte []) ((byte []) 
            LeaseIPs[this.index-1]).Clone());
        }
        LeaseIPs.Add(ip);
        LeaseHWAddrs.Add(hwaddr.Clone());
        LeaseExpirations.Add(now.AddDays(leasetime));
        return this.index++;
      }
      else {
        /* Find the first expired lease and return it */
        do {
          if(this.index >= this.size)
            this.index = 0;
          if(((DateTime) LeaseExpirations[index]) < now)
            return this.index;
          this.index++;
        } while(this.index != temp);
      }
      return -1;
    }



    public int keygen(byte [] input) {
      int key = 0;
      for(int i = 0; i < input.Length; i++)
        key += input[i] << 8*i;
      return key;
    }

     public bool UpdateLog(int index) {
      bool success = true;
      try {
        FileStream file = new FileStream("logs/" + namespace_value + ".log",
            FileMode.Append, FileAccess.Write);
        StreamWriter sw = new StreamWriter(file);
        sw.WriteLine(index);
        sw.WriteLine(DHCPCommon.BytesToString((byte[]) LeaseIPs[index], '.'));
        sw.WriteLine(DHCPCommon.BytesToString((byte[]) LeaseHWAddrs[index], ':'));
        sw.WriteLine(((DateTime) LeaseExpirations[index]).Ticks);
        sw.Close();
        file.Close();

        file = new FileStream("logs/" + namespace_value + ".log",
            FileMode.OpenOrCreate, FileAccess.Read);
        long length = file.Length;
        file.Close();
        if(length > logsize) {
            success = StoreOldLog();
            if(success)
                success = NewLog();
        }
      }
      catch (Exception)
      {
        success = false;
      }
      return success;
    }

    public bool StoreOldLog() {
      bool success = true;
      try {
        FileStream fileold = new FileStream("logs/" + namespace_value + ".log",
            FileMode.OpenOrCreate, FileAccess.Read);
        FileStream filenew = new FileStream("logs/" + namespace_value + ".log.bak",
            FileMode.OpenOrCreate, FileAccess.Write);
        StreamReader sr = new StreamReader(fileold);
        StreamWriter sw = new StreamWriter(filenew);
        sw.Write(sr.ReadToEnd());
        sr.Close();
        sw.Close();
        fileold.Close();
        filenew.Close();
      }
      catch (Exception)
      {
        success = false;
      }
      return success;
    }

    public bool NewLog() {
      bool success = true;
      try {
        FileStream file = new FileStream("logs/" + namespace_value + ".log",
            FileMode.Create, FileAccess.Write);
        StreamWriter sw = new StreamWriter(file);
        for(int i = 0; i < LeaseIPs.Count; i++) {
            sw.WriteLine(i);
            sw.WriteLine(DHCPCommon.BytesToString((byte[]) LeaseIPs[i], '.'));
            sw.WriteLine(DHCPCommon.BytesToString((byte[]) LeaseHWAddrs[i], ':'));
            sw.WriteLine(((DateTime) LeaseExpirations[i]).Ticks);
        }
        sw.Close();
        file.Close();
      }
      catch (Exception)
      {
        success = false;
      }
      return success;
    }

    public bool ReadLog() {
      bool success = true;
      try {
        FileStream file = new FileStream("logs/" + namespace_value + ".log",
            FileMode.OpenOrCreate, FileAccess.Read);
        StreamReader sr = new StreamReader(file);
        string value = "";
        int index = 0;
        while((value = sr.ReadLine()) != null) {
          index = Int32.Parse(value);
          string ip_str = sr.ReadLine();
          string hw_str = sr.ReadLine();
          if(LeaseIPs.Count <= index) {
            LeaseIPs.Add(DHCPCommon.StringToBytes(ip_str, '.'));
            LeaseHWAddrs.Add(DHCPCommon.StringToBytes(hw_str, ':'));
            LeaseExpirations.Add(new DateTime(long.Parse(sr.ReadLine())));
            this.index++;
          }
          else {
            LeaseIPs[index] = DHCPCommon.StringToBytes(ip_str, '.');
            LeaseHWAddrs[index] = DHCPCommon.StringToBytes(hw_str, ':');
            LeaseExpirations[index] = new DateTime(long.Parse(sr.ReadLine()));
          }
        }
        sr.Close();
        file.Close();
      }
      catch (Exception)
      {
        success = false;
      }
      return success;
    }

    public void WriteCache() {
      for(int i = 0; i < LeaseIPs.Count; i++) {
        Console.WriteLine(i);
        Console.WriteLine(DHCPCommon.BytesToString((byte[]) LeaseIPs[i], '.'));
        Console.WriteLine(DHCPCommon.BytesToString((byte[]) LeaseHWAddrs[i], ':'));
        Console.WriteLine(((DateTime) LeaseExpirations[i]).Ticks);
        Console.WriteLine("\n");
      }
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
      int max_attempts = 10;
      HashAlgorithm algo = new SHA1CryptoServiceProvider();
      byte[] bin_password = new byte[10];
      _rand = new Random();
      _rand.NextBytes(bin_password);

      new_password = "SHA1:" + Convert.ToBase64String(bin_password);
      byte[] sha1_pass = algo.ComputeHash(bin_password);
      string new_hashed_password = "SHA1:" + Convert.ToBase64String(sha1_pass);

      if (old_password == null) {
	old_password = new_password;
      }
      
      byte[] guessed_ip = preferred_ip;

      while (true) {
	try {
	  string guessed_ip_str = "";
	  for (int k = 0; k < guessed_ip.Length-1; k++) {
	    guessed_ip_str += (guessed_ip[k] + ".");
	  }
	  guessed_ip_str += guessed_ip[guessed_ip.Length - 1];
#if DHCP_DEBUG
	  Console.WriteLine("attempting to acquire: {0}", guessed_ip_str);
#endif
	  string str_key = "dhcp:ipop_namespace:" + namespace_value + ":ip:" + guessed_ip_str;
	  byte[] dht_key = Encoding.UTF8.GetBytes(str_key);

#if DHCP_DEBUG
	  Console.WriteLine("Invoking recreate() on: {0}", str_key);
#endif
	  
	  BlockingQueue [] queues = _dht.RecreateF(dht_key, old_password, leasetime, new_hashed_password, brunet_id);

	  int max_results_per_queue = 2;
	  int min_majority = 3;
	  
	  ArrayList []results = BlockingQueue.ParallelFetchWithTimeout(queues, 3000);
#if DHCP_DEBUG
	  Console.WriteLine("Parellel fetch returning {0} results.", results.Length);
#endif
	  //this method will return as soon as we have results available
	  for (int i = 0; i < results.Length; i++) {
#if DHCP_DEBUG
	    Console.WriteLine("analysing queue:{0}", i);
#endif
	    bool success = true;
	    ArrayList q_result = results[i];
	    if (q_result.Count < max_results_per_queue) {
#if DHCP_DEBUG
	      Console.WriteLine("queue:{0} has fewer results: {1} ({2} expercted).", i, q_result.Count, max_results_per_queue);
#endif
	      continue;
	    }
	    foreach (RpcResult rpc_result in q_result) {
	      try {
		bool result = (bool) rpc_result.Result;
#if DHCP_DEBUG
		Console.WriteLine("queue: {0}, result for acquire: {1}", i, result);
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
	      Console.WriteLine("queue:{0} had the desired results", i);
#endif
	      min_majority--;
	    }
	  }
	  if (min_majority > 0) {
	    //we have not been able to acquire a majority, delete all keys
	    queues = _dht.DeleteF(dht_key, new_password);
	    BlockingQueue.ParallelFetch(queues, 1);//1 reply is sufficient
	  } else {
#if DHCP_DEBUG
	    Console.WriteLine("successfully acquired IP address: {0}", guessed_ip_str);
#endif
	    return guessed_ip;
	  }
	  if (max_attempts > 0) {
	    //guess a new IP address
	    guessed_ip = GuessIPAddress();
#if DHCP_DEBUG
	    Console.WriteLine("wating for 5 seconds, before trying another guess");
#endif
	    System.Threading.Thread.Sleep(5000);
	    continue;
	  } 
	  break;
	} catch(DhtException ex) {
#if DHCP_DEBUG
	  System.Console.WriteLine(ex);
#endif
	  //sleep 10 seconds and retry
	  System.Threading.Thread.Sleep(10000);
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
