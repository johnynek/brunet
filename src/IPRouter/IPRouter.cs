using System;
using Brunet;
using Brunet.Dht;
using System.Text;
using System.Threading;
using System.Collections;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Diagnostics;

namespace Ipop {
  public class IPRouter {
    private static string ConfigFile;
    public static IPRouterConfig config;
    protected static IpopNode _node;
    protected static byte []unicastMAC = new byte[]{0xFE, 0xFD, 0, 0, 0, 0};
    protected static byte []broadcastMAC = new byte[]{0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF};
    protected static bool in_dhcp;

    private static bool ARPHandler(byte []packet) {
      string TargetIPAddress = "", SenderIPAddress = "";
      for(int i = 0; i < 3; i++) { 
        TargetIPAddress += packet[24+i].ToString() + ".";
        SenderIPAddress += packet[14+i].ToString() + ".";
      }
      SenderIPAddress += packet[17].ToString();
      TargetIPAddress += packet[27].ToString();
      /* Must return nothing if the node is checking availability of IPs */
      /* Or he is looking himself up. */
      if((_node.IP != null) && _node.IP.Equals(TargetIPAddress) ||
        SenderIPAddress.Equals("255.255.255.255") ||
        SenderIPAddress.Equals("0.0.0.0")) {
        return false;
      }

      byte [] replyPacket = new byte[packet.Length];
      /* Same base */
      Array.Copy(packet, 0, replyPacket, 0, 7);
      /* ARP Reply */
      replyPacket[7] = 2;
      /* Source MAC Address */
      Array.Copy(broadcastMAC, 0, replyPacket, 8, 6);
      /* Source IP Address */
      Array.Copy(packet, 24, replyPacket, 14, 4);
      /* Target MAC Address */
      byte []dstMACAddr = new byte[6];
      Array.Copy(packet, 8, dstMACAddr, 0, 6);
      Array.Copy(packet, 8, replyPacket, 18, 6);
      /* Target IP Address */
      if(packet[14] == 0) {
        for (int i = 0; i < 4; i++) {
          replyPacket[24+i] = 0xFF;
        }
      }
      else {
        Array.Copy(packet, 14, replyPacket, 24, 4);
      }
      _node.Ether.Write(MemBlock.Reference(replyPacket), 
                       EthernetPacket.Types.ARP, dstMACAddr);
      return true;
    }

    private static void IPHandler(IPPacket ipp) {
      UDPPacket udpp = new UDPPacket(ipp.Payload);
      if(IPOPLog.PacketLog.Enabled) {
        ProtocolLog.Write(IPOPLog.PacketLog, String.Format(
                          "Outgoing {0} packet::IP src: {1}:{2}, IP dst: {3}:{4}", 
                          ipp.Protocol, ipp.SSourceIP, udpp.SourcePort,
                          ipp.SDestinationIP, udpp.DestinationPort));
      }

      if(ipp.DestinationIP[0] >= 224 && ipp.DestinationIP[0] <= 239) {
        ThreadPool.QueueUserWorkItem(new WaitCallback(HandleMulticast), ipp);
      }

      switch(ipp.Protocol) {
        case (byte) IPPacket.Protocols.UDP:
          if(udpp.SourcePort == 68 && udpp.DestinationPort == 67) {
            ProtocolLog.WriteIf(IPOPLog.DHCPLog, String.Format(
              "DHCP packet at time: {0}, status: {1}", DateTime.Now, in_dhcp));
            if(!in_dhcp) {
            in_dhcp = true;
            ThreadPool.QueueUserWorkItem(HandleDHCP, ipp);
          }
        }
        else if(udpp.DestinationPort == 53 && ipp.DestinationIP[3] == 255) {
          ThreadPool.QueueUserWorkItem(_node.DhtDNS.LookUp, ipp);
        }
        else {
          goto default;
        }
        break;
      default:
        AHAddress target = (AHAddress) _node.Routes.GetAddress(ipp.SDestinationIP);
        if (target == null) {
          _node.Routes.RouteMiss(ipp.SDestinationIP);
        }
        else {
          if(IPOPLog.PacketLog.Enabled) {
            ProtocolLog.Write(IPOPLog.PacketLog, String.Format(
                                  "Brunet destination ID: {0}", target));
          }
          _node.IPHandler.Send(target, ipp.Packet);
        }
        break;
      }
    }

