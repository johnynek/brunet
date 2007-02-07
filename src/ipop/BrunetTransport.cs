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
    ArrayList edgeListeners;

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

      edgeListeners = new ArrayList();

      foreach(EdgeListener item in EdgeListeners) {
        int port = Int32.Parse(item.port);
        Brunet.EdgeListener el = null;
        if(DevicesToBind == null) {
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
            el = new TcpEdgeListener(port, (IEnumerable) (new IPAddresses(DevicesToBind)), null);*/
          if (item.type == "udp")
            el = new UdpEdgeListener(port, OSDependent.GetIPAddresses(DevicesToBind));
/*          else if (item.type == "udp-as")
            el = new ASUdpEdgeListener(port, (IEnumerable) (new IPAddresses(DevicesToBind)), null);*/
          else
            throw new Exception("Unrecognized transport: " + item.type);
        }
        edgeListeners.Add(el);
        brunetNode.AddEdgeListener(el);
      }

      //Here is where we connect to some well-known Brunet endpoints
      brunetNode.RemoteTAs = RemoteTAs;

      //now try sending some messages out 
      //subscribe to the IP protocol packet
      ip_handler = new IPPacketHandler(ether, debug, node);
      brunetNode.Subscribe(AHPacket.Protocol.IP, ip_handler);

      if (dht_media == null || dht_media.Equals("disk")) {
        dht = new FDht(brunetNode, EntryFactory.Media.Disk, 3);
      } else if (dht_media.Equals("memory")) {
        dht = new FDht(brunetNode, EntryFactory.Media.Memory, 3);
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
        node.brunet.UpdateTAAuthorizer();
        return true;
      }

      node.password = null;
      node.ip = null;
      return false;
    }

    public void UpdateTAAuthorizer() {
      if(node.netmask == null)
        return;
      byte [] netmask = DHCPCommon.StringToBytes(node.netmask, '.');
      int nm_value = (netmask[0] << 24) + (netmask[1] << 16) +
        (netmask[2] << 8) + netmask[3];
      int value = 0;
      for(value = 0; value < 32; value++)
        if((1 << value) == (nm_value & (1 << value)))
      break;
      value = 32 - value;
      System.Console.WriteLine("Updating TAAuthorizer with " + node.ip.ToString() + "/" + value);
      TAAuthorizer taAuth = new NetmaskTAAuthorizer(node.ip, value,
        TAAuthorizer.Decision.Deny, TAAuthorizer.Decision.None);
      foreach (Brunet.EdgeListener el in edgeListeners)
        el.TAAuth = taAuth;
    }
  }
}
