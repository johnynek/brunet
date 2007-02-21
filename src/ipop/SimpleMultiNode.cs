using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;

using Brunet;
using Brunet.Dht;


#if IPOP_LOG
using log4net;
using log4net.Config;
#endif


/* The SimpleNode just works for a p2p router
 * (Doesn't generate or sink any packets)
 * Could sink; in case no route to destination is available!
 */
namespace Ipop {
  public class SimpleMultiNode {
#if IPOP_LOG
    private static readonly log4net.ILog _log =
    log4net.LogManager.GetLogger(System.Reflection.MethodBase.
                                 GetCurrentMethod().DeclaringType);
#endif
    public static void Main(string []args) {
      OSDependent.DetectOS();
      
      if (args.Length < 1) {
        Console.WriteLine("please specify the SimpleNode configuration " + 
          "file... ");
        Environment.Exit(0);
      }
      //configuration file 
      IPRouterConfig config = IPRouterConfigHandler.Read(args[0]);

      if (args.Length < 2) {
        Console.WriteLine("please specify the number of p2p nodes."); 
        Environment.Exit(0);
      }
      int num_nodes = Int32.Parse(args[1]);
#if IPOP_LOG
      if (args.Length < 3) {
        Console.WriteLine("please specify the full path to the Logger " + 
          "configuration file...");
        Environment.Exit(1);
      }
      XmlConfigurator.Configure(new System.IO.FileInfo(args[2]));
#endif


      ArrayList node_list = new ArrayList();
      ArrayList dht_list = new ArrayList();
      for (int count = 0; count < num_nodes; count++) {
	try {
	  Node brunetNode = new StructuredNode(IPOP_Common.GenerateAHAddress(),
					       config.brunet_namespace);
	  //Where do we listen 
	  foreach(EdgeListener item in config.EdgeListeners) {
	    int port = Int32.Parse(item.port) + count;
	    Brunet.EdgeListener el = null;
	    if(config.DevicesToBind == null) {
	      if (item.type =="tcp")
		el = new TcpEdgeListener(port);
	      else if (item.type == "udp")
		el = new UdpEdgeListener(port);
	      else if (item.type == "udp-as")
		el = new ASUdpEdgeListener(port);
	      else
		throw new Exception("Unrecognized transport: " + item.type);
	    }
	    else {
	      if (item.type == "udp")
		el = new UdpEdgeListener(port, OSDependent.GetIPAddresses(config.DevicesToBind));
	      else
		throw new Exception("Unrecognized transport: " + item.type);
	    }
	    brunetNode.AddEdgeListener(el);
	  }

	  //Here is where we connect to some well-known Brunet endpoints
	  ArrayList RemoteTAs = new ArrayList();
	  foreach(string ta in config.RemoteTAs)
	    RemoteTAs.Add(new TransportAddress(ta));
	  brunetNode.RemoteTAs = RemoteTAs;



	  //following line of code enables DHT support inside the SimpleNode
	  FDht dht = null;
	  if (config.dht_media == null || config.dht_media.Equals("disk")) {
	    dht = new FDht(brunetNode, EntryFactory.Media.Disk, 3);
	  } else if (config.dht_media.Equals("memory")) {
	    dht = new FDht(brunetNode, EntryFactory.Media.Memory, 3);
	  }	

	  System.Console.WriteLine("Calling Connect");
	  brunetNode.Connect();
	  node_list.Add(brunetNode);
	  dht_list.Add(dht);
	  
	  
	} catch(Exception e) {
	  Console.Error.WriteLine("Unable to start node: " + count);
	}
      }
      while(true) System.Threading.Thread.Sleep(1000*60*60);
    }
  }
}
