using System;
using Brunet;
using System.IO;

/* The SimpleNode just works for a p2p router
 * (Doesn't generate or sink any packets)
 * Could sink; in case no route to destination is available!
 */
namespace PeerVM {
  public class SimpleNode
  {
    private static string realm;
    //transport (tcp,udp)
    private static string transport;

    //local port where Brunet is running
    private static int local_port; 
    
    //remote TA
    private static string remoteTA;
    
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
      //network namespace we belong to
      string line = NextLine(sr);
      realm = line;
      //first argument is the transport type (udp or tcp)
      line = NextLine(sr);
      transport = line.Trim();
      
      //next argument to read is the local brunet port
      line =  NextLine(sr);
      local_port = Int32.Parse(line.Trim());

      //next argument is the remote TA
      line = NextLine(sr);
      remoteTA = line;
     }

     public static void Main(string []args)
     {
       if (args.Length < 1) {
           Console.WriteLine("please specify the configuration file... ");
       }
       //configuration file 
       string configFile = args[0];
       ReadConfiguration(configFile);


       System.Console.WriteLine("IPRouter starting up...");
       System.Console.WriteLine("local brunet port: " + local_port);
       System.Console.WriteLine("remote TA: " + remoteTA);

       //Make a random address
       Random my_rand = new Random();
       byte[] address = new byte[Address.MemSize];
       my_rand.NextBytes(address);
       address[Address.MemSize -1] &= 0xFE;
       
       //local node
       Node tmp_node = new StructuredNode(new AHAddress(address), realm);

       //First argument is port, for local node
       int port = local_port;

       //Where do we listen:
       if (transport.Equals("tcp")) { 
           tmp_node.AddEdgeListener(new TcpEdgeListener(port));
       } else if (transport.Equals("udp")) {
           tmp_node.AddEdgeListener(new UdpEdgeListener(port));
       }

       //Here is where we connect to; some well-known Brunet endpoint
       TransportAddress ta = new TransportAddress(remoteTA);

       tmp_node.RemoteTAs.Add(ta);


       tmp_node.Connect();
       System.Console.WriteLine("Called Connect");
     }
  }
}

