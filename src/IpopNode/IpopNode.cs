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

using System;
using System.Net;
using Brunet;
using Brunet.Dht;
using System.Collections;
using System.Threading;

namespace Ipop {
  public abstract class IpopNode: IDataHandler {
    protected readonly IpopConfig _ipop_config;
    protected readonly String _ipop_config_path;
    public readonly Ethernet Ethernet;
//    public readonly IpopInformation IpopInfo;

    //Services
    public readonly StructuredNode Brunet;
    public readonly Dht Dht;
    protected IRoutes _routes;
    protected DHCPServer _dhcp_server;

    protected string _ip, _netmask;
    public string IP { get { return _ip; } }
    public string Netmask { get { return _netmask; } }
    public byte [] MACAddress;

    public IpopNode(StructuredNode Brunet, Dht Dht, string IpopConfigPath) {
      this.Dht = Dht;
      this.Brunet = Brunet;
      _ipop_config_path = IpopConfigPath;
      _ipop_config = IpopConfigHandler.Read(_ipop_config_path);
      Ethernet = new Ethernet(_ipop_config.VirtualNetworkDevice);
      Ethernet.Subscribe(this, null);

/*      this.IpopInfo = IpopInfo;
      IpopInfo.Type = "IPRouter";
      IpopInfo.IpopNamespace = _ipop_config.IpopNamespace;
*/
      Brunet.GetTypeSource(PType.Protocol.IP).Subscribe(this, null);
    }

    public void UpdateAddressData(string IP, string Netmask) {
      _ip = IP;
      _ipop_config.AddressData.IPAddress = _ip;
      _netmask = Netmask;
      _ipop_config.AddressData.Netmask = _netmask;
      IpopConfigHandler.Write(_ipop_config_path, _ipop_config);
    }

    /**
     * This method handles all incoming packets into the IpopNode, both abroad
     * and local.  This is done to reduce unnecessary extra classes and
     * circular dependencies.  This method probably shouldn't be called
     * directly.
     * @param b The incoming packet
     * @param from the ISender of the packet (Ethernet or Brunet)
     * @param state always will be null
     */

    public void HandleData(MemBlock b, ISender from, object state) {
      if(from is Ethernet) {
        EthernetPacket ep = new EthernetPacket(b);
        if(MACAddress == null) {
          MACAddress = ep.SourceAddress;
        }

        switch (ep.Type) {
          case (int) EthernetPacket.Types.ARP:
            HandleARP(ep.Payload);
            break;
          case (int) EthernetPacket.Types.IP:
            HandleIPOut(ep.Payload, from);
            break;
        }
      }
      else {
        HandleIPIn(b, from);
      }
    }

    /**
     * This method handles IPPackets that come from Brunet, i.e., abroad.
     * @param packet The packet from Brunet
     * @param from The Brunet node that sent the packet
     */

    public virtual void HandleIPIn(MemBlock packet, ISender from) {
      IPPacket ipp = new IPPacket(packet);

      if(IpopLog.PacketLog.Enabled) {
        ProtocolLog.Write(IpopLog.PacketLog, String.Format(
                          "Incoming packet:: IP src: {0}, IP dst: {1}, p2p " +
                              "from: {2}, size: {3}", ipp.SSourceIP, ipp.SDestinationIP,
                              from, packet.Length));
      }

      if(MACAddress != null) {
        EthernetPacket res_ep = new EthernetPacket(MACAddress, EthernetPacket.UnicastAddress,
            EthernetPacket.Types.IP, packet);
        Ethernet.Send(res_ep.ICPacket);
      }
    }

    /**
     * This method handles IPPackets that come from the TAP Device, i.e., 
     * local system.
     * @param packet The packet from the TAP device
     * @param from This should always be the tap device
     */

