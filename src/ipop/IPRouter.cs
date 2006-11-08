using System;
using Brunet;
using System.Text;
using System.Threading;
using System.Collections;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Serialization;
using Mono.Security.Authenticode;

namespace Ipop {
  public class IPRouter {
#if IPOP_LOG
    private static readonly log4net.ILog _log =
    log4net.LogManager.GetLogger(System.Reflection.MethodBase.
                                 GetCurrentMethod().DeclaringType);
#endif
    //if debugging information is needed
    private static bool debug;
    //the class modeling the ethernet;
    private static Ethernet ether;
    //status 0 = inactive, 1 = active
    private static int status;
    //Configuration Data
    private static IPRouterConfig config;

    private static ArrayList RemoteTAs;

    private static OSDependent routines;
    private static string Virtual_IPAddress;
    private static string Netmask;
    private static string ConfigFile;
    private static BrunetTransport brunet;
    private static RoutingTable routes;

/*  Generic */
    private static BigInteger GetHash(IPAddress addr) {
       //Console.WriteLine("The IP addr: {0}", addr);
       HashAlgorithm hashAlgo = HashAlgorithm.Create();
       //hashAlgo.HashSize = AHAddress.MemSize;
       //Console.WriteLine("hash size: {0}" + hashAlgo.HashSize);
       byte[] hash = hashAlgo.ComputeHash(addr.GetAddressBytes());
       hash[Address.MemSize -1] &= 0xFE;
       return new BigInteger(hash);
    }

    static BrunetTransport Start() {
      //Should be active now
      status = 1;
      //local node
      AHAddress us = new AHAddress(GetHash(IPAddress.Parse(Virtual_IPAddress)));
      Console.WriteLine("Generated address: {0}", us);
      //AHAddress us = new AHAddress(new BigInteger(Int32.Parse(args[1])));
      Node tmp_node = new StructuredNode(us, config.brunet_namespace);

      //Where do we listen:
      IPAddress[] tas = routines.GetIPTAs(config.DevicesToBind);

      foreach(EdgeListener item in config.EdgeListeners) {
        int port = 0;
        if(item.port_high != null && item.port_low != null && item.port == null) {
          int port_high = Int32.Parse(item.port_high);
          int port_low = Int32.Parse(item.port_low);
          Random random = new Random();
          port = (random.Next() % (port_high - port_low)) + port_low;
          }
        else
            port = Int32.Parse(item.port);
        if (item.type =="tcp") { 
            tmp_node.AddEdgeListener(new TcpEdgeListener(port, tas));
        }
        else if (item.type == "udp") {
            tmp_node.AddEdgeListener(new UdpEdgeListener(port , tas));
        }
        else if (item.type == "udp-as") {
            tmp_node.AddEdgeListener(new ASUdpEdgeListener(port, tas));
        }
        else {
          throw new Exception("Unrecognized transport: " + item.type);
        }
      }

      //Here is where we connect to some well-known Brunet endpoints
      tmp_node.RemoteTAs = RemoteTAs;

      //now try sending some messages out	
      //subscribe to the IP protocol packet

      IPPacketHandler ip_handler = new IPPacketHandler(ether, debug, 
        IPAddress.Parse(Virtual_IPAddress));
      tmp_node.Subscribe(AHPacket.Protocol.IP, ip_handler);

      tmp_node.Connect();
      System.Console.WriteLine("Called Connect");

      BrunetTransport brunet = new BrunetTransport(tmp_node);
      return brunet;
    }

