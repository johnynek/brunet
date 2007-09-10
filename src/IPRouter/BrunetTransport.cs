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
    public StructuredNode node;
//    NodeMapping node;
    IPPacketHandler ip_handler;
    public Dht dht;
    bool debug;
//    Thread Refresher;
    ArrayList edgeListeners;

    public BrunetTransport(Ethernet ether, string brunet_namespace, 
      NodeMapping node_map, EdgeListener []EdgeListeners, string [] DevicesToBind,
      ArrayList RemoteTAs, bool debug) {
//      this.node_map = node_map;
      this.debug = debug;

      //Static mapping
      //AHAddress us = new AHAddress(IPOP_Common.GetHash(node_map.ip));
      //Dht mapping
      AHAddress us = new AHAddress(IPOP_Common.StringToBytes(node_map.nodeAddress, ':'));
      Console.Error.WriteLine("Generated address: {0}", us);
      node = new StructuredNode(us, brunet_namespace);
//      Refresher = null;

      edgeListeners = new ArrayList();
      Brunet.EdgeListener el = null;

      foreach(EdgeListener item in EdgeListeners) {
        int port = Int32.Parse(item.port);
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
          if (item.type =="tcp")
            el = new TcpEdgeListener(port, OSDependent.GetIPAddresses(DevicesToBind));
          else if (item.type == "udp")
            el = new UdpEdgeListener(port, OSDependent.GetIPAddresses(DevicesToBind));
          else if (item.type == "udp-as")
            el = new ASUdpEdgeListener(port, OSDependent.GetIPAddresses(DevicesToBind), null);
          else
            throw new Exception("Unrecognized transport: " + item.type);
        }
        edgeListeners.Add(el);
        node.AddEdgeListener(el);
      }
      el = new TunnelEdgeListener(node);
      node.AddEdgeListener(el);

      //Here is where we connect to some well-known Brunet endpoints
      node.RemoteTAs = RemoteTAs;

      //now try sending some messages out 
      //subscribe to the IP protocol packet
      ip_handler = new IPPacketHandler(ether, debug, node_map);
      node.GetTypeSource(PType.Protocol.IP).Subscribe(ip_handler, null);
      dht = new Dht(node, 3, 20);

      node.Connect();
      System.Console.Error.WriteLine("Called Connect at time: {0}", DateTime.Now);
    }

    public void SendPacket(AHAddress target, MemBlock p) {
      ISender s = new AHExactSender(node, target);
      s.Send(new CopyList(PType.Protocol.IP, p));
    }


    public void Disconnect() {
      node.Disconnect();
    }

// We are not supporting this api at the moment
/*
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
      System.Console.Error.WriteLine("Closing Refresher Thread");
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
      if(node.ip != null && !node.ip.Equals(ip) && node.password != null) {
        node.password = null;
        node.ip = null;
      }
      string password = node.password;
      byte [] brunet_id = IPOP_Common.StringToBytes(node.nodeAddress, ':');

      if(DhtIP.GetIP(dht, node.ipop_namespace, ip.ToString(), 6048000, brunet_id, ref password)) {
        node.password = password;
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
      System.Console.Error.WriteLine("Updating TAAuthorizer with " + node.ip.ToString() + "/" + value);
      TAAuthorizer taAuth = new NetmaskTAAuthorizer(node.ip, value,
        TAAuthorizer.Decision.Deny, TAAuthorizer.Decision.None);
      foreach (Brunet.EdgeListener el in edgeListeners) {
        System.Console.Error.WriteLine("ERHERHEH" + el.ToString());
        el.TAAuth = taAuth;
      }
    }*/
  }
}
