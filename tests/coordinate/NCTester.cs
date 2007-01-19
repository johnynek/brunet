using System;
using System.Threading;

using System.Collections;
using System.Text;
using System.Security.Cryptography;

using Brunet;
using Brunet.Coordinate;

namespace Brunet.Coordinate {

  public class DhtAutoTester {
    public static void Main(string[] args) 
    {
      int net_size = Int32.Parse(args[0]);
      int base_port = Int32.Parse(args[1]);

      ArrayList node_list = new ArrayList();
      ArrayList nc_list = new ArrayList();
      Console.WriteLine("Building the network...");

      //create a network:
      for (int loop1 = 0; loop1 < net_size; loop1++) { 
	Console.WriteLine("Creating node: {0}", loop1);
	AHAddress addr = new AHAddress(new RNGCryptoServiceProvider());
	Console.WriteLine(addr);
	Node node = new StructuredNode(addr);
	node.AddEdgeListener(new UdpEdgeListener(base_port + loop1));
	
	for (int loop2 = 0; loop2 < loop1; loop2++) {
	  //we dont want to make us our own TA
	    int port = base_port + loop2;
	    string remoteTA = "gnucla.udp://localhost:" + port;
	    node.RemoteTAs.Add(new TransportAddress(remoteTA));
	}

	NCService nc  = new NCService();
	nc.InstallOnNode(node);
	node.Connect();

	node_list.Add(node);
	nc_list.Add(nc);

	//sleep 5 seconds
	Thread.Sleep(5000);
      }
      while(true) {
	//get a command an execute
	Console.Error.Write("enter command <print>");
	string ss = Console.ReadLine();
	if (ss.Equals("print")) {
	  for (int i = 0; i < node_list.Count; i++) {
	    Node n = (Node) node_list[i];
	    NCService nc = (NCService) nc_list[i];
	    NCService.VivaldiState state = nc.State;
	    Console.Error.WriteLine("node: {0}, position: {1}, error: {2}", n.Address, state.Position, state.WeightedError);
	  }
	}
      }
    }
  }
}
