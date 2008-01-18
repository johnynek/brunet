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
    public byte [] last_ip;
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

      DHCPLeaseResponse leaseReturn = null;
      try {
        leaseReturn = dhcp_lease.GetLease(packet);
      }
      catch(Exception e) {
        returnPacket.return_message = e.Message;
        return returnPacket;
      }

      returnPacket.yiaddr = leaseReturn.ip;
      returnPacket.op = 2; /* BOOT REPLY */
      returnPacket.siaddr = this.ServerIP;
      returnPacket.options = new SortedList();

      returnPacket.options.Add(DHCPOptions.SUBNET_MASK, (DHCPOption) 
          CreateOption(DHCPOptions.SUBNET_MASK, leaseReturn.netmask));
      returnPacket.options.Add(DHCPOptions.LEASE_TIME, (DHCPOption) 
          CreateOption(DHCPOptions.LEASE_TIME, leaseReturn.leasetime));
      returnPacket.options.Add(DHCPOptions.MTU, (DHCPOption) 
          CreateOption(DHCPOptions.MTU, new byte[]{4, 176}));
      returnPacket.options.Add(DHCPOptions.SERVER_ID, (DHCPOption) 
          CreateOption(DHCPOptions.SERVER_ID, this.ServerIP));

      /* Host and Domain name */
      string string_value = "C";
      for(int i = 1; i < 4; i++) {
        if(returnPacket.yiaddr[i] < 10)
          string_value += "00";
        else if(returnPacket.yiaddr[i] < 100)
          string_value += "0";
        string_value += returnPacket.yiaddr[i].ToString();
      }
      returnPacket.options.Add(DHCPOptions.HOST_NAME, (DHCPOption) 
          CreateOption(DHCPOptions.HOST_NAME, string_value));
      /* End Host and Domain Name */

      byte messageType = ((DHCPOption) packet.options[DHCPOptions.MESSAGE_TYPE]).byte_value[0];
      if(messageType == DHCPMessage.DISCOVER) {
        messageType = DHCPMessage.OFFER;
      }
      else if(messageType == DHCPMessage.REQUEST) {
        messageType = DHCPMessage.ACK;
      }
      else {
        messageType = DHCPMessage.NACK;
      }
      returnPacket.options.Add(DHCPOptions.MESSAGE_TYPE, (DHCPOption)
          CreateOption(DHCPOptions.MESSAGE_TYPE, new byte[]{messageType}));
      /* End Response Type */
      returnPacket.return_message = "Success";
      return returnPacket;
    }

    public DHCPOption CreateOption(int type, byte [] value) {
      DHCPOption option = new DHCPOption();
      option.type = type;
      option.byte_value = value;
      option.length = value.Length;
      option.encoding = "int";
      return option;
    }

    public DHCPOption CreateOption(int type, string value) {
      DHCPOption option = new DHCPOption();
      option.type = type;
      option.string_value = value;
      option.length = value.Length;
      option.encoding = "string";
      return option;
    }

    protected abstract bool IsValidBrunetNamespace(string brunet_namespace);
    protected abstract DHCPLease GetDHCPLease(string ipop_namespace);
  }
}
