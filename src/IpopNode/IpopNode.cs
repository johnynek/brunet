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
using Brunet.Security;
using Brunet.Applications;
using System.Collections;
using System.Threading;

/**
\namespace Ipop
\brief Defines IpopNode the base IP over P2P utilities.
*/
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
  </remarks>

  <summary>IP over P2P base class.</summary>
  @deprecated please [look at IpopRouter, that will soon become an abstract
  class and should be used as the basis for your code]
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
    /// <summary>Resolves IP Addresses to Brunet.Addresses</summary>
    protected IAddressResolver _address_resolver;
    /// <summary>Resolves hostnames and IP Addresses</summary>
    protected DNS _dns;
    /// <summary>If necessary, acts as a DNAT / SNAT</summary>
    protected ITranslator _translator;
    /**
    <summary>Optional method to supply IP Addresses automatically to the
    operating sytem</summary>
    */
    protected DHCPServer _dhcp_server;

    protected MemBlock _local_ip;
    protected MemBlock _netmask;
    protected readonly int _dhcp_server_port;
    protected readonly int _dhcp_client_port;

    /// <summary> Byte array representation for the local IP of this node</summary>
    public MemBlock LocalIP { get { return _local_ip; } }
    /// <summary> Byte array representation for the local IP of this node</summary>
    public MemBlock Netmask { get { return _netmask; } }
    protected readonly bool _multicast;
    protected bool _secure_senders;

    /**
    <summary>Creates an IpopNode given a path to a NodeConfig and an
    IpopConfig.  Also sets up the Information, Ethernet device, and subscribes
    to Brunet for IP Packets</summary>
    <param name="NodeConfigPath">The path to a NodeConfig xml file</param>
    <param name="IpopConfigPath">The path to a IpopConfig xml file</param>
    */
    public IpopNode(string NodeConfigPath, string IpopConfigPath):
      base(NodeConfigPath)
    {
      CreateNode();
      this.Brunet = _node;
      _ipop_config_path = IpopConfigPath;
      _ipop_config = LoadConfig();

      _dhcp_client_port = _ipop_config.DHCPPort != 0 ? _ipop_config.DHCPPort : 68;
      _dhcp_server_port = _dhcp_client_port - 1;
      ProtocolLog.WriteIf(IpopLog.DHCPLog, String.Format(
          "Setting DHCP Ports to: {0},{1}", _dhcp_server_port, _dhcp_client_port));

      Ethernet = new Ethernet(_ipop_config.VirtualNetworkDevice);
      Ethernet.Subscribe(this, null);

      _info = new Information(Brunet, "IpopNode");
      _info.UserData["IpopNamespace"] = _ipop_config.IpopNamespace;

      if(_ipop_config.EndToEndSecurity && _bso != null) {
        _secure_senders = true;
      } else {
        _secure_senders = false;
      }
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
      if(_shutdown != null) {
        _shutdown.OnExit += Ethernet.Stop;
      }
      _node.Connect();
      StopServices();
    }

    /**
    <summary>This is called by IP and Netmask handler to update the readonly 
    properties _ip and _netmask as well as the IpopConfig.  This is called by
    DHCP Process even if in the config file matches the new ip address and so
    applications should only rely on IpopNode.IP or implement this method to
    receive the IP address of this sytem from boot up or changes.</summary>
    <param name="IP">The new IP Address.</param>
    <param name="Netmask">The new Netmask.</param>
    */
    protected virtual void UpdateAddressData(MemBlock IP, MemBlock Netmask) {
      string ip = Utils.MemBlockToString(IP, '.');
      string netmask = Utils.MemBlockToString(Netmask, '.');
      _info.UserData["Virtual IP"] = ip;
      _ipop_config.AddressData.IPAddress = ip;
      _ipop_config.AddressData.Netmask = netmask;
      Utils.WriteConfig(_ipop_config_path, _ipop_config);

      _local_ip = IP;
      _netmask = Netmask;
    }

    /**
    <summary> This method handles all incoming packets into the IpopNode, both
    abroad and local.  This is done to reduce unnecessary extra classes and
    circular dependencies.  This method probably shouldn't be called
    directly.</summary>
    <param name="b"> The incoming packet</param>
    <param name="ret">An ISender to return data from the original sender.</param>
    <param name="state">always will be null</param>
    */
    public void HandleData(MemBlock b, ISender ret, object state) {
      if(ret is Ethernet) {
        EthernetPacket ep = new EthernetPacket(b);

        switch (ep.Type) {
          case EthernetPacket.Types.ARP:
            HandleARP(ep.Payload);
            break;
          case EthernetPacket.Types.IP:
            HandleIPOut(ep, ret);
            break;
        }
      }
      else {
        HandleIPIn(b, ret);
      }
    }

    /// <summary>This method handles IPPackets that come from Brunet, i.e.,
    /// abroad.  </summary>
    /// <param name="packet"> The packet from Brunet.</param>
    /// <param name="ret">An ISender to send data to the Brunet node that sent
    /// the packet.</param>
    public virtual void HandleIPIn(MemBlock packet, ISender ret) {
      if(_secure_senders && !(ret is SecurityAssociation)) {
        return;
      }

      Address addr = null;
      if(ret is SecurityAssociation) {
        ret = ((SecurityAssociation) ret).Sender;
      }

      if(ret is AHSender) {
        addr = ((AHSender) ret).Destination;
      } else {
        ProtocolLog.Write(IpopLog.PacketLog, String.Format(
          "Incoming packet was not from an AHSender: {0}.", ret));
        return;
      }

      if(_translator != null) {
        try {
          packet = _translator.Translate(packet, addr);
        }
        catch (Exception e) {
          if(ProtocolLog.Exceptions.Enabled) {
            ProtocolLog.Write(ProtocolLog.Exceptions, e.ToString());
          }
          return;
        }
      }

      IPPacket ipp = new IPPacket(packet);

      if(!_address_resolver.Check(ipp.SourceIP, addr)) {
        return;
      }

      if(IpopLog.PacketLog.Enabled) {
        ProtocolLog.Write(IpopLog.PacketLog, String.Format(
                          "Incoming packet:: IP src: {0}, IP dst: {1}, p2p " +
                              "from: {2}, size: {3}", ipp.SSourceIP, ipp.SDestinationIP,
                              ret, packet.Length));
      }

      WriteIP(packet);
    }

    /// <summary>Writes an IPPacket as is to the TAP device.</summary>
    /// <param name="packet">The IPPacket!</param>
    protected virtual void WriteIP(ICopyable packet) {
      EthernetPacket res_ep = new EthernetPacket(Ethernet.Address, EthernetPacket.UnicastAddress,
          EthernetPacket.Types.IP, packet);
      Ethernet.Send(res_ep.ICPacket);
    }

    /// <summary>This method handles IPPackets that come from the TAP Device, i.e.,
    /// local system.</summary>
    /// <remarks>Currently this supports HandleMulticast (ip[0] >= 244 &&
    /// ip[0]<=239), HandleDNS (dport = 53 and ip[3] == 1), dhcp (sport 68 and
    /// dport 67.</remarks>
    /// <param name="packet">The packet from the TAP device</param>
    /// <param name="from"> This should always be the tap device</param>
    protected virtual void HandleIPOut(EthernetPacket packet, ISender ret) {
      IPPacket ipp = new IPPacket(packet.Payload);
      if(IpopLog.PacketLog.Enabled) {
        ProtocolLog.Write(IpopLog.PacketLog, String.Format(
                          "Outgoing {0} packet::IP src: {1}, IP dst: {2}", 
                          ipp.Protocol, ipp.SSourceIP, ipp.SDestinationIP));
      }

      if(!IsLocalIP(ipp.SourceIP)) {
        HandleNewStaticIP(packet.SourceAddress, ipp.SourceIP);
        return;
      }

      if(ipp.DestinationIP[0] >= 224 && ipp.DestinationIP[0] <= 239) {
        if(HandleMulticast(ipp)) {
          return;
        }
      }

      switch(ipp.Protocol) {
        case IPPacket.Protocols.UDP:
          UDPPacket udpp = new UDPPacket(ipp.Payload);
          if(udpp.SourcePort == _dhcp_client_port && udpp.DestinationPort == _dhcp_server_port) {
            if(HandleDHCP(ipp)) {
              return;
            }
          } else if(udpp.DestinationPort == 53 &&ipp.DestinationIP.Equals(_dhcp_server.ServerIP)) {
            if(HandleDNS(ipp)) {
              return;
            }
          }
          break;
      }

      if(HandleOther(ipp)) {
        return;
      }

      if(_dhcp_server == null || ipp.DestinationIP.Equals(_dhcp_server.ServerIP)) {
        return;
      }

      AHAddress target = (AHAddress) _address_resolver.Resolve(ipp.DestinationIP);
      if (target != null) {
        if(IpopLog.PacketLog.Enabled) {
          ProtocolLog.Write(IpopLog.PacketLog, String.Format(
                            "Brunet destination ID: {0}", target));
        }
        SendIP(target, packet.Payload);
      }
    }

    /// <summary>Is this our IP?  Are we routing for it?</summary>
    /// <param name="ip">The IP in question.</param>
    protected virtual bool IsLocalIP(MemBlock ip) {
      return ip.Equals(_local_ip) || ip.Equals(IPPacket.ZeroAddress);
    }

    /// <summary>Let's see if we can route for an IP.  Default is do
    /// nothing!</summary>
    /// <param name="ip">The IP in question.</param>
    protected virtual void HandleNewStaticIP(MemBlock ether_addr, MemBlock ip) {
    }

    /**
    <summary>This method is called by HandleIPOut if the packet does match any
    of the common mappings.</summary>
    <param name="ipp"> The miscellaneous IPPacket.</param>
    <returns>True if implemented, false otherwise.</returns>
     */
    protected virtual bool HandleOther(IPPacket ipp) {
      return false;
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
      try {
        UDPPacket udpp = new UDPPacket(ipp.Payload);
        DHCPPacket dhcp_packet = new DHCPPacket(udpp.Payload);

        byte []last_ip = null;
        if(_ipop_config.AddressData.IPAddress != null) {
          last_ip = IPAddress.Parse(_ipop_config.AddressData.IPAddress).GetAddressBytes();
        }

        DHCPPacket rpacket = _dhcp_server.ProcessPacket(dhcp_packet,
            Brunet.Address.ToString(), last_ip, dhcp_params);

        /* Check our allocation to see if we're getting a new address */
        MemBlock new_addr = rpacket.yiaddr;
        MemBlock new_netmask = rpacket.Options[DHCPPacket.OptionTypes.SUBNET_MASK];

        if(!new_addr.Equals(LocalIP) || !new_netmask.Equals(Netmask)) {
          UpdateAddressData(new_addr, new_netmask);
          ProtocolLog.WriteIf(IpopLog.DHCPLog, String.Format(
            "IP Address changed to {0}", Utils.MemBlockToString(new_addr, '.')));
        }

        byte[] destination_ip = null;
        if(ipp.SourceIP.Equals(IPPacket.ZeroAddress)) {
          destination_ip = IPPacket.BroadcastAddress;
        } else {
          destination_ip = ipp.SourceIP;
        }

        UDPPacket res_udpp = new UDPPacket(_dhcp_server_port, _dhcp_client_port, rpacket.Packet);
        IPPacket res_ipp = new IPPacket(IPPacket.Protocols.UDP,
                                         rpacket.ciaddr, destination_ip,
                                         res_udpp.ICPacket);
        EthernetPacket res_ep = new EthernetPacket(Ethernet.Address,
            EthernetPacket.UnicastAddress, EthernetPacket.Types.IP,
            res_ipp.ICPacket);
        Ethernet.Send(res_ep.ICPacket);
        return true;
      }
      catch(Exception e) {
        ProtocolLog.WriteIf(IpopLog.DHCPLog, e.ToString());
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
      ISender s = null;
      if(_secure_senders) {
        try {
          s = _bso.GetSecureSender(target);
        }
        catch(Exception e) {
          Console.WriteLine(e);
          return;
        }
      }
      else {
        s = new AHExactSender(Brunet, target);
      }
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
    protected virtual void HandleARP(MemBlock packet) {
      // Can't do anything until we have network connectivity!
      if(_dhcp_server == null) {
        return;
      }

      ARPPacket ap = new ARPPacket(packet);

      if(ap.Operation == ARPPacket.Operations.Reply) {
      // This would be a unsolicited ARP
        if(ap.TargetProtoAddress.Equals(IPPacket.BroadcastAddress) &&
            !ap.SenderHWAddress.Equals(EthernetPacket.BroadcastAddress) &&
            !ap.SenderProtoAddress.Equals(IPPacket.BroadcastAddress) &&
            _dhcp_server.IPInRange((byte[]) ap.SenderProtoAddress))
        {
          HandleNewStaticIP(ap.SenderHWAddress, ap.SenderProtoAddress);
        }
        return;
      }

      // We only support request operation hereafter
      if(ap.Operation != ARPPacket.Operations.Request) {
        return;
      }

      // Not in our range!
      if(!_dhcp_server.IPInRange((byte[]) ap.TargetProtoAddress)) {
        return;
      }

      // We shouldn't be returning these messages if no one exists at that end
      // point
      if(!_dhcp_server.ServerIP.Equals(ap.TargetProtoAddress) && (
            _address_resolver.Resolve(ap.TargetProtoAddress) == null ||
            _address_resolver.Resolve(ap.TargetProtoAddress) == Brunet.Address))
      {
        return;
      }

      /* Must return nothing if the node is checking availability of IPs */
      /* Or he is looking himself up. */
      if(ap.TargetProtoAddress.Equals(LocalIP) ||
          ap.SenderProtoAddress.Equals(IPPacket.BroadcastAddress) ||
          ap.SenderProtoAddress.Equals(IPPacket.ZeroAddress)) 
      {
        return;
      }

      ARPPacket response = ap.Respond(EthernetPacket.UnicastAddress);

      EthernetPacket res_ep = new EthernetPacket(Ethernet.Address,
        EthernetPacket.UnicastAddress, EthernetPacket.Types.ARP,
        response.ICPacket);
      Ethernet.Send(res_ep.ICPacket);
    }
  }

  /**
  <summary>This interface is used for IP Address to Brunet address translations.
  It must implement the method GetAddress.  All IpopNode sub classes MUST
  have some IAddressResolver.</summary>
  */
  public interface IAddressResolver {
    /// <summary>Takes an IP and returns the mapped Brunet.Address</summary>
    /// <param name="ip"> the MemBlock representation of the IP</param>
    /// <returns>translated Brunet.Address</returns>
    Address Resolve(MemBlock ip);
    /// <summary>Sometimes mappings change or we get packets from a place
    /// that doesn't map correctly, this checks the mapping, returns false
    /// if the mapping is currently invalid, but then causes a revalidation
    /// of the mapping</summary>
    bool Check(MemBlock ip, Address addr);
  }

  /**
  <summary>This interface is used for Nodes which would like to translate IP
  header data on arrival to a remote node.  This is similar to the DNAT/SNAT
  features provided in iptables</summary>
  */
  public interface ITranslator {
    /**
    <summary>This takes in an IPPacket, translates it and returns the resulting
    IPPacket</summary>
    <param name="packet">The IP Packet to translate.</param>
    <param name="from">The Brunet address the packet was sent from.</param>
    <returns>The translated IP Packet.</returns>
    */
    MemBlock Translate(MemBlock packet, Address from);
  }
}

