using System;
using System.Collections;

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
  }

  public class DHCPServer : MarshalByRefObject {
    DHCPLease leases;
    int [] CurrentIP;
    byte [] ServerIP;

    public DHCPServer() { 
      leases = new DHCPLease(1000);
      CurrentIP = new int[4];
      CurrentIP[0] = 10;
      CurrentIP[1] = 128;
      CurrentIP[2] = 0;
      CurrentIP[3] = 2;
      this.ServerIP = new byte[4] {0, 0, 0, 0};
    }

    public DHCPServer(byte [] ServerIP) { 
      leases = new DHCPLease(1000);
      CurrentIP = new int[4];
      CurrentIP[0] = 10;
      CurrentIP[1] = 128;
      CurrentIP[2] = 0;
      CurrentIP[3] = 2;
      this.ServerIP = ServerIP;
    }

    public DecodedDHCPPacket SendMessage(DecodedDHCPPacket packet) {
      DecodedDHCPPacket returnPacket = packet;
      byte messageType = 0;
      byte [] requestedIP = new byte[] {0, 0, 0, 0};
      for(int i = 0; i < packet.options.Length; i++) {
        if(packet.options[i].type == 53)
          messageType = packet.options[i].byte_value[0];
        else if(packet.options[i].type == 50)
          requestedIP = packet.options[i].byte_value;
      }
      returnPacket.op = 2; /* BOOT REPLY */
      returnPacket.yiaddr = leases.GetLease(packet.chaddr, requestedIP);
      returnPacket.siaddr = this.ServerIP;
      ArrayList options = new ArrayList();
      string string_value = "";
      byte [] byte_value = null;

      /* Subnet Mask */
      options.Add((DHCPOption) CreateOption(1, new byte[]{255, 128, 0, 0}));
      /* DNS */
      options.Add((DHCPOption) CreateOption(6, new byte[]{192, 168, 121, 2}));
      /* Lease Time */
      options.Add((DHCPOption) CreateOption(51, new byte[]{0, 0, 255, 255}));
      /* MTU Size */
      options.Add((DHCPOption) CreateOption(26, new byte[]{5, 46}));
      /* Server Identifier */
      options.Add((DHCPOption) CreateOption(54, this.ServerIP));

      /* Host name */
      string_value = "C";
      for(int i = 1; i < 4; i++) {
        if(returnPacket.yiaddr[i] < 10)
          string_value += "00";
        else if(returnPacket.yiaddr[i] < 100)
          string_value += "0";
        string_value += returnPacket.yiaddr[i].ToString();
      }
      options.Add((DHCPOption) CreateOption(12, string_value));
      /* End Host Name */

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
}