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
using System.Threading;

namespace Ipop {
  public class BrunetTransport {
    public Node brunetNode;
    NodeMapping node;
    IPPacketHandler ip_handler;
    public FDht dht;
    object sync;
    bool debug;
    Thread Refresher;

    public BrunetTransport(Ethernet ether, string brunet_namespace, 
      NodeMapping node, EdgeListener []EdgeListeners, string [] DevicesToBind,
      ArrayList RemoteTAs, bool debug, string dht_media ) {
      this.node = node;
      sync = new object();
      this.debug = debug;
      //local node

      AHAddress us = new AHAddress(IPOP_Common.GetHash(node.ip));
//Dht DHCP
//      AHAddress us = new AHAddress(IPOP_Common.StringToBytes(node.nodeAddress, ':'));
      Console.WriteLine("Generated address: {0}", us);
      brunetNode = new StructuredNode(us, brunet_namespace);
      Refresher = null;

      //Where do we listen:
      IPAddress[] tas = Routines.GetIPTAs(DevicesToBind);

#if IPOP_LOG
      string listener_log = "BeginListener::::";
#endif

      foreach(EdgeListener item in EdgeListeners) {
        int port = Int32.Parse(item.port);
        System.Console.WriteLine(port + " " + tas[0]);
        if (item.type =="tcp") {
            brunetNode.AddEdgeListener(new TcpEdgeListener(port));
        }
        else if (item.type == "udp") {
            brunetNode.AddEdgeListener(new UdpEdgeListener(port));
        }
        else if (item.type == "udp-as") {
            brunetNode.AddEdgeListener(new ASUdpEdgeListener(port));
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

    public void RefreshThread() {
      try {
        while(node.ip != null && node.password != null) {
          Thread.Sleep(604800);
          if(node.ip == null || node.password == null)
            break;
          Refresh();
        }
      }
      catch (Exception) {;}
      System.Console.WriteLine("Closing Refresher Thread");
      Refresher = null;
    }

    public bool Refresh() {
      return Update(node.ip.ToString());
    }

    public void InterruptRefresher() {
      if(Refresher != null)
        Refresher.Interrupt();
    }

    public bool Update(string ip) {
      String new_password;

      if(node.ip != null && !node.ip.Equals(ip) && node.password != null) {
        BlockingQueue [] queues = dht.DeleteF(
          Encoding.UTF8.GetBytes("dhcp:ipop_namespace:" + node.ipop_namespace +
            ":ip:" + node.ip.ToString()),
          node.password);
        BlockingQueue.ParallelFetch(queues, 0);
        node.password = null;
        node.ip = null;
      }

      string dht_key = "dhcp:ip:" + ip;
      byte [] brunet_id = IPOP_Common.StringToBytes(node.nodeAddress, ':');

      if(DhtIP.GetIP(dht, dht_key, node.password, 6048000, brunet_id, out new_password)) {
        node.password = new_password;
        node.ip = IPAddress.Parse(ip);
        if(Refresher == null)
          Refresher = new Thread(new ThreadStart(RefreshThread));
        return true;
      }

      node.password = null;
      node.ip = null;
      return false;
    }
  }
}
