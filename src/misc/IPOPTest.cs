using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Threading;
using System.Net;

using Brunet;
using Brunet.Dht;

namespace Ipop {
  public class IPOPTest {
    private static Node _node;
    private static Dht _dht;
    private static RpcManager _rpc;

    public static void Main(string []args) {
      Init(args[0]);
      Test0();
      Test1();
      End();
    }

    /* This just pings a random node in the network, greedy makes sure
     * it only goes to a single node
     */
    public static void Test0() {
      Address target = IPOP_Common.GenerateAHAddress();
      AHSender s = new AHSender(_rpc.Node, target, AHPacket.AHOptions.Greedy);
      BlockingQueue q = new BlockingQueue();
      DateTime start = DateTime.Now;
      _rpc.Invoke(s, q, "sys:link.Ping", string.Empty);
      try {
        bool timedout;
        RpcResult res = (RpcResult) q.Dequeue(60000, out timedout);
        if(timedout) {
          throw new Exception("Operation took too long");
        }
        string result = (string) res.Result;
        if(result == string.Empty) {
          Console.WriteLine("Successful");
        }
        else {
          Console.WriteLine("Funnny :(");
        }
        Console.WriteLine("Received result from " + res.ResultSender);
        Console.WriteLine("Finished in... " + (DateTime.Now - start));
      }
      catch(Exception e) {
        Console.WriteLine(e);
      }
    }

    /* This asks a random location in the network for its connections we
     * don't supply any extra arguments to AHSender, so we expect two replies
     */
    public static void Test1() {
      Address target = IPOP_Common.GenerateAHAddress();
      AHSender s = new AHSender(_rpc.Node, target);
      BlockingQueue q = new BlockingQueue();
      _rpc.Invoke(s, q, "sys:link.GetNeighbors");
      try {
        while(true) {
          bool timedout;
          RpcResult res = (RpcResult) q.Dequeue(60000, out timedout);
          if(timedout) {
            throw new Exception("Operation took too long");
          }
          Hashtable ht = (Hashtable) res.Result;
          Console.WriteLine("Received result from " + res.ResultSender);
          foreach(DictionaryEntry de in ht) {
            Console.WriteLine(de.Key + " = " + de.Value);
          }
        }
      }
      catch (Exception e) {
        Console.WriteLine(e);
      }
    }

    public static void End() {
      _node.Disconnect();
    }

    public static void Init(string config_file) {
      OSDependent.DetectOS();

      //configuration file 
      IPRouterConfig config = IPRouterConfigHandler.Read(config_file, true);

      //local node
      _node = new StructuredNode(IPOP_Common.GenerateAHAddress(),
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
        _node.AddEdgeListener(el);
      }
      el = new TunnelEdgeListener(_node);
      _node.AddEdgeListener(el);

      //Here is where we connect to some well-known Brunet endpoints
      ArrayList RemoteTAs = new ArrayList();
      foreach(string ta in config.RemoteTAs)
        RemoteTAs.Add(TransportAddressFactory.CreateInstance(ta));
      _node.RemoteTAs = RemoteTAs;

      //following line of code enables DHT support inside the SimpleNode
      _dht = new Dht(_node, 3, 20);

      _node.Connect();
      Console.Error.WriteLine("Called Connect, I am " + _node.Address.ToString());
      _rpc = RpcManager.GetInstance(_node);

      while(!_dht.Activated) {
        Console.WriteLine("Not activated yet, sleeping 5 seconds.");
        System.Threading.Thread.Sleep(5000);
      }
    }
  }
}
