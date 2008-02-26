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

using NetworkPackets;
using NetworkPackets.DHCP;
using System;
using System.Net;
using Brunet;
using Brunet.DistributedServices;
using Brunet.Applications;
using System.Collections;
using System.Threading;

namespace Ipop {
  /**
  <remarks><para>IpopNode provides the IP over P2P library that is to be used
  in conjunction with Brunet.  Main features of this library include Virtual 
  Ethernet (tap) handler, DHCP Server, DNS Server, and network packet parsing.
  This is a filter between Brunet and the host system via the Virtual Ethernet
  device.  Data is read from the ethernet device, parsed and sent to the
  proper handler if one exists or a query to resolve the ip to a Brunet
  address.  If a mapping exists, the packet will be sent to the proper end
  point.  From Brunet, the data is sent to the Ipop handler and optional
  translation services can occur before being written to the Ethernet device.
  Both these enter through the HandleData, data read from the Ethernet goes
  to HandleIPOut, where as Brunet incoming data goes to HandleIPIn. </para>
  </remarks>
  <example>
  <para>Don't forget to implement this two methods in all subclasses!</para>
  <code>
  public static new int Main(String[] args) {
    *IpopNode node = new *IpopNode(args[0], args[1]);
      node.Run();
      return 0;
    }
  </code>
  </example>

  <summary>IP over P2P base class.</summary>
 */
  public abstract class IpopNode: BasicNode, IDataHandler {
    /// <summary>The IpopConfig for this IpopNode</summary>
    protected readonly IpopConfig _ipop_config;
    /**
    <summary>The path where the IpopConfig comes from, this is used to write
    back any future changes.</summary>
    */
    protected readonly String _ipop_config_path;
    /// <summary>The Virtual Network handler</summary>
    public readonly Ethernet Ethernet;
    /// <summary>The Rpc handler for Information</summary>
    public readonly Information _info;

    /// <summary>The Brunet.Node for this IpopNode</summary>
    public readonly StructuredNode Brunet;
    /// <summary>Dht provider for this node</summary>
    public readonly Dht Dht;
    /// <summary>Resolves IP Addresses to Brunet.Addresses</summary>
    protected IAddressResolver _address_resolver;
    /// <summary>Resolves hostnames and IP Addresses</summary>
    protected DNS _dns;
    /**
    <summary>Optional method to supply IP Addresses automatically to the
    operating sytem</summary>
    */
    protected DHCPServer _dhcp_server;

    /// <summary> Protected string representation for the local IP of this node</summary>
    protected string _ip;
    /// <summary> Public tring representation for the local IP of this node </summary>
    public string IP { get { return _ip; } }
    /// <summary> Protected string representation for the Netmask of this node </summary>
    protected string _netmask;
    /// <summary> Public string representation for the Netmask of this node </summary>
    public string Netmask { get { return _netmask; } }
    /**
    <summary> The Ethernet Address of the device network device we are 
    communicating through.</summary>
    */
    public byte [] MACAddress;

    /**
    <summary>Creates an IpopNode given a path to a NodeConfig and an
    IpopConfig.  Also sets up the Information, Ethernet device, and subscribes
    to Brunet for IP Packets</summary>
    <param name="NodeConfigPath">The path to a NodeConfig xml file</param>
    <param name="IpopConfigPath">The path to a IpopConfig xml file</param>
    */
    public IpopNode(string NodeConfigPath, string IpopConfigPath):
      base(NodeConfigPath) {
      CreateNode();
      this.Dht = _dht;
      this.Brunet = _node;
      _ipop_config_path = IpopConfigPath;
      _ipop_config = LoadConfig();
      Ethernet = new Ethernet(_ipop_config.VirtualNetworkDevice);
      Ethernet.Subscribe(this, null);

      _info = new Information(Brunet, "IpopNode");
      _info.UserData["IpopNamespace"] = _ipop_config.IpopNamespace;

      Brunet.GetTypeSource(PType.Protocol.IP).Subscribe(this, null);
    }

    /**
    <summary>This allows nodes to implement the loading of the IpopConfig
    so that they can implement a sublcass config</summary>
    <returns>Returns the IpopConfig stored at _ipop_config_path.</returns>
    */
    protected virtual IpopConfig LoadConfig() {
      return Utils.ReadConfig<IpopConfig>(_ipop_config_path);
    }

