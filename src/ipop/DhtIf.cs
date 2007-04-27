#define HACK

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
  public class DhtIf {
    public static void Main(string []args) {
      OSDependent.DetectOS();
      if (args.Length < 1) {
        Console.Error.WriteLine("please specify the SimpleNode configuration " + 
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
          else if (item.type == "tunnel")
            el = new TunnelEdgeListener(brunetNode);
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
          else if (item.type == "tunnel")
            el = new TunnelEdgeListener(brunetNode);

          else
            throw new Exception("Unrecognized transport: " + item.type);
        }
        brunetNode.AddEdgeListener(el);
      }

      //Here is where we connect to some well-known Brunet endpoints
      ArrayList RemoteTAs = new ArrayList();
      foreach(string ta in config.RemoteTAs)
        RemoteTAs.Add(TransportAddressFactory.CreateInstance(ta));
      brunetNode.RemoteTAs = RemoteTAs;



      //following line of code enables DHT support inside the SimpleNode
      FDht dht = null;
      if (config.dht_media == null || config.dht_media.Equals("disk")) {
        dht = new FDht(brunetNode, EntryFactory.Media.Disk, 3);
      } else if (config.dht_media.Equals("memory")) {
        dht = new FDht(brunetNode, EntryFactory.Media.Memory, 3);
      } 

      System.Console.Error.WriteLine("Calling Connect");

      brunetNode.Connect();

      DhtData data = DhtDataHandler.Read(args[1]);
      if(data.key == null || data.value == null || data.ttl == null) {
        Environment.Exit(1);
      }
      int ttl = Int32.Parse(data.ttl);
      while(true) {
        try {
          Console.Error.WriteLine("DATA:::Attempting Dht operation!");
          string password = DhtOp.Create(data.key, data.value, data.password, ttl, dht);
          if(password == null) {
            if(args.Length == 3) {
              Console.WriteLine("Fail");
            }
            else {
              Console.Error.WriteLine("DATA:::Dht operatin failed!");
            }
            Environment.Exit(1);
          }
          else if(password != data.password) {
            data.password = password;
            DhtDataHandler.Write(args[1], data);
          }
/* We exit if this was meant to try to create a data point */
          if(args.Length == 3) {
            Console.WriteLine("Pass");
            Environment.Exit(0);
          }
          Console.Error.WriteLine("DATA:::Dht operation succeeded, sleeping for " + (ttl / 2));
          System.Threading.Thread.Sleep((ttl / 2) * 1000);
        }
        catch(Exception) {
          Console.Error.WriteLine("DATA:::Dht operation failed, sleeping for 15 seconds and trying again.");
          System.Threading.Thread.Sleep(15000);
        }
      }
    }
  }
}
