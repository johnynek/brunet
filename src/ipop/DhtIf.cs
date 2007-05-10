#define HACK

using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Threading;

using Brunet;
using Brunet.Dht;

/* The SimpleNode just works for a p2p router
 * (Doesn't generate or sink any packets)
 * Could sink; in case no route to destination is available!
 */
namespace Ipop {
  public class DhtIf {
    static string []files;
    static FDht dht;
    static bool one_run;

    public static void Main(string []args) {
      OSDependent.DetectOS();
      if (args.Length < 1) {
        Console.Error.WriteLine("please specify the SimpleNode configuration " + 
          "file... ");
        Environment.Exit(0);
      }
      //configuration file 
      IPRouterConfig config = IPRouterConfigHandler.Read(args[0], true);

      //local node
      Node brunetNode = new StructuredNode(IPOP_Common.GenerateAHAddress(),
        config.brunet_namespace);
      //Where do we listen 
      Brunet.EdgeListener el = null;
      foreach(EdgeListener item in config.EdgeListeners) {
        int port = Int32.Parse(item.port);
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
      el = new TunnelEdgeListener(brunetNode);
      brunetNode.AddEdgeListener(el);

      //Here is where we connect to some well-known Brunet endpoints
      ArrayList RemoteTAs = new ArrayList();
      foreach(string ta in config.RemoteTAs)
        RemoteTAs.Add(TransportAddressFactory.CreateInstance(ta));
      brunetNode.RemoteTAs = RemoteTAs;



      //following line of code enables DHT support inside the SimpleNode
      dht = null;
      if (config.dht_media == null || config.dht_media.Equals("disk")) {
        dht = new FDht(brunetNode, EntryFactory.Media.Disk, 3);
      } else if (config.dht_media.Equals("memory")) {
        dht = new FDht(brunetNode, EntryFactory.Media.Memory, 3);
      } 

      System.Console.Error.WriteLine("Calling Connect");

      brunetNode.Connect();

      if(args.Length == 3 && args[2] == "one_run") {
        one_run = true;
        files = new string[1];
      }
      else {
        one_run = false;
        files = new string[args.Length - 1];
      }


      Array.Copy(args, 1, files, 0, files.Length);
      Thread []threads = new Thread [files.Length];
      for(int i = 0; i < files.Length; i++) {
        threads[i] = new Thread(DhtProcess);
        threads[i].Start(i);
      }

      for(int i = 0; i < files.Length; i++) {
        threads[i].Join();
      }
      brunetNode.Disconnect();
    }

    public static void  DhtProcess(object number) {
      string filename = files[(int) number];
      DhtData data = DhtDataHandler.Read(filename);
       // Create a thread for each of these...
      if(data.key == null || data.value == null || data.ttl == null) {
        return;
      }
      int ttl = Int32.Parse(data.ttl);
      while(true) {
        try {
          if(files.Length == 1) {
            Console.Error.WriteLine("DATA:::Attempting Dht operation!");
          }
          string password = DhtOp.Create(data.key, data.value, data.password, ttl, dht);
          if(password == null) {
            if(one_run && files.Length == 1) {
              Console.WriteLine("Fail");
            }
            else if(files.Length == 1) {
              Console.Error.WriteLine("DATA:::Dht operatin failed!");
            }
            return;
          }
          else if(password != data.password) {
            data.password = password;
            DhtDataHandler.Write(filename, data);
          }
/* We exit if this was meant to try to create a data point */
          if(one_run && files.Length == 1) {
            Console.WriteLine("Pass");
            return;
          }
          if(files.Length == 1) {
            Console.Error.WriteLine("DATA:::Dht operation succeeded, sleeping for " + (ttl / 2));
          }
          System.Threading.Thread.Sleep((ttl / 2) * 1000);
        }
        catch(Exception) {
          if(files.Length == 1) {
            Console.Error.WriteLine("DATA:::Dht operation failed, sleeping for 15 seconds and trying again.");
          }
          System.Threading.Thread.Sleep(15000);
        }
      }
    }
  }
}
