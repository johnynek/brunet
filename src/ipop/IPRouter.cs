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
  public class IPRouter 
  {
    //if debugging information is needed
    private static bool debug;
    //the class modeling the ethernet;
    private static Ethernet ether;
    //device
    private static string device;


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

    //IP addresses we are routing for
    private static ArrayList ipList;
    private static ArrayList macList;

    
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
      
      //first argument is the transport type (udp or tcp)
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

       // # of IP addresses
       line = NextLine(sr);
       int num = Int32.Parse(line.Trim());

       //list of IP addreses we route
       ipList = new ArrayList();
       macList = new ArrayList();
       for (int i = 0; i < num; i++) {
	 line = NextLine(sr);
	 IPAddress ip = new IPAddress(line);
	 ipList.Add(ip);
	 line = NextLine(sr);
	 Console.WriteLine("hello");
	 macList.Add(line);
       }

     }

    private static void SetupEthernet() {
      IEnumerator ie = macList.GetEnumerator();
      ie.MoveNext();
      String mac = (String) ie.Current; 
      ether = new Ethernet(device, mac, "FE:FD:00:00:00:00");
    }

    private static IPAddress GetMyAddress() {
      //the address is just a Hash of the IP address I am routing for
      IEnumerator ie = ipList.GetEnumerator();
      ie.MoveNext();
      IPAddress addr = (IPAddress) ie.Current;
      return addr;
    }
    private static BigInteger GenerateAddress() {
      //the address is just a Hash of the IP address I am routing for
      IEnumerator ie = ipList.GetEnumerator();
      ie.MoveNext();
      IPAddress addr = (IPAddress) ie.Current;
      BigInteger retval = GetHash(addr); 
      return retval;
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
	  IEnumerator ie = ipList.GetEnumerator();
	  while(ie.MoveNext()) {
	    IPAddress addr = (IPAddress) ie.Current;
	    IPAddress testIp = new IPAddress(a.GetAddressBytes());
	    if (addr.Equals(testIp)) {
	      virtualIp = true;
	      Console.WriteLine("Detected {0} as virtual Ip.", addr);
	      break;
	    }
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
       SetupEthernet();
       if (ether.Open() < 0) {
           Console.WriteLine("unable to set up the tap");
	   return;
       }

       //local node
       AHAddress us = new AHAddress(GenerateAddress());
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

       IPPacketHandler ip_handler = new IPPacketHandler(ether, debug, GetMyAddress());
       tmp_node.Subscribe(AHPacket.Protocol.IP, ip_handler);


       tmp_node.Connect();
       System.Console.WriteLine("Called Connect");

       //created a new node; now define a transport for the node
       BrunetTransport brunet = new BrunetTransport(tmp_node);
       //build a new routes table and populate it artificially
       RoutingTable routes = new RoutingTable();
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
	// 	for (int i = 0; i < packet.Length; i++) {
	// 	  buffer[i] = packet[i];
	// 	}
	IPAddress destAddr = IPPacketParser.DestAddr(buffer);
	IPAddress srcAddr = IPPacketParser.SrcAddr(buffer);
	if (debug) {
          Console.WriteLine("Outgoing packet::IP src: {0}, IP dst: {1}", srcAddr, destAddr);
	}
		
	AHAddress target = (AHAddress) routes.SearchRoute(destAddr);
	if (target == null) {
	  target = new AHAddress(GetHash(destAddr));
	  routes.AddRoute(destAddr, target);
	}
	//we have
	//build an IP packet
	buffer = IPPacketBuilder.BuildPacket(buffer, IPPacketBuilder.Protocol.IP_PACKET);
	if (debug) {
	  Console.WriteLine("Brunet destination ID: {0}", target);
	}
	brunet.SendPacket(target, buffer);
      }
    }
  }
}
