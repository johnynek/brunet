/** The class implements the unreliable transport
    provided by Brunet; assuming that the node is connected to the network
 **/
using Brunet;
using Brunet.Dht;
using System.Net;
using System.Collections;
using System;
using System.Security.Cryptography;
using System.IO;
using System.Text;

namespace Ipop {
  public class BrunetTransport {
    public Node brunetNode;
    NodeMapping node;
    IPPacketHandler ip_handler;
    public FDht dht;
    object sync;
    bool debug;

    public BrunetTransport(Ethernet ether, string brunet_namespace, 
      NodeMapping node, EdgeListener []EdgeListeners, string [] DevicesToBind,
      ArrayList RemoteTAs, bool debug, string dht_media ) {
      this.node = node;
      sync = new object();
      this.debug = debug;
      //local node
      System.Console.WriteLine("test " + node.nodeAddress);
      AHAddress us = new AHAddress(IPOP_Common.StringToBytes(node.nodeAddress, ':'));
      Console.WriteLine("Generated address: {0}", us);
      brunetNode = new StructuredNode(us, brunet_namespace);

      //Where do we listen:
      IPAddress[] tas = Routines.GetIPTAs(DevicesToBind);

#if IPOP_LOG
      string listener_log = "BeginListener::::";
#endif

      foreach(EdgeListener item in EdgeListeners) {
        int port = Int32.Parse(item.port);
        System.Console.WriteLine(port + " " + tas[0]);
        if (item.type =="tcp") {
            brunetNode.AddEdgeListener(new TcpEdgeListener(port, tas));
        }
        else if (item.type == "udp") {
            brunetNode.AddEdgeListener(new UdpEdgeListener(port , tas));
        }
        else if (item.type == "udp-as") {
            brunetNode.AddEdgeListener(new ASUdpEdgeListener(port, tas));
        }
        else {
          throw new Exception("Unrecognized transport: " + item.type);
        }
      }

      //Here is where we connect to some well-known Brunet endpoints
      brunetNode.RemoteTAs = RemoteTAs;

#if IPOP_LOG
      _log.Debug("IGNORE");
      _log.Debug(tmp_node.Address + "::::" + DateTime.UtcNow.Ticks
        + "::::Connecting::::" + System.Net.Dns.GetHostName() + 
        "::::" + listener_log);
#endif

      //now try sending some messages out 
      //subscribe to the IP protocol packet
      ip_handler = new IPPacketHandler(ether, debug, node);
      brunetNode.Subscribe(AHPacket.Protocol.IP, ip_handler);

      if (dht_media == null || dht_media.Equals("disk")) {
        dht = new FDht(brunetNode, EntryFactory.Media.Disk, 5);
      } else if (dht_media.Equals("memory")) {
        dht = new FDht(brunetNode, EntryFactory.Media.Memory, 5);
      }

      lock(sync) {
        brunetNode.Connect();
        System.Console.WriteLine("Called Connect");
      }
    }

    //method to send a packet out on the network
    public void SendPacket(AHAddress target, byte[] packet) {
      AHPacket p = new AHPacket(0, 30,   brunetNode.Address,
        target, AHPacket.AHOptions.Exact,
        AHPacket.Protocol.IP, packet);
      brunetNode.Send(p);
    }

    public void Disconnect() {
      brunetNode.Disconnect();
    }

    public bool Update(string ip) {
/*      HashAlgorithm algo = new SHA1CryptoServiceProvider();
      byte[] bin_password = new byte[10];
      Random rand = new Random();
      rand.NextBytes(bin_password);

      string new_password = "SHA1:" + Convert.ToBase64String(bin_password);
      byte[] sha1_pass = algo.ComputeHash(bin_password);
      string new_hashed_password = "SHA1:" + Convert.ToBase64String(sha1_pass);

      if (node.password == null)
        node.password = new_password;

      byte[] dht_key = null;
      BlockingQueue [] queues = null;

      try {
        // Release our old IP Address
        if(node.ip != null && !node.ip.Equals(ip) && node.password != null) {
          dht_key = Encoding.UTF8.GetBytes("dhcp:ipop_namespace:" + 
          node.ipop_namespace + ":ip:" + node.ip.ToString());
          queues = dht.DeleteF(dht_key, node.password);
        }

        dht_key = Encoding.UTF8.GetBytes("dhcp:ip:" + ip);

        byte [] nodeAddress = IPOP_Common.StringToBytes(node.nodeAddress, ':');
        if(node.password == null)
          queues = dht.CreateF(dht_key, 86400, new_hashed_password, nodeAddress);
        else
          queues = dht.RecreateF(dht_key, node.password, 86400, new_hashed_password, nodeAddress);
      }
      catch (Exception) {
        System.Console.WriteLine("Somehow a program is supposed to know it doesn\'t have DHT enabled yet... how????");
        return false;
      }

      int max_results_per_queue = 2;
      int min_majority = 3;
      ArrayList []results = BlockingQueue.ParallelFetchWithTimeout(queues, 3000);

      //this method will return as soon as we have results available
      for (int i = 0; i < results.Length; i++) {
        bool success = true;
        ArrayList q_result = results[i];
        if (q_result.Count < max_results_per_queue)
          continue;

        foreach (RpcResult rpc_result in q_result) {
          try {
            if((bool) rpc_result.Result)
              continue;
            continue;
          }
          catch(AdrException) {
            success = false;
            continue;
          }
        }

        if (success)
          min_majority--;
      }

      if (min_majority > 0) {
        //we have not been able to acquire a majority, delete all keys
        queues = dht.DeleteF(dht_key, new_password);
        BlockingQueue.ParallelFetch(queues, 1);//1 reply is sufficient
        System.Console.WriteLine("Unable to get requested ip address " + ip);
        node.password = null;
        return false;
      }

      node.password = new_password;
      node.ip = IPAddress.Parse(ip);
      System.Console.WriteLine("Got the requested ip address " + ip);*/
      return true;
    }
  }
}