    private static void ProcessDHCP(byte []buffer)
    {
      DHCPPacket dhcpPacket = new DHCPPacket(buffer);
      /* Create new DHCPPacket, parse the bytes, add relevant data, 
          and send to DHCP Server */
      dhcpPacket.DecodePacket();
      dhcpPacket.decodedPacket.brunet_namespace = config.brunet_namespace;
      dhcpPacket.decodedPacket.ipop_namespace = config.ipop_namespace;
      dhcpPacket.decodedPacket.NodeAddress = config.NodeAddress;

      /* DHCP Server returns our incoming packet, which we decode, if it
          is successful, we continue, otherwise we fail and print out a message */
      DHCPPacket returnPacket = null;
      string response = null;
      try {
        returnPacket = new DHCPPacket(
          DHCPClient.SendMessage(dhcpPacket.decodedPacket));
      }
      catch (Exception e)
      {
        System.Console.WriteLine(e);
        response = e.ToString();
      }
      if(returnPacket != null &&
        returnPacket.decodedPacket.return_message == "Success") {
        /* Convert the packet into byte format, run Arp and Route updater */
         returnPacket.EncodePacket();
         ether.SendPacket(returnPacket.packet, 0x800);
        /* Do we have a new IP address, if so (re)start Brunet */
         string newAddress = DHCPCommon.BytesToString(
          returnPacket.decodedPacket.yiaddr, '.');
        String newNetmask = DHCPCommon.BytesToString(((DHCPOption) returnPacket.
          decodedPacket.options[1]).byte_value, '.');
        if(Virtual_IPAddress == null || Virtual_IPAddress != newAddress ||
          newNetmask != Netmask) {
          Netmask = newNetmask;
          Virtual_IPAddress = newAddress;
          config.DHCPData.IPAddress = Virtual_IPAddress;
          config.DHCPData.Netmask = Netmask;
          IPRouterConfigHandler.Write(ConfigFile, config);
          if(config.Setup == "auto") {
            if(config.Hostname == null)
              routines.SetHostname(routines.DHCPGetHostname(Virtual_IPAddress));
            else
              routines.SetHostname(config.Hostname);
          }
          brunet = Start();
          routes = new RoutingTable();
        }
      }
      else {
        if (returnPacket != null)
          response = returnPacket.decodedPacket.return_message;
        /* Not a success, means we can't continue on, sorry, 
           print the friendly server message */
        Console.WriteLine("The DHCP Server has a message to share with you...");
        Console.WriteLine("\n" + response);
        Console.WriteLine("\nSorry, this program will sleep and try again later.");
        Thread.Sleep(600);
      }
    }


