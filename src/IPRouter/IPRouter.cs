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
    private static Ethernet ether;
    private static IPRouterConfig config;
    private static ArrayList RemoteTAs;
    private static string ConfigFile;
    public static NodeMapping node;
    private static byte []routerMAC = new byte[]{0xFE, 0xFD, 0, 0, 0, 0};
    private static bool in_dht;

    private static Thread sdthread;
    private static Thread xrmthread;

    private static DHCPClient dhcpClient;
    private static Routes routes;

/*  Generic */
    private static void BrunetStart() {
      node.brunet = new BrunetTransport(ether, config.brunet_namespace,
        node, config.EdgeListeners, config.DevicesToBind, RemoteTAs);
      routes = new Routes(node.brunet.dht, node.ipop_namespace);

      if(config.EnableSoapDht && sdthread == null) {
        try {
          int dht_port = Int32.Parse(config.DhtPort);
          sdthread = DhtServer.StartDhtServerAsThread(node.brunet.dht, dht_port);
        }
        catch {}
      }

      if (config.EnableXmlRpcManager && xrmthread == null) {
        try {
          int xml_port = Int32.Parse(config.XmlRpcPort);
          RpcManager rpc = RpcManager.GetInstance(node.brunet.node);
          XmlRpcManagerServer.StartXmlRpcManagerServerAsThread(rpc, xml_port);
        }
        catch {}
      }
    }

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
      Array.Copy(routerMAC, 0, replyPacket, 8, 6);
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
      ether.SendPacket(replyPacket, 0x806, dstMACAddr);
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

      if (config.AddressData.IPAddress != null) {
        dhcpPacket.decodedPacket.yiaddr =
          IPAddress.Parse(config.AddressData.IPAddress).GetAddressBytes();
      }

      /* DHCP Server returns our incoming packet, which we decode, if it
          is successful, we continue, otherwise we fail and print out a message */
      DHCPPacket returnPacket = null;
      string response = null;
      try {
        returnPacket = new DHCPPacket(
          dhcpClient._dhcp_server.SendMessage(dhcpPacket.decodedPacket));
        response = returnPacket.decodedPacket.return_message;
      }
      catch (Exception e) {
        response = e.ToString();
      }
      if(response == "Success") {
        /* Convert the packet into byte format, run Arp and Route updater */
        returnPacket.EncodePacket();
        ether.SendPacket(returnPacket.packet, 0x800, node.mac);
        /* Check our allocation to see if we're getting a new address */
        string newAddress = IPOP_Common.BytesToString(
          returnPacket.decodedPacket.yiaddr, '.');

        string newNetmask = IPOP_Common.BytesToString(((DHCPOption)
          returnPacket.decodedPacket.options[1]).byte_value, '.');

        if(node.ip == null || newAddress != node.ip.ToString() ||node.netmask !=  newNetmask) {
          node.netmask = newNetmask;
          node.ip = IPAddress.Parse(newAddress);
          Debug.WriteLine(String.Format("DHCP:  IP Address changed to {0}", node.ip));
          config.AddressData.IPAddress = newAddress;
          config.AddressData.Netmask = node.netmask;
          IPRouterConfigHandler.Write(ConfigFile, config);
// This is currently broken
//            node.brunet.UpdateTAAuthorizer();
        }
      }
      else {
        Debug.WriteLine("The DHCP Server has a message to share with you...");
        Debug.WriteLine("\t\n" + response);
      }
      in_dht = false;
    }

    static void Main(string []args) {
      //configuration file
      if (args.Length < 1) {
        Debug.WriteLine("please specify the configuration file name...");
        Environment.Exit(0);
      }
      ConfigFile = args[0];

      if (args.Length == 2)
        Debug.Listeners.Add(new ConsoleTraceListener(true));

      config = IPRouterConfigHandler.Read(ConfigFile, true);

      RemoteTAs = new ArrayList();
      foreach(string TA in config.RemoteTAs) {
        TransportAddress ta = TransportAddressFactory.CreateInstance(TA);
        RemoteTAs.Add(ta);
      }

      // Generate a Brunet Address if one doesn't already exist
      if(config.NodeAddress == null) {
        config.NodeAddress = IPOP_Common.GenerateAHAddress().ToString();
        IPRouterConfigHandler.Write(ConfigFile, config);
      }

      OSDependent.Setup();
      if(OSDependent.OSVers == OSDependent.Linux) {
        new LinuxShutdown();
      }

      Debug.WriteLine(String.Format("IPRouter starting up at time: {0}", DateTime.Now));
      ether = new Ethernet(config.device, routerMAC);
      if (ether.Open() < 0) {
        Debug.WriteLine("Unable to set up the tap");
        return;
      }

      node = new NodeMapping();
      node.address = (AHAddress) AddressParser.Parse(config.NodeAddress);
      node.ipop_namespace = config.ipop_namespace;
      node.netmask = config.AddressData.Netmask;

      if(config.AddressData.IPAddress != null) {
        node.ip = IPAddress.Parse(config.AddressData.IPAddress);
      }

      BrunetStart();

      if(config.AddressData.DHCPServerAddress != null && !config.AddressData.DhtDHCP)
        dhcpClient = new SoapDHCPClient(config.AddressData.DHCPServerAddress);
      else {
        dhcpClient = new DhtDHCPClient(node.brunet.dht);
      }

      in_dht = false;
      bool ethernet = false;
      //start the asynchronous communication now
      while(true) {
        //now the packet
        MemBlock packet = ether.ReceivePacket();
        if (packet == null) {
          Debug.WriteLine("error reading packet from ethernet");
          continue;
        }
  /* We should really be checking each and every packet, but for simplicity sake
     we will only check until we are satisfied! */
        else if(!ethernet) {
          node.mac = EthernetPacketParser.GetMAC(packet);
          ethernet = true;
        }

        int type = EthernetPacketParser.GetProtocol(packet);
        MemBlock payload = EthernetPacketParser.GetPayload(packet);

        if(type == 0x806)
          ARPHandler(payload);
        else if(type == 0x800) {
          IPAddress srcAddr = IPPacketParser.GetSrcAddr(payload);
          IPAddress destAddr = IPPacketParser.GetDestAddr(payload);
          int destPort = IPPacketParser.GetDestPort(payload);
          int srcPort = IPPacketParser.GetSrcPort(payload);
          int protocol = IPPacketParser.GetProtocol(payload);

          Debug.WriteLine(String.Format("Outgoing {0} packet::IP src: {1}:{2}," +
              "IP dst: {3}:{4}", protocol, srcAddr, srcPort, destAddr,
              destPort));

          if(srcPort == 68 && destPort == 67 && protocol == 17) {
            Debug.WriteLine(String.Format("DHCP packet at time: {0}, status: {1}",
              DateTime.Now, in_dht));
            if(!in_dht) {
              in_dht = true;
              ThreadPool.QueueUserWorkItem(new WaitCallback(ProcessDHCP), payload);
            }
            continue;
          }

          AHAddress target = (AHAddress) routes.GetAddress(destAddr);
          if (target == null) {
            routes.RouteMiss(destAddr);
            continue;
          }
          Debug.WriteLine(String.Format("Brunet destination ID: {0}", target));
          node.brunet.SendPacket(target, payload);
        }
      }
    }
  }
}
