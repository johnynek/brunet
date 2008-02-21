
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
using System;
using System.Net;
using System.Threading;

namespace Ipop {
  public class DhtIpopNode: IpopNode {
    protected bool in_dhcp;
    protected DhtDNS DhtDNS;

    public DhtIpopNode(string NodeConfigPath, string IpopConfigPath):
      base(NodeConfigPath, IpopConfigPath) {
      in_dhcp = false;
      _dhcp_server = new DhtDHCPServer(Dht, _ipop_config.EnableMulticast);
      DhtDNS = new DhtDNS(this);
      _address_resolver = new DhtAddressResolver(Dht, _ipop_config.IpopNamespace);
    }

    protected override bool HandleDHCP(IPPacket ipp) {
      if(IpopLog.DHCPLog.Enabled) {
        ProtocolLog.WriteIf(IpopLog.DHCPLog, String.Format(
                            "Incoming DHCP Request, DHCP Status: {0}.", in_dhcp));
      }
      if(!in_dhcp) {
        in_dhcp = true;
        ThreadPool.QueueUserWorkItem(new WaitCallback(HandleDHCP), ipp);
      }
      return true;
    }

    protected override bool HandleDNS(IPPacket ipp) {
      ThreadPool.QueueUserWorkItem(new WaitCallback(DhtDNS.LookUp), ipp);
      return true;
    }

    protected override bool HandleMulticast(IPPacket ipp) {
      if(_ipop_config.EnableMulticast) {
        ThreadPool.QueueUserWorkItem(new WaitCallback(HandleMulticast), ipp);
      }
      return true;
    }

    protected void HandleDHCP(Object IPPacketo) {
      IPPacket ipp = (IPPacket) IPPacketo;
      UDPPacket udpp = new UDPPacket(ipp.Payload);
      DHCPPacket dhcp_packet = new DHCPPacket(udpp.Payload);

      byte []last_ip = null;
      string hostname = null;
      if(_ipop_config.AddressData == null) {
        _ipop_config.AddressData = new AddressInfo();
      }
      else {
        try {
          hostname = _ipop_config.AddressData.Hostname;
          last_ip = IPAddress.Parse(_ipop_config.AddressData.IPAddress).GetAddressBytes();
        }
        catch {}
      }

      try {
        DHCPPacket rpacket = _dhcp_server.Process(dhcp_packet, last_ip,
            Brunet.Address.ToString(), _ipop_config.IpopNamespace,
            hostname);

        /* Check our allocation to see if we're getting a new address */
        string new_address = Utils.MemBlockToString(rpacket.yiaddr, '.');
        string new_netmask = Utils.BytesToString(
            (byte[]) rpacket.Options[DHCPPacket.OptionTypes.SUBNET_MASK], '.');
        if(new_address != IP || Netmask !=  new_netmask) {
          UpdateAddressData(new_address, new_netmask);
          ProtocolLog.WriteIf(IpopLog.DHCPLog, String.Format(
                              "DHCP:  IP Address changed to {0}", IP));
        }
        byte[] destination_ip = null;
        if(ipp.SourceIP[0] == 0) {
          destination_ip = new byte[4]{255, 255, 255, 255};
        }
        else {
          destination_ip = ipp.SourceIP;
        }
        UDPPacket res_udpp = new UDPPacket(67, 68, rpacket.Packet);
        IPPacket res_ipp = new IPPacket((byte) IPPacket.Protocols.UDP,
                                         rpacket.ciaddr, destination_ip, res_udpp.ICPacket);
        EthernetPacket res_ep = new EthernetPacket(MACAddress, EthernetPacket.UnicastAddress,
            EthernetPacket.Types.IP, res_ipp.ICPacket);
        Ethernet.Send(res_ep.ICPacket);
      }
      catch(Exception e) {
        ProtocolLog.WriteIf(IpopLog.DHCPLog, e.ToString());//Message);
      }
      in_dhcp = false;
    }

    public void HandleMulticast(Object ippo) {
      IPPacket ipp = (IPPacket) ippo;
      DhtGetResult []dgrs = Dht.Get("multicast.ipop_vpn");
      foreach(DhtGetResult dgr in dgrs) {
        try {
          AHAddress target = (AHAddress) AddressParser.Parse(dgr.valueString);
          if(IpopLog.PacketLog.Enabled) {
            ProtocolLog.Write(IpopLog.PacketLog, String.Format(
                              "Brunet destination ID: {0}", target));
          }
          SendIP(target, ipp.Packet);
        }
        catch {}
      }
    }

    public static new void Main(String[] args) {
      DhtIpopNode node = new DhtIpopNode(args[0], args[1]);
      node.Run();
    }
  }
}
