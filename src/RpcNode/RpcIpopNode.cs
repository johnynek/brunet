
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
using Brunet.Applications;
using Brunet.DistributedServices;
using NetworkPackets;
using System;
using System.Net;
using System.Threading;

namespace Ipop {
  public class RpcIpopNode: IpopNode {

    public RpcIpopNode(string NodeConfigPath, string IpopConfigPath):
      base(NodeConfigPath, IpopConfigPath) {
      RpcAddressResolverAndDNS rarad = new RpcAddressResolverAndDNS(Brunet);
      _dns = rarad;
      _address_resolver = rarad;
      _dhcp_server = new RpcDHCPServer(_ipop_config.VirtualNetworkDevice);  
    }

    protected override bool HandleDHCP(IPPacket ipp) {
        ProcessDHCP(ipp, null);
        return true;
    }

    protected override bool HandleDNS(IPPacket ipp) {
      IPPacket res = _dns.LookUp(ipp);
      EthernetPacket res_ep = new EthernetPacket(MACAddress, EthernetPacket.UnicastAddress,
          EthernetPacket.Types.IP, res.ICPacket);
      Ethernet.Send(res_ep.ICPacket);
      return true;
    }

    protected override bool HandleMulticast(IPPacket ipp) {
      return false;
    }

    public static new void Main(String[] args) {
      RpcIpopNode node = new RpcIpopNode(args[0], args[1]);
      node.Run();
    }
  }
}
