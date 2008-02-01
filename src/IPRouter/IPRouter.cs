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
    public static NodeMapping node;
    private static byte []unicastMAC = new byte[]{0xFE, 0xFD, 0, 0, 0, 0};
    private static byte []broadcastMAC = new byte[]{0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF};
    private static bool in_dht;

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
      if((node.ip != null) && node.ip.Equals(IPAddress.Parse(TargetIPAddress)) ||
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
      node.ether.Write(replyPacket, EthernetPacket.Types.ARP, dstMACAddr);
      return true;
    }

    private static void IPHandler(IPPacket ipp) {
      if(IPOPLog.PacketLog.Enabled) {
        ProtocolLog.Write(IPOPLog.PacketLog, String.Format(
          "Outgoing {0} packet::IP src: {1}:{2}, IP dst: {3}:{4}", 
          ipp.Protocol, ipp.SSourceIP, ipp.SourcePort,
          ipp.SDestinationIP, ipp.DestinationPort));
      }

      if(ipp.SourcePort == 68 && ipp.DestinationPort == 67 && 
         ipp.Protocol == (byte) IPPacket.Protocols.UDP) {
        ProtocolLog.WriteIf(IPOPLog.DHCPLog, String.Format(
                            "DHCP packet at time: {0}, status: {1}", DateTime.Now, in_dht));
        if(!in_dht) {
          in_dht = true;
          ThreadPool.QueueUserWorkItem(new WaitCallback(ProcessDHCP), ipp);
        }
      }
      else {
        AHAddress target = (AHAddress) node.routes.GetAddress(ipp.SDestinationIP);
        if (target == null) {
          node.routes.RouteMiss(ipp.SDestinationIP);
        }
        else {
          if(IPOPLog.PacketLog.Enabled) {
            ProtocolLog.Write(IPOPLog.PacketLog, String.Format(
                            "Brunet destination ID: {0}", target));
          }
          node.iphandler.Send(target, ipp.Packet);
        }
      }
    }

    private static void ProcessDHCP(object IPPacketo) {
      IPPacket ipp = (IPPacket) IPPacketo;
      DHCPPacket dhcpPacket = new DHCPPacket(ipp);
      dhcpPacket.decodedPacket.brunet_namespace = config.brunet_namespace;
      dhcpPacket.decodedPacket.ipop_namespace = config.ipop_namespace;
      dhcpPacket.decodedPacket.NodeAddress = node.address.ToString();

      try {
        dhcpPacket.decodedPacket.yiaddr =
          IPAddress.Parse(config.AddressData.IPAddress).GetAddressBytes();
      }
      catch {}


      /* DHCP Server returns our incoming packet, which we decode, if it
          is successful, we continue, otherwise we fail and print out a message */
      DecodedDHCPPacket drpacket = 
          node.dhcpClient._dhcp_server.SendMessage(dhcpPacket.decodedPacket);
      string response = drpacket.return_message;

      if(response == "Success") {
        /* Convert the packet into byte format, run Arp and Route updater */
        DHCPPacket returnPacket = new DHCPPacket(drpacket);
        /* Check our allocation to see if we're getting a new address */
        string newAddress = IPOP_Common.BytesToString(drpacket.yiaddr, '.');

        string newNetmask = IPOP_Common.BytesToString(((DHCPOption)
          drpacket.options[DHCPOptions.SUBNET_MASK]).byte_value, '.');

        if(newAddress != node.ip || node.netmask !=  newNetmask) {
          node.netmask = newNetmask;
          node.ip = newAddress;
          ProtocolLog.WriteIf(IPOPLog.DHCPLog, String.Format(
            "DHCP:  IP Address changed to {0}", node.ip));
          config.AddressData = new AddressInfo();
          config.AddressData.IPAddress = newAddress;
          config.AddressData.Netmask = node.netmask;
          IPRouterConfigHandler.Write(ConfigFile, config);
// This is currently broken
//            node.brunet.UpdateTAAuthorizer();
        }
        node.ether.Write(returnPacket.IPPacket.Packet, EthernetPacket.Types.IP, node.mac);
      }
      else {
        ProtocolLog.WriteIf(IPOPLog.DHCPLog, String.Format(
          "The DHCP Server has a message to share with you...\n" + response));
      }
      in_dht = false;
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

      if(OSDependent.OSVersion == OSDependent.Linux) {
        new LinuxShutdown();
      }

      ProtocolLog.WriteIf(IPOPLog.BaseLog, String.Format(
        "IPRouter starting up at time: {0}", DateTime.Now));

      node = new NodeMapping(config.ipop_namespace, config.brunet_namespace,
                             (AHAddress) AddressParser.Parse(config.NodeAddress),
                            new Ethernet(config.device, unicastMAC));
      try {
        node.netmask = config.AddressData.Netmask;
        node.ip = config.AddressData.IPAddress;
      }
      catch{}

      node.BrunetStart();
      in_dht = false;

      bool ethernet = false;
      // Tap reading loop
      while(true) {
        EthernetPacket ep;
        try {
          ep = new EthernetPacket(node.ether.Read());
        }
        catch {
          ProtocolLog.WriteIf(IPOPLog.BaseLog, "error reading packet from ethernet");
          continue;
        }

  /* We should really be checking each and every packet, but for simplicity sake
     we will only check until we are satisfied! */
        if(!ethernet) {
          node.mac = ep.SourceAddress;
          ethernet = true;
        }

        // Maybe the node is sleeping now...
        if(node.brunet == null) {
          continue;
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
