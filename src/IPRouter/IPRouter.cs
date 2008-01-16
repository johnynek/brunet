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
      node.ether.SendPacket(replyPacket, 0x806, dstMACAddr);
      return true;
    }

    private static void ProcessDHCP(object buffero) {
      byte [] buffer = (MemBlock) buffero;
      DHCPPacket dhcpPacket = new DHCPPacket(buffer);
      /* Create new DHCPPacket, parse the bytes, add relevant data, 
          and send to DHCP Server */
      dhcpPacket.DecodePacket();
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
      DHCPPacket returnPacket = null;
      string response = null;
      try {
        returnPacket = new DHCPPacket(
          node.dhcpClient._dhcp_server.SendMessage(dhcpPacket.decodedPacket));
        response = returnPacket.decodedPacket.return_message;
      }
      catch (Exception e) {
        response = e.ToString();
      }
      if(response == "Success") {
        /* Convert the packet into byte format, run Arp and Route updater */
        returnPacket.EncodePacket();
        node.ether.SendPacket(returnPacket.packet, 0x800, node.mac);
        /* Check our allocation to see if we're getting a new address */
        IPAddress newAddress = IPAddress.Parse(IPOP_Common.BytesToString(
          returnPacket.decodedPacket.yiaddr, '.'));

        string newNetmask = IPOP_Common.BytesToString(((DHCPOption)
          returnPacket.decodedPacket.options[1]).byte_value, '.');

        if(newAddress != node.ip || node.netmask !=  newNetmask) {
          node.netmask = newNetmask;
          node.ip = newAddress;
          ProtocolLog.WriteIf(IPOPLog.DHCPLog, String.Format(
            "DHCP:  IP Address changed to {0}", node.ip));
          config.AddressData = new AddressInfo();
          config.AddressData.IPAddress = newAddress.ToString();
          config.AddressData.Netmask = node.netmask;
          IPRouterConfigHandler.Write(ConfigFile, config);
// This is currently broken
//            node.brunet.UpdateTAAuthorizer();
        }
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
        node.ip = IPAddress.Parse(config.AddressData.IPAddress);
      }
      catch{}

      node.BrunetStart();
      in_dht = false;

      bool ethernet = false;
      //start the asynchronous communication now
      while(true) {
        //now the packet
        MemBlock packet = node.ether.ReceivePacket();
        if(packet == null) {
          ProtocolLog.WriteIf(IPOPLog.BaseLog, "error reading packet from ethernet");
          continue;
        }
  /* We should really be checking each and every packet, but for simplicity sake
     we will only check until we are satisfied! */
        else if(!ethernet) {
          node.mac = EthernetPacketParser.GetMAC(packet);
          ethernet = true;
        }
        // Maybe the node is sleeping now...
        else if(node.brunet == null) {
          continue;
        }

        int type = EthernetPacketParser.GetProtocol(packet);
        MemBlock payload = EthernetPacketParser.GetPayload(packet);

        if(type == 0x806)
          ARPHandler(payload);
        else if(type == 0x800) {
          IPAddress destAddr = IPPacketParser.GetDestAddr(payload);
          int destPort = IPPacketParser.GetDestPort(payload);
          int srcPort = IPPacketParser.GetSrcPort(payload);
          int protocol = IPPacketParser.GetProtocol(payload);

          if(IPOPLog.PacketLog.Enabled) {
            IPAddress srcAddr = IPPacketParser.GetSrcAddr(payload);
            ProtocolLog.Write(IPOPLog.PacketLog, String.Format(
              "Outgoing {0} packet::IP src: {1}:{2}," +
              "IP dst: {3}:{4}", protocol, srcAddr, srcPort, destAddr,
              destPort));
          }

          if(srcPort == 68 && destPort == 67 && protocol == 17) {
            ProtocolLog.WriteIf(IPOPLog.DHCPLog, String.Format(
              "DHCP packet at time: {0}, status: {1}", DateTime.Now, in_dht));
            if(!in_dht) {
              in_dht = true;
              ThreadPool.QueueUserWorkItem(new WaitCallback(ProcessDHCP), payload);
            }
            continue;
          }

          AHAddress target = (AHAddress) node.routes.GetAddress(destAddr);
          if (target == null) {
            node.routes.RouteMiss(destAddr);
            continue;
          }
          if(IPOPLog.PacketLog.Enabled) {
            ProtocolLog.Write(IPOPLog.PacketLog, String.Format(
              "Brunet destination ID: {0}", target));
          }
          node.iphandler.Send(target, payload);
        }
      }
    }
  }
}