    private static void HandleDHCP(object IPPacketo) {
      IPPacket ipp = (IPPacket) IPPacketo;
      UDPPacket udpp = new UDPPacket(ipp.Payload);
      DHCPPacket dhcp_packet = new DHCPPacket(udpp.Payload);

      byte []last_ip = null;
      string hostname = null;
      if(config.AddressData == null) {
        config.AddressData = new AddressInfo();
      }
      else {
        try {
          hostname = config.AddressData.Hostname;
          last_ip = IPAddress.Parse(config.AddressData.IPAddress).GetAddressBytes();
        }
        catch {}
      }

      try {
        DHCPPacket rpacket = _node.DHCPServer.Process(dhcp_packet, last_ip,
                                        _node.Address.ToString(), config.ipop_namespace, 
                                        hostname);

        /* Check our allocation to see if we're getting a new address */
        string new_address = IPOP_Common.BytesToString(rpacket.yiaddr, '.');
        string new_netmask = IPOP_Common.BytesToString(
            (byte[]) rpacket.Options[DHCPPacket.OptionTypes.SUBNET_MASK], '.');

        if(new_address != _node.IP || _node.Netmask !=  new_netmask) {
          _node.Netmask = new_netmask;
          _node.IP = new_address;
          ProtocolLog.WriteIf(IPOPLog.DHCPLog, String.Format(
                              "DHCP:  IP Address changed to {0}", _node.IP));
          config.AddressData.IPAddress = new_address;
          config.AddressData.Netmask = new_netmask;
          IPRouterConfigHandler.Write(ConfigFile, config);
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
        _node.Ether.Write(res_ipp.Packet, EthernetPacket.Types.IP, _node.MAC);
      }
      catch(Exception e) {
        ProtocolLog.WriteIf(IPOPLog.DHCPLog, e.Message);
      }
      in_dhcp = false;
    }

    public static void HandleMulticast(Object ippo) {
      try {
        IPPacket ipp = (IPPacket) ippo;
        DhtGetResult []dgrs = _node.Dht.Get("multicast.ipop_vpn");
        foreach(DhtGetResult dgr in dgrs) {
          try {
            AHAddress target = (AHAddress) AddressParser.Parse(dgr.valueString);
            if(IPOPLog.PacketLog.Enabled) {
              ProtocolLog.Write(IPOPLog.PacketLog, String.Format(
                              "Brunet destination ID: {0}", target));
            }
            _node.IPHandler.Send(target, ipp.Packet);
          }
          catch(Exception e) {
//            Console.WriteLine("Inside: " + e);
          }
        }
      }
      catch(Exception e) {
//        Console.WriteLine(e);
      }
    }

    public static void Main(string []args) {
      try {
        ConfigFile = args[0];
        config = IPRouterConfigHandler.Read(ConfigFile);
      }
      catch {
        Console.WriteLine("Invalid or missing configuration file...");
        Environment.Exit(1);
      }

      /* Generate a Brunet Address if one doesn't already exist, so this node
       * gets a static Brunet Address.
       */
      if(config.NodeAddress == null) {
        config.NodeAddress = IPOP_Common.GenerateAHAddress().ToString();
        IPRouterConfigHandler.Write(ConfigFile, config);
      }

      ProtocolLog.WriteIf(IPOPLog.BaseLog, String.Format(
        "IPRouter starting up at time: {0}", DateTime.Now));

      _node = new IpopNode(config.ipop_namespace, config.brunet_namespace,
                             (AHAddress) AddressParser.Parse(config.NodeAddress),
                            new Ethernet(config.device, unicastMAC));

      if(OSDependent.OSVersion == OSDependent.Linux) {
        new LinuxShutdown(_node);
      }

      try {
        _node.Netmask = config.AddressData.Netmask;
        _node.IP = config.AddressData.IPAddress;
      }
      catch{}

      in_dhcp = false;
      bool ethernet = false;
      // Tap reading loop
      while(true) {
        EthernetPacket ep;
        try {
          ep = new EthernetPacket(_node.Ether.Read());
        }
        catch {
          ProtocolLog.WriteIf(IPOPLog.BaseLog, "error reading packet from ethernet");
          continue;
        }

  /* We should really be checking each and every packet, but for simplicity sake
     we will only check until we are satisfied! */
        if(!ethernet) {
          _node.MAC = ep.SourceAddress;
          ethernet = true;
        }

        switch (ep.Type) {
          case (int) EthernetPacket.Types.ARP:
            ARPHandler(ep.Payload);
            break;
          case (int) EthernetPacket.Types.IP:
            IPHandler(new IPPacket(ep.Payload));
            break;
        }
      }
    }
  }
}