    static void Main(string []args) {
      //configuration file 
      if (args.Length < 1) {
        Console.WriteLine("please specify the configuration file name...");
        Environment.Exit(0);
      }
      ConfigFile = args[0];

#if IPOP_LOG
      if (args.Length < 2) {
        Console.WriteLine("please specify the full path to the Logger " + 
          "configuration file...");
        Environment.Exit(1);
      }
      XmlConfigurator.Configure(new System.IO.FileInfo(args[1]));
#endif

      config = IPRouterConfigHandler.Read(ConfigFile);
      RemoteTAs = new ArrayList();
      foreach(string TA in config.RemoteTAs) {
        TransportAddress ta = new TransportAddress(TA);
        RemoteTAs.Add(ta);
      }

      if(config.NodeAddress == null) {
        RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        byte [] temp = new byte[16];
        rng.GetBytes(temp);
        config.NodeAddress = DHCPCommon.BytesToString(temp, ':');
        IPRouterConfigHandler.Write(ConfigFile, config);
      }

      if (args.Length == 3) {
        debug = true;
      } else {
        debug = false;
      }

      routines = new OSDependent();
      System.Console.WriteLine("IPRouter starting up...");
      if(config.TapMAC != null && config.Setup == "manual")
        ether = new Ethernet(config.device, config.TapMAC,
          "FE:FD:00:00:00:00");
      else
        ether = new Ethernet(config.device, "FE:FD:00:00:00:01", 
          "FE:FD:00:00:00:00");
      if (ether.Open() < 0) {
        Console.WriteLine("unable to set up the tap");
        return;
      }

      brunet = null;
      routes = null;

      if(config.Setup == "auto")
        routines.SetTapMAC(config.device);

      if(config.IPConfig == "static")
      {
        Virtual_IPAddress = config.StaticData.IPAddress;
        Netmask = config.StaticData.Netmask;
        if(config.Setup == "auto") {
          routines.SetTapDevice(config.device, Virtual_IPAddress, Netmask);
          if(config.Hostname != null)
            routines.SetHostname(config.Hostname);
        }
        //setup Brunet node
        brunet = Start();
        //build a new routes table and populate it artificially
        routes = new RoutingTable();
      }
      else {
        DHCPClient.DHCPInit(config.DHCPData.DHCPServerAddress);
        if(config.DHCPData.IPAddress != null && config.DHCPData.Netmask != null) {
          Virtual_IPAddress = config.DHCPData.IPAddress;
          Netmask = config.DHCPData.Netmask;
          brunet = Start();
          routes = new RoutingTable();
          if(config.Setup == "auto") {
            if(config.Hostname == null)
              routines.SetHostname(routines.DHCPGetHostname(Virtual_IPAddress));
            else
              routines.SetHostname(config.Hostname);
          }
        }
        else {
          Virtual_IPAddress = null;
          Netmask = null;
        }
      }
      // else wait for dhcp packet below

      //start the asynchronous communication now
      while(true) {
        //now the packet
        byte [] packet = ether.ReceivePacket();
        //Console.WriteLine("read a packet of length: {0}", packet.Length);
        if (packet == null) {
          Console.WriteLine("error reading packet from ethernet");
          continue;
        }

        /*  ARP Packet Handler */
        int type = (packet[12] << 8) + packet[13];
        byte [] buffer = null;

        if(type == 0x806 || type == 0x800) {
          buffer =  new byte[packet.Length - 14];
          Array.Copy(packet, 14, buffer, 0, buffer.Length);
        }
        else
          continue;

        if(type == 0x806) {
          string IP = buffer[24].ToString() + "." + buffer[25].ToString() + "." 
            + buffer[26].ToString() + "." + buffer[27].ToString();
          if(Virtual_IPAddress == IP)
            continue;
          /* Set HWAddr of dest to FE:FD:00:00:00:00 */
          buffer[7] = 2;
          byte [] temp;
          if(buffer[14] == 0)
            temp = new byte[] {0xFF, 0xFF, 0xFF, 0xFF};
          else
            temp = new byte[] {buffer[14], buffer[15], buffer[16], buffer[17]};
          buffer[8] = 0xFE;
          buffer[9] = 0xFD;
          buffer[10] = 0x00;
          buffer[11] = 0x00;
          buffer[12] = 0x00;
          buffer[13] = 0x00;

          for(int i = 0; i <= 3; i++)
            buffer[14+i] = buffer[24+i];

          if(config.TapMAC != null && config.Setup == "manual") {
            byte [] temp1 = DHCPCommon.HexStringToBytes(config.TapMAC, ':');
            for(int i = 0; i <= 5; i ++)
              buffer[18+i] = temp1[i];
          }
          else {
            buffer[18] = 0xFE;
            buffer[19] = 0xFD;
            buffer[20] = 0x00;
            buffer[21] = 0x00;
            buffer[22] = 0x00;
            buffer[23] = 0x01;
          }

          for(int i = 0; i <= 3; i++)
            buffer[24+i] = temp[i];
          ether.SendPacket(buffer, 0x806);
          continue;
        }

        /*  End Arp */

        IPAddress destAddr = IPPacketParser.DestAddr(buffer);
        IPAddress srcAddr = IPPacketParser.SrcAddr(buffer);

        int destPort = (buffer[22] << 8) + buffer[23];
        int srcPort = (buffer[20] << 8) + buffer[21];
        int protocol = buffer[9];

        if (debug) {
          Console.WriteLine("Outgoing {0} packet::IP src: {1}:{2}," + 
            "IP dst: {3}:{4}", protocol, srcAddr, srcPort, destAddr,
            destPort);
        }

        if(srcPort == 68 && destPort == 67 && protocol == 17 && 
          config.IPConfig == "dhcp") {
          if (debug)
            Console.WriteLine("DHCP Packet");
          ProcessDHCP(buffer);
          continue;
        }

        if(status == 1) {
          AHAddress target = (AHAddress) routes.SearchRoute(destAddr);
          if (target == null) {
            target = new AHAddress(GetHash(destAddr));
            routes.AddRoute(destAddr, target);
          }

          if (debug) {
            Console.WriteLine("Brunet destination ID: {0}", target);
          }
          brunet.SendPacket(target, buffer);
        }
      }
    }
  }
}
