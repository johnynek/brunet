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
using System.Diagnostics;

namespace Ipop {
  public class BrunetTransport {
    public StructuredNode node;
    IPPacketHandler ip_handler;
    public Dht dht;
    ArrayList edgeListeners;

    public BrunetTransport(Ethernet ether, string brunet_namespace, 
      NodeMapping node_map, EdgeListener []EdgeListeners, string [] DevicesToBind,
      ArrayList RemoteTAs) {
      AHAddress us = (AHAddress) node_map.address;
      ProtocolLog.WriteIf(IPOPLog.BaseLog, String.Format(
        "Generated address: {0}", us));
      node = new StructuredNode(us, brunet_namespace);

      edgeListeners = new ArrayList();
      Brunet.EdgeListener el = null;

      foreach(EdgeListener item in EdgeListeners) {
        int port = item.port;
        if(DevicesToBind == null) {
          if (item.type =="tcp")
            el = new TcpEdgeListener(port);
          else if (item.type == "udp")
            el = new UdpEdgeListener(port);
          else
            throw new Exception("Unrecognized transport: " + item.type);
        }
        else {
          if (item.type =="tcp")
            el = new TcpEdgeListener(port, OSDependent.GetIPAddresses(DevicesToBind));
          else if (item.type == "udp")
            el = new UdpEdgeListener(port, OSDependent.GetIPAddresses(DevicesToBind));
          else
            throw new Exception("Unrecognized transport: " + item.type);
        }
        edgeListeners.Add(el);
        node.AddEdgeListener(el);
      }
      el = new TunnelEdgeListener(node);
      node.AddEdgeListener(el);

      node.RemoteTAs = RemoteTAs;

      //subscribe to the IP protocol packet
      ip_handler = new IPPacketHandler(ether, node_map);
      node.GetTypeSource(PType.Protocol.IP).Subscribe(ip_handler, null);

      // Sets up the Dht to have 8 parallel nodes and a timeout of 20 seconds
      dht = new Dht(node, 3, 20);

      node.Connect();
      //Debug.WriteLine("Called Connect at time: {0}", DateTime.Now);
    }

    public void SendPacket(AHAddress target, MemBlock p) {
      ISender s = new AHExactSender(node, target);
      s.Send(new CopyList(PType.Protocol.IP, p));
    }

    public void Disconnect() {
      node.Disconnect();
    }

/*
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
