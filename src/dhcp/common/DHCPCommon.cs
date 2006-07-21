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
    public DHCPOption [] options;
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
      for(int i = 0; i < packet.options.Length; i++) {
        if(packet.options[i].type == 53)
          messageType = packet.options[i].byte_value[0];
      }

      returnPacket.yiaddr = ((DHCPLease) leases[packet.ipop_namespace]).
        GetLease(DHCPCommon.StringToBytes(packet.NodeAddress, ':'));
      if(returnPacket.yiaddr[0] == 0) {
        returnPacket.return_message = "No more available leases";
        return returnPacket;
      }

      returnPacket.op = 2; /* BOOT REPLY */
      returnPacket.siaddr = this.ServerIP;
      ArrayList options = new ArrayList();
      string string_value = "";
      byte [] byte_value = null;

      /* Subnet Mask */
      options.Add((DHCPOption) CreateOption(1, new byte[]{255, 128, 0, 0}));
      /* DNS */
/*      options.Add((DHCPOption) CreateOption(6, new byte[]{192, 168, 121, 2})); */
      /* Lease Time */
      options.Add((DHCPOption) CreateOption(51, new byte[]{0, 0, 255, 255}));
      /* MTU Size */
      options.Add((DHCPOption) CreateOption(26, new byte[]{5, 46}));
      /* Server Identifier */
      options.Add((DHCPOption) CreateOption(54, this.ServerIP));

      /* Host and Domain name */
      string_value = "C";
      for(int i = 1; i < 4; i++) {
        if(returnPacket.yiaddr[i] < 10)
          string_value += "00";
        else if(returnPacket.yiaddr[i] < 100)
          string_value += "0";
        string_value += returnPacket.yiaddr[i].ToString();
      }
      options.Add((DHCPOption) CreateOption(12, string_value));
      options.Add((DHCPOption) CreateOption(15, string_value));
      /* End Host and Domain Name */

      /* DHCP Response Type */
      if(messageType == 1)
        byte_value = new byte[1] {2};
      else if(messageType == 3)
        byte_value = new byte[1] {5};
      else
        byte_value = new byte[1] {6};
      options.Add((DHCPOption) CreateOption(53, byte_value));
      /* End Response Type */

      returnPacket.options = (DHCPOption []) options.ToArray(typeof(DHCPOption));
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