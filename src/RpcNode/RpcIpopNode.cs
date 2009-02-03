/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida
                    Pierre St Juste <ptony82@ufl.edu>, University of Florida

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
using Brunet.Applications;
using Brunet.DistributedServices;
using NetworkPackets;
using System;
using System.Collections;
using System.Net;
using System.Threading;

/**
\namespace Ipop::RpcNode
\brief Defines Ipop.RpcNode provide the ability to set up translation tables via Rpc
*/
namespace Ipop.RpcNode {

  /// <summary>
  /// This class is a subclass of IpopNode
  /// </summary>
  public class RpcIpopNode: IpopNode {
    /// <summary>Provides Address resolution, dns, and translation.</summary>
    protected RpcAddressResolverAndDNS _rarad;

    /// <summary>
    /// The constructor takes two config files
    /// </summary>
    /// <param name="NodeConfigPath">Path to the node config file</param>
    /// <param name="IpopConfigPath">Path to the ipop config file</param>
    public RpcIpopNode(string NodeConfigPath, string IpopConfigPath):
      base(NodeConfigPath, IpopConfigPath) {
      RpcDHCPServer dhcp_server = new RpcDHCPServer(_ipop_config.VirtualNetworkDevice);  
      _dhcp_server = dhcp_server;
      _rarad = new RpcAddressResolverAndDNS(Brunet, dhcp_server);
      _dns = _rarad;
      _address_resolver = _rarad;
      _translator = _rarad;
    }

    /// <summary>
    /// Update the address information
    /// </summary>
    /// <param name="IP">A string with the new IP</param>
    /// <param name="Netmask">A netmask of the address</param>
    public override void UpdateAddressData(String IP, String Netmask) {
      base.UpdateAddressData(IP, Netmask);
      _rarad.UpdateAddressData(IP, Netmask);
    }

    /// <summary>
    /// This method handles incoming DHCP packets
    /// </summary>
    /// <param name="ipp">A DHCP IPPacket to be processed</param>
    /// <returns></returns>
    protected override bool HandleDHCP(IPPacket ipp) {
        ProcessDHCP(ipp, null);
        return true;
    }

    /// <summary>
    /// This method handles incoming DNS Packets
    /// </summary>
    /// <param name="ipp">A DNS IPPacket to be processed</param>
    /// <returns>A boolean result</returns>
    protected override bool HandleDNS(IPPacket ipp) {
      IPPacket res = _dns.LookUp(ipp);
      EthernetPacket res_ep = new EthernetPacket(Ethernet.Address, EthernetPacket.UnicastAddress,
          EthernetPacket.Types.IP, res.ICPacket);
      Ethernet.Send(res_ep.ICPacket);
      return true;
    }

    /// <summary>
    /// This method handles multicast packets (not yet implemented)
    /// </summary>
    /// <param name="ipp">A multicast packet to be processed</param>
    /// <returns></returns>
    protected override bool HandleMulticast(IPPacket ipp) {
      foreach(Address addr in _rarad.mcast_addr) {
        SendIP(addr, ipp.Packet);
      }
      return true;
    }

    /// <summary>
    /// Main method
    /// </summary>
    /// <param name="args">Argument passed by the user</param>
    public static new void Main(String[] args) {
      RpcIpopNode node = new RpcIpopNode(args[0], args[1]);
      node.Run();
    }
  }
}
