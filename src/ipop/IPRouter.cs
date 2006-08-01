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
    public string StaticIP;
    public string StaticNetmask;
    public string NodeAddress;
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

    private static ArrayList Nameservers;

    private static string IPAddr;
    private static string Netmask;

/* Linux Only */

    private static string GetTapAddress() {
      try {
        System.Diagnostics.Process proc = new System.Diagnostics.Process();
        proc.EnableRaisingEvents = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.FileName = "/sbin/ifconfig";
        proc.StartInfo.Arguments = config.device;
        proc.Start();
        proc.WaitForExit();

        StreamReader sr = proc.StandardOutput;
        sr.ReadLine();
        string output = sr.ReadLine();
        int point1 = output.IndexOf("inet addr:") + 10;
        int point2 = output.IndexOf("Bcast:") - 2 - point1;
        return output.Substring(point1, point2);
      }
      catch (Exception e) {
         if(config.IPConfig == "static") {
           Console.WriteLine(e);
           Environment.Exit(0);
         }
         return null;
      }
    }

    private static string GetTapMAC() {
      System.Diagnostics.Process proc = new System.Diagnostics.Process();
      proc.EnableRaisingEvents = false;
      proc.StartInfo.RedirectStandardOutput = true;
      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.FileName = "/sbin/ifconfig";
      proc.StartInfo.Arguments = config.device;
      proc.Start();
      proc.WaitForExit();

      StreamReader sr = proc.StandardOutput;
      string output = sr.ReadLine();
      return output.Substring(output.IndexOf("HWaddr") + 7, 17);
    }

    private static string GetTapNetmask() {
      string result = null;
      System.Diagnostics.Process proc = new System.Diagnostics.Process();
      proc.EnableRaisingEvents = false;
      proc.StartInfo.RedirectStandardOutput = true;
      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.FileName = "/sbin/ifconfig";
      proc.StartInfo.Arguments = config.device;
      proc.Start();
      proc.WaitForExit();

      StreamReader sr = proc.StandardOutput;
      sr.ReadLine();
      string output = sr.ReadLine();
      int point1 = output.IndexOf("Mask:") + 5;
      result = output.Substring(point1, output.Length - point1);
      return result;
    }

    private static void SetupRouteAndArp(byte [] ip, byte [] netmask) {
      string router = "", net = "", nm = "";
      for(int i = 0; i < ip.Length - 1; i++) {
        router += (ip[i] & netmask[i]) + ".";
        nm += netmask[i] + ".";
      }
      net = router + "0";
      router += "1";
      nm += netmask[netmask.Length - 1];

      System.Diagnostics.Process proc = new System.Diagnostics.Process();
      proc.EnableRaisingEvents = false;
      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.FileName = "route";
      proc.StartInfo.Arguments = "add -net " + net + " gw " + router + 
        " netmask " + nm + " " + config.device;
      proc.Start();
      proc.WaitForExit();

      proc = new System.Diagnostics.Process();
      proc.EnableRaisingEvents = false;
      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.FileName = "arp";
      proc.StartInfo.Arguments = "-s " + router + " FE:FD:00:00:00:00";
      proc.Start();
      proc.WaitForExit();

      SetHostname();
    }

    private static void DHCPSetupRouteAndArp() {
      while(GetTapAddress() == null) ;
        Thread.Sleep(1000);
      SetupRouteAndArp(
        DHCPCommon.StringToBytes(GetTapAddress(), '.'),
        DHCPCommon.StringToBytes(GetTapNetmask(), '.'));
    }

    private static void ParseResolvConf() {
      Nameservers = new ArrayList();
      FileStream file = new FileStream("/etc/resolv.conf",
        FileMode.OpenOrCreate, FileAccess.Read);
      StreamReader sr = new StreamReader(file);
      string temp = "", nameserver = "";
      while((temp = sr.ReadLine()) != null) {
        if(temp.StartsWith("nameserver")) {
          nameserver = temp.Substring(11, temp.Length - 11);
          if(nameserver != "127.0.0.1" && nameserver != "0.0.0.0" && nameserver != "")
            Nameservers.Add(nameserver);
        }
      }
      sr.Close();
      file.Close();
    }

    private static void SetHostname() {
      byte []ip_bytes = DHCPCommon.StringToBytes(IPAddr, '.');
      string hostname = "C";
      for(int i = 1; i < 4; i++) {
        if(ip_bytes[i] < 10)
          hostname += "00";
        else if(ip_bytes[i] < 100)
          hostname += "0";
        hostname += ip_bytes[i].ToString();
      }

      System.Diagnostics.Process proc = new System.Diagnostics.Process();
      proc.EnableRaisingEvents = false;
      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.FileName = "hostname";
      proc.StartInfo.Arguments = hostname;
      proc.Start();
      proc.WaitForExit();
    }

    private static void SetupTapDevice() {
      System.Diagnostics.Process proc = new System.Diagnostics.Process();
      proc.EnableRaisingEvents = false;
      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.FileName = "ifconfig";
      proc.StartInfo.Arguments = config.device + " " + config.StaticIP +
        " netmask " + config.StaticNetmask;
      proc.Start();
      proc.WaitForExit();
    }