    /**
    <summary>Starts the execution of the IpopNode, this passes the caller 
    to execute the Brunet.Connect to eventually become Brunet.AnnounceThread.
    </summary>
    */
    public override void Run() {
      StartServices();
      _node.Connect();
      StopServices();
    }

    /**
    <summary>This is called by IP and Netmask handler to update the readonly 
    properties _ip and _netmask as well as the IpopConfig.</summary>
    <param name="IP">The new IP Address.</param>
    <param name="Netmask">The new Netmask.</param>
    */
    public virtual void UpdateAddressData(string IP, string Netmask) {
      _info.UserData["Virtual IP"] = IP;
      _ip = IP;
      _ipop_config.AddressData.IPAddress = _ip;
      _netmask = Netmask;
      _ipop_config.AddressData.Netmask = _netmask;
      Utils.WriteConfig(_ipop_config_path, _ipop_config);
    }

    /**
    <summary> This method handles all incoming packets into the IpopNode, both
    abroad and local.  This is done to reduce unnecessary extra classes and
    circular dependencies.  This method probably shouldn't be called
    directly.</summary>
    <param name="b"> The incoming packet</param>
    <param name="from"> the ISender of the packet (Ethernet or Brunet)</param>
    <param name="state">always will be null</param>
    */
    public void HandleData(MemBlock b, ISender from, object state) {
      if(from is Ethernet) {
        EthernetPacket ep = new EthernetPacket(b);
        if(MACAddress == null) {
          MACAddress = ep.SourceAddress;
        }

        switch (ep.Type) {
          case EthernetPacket.Types.ARP:
            HandleARP(ep.Payload);
            break;
          case EthernetPacket.Types.IP:
            HandleIPOut(ep.Payload, from);
            break;
        }
      }
      else {
        HandleIPIn(b, from);
      }
    }

    /**
    <summary>This method handles IPPackets that come from Brunet, i.e., abroad.
    </summary>
    <param name="packet"> The packet from Brunet.</param>
    <param name="from"> The Brunet node that sent the packet.</param>
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
    <summary>This method handles IPPackets that come from the TAP Device, i.e.,
    local system.</summary>
    <param name="packet">The packet from the TAP device</param>
    <param name="from"> This should always be the tap device</param>
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
        case IPPacket.Protocols.UDP:
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

      AHAddress target = (AHAddress) _address_resolver.Resolve(ipp.SDestinationIP);
      if (target != null) {
        if(IpopLog.PacketLog.Enabled) {
          ProtocolLog.Write(IpopLog.PacketLog, String.Format(
                            "Brunet destination ID: {0}", target));
        }
        SendIP(target, packet);
      }
    }

    /**
    <summary>This method is called by HandleIPOut if the destination address
    is within the multicast address range.  If you want Multicast, implement
    this method, output will most likely be sent via the SendIP() method in the
    IpopNode base class.</summary>
    <param name="ipp"> The IPPacket the contains the multicast message</param>
    <returns>True if implemented, false otherwise.</returns>
    */
    protected virtual bool HandleMulticast(IPPacket ipp) {
      return false;
    }

    /**
    <summary>This method is called by HandleIPOut if the source and 
    destination ports are the well known DHCP ports.  If you want DHCP,
    implement this method, responses should be written directly to the
    Ethernet interface using Ethernet.Send().</summary>
    <param name="ipp"> The IPPacket the contains the DHCP message</param>
    <returns>True if implemented, false otherwise.</returns>
    */
    protected virtual bool HandleDHCP(IPPacket ipp) {
      return false;
    }