    protected virtual void HandleIPOut(MemBlock packet, ISender from) {
      IPPacket ipp = new IPPacket(packet);
      if(IpopLog.PacketLog.Enabled) {
        ProtocolLog.Write(IpopLog.PacketLog, String.Format(
                          "Outgoing {0} packet::IP src: {1}, IP dst: {2}", 
                          ipp.Protocol, ipp.SSourceIP, ipp.SDestinationIP));
      }

      if(ipp.DestinationIP[0] >= 224 && ipp.DestinationIP[0] <= 239) {
        if(HandleMulticast(ipp)) {
          return;
        }
      }

      switch(ipp.Protocol) {
        case (byte) IPPacket.Protocols.UDP:
          UDPPacket udpp = new UDPPacket(ipp.Payload);
          if(udpp.SourcePort == 68 && udpp.DestinationPort == 67) {
            if(HandleDHCP(ipp)) {
              return;
            }
          }
          else if(udpp.DestinationPort == 53 && ipp.DestinationIP[3] == 255) {
            if(HandleDNS(ipp)) {
              return;
            }
          }
          break;
      }

      AHAddress target = (AHAddress) _routes.GetAddress(ipp.SDestinationIP);
      if (target != null) {
        if(IpopLog.PacketLog.Enabled) {
          ProtocolLog.Write(IpopLog.PacketLog, String.Format(
                            "Brunet destination ID: {0}", target));
        }
        SendIP(target, packet);
      }
    }

    /**
     * If you want Multicast, implement this method, output will most likely
     * be sent via the SendIP() method in the IpopNode base class.
     * directly to the Ethernet interface using Ethernet.Send()
     * @param ipp The IPPacket the contains the multicast message
     */

    protected virtual bool HandleMulticast(IPPacket ipp) {
      return false;
    }

    /**
     * If you want DHCP, implement this method, responses should be written
     * directly to the Ethernet interface using Ethernet.Send()
     * @param ipp The IPPacket the contains the DHCP message
     */

    protected virtual bool HandleDHCP(IPPacket ipp) {
      return false;
    }

    /**
     * If you want DNS, implement this method, responses should be written
     * directly to the tap interface using Ethernet.Send()
     * @param ipp The IPPacket contain the DNS packet
     */

    protected virtual bool HandleDNS(IPPacket ipp) {
      return false;
    }

    /**
     * For IP, if the packet is to be sent over Brunet, use this method
     * @param target the Brunet Address of the target
     * @param packet the data to send to the recepient
     */

    protected virtual void SendIP(Address target, MemBlock packet) {
      ISender s = new AHExactSender(Brunet, target);
      s.Send(new CopyList(PType.Protocol.IP, packet));
    }

    /**
     * HandleARP is implemented in IpopNode due to its simplicity.  It takes
     * in a packet and writes the lookup to the virtual Ethernet device.
     * @param packet the packet to translate
     */
    protected void HandleARP(MemBlock packet) {
      string TargetIPAddress = "", SenderIPAddress = "";
      for(int i = 0; i < 3; i++) { 
        TargetIPAddress += packet[24+i].ToString() + ".";
        SenderIPAddress += packet[14+i].ToString() + ".";
      }
      SenderIPAddress += packet[17].ToString();
      TargetIPAddress += packet[27].ToString();
      /* Must return nothing if the node is checking availability of IPs */
      /* Or he is looking himself up. */
      if((IP != null) && IP.Equals(TargetIPAddress) ||
          SenderIPAddress.Equals("255.255.255.255") ||
          SenderIPAddress.Equals("0.0.0.0")) {
        return;
      }

      byte [] replyPacket = new byte[packet.Length];
      /* Same base */
      packet.Slice(0, 7).CopyTo(replyPacket, 0);
      /* ARP Reply */
      replyPacket[7] = 2;
      /* Source MAC Address */
      EthernetPacket.BroadcastAddress.CopyTo(replyPacket, 8);
      /* Source IP Address */
      packet.Slice(24, 4).CopyTo(replyPacket, 14);
      /* Target MAC Address */
      packet.Slice(8, 6).CopyTo(replyPacket, 18);
      /* Target IP Address */
      if(packet[14] == 0) {
        for (int i = 0; i < 4; i++) {
          replyPacket[24+i] = 0xFF;
        }
      }
      else {
        packet.Slice(14, 4).CopyTo(replyPacket, 24);
      }
      EthernetPacket res_ep = new EthernetPacket(MACAddress,
        EthernetPacket.UnicastAddress, EthernetPacket.Types.ARP,
        MemBlock.Reference(replyPacket));
      Ethernet.Send(res_ep.ICPacket);
    }
  }

  public interface IRoutes {
    Address GetAddress(String ip);
  }
}

