/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using Brunet;
using NetworkPackets;
using NetworkPackets.DHCP;
using System;
using System.Collections;
using System.IO;
using System.Text;

namespace Ipop {
  /**
  <summary>A Simple DHCP server for IPOP</summary>
  <remarks>There is a single entry point via the Process method.  For the DHCP
  to work, the requestor must ask for an IP Address in an existing
  DHCPLeaseController or the DHCP Server must allocate one somehow, this is 
  configured by the IPOPNamespace class in DHCPServerConfig.  This method
  allows for a single server to be configured for multiple namespaces.
  </remarks>
  */
  public abstract class DHCPServer {
    /// <summary>List of Lease Controllers indexed by their IPOP namspace</summary>
    protected SortedList _dhcp_lease_controllers = new SortedList();

    /**
    <summary>This is the input to the DHCP System, it takes in the dhcp packet
    and extra information in an attempt to return back a valid positive dhcp
    response (such as ACK or OFFER).  There is no support for DHCP Messages
    such as NAK or RELEASE.</summary>
    <param name="packet">The dhcp packet containing the request</param>
    <param name="last_ip">Optional last ip address as stored by the node,
    sometimes the underlying O/S will keep this information, but if you plan
    on moving around the software to different systems, this will allow you to
    easily retain the IP.</param>
    <param name="node_address">The brunet address, this is better to use than a 
    Ethernet address for address registration.</param>
    <param name="IpopNamespace">The namespace that you want to get your lease
    from.</param>
    <param name="para">Extra optional parameters passed to the DHCP Lease
    Controller</param>
    */
    public DHCPPacket Process(DHCPPacket packet, byte[] last_ip, string node_address,
                              string IpopNamespace, params object[] para) {
      byte messageType = ((MemBlock) packet.Options[DHCPPacket.OptionTypes.MESSAGE_TYPE])[0];
      DHCPLeaseController _dhcp_lease_controller = GetDHCPLeaseController(IpopNamespace);
      if (_dhcp_lease_controller == null) {
        throw new Exception("Invalid IPOP Namespace");
      }

      DHCPReply reply = null;
      if(messageType == (byte) DHCPPacket.MessageTypes.DISCOVER) {
        reply = _dhcp_lease_controller.GetLease(last_ip, false, node_address, para);
        messageType = (byte) DHCPPacket.MessageTypes.OFFER;
      }
      else if(messageType == (byte) DHCPPacket.MessageTypes.REQUEST) {
        if(packet.Options.Contains(DHCPPacket.OptionTypes.REQUESTED_IP)) {
          byte[] requested_ip = (MemBlock) packet.Options[DHCPPacket.OptionTypes.REQUESTED_IP];
          reply = _dhcp_lease_controller.GetLease(requested_ip, true, node_address, para);
        } else if(!packet.ciaddr.Equals(IPPacket.ZeroAddress)) {
          reply = _dhcp_lease_controller.GetLease(packet.ciaddr, true, node_address, para);
        } else {
          reply = _dhcp_lease_controller.GetLease(last_ip, true, node_address, para);
        }
        messageType = (byte) DHCPPacket.MessageTypes.ACK;
      } else {
        throw new Exception("Unsupported message type!");
      }

      Hashtable options = new Hashtable();

      options[DHCPPacket.OptionTypes.DOMAIN_NAME] = Encoding.UTF8.GetBytes("ipop");
//  The following option is needed for dhcp to "succeed" in Vista, but they break Linux
//    options[DHCPPacket.OptionTypes.ROUTER] = reply.ip;
      byte[] tmp = new byte[4];
      for(int i = 0; i < reply.ip.Length - 1; i++) {
        tmp[i] = (byte) (reply.ip[i] & reply.netmask[i]);
      }
      tmp[3] = 1;

      options[DHCPPacket.OptionTypes.DOMAIN_NAME_SERVER] = tmp;
      options[DHCPPacket.OptionTypes.SUBNET_MASK] = reply.netmask;
      options[DHCPPacket.OptionTypes.LEASE_TIME] = reply.leasetime;
      int mtu = 1200;
      tmp = new byte[2] { (byte) ((mtu >> 8) & 0xFF), (byte) (mtu & 0xFF) };
      options[DHCPPacket.OptionTypes.MTU] = tmp;
      options[DHCPPacket.OptionTypes.SERVER_ID] = _dhcp_lease_controller.ServerIP;
      options[DHCPPacket.OptionTypes.MESSAGE_TYPE] = new byte[]{messageType};
      DHCPPacket rpacket = new DHCPPacket(2, packet.xid, packet.ciaddr, reply.ip,
                               _dhcp_lease_controller.ServerIP, packet.chaddr, options);
      return rpacket;
    }

    /**  <summary>All DHCP Servers must have some method for loading DHCP Lease
    this is implemented here</summary>*/
    protected abstract DHCPLeaseController GetDHCPLeaseController(string ipop_namespace);
  }
}
