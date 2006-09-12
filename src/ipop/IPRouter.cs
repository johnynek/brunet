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
  public class IPRouterConfig {
    public string ipop_namespace;
    public string brunet_namespace;
    public string device;
    [XmlArrayItem (typeof(string), ElementName = "transport")]
    public string [] RemoteTAs;
    public EdgeListener [] EdgeListeners;
    public string IPConfig;
    public string DHCPServerIP;
    public string NodeAddress;
    public string Setup;
    public string Hostname;
    public string TapMAC;
    public StaticInfo StaticData;
    public DHCPInfo DHCPData;
  }

  public class StaticInfo {
    public string IPAddress;
    public string Netmask;
  }

  public class DHCPInfo {
    public string DHCPServerAddress;
    public string IPAddress;
    public string Netmask;
  }

  public class EdgeListener {
    [XmlAttribute]
    public string type;
    public int port;
  }

  public class IPRouter {
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
    private static ArrayList Nameservers;
    private static string Virtual_IPAddress;
    private static string Netmask;

/*  Generic */

    private static void ReadConfiguration(string configFile) {
      XmlSerializer serializer = new XmlSerializer(typeof(IPRouterConfig));
      FileStream fs = new FileStream(configFile, FileMode.Open);
      config = (IPRouterConfig) serializer.Deserialize(fs);
      RemoteTAs = new ArrayList();
      foreach(string TA in config.RemoteTAs) {
        TransportAddress ta = new TransportAddress(TA);
        RemoteTAs.Add(ta);
      }
      fs.Close();
      if(config.NodeAddress == null) {
        RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        byte [] temp = new byte[16];
        rng.GetBytes(temp);
        config.NodeAddress = DHCPCommon.BytesToString(temp, ':');
        UpdateConfiguration(configFile);
      }
      if(config.Setup == null) {
        config.Setup = "auto";
      }
    }

    private static void UpdateConfiguration(string configFile) {
      FileStream fs = new FileStream(configFile, FileMode.OpenOrCreate, 
        FileAccess.Write);
      XmlSerializer serializer = new XmlSerializer(typeof(IPRouterConfig));
      serializer.Serialize(fs, config);
      fs.Close();
    }

    private static BigInteger GetHash(IPAddress addr) {
       //Console.WriteLine("The IP addr: {0}", addr);
       HashAlgorithm hashAlgo = HashAlgorithm.Create();
       //hashAlgo.HashSize = AHAddress.MemSize;
       //Console.WriteLine("hash size: {0}" + hashAlgo.HashSize);
       byte[] hash = hashAlgo.ComputeHash(addr.IPBuffer);
       hash[Address.MemSize -1] &= 0xFE;
       return new BigInteger(hash);
    }

    static BrunetTransport Start() {
      //Should be active now
      status = 1;
      //Setup TAAuthorizer
      byte [] netmask = DHCPCommon.StringToBytes(Netmask, '.');
      int nm_value = (netmask[0] << 24) + (netmask[1] << 16) +
        (netmask[2] << 8) + netmask[3];
      int value = 0;
      for(value = 0; value < 32; value++)
        if((1 << value) == (nm_value & (1 << value)))
          break;
      value = 32 - value;
      TAAuthorizer ta_auth = new NetmaskTAAuthorizer(
        System.Net.IPAddress.Parse(Virtual_IPAddress), value,
        TAAuthorizer.Decision.Deny, TAAuthorizer.Decision.Allow);
      //local node
      AHAddress us = new AHAddress(GetHash(new IPAddress(Virtual_IPAddress)));
      Console.WriteLine("Generated address: {0}", us);
      //AHAddress us = new AHAddress(new BigInteger(Int32.Parse(args[1])));
      Node tmp_node = new StructuredNode(us, config.brunet_namespace);

      //Where do we listen:
      System.Net.IPAddress[] tas = routines.GetIPTAs(Virtual_IPAddress);
      foreach(EdgeListener item in config.EdgeListeners) {
        if (item.type =="tcp") { 
            tmp_node.AddEdgeListener(new TcpEdgeListener(item.port, tas, 
              ta_auth));
        }
        else if (item.type == "udp") {
            tmp_node.AddEdgeListener(new UdpEdgeListener(item.port , tas, 
              ta_auth));
        }
        else if (item.type == "udp-as") {
            tmp_node.AddEdgeListener(new ASUdpEdgeListener(item.port, tas, 
              ta_auth));
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
        new IPAddress(Virtual_IPAddress));
      tmp_node.Subscribe(AHPacket.Protocol.IP, ip_handler);


      tmp_node.Connect();
      System.Console.WriteLine("Called Connect");

      BrunetTransport brunet = new BrunetTransport(tmp_node);
      return brunet;
    }

    static void Main(string []args) {
      //configuration file 
      if (args.Length < 1) {
        Console.WriteLine("please specify the configuration file name...");
	return;
      }

      ReadConfiguration(args[0]);
      if (args.Length == 2) {
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

      BrunetTransport brunet = null;
      RoutingTable routes = null;

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
        Nameservers = routines.GetNameservers();
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
        byte [] buffer = new byte[packet.Length - 14];

        if(type == 0x806 || type == 0x800)
          Array.Copy(packet, 14, buffer, 0, buffer.Length);
        else
          continue;

        if(type == 0x806) {
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

          buffer[14] = buffer[24];
          buffer[15] = buffer[25];
          buffer[16] = buffer[26];
          buffer[17] = buffer[27];

          if(config.TapMAC != null && config.Setup == "manual") {
            byte [] temp1 = DHCPCommon.HexStringToBytes(config.TapMAC, ':');
            buffer[18] = temp1[0];
            buffer[19] = temp1[1];
            buffer[20] = temp1[2];
            buffer[21] = temp1[3];
            buffer[22] = temp1[4];
            buffer[23] = temp1[5];
          }
          else {
            buffer[18] = 0xFE;
            buffer[19] = 0xFD;
            buffer[20] = 0x00;
            buffer[21] = 0x00;
            buffer[22] = 0x00;
            buffer[23] = 0x01;
          }

          buffer[24] = temp[0];
          buffer[25] = temp[1];
          buffer[26] = temp[2];
          buffer[27] = temp[3];
          ether.SendPacket(buffer, 0x806);
          continue;
        }

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
        /* Create new DHCPPacket, parse the bytes, add relevant data, 
            and send to DHCP Server */
          DHCPPacket dhcpPacket = new DHCPPacket(buffer);
          dhcpPacket.DecodePacket();
          dhcpPacket.decodedPacket.brunet_namespace = config.brunet_namespace;
          dhcpPacket.decodedPacket.ipop_namespace = config.ipop_namespace;
          dhcpPacket.decodedPacket.NodeAddress = config.NodeAddress;
        /* DHCP Server returns our incoming packet, which we decode, if it
            is successful, we continue, otherwise we fail and print out a message */
          DHCPPacket returnPacket = new DHCPPacket(
            DHCPClient.SendMessage(dhcpPacket.decodedPacket));
          if(returnPacket.decodedPacket.return_message == "Success") {
            /* Add nameservers if it doesn't contain it already */
            if(!returnPacket.decodedPacket.options.Contains(6)) {
              DHCPOption option = new DHCPOption();
              option.type = 6;
              option.length = Nameservers.Count * 4;
              option.encoding = "int";
              option.byte_value = new byte[option.length];
              int i = 0, ci = 4;

              foreach(string item0 in Nameservers) {
                byte [] temp = DHCPCommon.StringToBytes(item0, '.');
                for(; i < ci; i++)
                  option.byte_value[i] = temp[i%4];
                ci += 4;
              }
              returnPacket.decodedPacket.options.Add(option.type, option);
            }

        /* Convert the packet into byte format, run Arp and Route updater */
            returnPacket.EncodePacket();
            ether.SendPacket(returnPacket.packet, 0x800);
        /* Do we have a new IP address, if so (re)start Brunet */
            byte [] ip = returnPacket.decodedPacket.yiaddr;
            string newAddress = DHCPCommon.BytesToString(ip, '.');
            if(Virtual_IPAddress == null || Virtual_IPAddress != newAddress) {
              Virtual_IPAddress = newAddress;
              Netmask = DHCPCommon.BytesToString(((DHCPOption) returnPacket.
                decodedPacket.options[1]).byte_value, '.');
              config.DHCPData.IPAddress = Virtual_IPAddress;
              config.DHCPData.Netmask = Netmask;
              UpdateConfiguration(args[0]);
              if(config.Setup == "auto") {
                if(config.Hostname == null)
                  routines.SetHostname(routines.DHCPGetHostname(Virtual_IPAddress));
                else
                  routines.SetHostname(config.Hostname);
              }
              brunet = Start();
              routes = new RoutingTable();
            }
            continue;
          }
          else {
        /* Not a success, means we can't continue on, sorry, 
            print the friendly server message */
            Console.WriteLine("The DHCP Server has a message to share with you...");
            Console.WriteLine("\n" +
              returnPacket.decodedPacket.return_message);
            Console.WriteLine("\nSorry, this program will now close.");
            Environment.Exit(0);
          }
        }

        if(status == 1) {
          AHAddress target = (AHAddress) routes.SearchRoute(destAddr);
          if (target == null) {
            target = new AHAddress(GetHash(destAddr));
            routes.AddRoute(destAddr, target);
          }
          //build an IP packet
          //buffer = IPPacketBuilder.BuildPacket(buffer,
          //IPPacketBuilder.Protocol.IP_PACKET);
          if (debug) {
            Console.WriteLine("Brunet destination ID: {0}", target);
          }
          brunet.SendPacket(target, buffer);
        }
      }
    }
  }
}