    /**
    <summary>This is used to process a dhcp packet on the node side, that
    includes placing data such as the local Brunet Address, Ipop Namespace, the
    nodes last ip, and other optional parameters in our request to the dhcp
    server.  When receiving the results, if it is successful, the results are
    written to the Ethernet device.  if there is a change, this triggers
    updates to the IpopConfig.</summary>
    <param name="ipp"> The IPPacket that contains the DHCP Request</param>
    <param name="dhcp_params"> an object containing any extra parameters for 
    the dhcp server</param>
    <returns> true on success and false on failure, if true the ethernet had 
    the result written to it as well.</returns>
    */
    protected virtual bool ProcessDHCP(IPPacket ipp, params Object[] dhcp_params) {
      UDPPacket udpp = new UDPPacket(ipp.Payload);
      DHCPPacket dhcp_packet = new DHCPPacket(udpp.Payload);

      byte []last_ip = null;
      if(_ipop_config.AddressData == null) {
        _ipop_config.AddressData = new IpopConfig.AddressInfo();
      }
      try {
        last_ip = IPAddress.Parse(_ipop_config.AddressData.IPAddress).GetAddressBytes();
      }
      catch {}

      try {
        DHCPPacket rpacket = _dhcp_server.Process(dhcp_packet, last_ip,
            Brunet.Address.ToString(), _ipop_config.IpopNamespace,
            dhcp_params);

        /* Check our allocation to see if we're getting a new address */
        string new_address = Utils.MemBlockToString(rpacket.yiaddr, '.');
        string new_netmask = Utils.BytesToString(
            (byte[]) rpacket.Options[DHCPPacket.OptionTypes.SUBNET_MASK], '.');
        if(new_address != IP || Netmask !=  new_netmask) {
          UpdateAddressData(new_address, new_netmask);
          ProtocolLog.WriteIf(IpopLog.DHCPLog, String.Format(
                              "IP Address changed to {0}", IP));
        }
        byte[] destination_ip = null;
        if(ipp.SourceIP[0] == 0) {
          destination_ip = new byte[4]{255, 255, 255, 255};
        }
        else {
          destination_ip = ipp.SourceIP;
        }
        UDPPacket res_udpp = new UDPPacket(67, 68, rpacket.Packet);
        IPPacket res_ipp = new IPPacket(IPPacket.Protocols.UDP,
                                         rpacket.ciaddr, destination_ip,
                                         res_udpp.ICPacket);
        EthernetPacket res_ep = new EthernetPacket(MACAddress,
            EthernetPacket.UnicastAddress, EthernetPacket.Types.IP,
            res_ipp.ICPacket);
        Ethernet.Send(res_ep.ICPacket);
        return true;
      }
      catch(Exception e) {
        ProtocolLog.WriteIf(IpopLog.DHCPLog, e.ToString());//Message);
      }
      return false;
    }

    /**
    <summary>If a request is sent to address a.b.c.255 with the dns port (53),
    this method will be called by HandleIPOut.  If you want DNS, implement this
    method, responses should be written directly to the tap interface using
    Ethernet.Send()</summary>
    <param name="ipp"> The IPPacket contain the DNS packet</param>
    <returns>True if implemented, false otherwise.</returns>
    */
    protected virtual bool HandleDNS(IPPacket ipp) {
      return false;
    }

    /**
    <summary>Sends the IP Packet to the specified target address.</summary>
    <param name="target"> the Brunet Address of the target</param>
    <param name="packet"> the data to send to the recepient</param>
    */
    protected virtual void SendIP(Address target, MemBlock packet) {
      ISender s = new AHExactSender(Brunet, target);
      s.Send(new CopyList(PType.Protocol.IP, packet));
    }

    /**
    <summary>Parses ARP Packets and writes to the Ethernet the translation.
    </summary>
    <remarks>Since IpopNode uses tap as the underlying virtual network 
    handler, we must handling incoming ARP requests.  This makes the operating
    system think that all the nodes are on the same remote gateway
    FE:00:00:00:00.  Results, if valid are automatically written to the
    Ethernet address.  Two cases, when respones are not valid, the node does an
    ARP lookup on the address its attempting to acquire or when it does an ARP
    and it does not have an address.  If either of these were to be responded
    to, the node would refuse the address, thinking someone else already has
    it.</remarks>
    <param name="packet">The Ethernet packet to translate</param>
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

  /**
  <summary>This interface is used for IP Address to Brunet address translations.
  It must implement the method GetAddress.  All IpopNode sub classes MUST
  have some IAddressResolver.</summary>
  */
  public interface IAddressResolver {
    /**
    <summary>Takes a string representation an IP and returns the mapped
    Brunet.Address</summary>
    <param name="ip"> the string representation of the IP</param>
    <returns>translated Brunet.Address</returns>
    */
    Address Resolve(String ip);
  }
}

