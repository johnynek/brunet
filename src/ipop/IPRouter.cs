#define DAVID_DHCP

using System;
using Brunet;
using System.Text;
using System.Collections;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Mono.Security.Authenticode;

namespace Ipop {
  public class IPRouter {
    //if debugging information is needed
    private static bool debug;
    //the class modeling the ethernet;
    private static Ethernet ether;
    //device
    private static string device;
    //mac
    private static string macAddress;
    //ip address
    private static IPAddress ipAddress;

    //IP Init type
    private static string ipInit;

    //DHCP Server Address
    private static System.Net.IPAddress dhcpServer;

    //the namespace or realm we belong to
    private static string realm;

    //transport (tcp,udp)
    private static string transport;
    private static string configFile; 

    //local port where Brunet is running
    private static int local_port; 
    
    //remote TA
    private static ArrayList remoteTA;
    
    //local port for p1
    private static int p1_port;

    //status 0 = inactive, 1 = active
    private static int status;

    private static string NextLine(StreamReader sr) {
      while (true) {
	string line = sr.ReadLine();
	if (line == null) {
	  return null;
	}
	if (line.StartsWith("#")) {
	  continue;
	}
	Console.WriteLine(line);
	return line;
      }
      
    }
    private static void ReadConfiguration(string configFile) {
      FileStream fs = new FileStream(configFile, FileMode.Open, FileAccess.Read);
      StreamReader sr = new StreamReader(fs);
      //first argument is the network affiliation (ncn, scoop or anything else)
      string line = NextLine(sr);
      realm = line.Trim();
      
      //first argument is the transport type (udp, udp-as, tcp)
      line = NextLine(sr);
      transport = line.Trim();
      
      //next argument to read is the local brunet port
      line =  NextLine(sr);
      local_port = Int32.Parse(line.Trim());

      //number of remote TAs
      line = NextLine(sr);
      int n = Int32.Parse(line.Trim());
      remoteTA = new ArrayList();
      for (int i = 0; i < n; i++) {
          line = NextLine(sr);
	  remoteTA.Add(line);
      }

      //local port for p1
      line = NextLine(sr);
      device = line;

      //ipInit type
      line = NextLine(sr);
      ipInit = line;

      if(ipInit == "dhcp")
      {
        line = NextLine(sr);
        dhcpServer = System.Net.IPAddress.Parse(line);
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

    private static string GetTapAddress()
    {
      System.Diagnostics.Process proc = new System.Diagnostics.Process();
      proc.EnableRaisingEvents = false;
      proc.StartInfo.RedirectStandardOutput = true;
      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.FileName = "/sbin/ifconfig";
      proc.StartInfo.Arguments = device;
      proc.Start();
      proc.WaitForExit();

      StreamReader sr = proc.StandardOutput;
      sr.ReadLine();
      string output = sr.ReadLine();
      int point1 = output.IndexOf("inet addr:") + 10;
      int point2 = output.IndexOf("Bcast:") - 2 - point1;
      return output.Substring(point1, point2);
    }

    private static string GetTapMAC()
    {
      System.Diagnostics.Process proc = new System.Diagnostics.Process();
      proc.EnableRaisingEvents = false;
      proc.StartInfo.RedirectStandardOutput = true;
      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.FileName = "/sbin/ifconfig";
      proc.StartInfo.Arguments = device;
      proc.Start();
      proc.WaitForExit();

      StreamReader sr = proc.StandardOutput;
      string output = sr.ReadLine();
      return output.Substring(output.IndexOf("HWaddr") + 7, 17);
    }

    private static System.Net.IPAddress[] GetIPTAs()
    {
      ArrayList tas = new ArrayList();
      try {
	//we make a call to ifconfig here
	ArrayList addr_list = new ArrayList();
	System.Diagnostics.Process proc = new System.Diagnostics.Process();
	proc.EnableRaisingEvents = false;
	proc.StartInfo.RedirectStandardOutput = true;
	proc.StartInfo.UseShellExecute = false;
	proc.StartInfo.FileName = "/sbin/ifconfig";
	
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
	  bool virtualIp = false;
          IPAddress testIp = new IPAddress(a.GetAddressBytes());
	  if (ipAddress.Equals(testIp)) {
	    virtualIp = true;
	    Console.WriteLine("Detected {0} as virtual Ip.", ipAddress);
	    break;
	  }
	  if (virtualIp) {
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


    static BrunetTransport Start()
    {
      //Should be active now
      status = 1;
      //local node
      AHAddress us = new AHAddress(GetHash(ipAddress));
      Console.WriteLine("Generated address: {0}", us);
      //AHAddress us = new AHAddress(new BigInteger(Int32.Parse(args[1])));
      Node tmp_node = new StructuredNode(us, realm);

      //First argument is port, for local node
      int port = local_port;

      //Where do we listen:
      System.Net.IPAddress[] tas = GetIPTAs();
      if (transport.Equals("tcp")) { 
          tmp_node.AddEdgeListener(new TcpEdgeListener(port, tas));
      } else if (transport.Equals("udp")) {
          tmp_node.AddEdgeListener(new UdpEdgeListener(port, tas));
      }
      else if (transport.Equals("udp-as")) {
          tmp_node.AddEdgeListener(new ASUdpEdgeListener(port, tas));
      }
      else {
        throw new Exception("Unrecognized transport: " + transport);
      }
      //else if (transport.Equals("tls")) {
      //	   X509Certificate cert = X509Certificate.CreateFromCertFile("ssl.cer");
      //           PrivateKey priv = PrivateKey.CreateFromFile("ssl.pvk");
      //           tmp_node.AddEdgeListener(new TlsEdgeListener(priv, cert, port, tas));
      //       }

      IEnumerator ie = remoteTA.GetEnumerator();
      while(ie.MoveNext()) {
      string TA = (string) ie.Current;
      //Here is where we connect to; some well-known Brunet endpoint
      TransportAddress ta = new TransportAddress(TA);
      tmp_node.RemoteTAs.Add(ta);
      }

      //now try sending some messages out	
      //subscribe to the IP protocol packet

      IPPacketHandler ip_handler = new IPPacketHandler(ether, debug, ipAddress);
      tmp_node.Subscribe(AHPacket.Protocol.IP, ip_handler);


      tmp_node.Connect();
      System.Console.WriteLine("Called Connect");

      BrunetTransport brunet = new BrunetTransport(tmp_node);
      return brunet;
    }

    static void Main(string []args)
    {
      //configuration file 
      if (args.Length < 1) {
        Console.WriteLine("please specify the configuration file name...");
	return;
      }
      string configFile = args[0];
      ReadConfiguration(configFile);
      if (args.Length == 2) {
        debug = true;
      } else {
        debug = false;
      }

      System.Console.WriteLine("IPRouter starting up...");
      System.Console.WriteLine("local brunet port: " + local_port);
      System.Console.WriteLine("# remote TAs: " + remoteTA.Count);

      System.Console.WriteLine("ethernet device: " + device);
      macAddress = GetTapMAC();
      Console.WriteLine("ethernet mac: " + macAddress);
      ether = new Ethernet(device, macAddress, "FE:FD:00:00:00:00");
      if (ether.Open() < 0) {
        Console.WriteLine("unable to set up the tap");
        return;
      }

      BrunetTransport brunet = null;
      RoutingTable routes = null;

      if(ipInit == "static")
      {
        ipAddress = new IPAddress(GetTapAddress());
        //setup Brunet node
        brunet = Start();
        //build a new routes table and populate it artificially
        routes = new RoutingTable();
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
          Console.WriteLine("Outgoing {0} packet::IP src: {1}:{2}, IP dst: {3}:{4}", 
            protocol, srcAddr, srcPort, destAddr, destPort);
        }

        if(status == 1) {
          AHAddress target = (AHAddress) routes.SearchRoute(destAddr);
          if (target == null) {
            target = new AHAddress(GetHash(destAddr));
            routes.AddRoute(destAddr, target);
          }

          //build an IP packet
          buffer = IPPacketBuilder.BuildPacket(buffer, IPPacketBuilder.Protocol.IP_PACKET);
          if (debug) {
            Console.WriteLine("Brunet destination ID: {0}", target);
          }
          brunet.SendPacket(target, buffer);
        }

        if(srcPort == 68 && destPort == 67 && protocol == 17 && ipInit == "dhcp") {
          if (debug) {
            Console.WriteLine("DHCP Packet");
          }
          UdpClient dhcpClient = new UdpClient(dhcpServer.ToString(), 61234);
          dhcpClient.Send(buffer, buffer.Length);
          IPEndPoint dhcpEndPoint = new IPEndPoint(dhcpServer, 61234);
          buffer = new byte[512];
          buffer = dhcpClient.Receive(ref dhcpEndPoint);
          ether.SendPacket(buffer);
          Thread.Sleep(5000);
          //Assume it works for now...
          string new_address = GetTapAddress();
          if(ipAddress != null) {
            string old_address = ipAddress.ToString();
            Console.WriteLine("{0} {1}", old_address, new_address);
          }
          if(ipAddress == null || ipAddress.ToString() != new_address) {
            ipAddress = new IPAddress(new_address);
            brunet = Start();
            routes = new RoutingTable();
          }
        }
      }
    }
  }
}