/* End Linux Only */

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
        fs = new FileStream(configFile, FileMode.OpenOrCreate, 
          FileAccess.Write);
        serializer.Serialize(fs, config);
        fs.Close();
      }
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

    private static System.Net.IPAddress[] GetIPTAs() {
      ArrayList tas = new ArrayList();
      try {
	//we make a call to ifconfig here
	ArrayList addr_list = new ArrayList();
	System.Diagnostics.Process proc = new System.Diagnostics.Process();
	proc.EnableRaisingEvents = false;
	proc.StartInfo.RedirectStandardOutput = true;
	proc.StartInfo.UseShellExecute = false;
	proc.StartInfo.FileName = "ifconfig";
	
	proc.Start();
	proc.WaitForExit();
	
	StreamReader sr = proc.StandardOutput;
	while (true) {
	  string output = sr.ReadLine();
	  if (output == null) {
	    break;
	  }
	  output = output.Trim();
	  if (output.StartsWith("inet addr")) {
	    string[] arr = output.Split(' ');
	    if (arr.Length > 1) {
	      string[] s_arr = arr[1].Split(':');
	      if (s_arr.Length > 1) {
		System.Net.IPAddress ip = System.Net.IPAddress.Parse(s_arr[1]);
		Console.WriteLine("Discovering: {0}", ip);
		addr_list.Insert(0, ip);
	      }
	    }
	  }
	}
        foreach(System.Net.IPAddress a in addr_list) {
	  //first and foremost, test if it is a virtual IP
          IPAddress testIp = new IPAddress(a.GetAddressBytes());
          IPAddress temp = new IPAddress(IPAddr);
	  if (temp.Equals(testIp)) {
	    Console.WriteLine("Detected {0} as virtual Ip.", IPAddr);
	    continue;
	  }
          /**
           * We add Loopback addresses to the back, all others to the front
           * This makes sure non-loopback addresses are listed first.
           */
          if( System.Net.IPAddress.IsLoopback(a) ) {
            //Put it at the back
            tas.Add(a);
          }
          else {
            //Put it at the front
            tas.Insert(0, a);
          }
        }
      }
      catch(Exception x) {
        //If the hostname is not properly configured, we could wind
        //up here.  Just put the loopback address is:
        tas.Add(System.Net.IPAddress.Loopback);
      }
      return (System.Net.IPAddress[]) tas.ToArray(typeof(System.Net.IPAddress));
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
        System.Net.IPAddress.Parse(IPAddr), value,
        TAAuthorizer.Decision.Deny, TAAuthorizer.Decision.Allow);
      //local node
      AHAddress us = new AHAddress(GetHash(new IPAddress(IPAddr)));
      Console.WriteLine("Generated address: {0}", us);
      //AHAddress us = new AHAddress(new BigInteger(Int32.Parse(args[1])));
      Node tmp_node = new StructuredNode(us, config.brunet_namespace);

      //Where do we listen:
      System.Net.IPAddress[] tas = GetIPTAs();
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
        new IPAddress(IPAddr));
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

      System.Console.WriteLine("IPRouter starting up...");
      ether = new Ethernet(config.device, GetTapMAC(), "FE:FD:00:00:00:00");
      if (ether.Open() < 0) {
        Console.WriteLine("unable to set up the tap");
        return;
      }

      BrunetTransport brunet = null;
      RoutingTable routes = null;
      byte [] netmask = null;
      byte [] ip = null;

      if(config.IPConfig == "static")
      {
        SetupTapDevice();
        IPAddr = GetTapAddress();
        Netmask = GetTapNetmask();
        netmask = DHCPCommon.StringToBytes(Netmask, '.');
        ip = DHCPCommon.StringToBytes(IPAddr, '.');

        SetupRouteAndArp(ip, netmask);
        //setup Brunet node
        brunet = Start();
        //build a new routes table and populate it artificially
        routes = new RoutingTable();
      }
      else {
        ParseResolvConf();
        IPAddr = null;
        Netmask = null;
        DHCPClient.DHCPInit(config.DHCPServerIP);
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
        //write a primitive copy method
        byte [] buffer = packet;

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
            ether.SendPacket(returnPacket.packet);
        /* Do we have a new IP address, if so (re)start Brunet */
            ip = returnPacket.decodedPacket.yiaddr;
            string newAddress = DHCPCommon.BytesToString(ip, '.');
            (new Thread(DHCPSetupRouteAndArp)).Start();
            if(IPAddr == null || IPAddr != newAddress) {
              IPAddr = newAddress;
              Netmask = DHCPCommon.BytesToString(((DHCPOption) returnPacket.
                decodedPacket.options[1]).byte_value, '.');
              brunet = Start();
              routes = new RoutingTable();
            }
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
          buffer = IPPacketBuilder.BuildPacket(buffer,
            IPPacketBuilder.Protocol.IP_PACKET);
          if (debug) {
            Console.WriteLine("Brunet destination ID: {0}", target);
          }
          brunet.SendPacket(target, buffer);
        }
      }
    }
  }
}
