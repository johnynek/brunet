using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;

using Brunet;
using Brunet.Dht;

/* The SimpleNode just works for a p2p router
 * (Doesn't generate or sink any packets)
 * Could sink; in case no route to destination is available!
 */
namespace Ipop {
  public class SimpleNode {
    public static void Main(string []args) {
      OSDependent.DetectOS();
      if (args.Length < 1) {
        Console.WriteLine("please specify the SimpleNode configuration " + 
          "file... ");
        Environment.Exit(0);
      }

      //configuration file 
      IPRouterConfig config = IPRouterConfigHandler.Read(args[0]);

      //local node
      Node brunetNode = new StructuredNode(IPOP_Common.GenerateAHAddress(),
        config.brunet_namespace);
      //Where do we listen 
      foreach(EdgeListener item in config.EdgeListeners) {
        int port = Int32.Parse(item.port);
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
/*          if (item.type =="tcp")
            el = new TcpEdgeListener(port, (IEnumerable) (new IPAddresses(config.DevicesToBind)), null);*/
          if (item.type == "udp")
            el = new UdpEdgeListener(port, OSDependent.GetIPAddresses(config.DevicesToBind));
/*          else if (item.type == "udp-as")
            el = new ASUdpEdgeListener(port, (IEnumerable) (new IPAddresses(config.DevicesToBind)), null);*/
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
      Dht dht = null;
      if (config.dht_media == null || config.dht_media.Equals("disk")) {
        dht = new Dht(brunetNode, EntryFactory.Media.Disk);
      } else if (config.dht_media.Equals("memory")) {
        dht = new Dht(brunetNode, EntryFactory.Media.Memory);
      }

      System.Console.WriteLine("Calling Connect");

      brunetNode.Connect();
      while(true) System.Threading.Thread.Sleep(1000*60*60);
    }
  }
}
