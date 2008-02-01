using System;
using System.Collections;
using System.IO;
using System.Text;

namespace Ipop {
  public abstract class DHCPServer {
    protected byte[] ServerIP;
    protected SortedList _dhcp_lease_controllers;

    public DecodedDHCPPacket SendMessage(DecodedDHCPPacket packet) {
      if (!IsValidBrunetNamespace(packet.brunet_namespace)) {
        packet.return_message = "Invalid Brunet Namespace";
        return packet;
      }
      else if (packet.ipop_namespace == null) {
        packet.return_message = "Invalid IPOP Namespace";
        return packet;
      }

      DHCPReply reply = null;
      SortedList options = packet.options;
      byte messageType = ((DHCPOption) options[DHCPOptions.MESSAGE_TYPE]).byte_value[0];
      packet.options = new SortedList();
      packet.return_message = "Success";

      try {
        DHCPLeaseController _dhcp_lease_controller = GetDHCPLeaseController(packet.ipop_namespace);
        if (_dhcp_lease_controller == null) {
          packet.return_message = "Invalid IPOP Namespace";
          return packet;
        }

        if(messageType == DHCPMessage.DISCOVER) {
          reply = _dhcp_lease_controller.GetLease(packet.yiaddr, false, packet);
          messageType = DHCPMessage.OFFER;
        }
        else if(messageType == DHCPMessage.REQUEST) {
          if(options.Contains(DHCPOptions.REQUESTED_IP)) {
            byte[] requested_ip = ((DHCPOption) options[DHCPOptions.REQUESTED_IP]).byte_value;
            reply = _dhcp_lease_controller.GetLease(requested_ip, true, packet);
          }
          else if(packet.ciaddr[0] != 0) {
            reply = _dhcp_lease_controller.GetLease(packet.ciaddr, true, packet);
            packet.SendTo = reply.ip;
          }
          else {
            reply = _dhcp_lease_controller.GetLease(packet.yiaddr, true, packet);
            packet.SendTo = reply.ip;
          }
          messageType = DHCPMessage.ACK;
        }
        else {
          packet.return_message = "Unsupported message type!";
          return packet;
        }
      }
      catch(Exception e) {
        packet.return_message = e.Message;
        return packet;
      }

      packet.op = 2; /* BOOT REPLY */
      packet.yiaddr = reply.ip;
      packet.siaddr = this.ServerIP;
      packet.SendFrom = this.ServerIP;
      packet.options = new SortedList();

      packet.options.Add(15, (DHCPOption) CreateOption(15, "ipop"));
//	The following two are needed for dhcp to "succeed" in Vista, but they break Linux
//      packet.options.Add(3, (DHCPOption) CreateOption(3, reply.ip));
      byte[] tmp = new byte[4] {reply.ip[0], reply.ip[1], reply.ip[2], 255};
      packet.options.Add(6, (DHCPOption) CreateOption(6, tmp));
      packet.options.Add(DHCPOptions.SUBNET_MASK, (DHCPOption) 
          CreateOption(DHCPOptions.SUBNET_MASK, reply.netmask));
      packet.options.Add(DHCPOptions.LEASE_TIME, (DHCPOption) 
          CreateOption(DHCPOptions.LEASE_TIME, reply.leasetime));
      packet.options.Add(DHCPOptions.MTU, (DHCPOption) 
          CreateOption(DHCPOptions.MTU, new byte[]{4, 176}));
      packet.options.Add(DHCPOptions.SERVER_ID, (DHCPOption) 
          CreateOption(DHCPOptions.SERVER_ID, this.ServerIP));
      packet.options.Add(DHCPOptions.MESSAGE_TYPE, (DHCPOption)
          CreateOption(DHCPOptions.MESSAGE_TYPE, new byte[]{messageType}));
      return packet;
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
    protected abstract DHCPLeaseController GetDHCPLeaseController(string ipop_namespace);
  }
}
