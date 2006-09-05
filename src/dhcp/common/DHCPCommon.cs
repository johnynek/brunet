using System;
using System.Collections;
using System.Runtime.Remoting.Lifetime;

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
  }

  public class DHCPServer : MarshalByRefObject {
    SortedList leases;
    DHCPServerConfig config;
    byte [] ServerIP;

    public DHCPServer() {;} // Dummy for Client

    public DHCPServer(byte [] ServerIP, string filename) { 
      this.ServerIP = ServerIP;
      this.config = DHCPServerConfigurationReader.ReadConfig(filename);
      leases = new SortedList();
      foreach(IPOPNamespace item in this.config.ipop_namespace) {
        DHCPLease lease = new DHCPLease(item);
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


    public DecodedDHCPPacket SendMessage(DecodedDHCPPacket packet) {
      DecodedDHCPPacket returnPacket = packet;

      if(packet.brunet_namespace != config.brunet_namespace) {
        returnPacket.return_message = "Invalid Brunet Namespace";
        return returnPacket;
      }

      if(packet.ipop_namespace == null || !leases.Contains(
        packet.ipop_namespace)) {
        returnPacket.return_message = "Invalid IPOP Namespace";
        return returnPacket;
      }

      byte messageType = 0;
      messageType = ((DHCPOption) packet.options[53]).byte_value[0];

      DHCPLeaseResponse leaseReturn = ((DHCPLease) leases[packet.ipop_namespace]).
        GetLease(DHCPCommon.StringToBytes(packet.NodeAddress, ':'));
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