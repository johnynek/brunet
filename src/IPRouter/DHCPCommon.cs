using System;
using System.Text;
using System.IO;

using System.Globalization;
using System.Collections;
using System.Runtime.Remoting.Lifetime;

using System.Xml;
using System.Xml.Serialization;

/* For a complete description of the contents of a DHCP packet see
     http://rfc.net/rfc2131.html for the DHCP main portion and ...
     http://rfc.net/rfc2132.html for the options                     */

namespace Ipop {
  public struct DHCPOption {
    public int type;
    public int length;
    public string encoding;
    public string string_value;
    public byte [] byte_value;
  }

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

  public abstract class DHCPServer {
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

      byte messageType = ((DHCPOption) packet.options[53]).byte_value[0];

      DHCPLeaseResponse leaseReturn = null;
      try {
        leaseReturn = GetLease(dhcp_lease, packet, messageType);
      }
      catch(Exception e) {
        returnPacket.return_message = e.Message;
        return returnPacket;
      }

      returnPacket.yiaddr = leaseReturn.ip;
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
      returnPacket.options.Add(26, (DHCPOption) CreateOption(26, new byte[]{4, 176}));
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

    protected abstract bool IsValidBrunetNamespace(string brunet_namespace);
    protected abstract DHCPLease GetDHCPLease(string ipop_namespace);
    protected abstract DHCPLeaseResponse GetLease(DHCPLease dhcp_lease, 
                                  DecodedDHCPPacket packet, byte messageType);
  }
}
