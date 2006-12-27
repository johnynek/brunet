#define DHCP_DEBUG


using System;
using System.Text;
using System.IO;

using System.Globalization;
using System.Collections;
using System.Runtime.Remoting.Lifetime;

using System.Xml;
using System.Xml.Serialization;

using Brunet;
using Brunet.Dht;
/* For a complete description of the contents of a DHCP packet see
     http://rfc.net/rfc2131.html for the DHCP main portion and ...
     http://rfc.net/rfc2132.html for the options                     */

namespace Ipop {
  [Serializable]
  public struct DHCPOption {
    public int type;
    public int length;
    public string encoding;
    public string string_value;
    public byte [] byte_value;
  }

  [Serializable]
  public struct DecodedDHCPPacket {
    public byte op;
    public byte [] xid;
    public byte [] ciaddr;
    public byte [] yiaddr;
    public byte [] siaddr;
    public byte [] chaddr;
    public SortedList options;
    public string brunet_namespace;
    public string ipop_namespace;
    public string return_message;
    public string NodeAddress;
    public string StoredPassword;
  }
  abstract public class DHCPServer : MarshalByRefObject {
    protected byte[] ServerIP;
    protected SortedList leases;

    public DecodedDHCPPacket SendMessage(DecodedDHCPPacket packet) {
      DecodedDHCPPacket returnPacket = packet;
      if (!IsValidBrunetNamespace(packet.brunet_namespace)) {
        returnPacket.return_message = "Invalid Brunet Namespace";
        return returnPacket;	
      }

      if (packet.ipop_namespace == null) {
        returnPacket.return_message = "Invalid IPOP Namespace";
        return returnPacket;
      }
      
      DHCPLease dhcp_lease = GetDHCPLease(packet.ipop_namespace);
      if (dhcp_lease == null) {
        returnPacket.return_message = "Invalid IPOP Namespace";
        return returnPacket;
      }

      byte messageType = 0;
      messageType = ((DHCPOption) packet.options[53]).byte_value[0];

      DHCPLeaseResponse leaseReturn = GetLease(dhcp_lease, packet);
      
      if(leaseReturn == null) {
        returnPacket.return_message = "There are some faults occurring when " +
          "attempting to request a lease, please try again later.";
        return returnPacket;
      }
      //we will have the password set to a new value
      returnPacket.StoredPassword = leaseReturn.password;

      returnPacket.yiaddr = leaseReturn.ip;
      if(returnPacket.yiaddr[0] == 0) {
        returnPacket.return_message = "No more available leases";
        return returnPacket;
      }

      returnPacket.op = 2; /* BOOT REPLY */
      returnPacket.siaddr = this.ServerIP;
      returnPacket.options = new SortedList();
      string string_value = "";
      byte [] byte_value = null;


      /* Subnet Mask */
      returnPacket.options.Add(1, (DHCPOption) CreateOption(1, leaseReturn.netmask));
      /* Lease Time */
      returnPacket.options.Add(51, (DHCPOption) CreateOption(51, leaseReturn.leasetime));
      /* MTU Size */
      returnPacket.options.Add(26, (DHCPOption) CreateOption(26, new byte[]{5, 46}));
      /* Server Identifier */
      returnPacket.options.Add(54, (DHCPOption) CreateOption(54, this.ServerIP));

      /* Host and Domain name */
      string_value = "C";
      for(int i = 1; i < 4; i++) {
        if(returnPacket.yiaddr[i] < 10)
          string_value += "00";
        else if(returnPacket.yiaddr[i] < 100)
          string_value += "0";
        string_value += returnPacket.yiaddr[i].ToString();
      }
      returnPacket.options.Add(12, (DHCPOption) CreateOption(12, string_value));
      /* End Host and Domain Name */

      /* DHCP Response Type */
      if(messageType == 1)
        byte_value = new byte[1] {2};
      else if(messageType == 3)
        byte_value = new byte[1] {5};
      else
        byte_value = new byte[1] {6};
      returnPacket.options.Add(53, (DHCPOption) CreateOption(53, byte_value));
      /* End Response Type */

      returnPacket.return_message = "Success";
      return returnPacket;
    }

    public DHCPOption CreateOption(byte type, byte [] value) {
      DHCPOption option = new DHCPOption();
      option.type = type;
      option.byte_value = value;
      option.length = value.Length;
      option.encoding = "int";
      return option;
    }

    public DHCPOption CreateOption(byte type, string value) {
      DHCPOption option = new DHCPOption();
      option.type = type;
      option.string_value = value;
      option.length = value.Length;
      option.encoding = "string";
      return option;
    }    
    abstract protected bool IsValidBrunetNamespace(string brunet_namespace);
    abstract protected DHCPLease GetDHCPLease(string ipop_namespace);
    abstract protected DHCPLeaseResponse GetLease(DHCPLease dhcp_lease, DecodedDHCPPacket packet);
  }
  public class SoapDHCPServer : DHCPServer {
    string brunet_namespace;
  
    DHCPServerConfig config;

    public SoapDHCPServer() {;} // Dummy for Client

    public SoapDHCPServer(byte [] server_ip, string filename) { 
      this.ServerIP = server_ip;
      this.config = DHCPServerConfigurationReader.ReadConfig(filename);

      this.brunet_namespace = config.brunet_namespace;

      leases = new SortedList();
      foreach(IPOPNamespace item in this.config.ipop_namespace) {
        DHCPLease lease = new SoapDHCPLease(item);
        leases.Add(item.value, lease);
      }
    }

    public override Object InitializeLifetimeService()
    {
      ILease lease = (ILease)base.InitializeLifetimeService();
      if (lease.CurrentState == LeaseState.Initial)
        lease.InitialLeaseTime = TimeSpan.FromDays(365);
      return lease;
    }
    protected override DHCPLease GetDHCPLease(string ipop_namespace) {
      if (!leases.ContainsKey(ipop_namespace)) {
	return null;
      }
      return (DHCPLease) leases[ipop_namespace];
    }
    protected override bool IsValidBrunetNamespace(string brunet_namespace) {
      return (this.brunet_namespace.Equals(brunet_namespace));
    }
    protected override DHCPLeaseResponse GetLease(DHCPLease dhcp_lease, DecodedDHCPPacket packet) {
      return dhcp_lease.GetLease(new SoapDHCPLeaseParam(DHCPCommon.StringToBytes(packet.NodeAddress, ':')));
    }
  }

  public class DhtDHCPServer: DHCPServer {
    protected FDht _dht; 
    public DhtDHCPServer(byte []server_ip, FDht dht) {
      _dht = dht;
      this.ServerIP = server_ip;
      this.leases = new SortedList();
      //do not have to even be concerned about brunet namespace so far
      
    }
    protected override bool IsValidBrunetNamespace(string brunet_namespace) {
      return true;
    }
    protected override DHCPLease GetDHCPLease(string ipop_namespace) {
      if (leases.ContainsKey(ipop_namespace)) {
	return (DHCPLease) leases[ipop_namespace];
      }
      string ns_key = "dhcp:ipop_namespace:" + ipop_namespace;
#if DHCP_DEBUG
      Console.WriteLine("searchig for key: {0}", ns_key);   
#endif
      byte[] utf8_key = Encoding.UTF8.GetBytes(ns_key);
      //get a maximum of 1000 bytes only
      BlockingQueue[] q = _dht.GetF(utf8_key, 1000, null);
      //we do expect to get atleast 1 result
      ArrayList result = null;
      try{
        while (true) {
          RpcResult res = q[0].Dequeue() as RpcResult;
          result = res.Result as ArrayList;
          if (result == null || result.Count < 3) {
            continue;
          }
          break;
        }
      } catch (Exception) {
        return null;
      }
      ArrayList values = (ArrayList) result[0];
#if DHCP_DEBUG
      Console.WriteLine("# of matching entries: " + values.Count);
#endif
      string xml_str = null;
      foreach (Hashtable ht in values) {
#if DHCP_DEBUG
        Console.WriteLine(ht["age"]);
#endif
        byte[] data = (byte[]) ht["data"];
        xml_str = Encoding.UTF8.GetString(data);
#if DHCP_DEBUG
        Console.WriteLine(xml_str);
#endif
        break;
      }
      if (xml_str == null) {
        return null;
      }
      XmlSerializer serializer = new XmlSerializer(typeof(IPOPNamespace));
      TextReader stringReader = new StringReader(xml_str);
      IPOPNamespace ipop_ns = (IPOPNamespace) serializer.Deserialize(stringReader);
      DHCPLease dhcp_lease = new DhtDHCPLease(_dht, ipop_ns);
      leases[ipop_namespace] = dhcp_lease;
      return dhcp_lease;
    }    
    protected override DHCPLeaseResponse GetLease(DHCPLease dhcp_lease, DecodedDHCPPacket packet) {
      DhtDHCPLeaseParam dht_param = new DhtDHCPLeaseParam(packet.yiaddr, packet.StoredPassword, DHCPCommon.StringToBytes(packet.NodeAddress, ':'));
      DHCPLeaseResponse ret = dhcp_lease.GetLease(dht_param);
      return ret;
    }
  }

  public class DHCPCommon {
    public static byte [] StringToBytes(string input, char sep) {
      char [] separator = {sep};
      string[] ss = input.Split(separator);
      byte [] ret = new byte[ss.Length];
      for (int i = 0; i < ss.Length; i++) {
	ret[i] = Byte.Parse(ss[i].Trim());
      }
      return ret;
    }

    public static byte [] HexStringToBytes(string input, char sep) {
      char [] separator = {sep};
      string[] ss = input.Split(separator);
      byte [] ret = new byte[ss.Length];
      for (int i = 0; i < ss.Length; i++) {
	ret[i] = Byte.Parse(ss[i].Trim(), NumberStyles.HexNumber);
      }
      return ret;
    }

    public static string BytesToString(byte [] input, char sep) {
      string return_msg = "";
      for(int i = 0; i < input.Length - 1; i++)
        return_msg += input[i].ToString() + sep.ToString();
      return_msg += input[input.Length - 1];
      return return_msg;
    }
  }

  public class LifeTimeSponsor : ISponsor {
    public TimeSpan Renewal (ILease lease)
    {
      TimeSpan ts = new TimeSpan();
      ts = TimeSpan.FromDays(365);
      return ts;
    }
  }
}
