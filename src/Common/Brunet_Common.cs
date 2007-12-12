using Brunet;
using Brunet.Dht;
using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Ipop
{
  public class Brunet_Common
  {
    /**
     * Creates a StructuredNode for connectivity over Brunet.
     * @param config an IPRouterConfig detailing information for the StructuredNode
     */
    public static StructuredNode CreateStructuredNode(IPRouterConfig config)
    {
      AHAddress address = null;
      try {
        address = (AHAddress) AddressParser.Parse(config.NodeAddress);
      }
      catch {
        address = IPOP_Common.GenerateAHAddress();
      }

      StructuredNode node = new StructuredNode(address,
                                      config.brunet_namespace);

      IEnumerable addresses = null;
      if(config.DevicesToBind != null) {
        addresses = OSDependent.GetIPAddresses(config.DevicesToBind);
      }

      Brunet.EdgeListener el = null;
      foreach(EdgeListener item in config.EdgeListeners) {
        int port = item.port;
        if (item.type =="tcp") {
          try {
            el = new TcpEdgeListener(port, addresses);
          }
          catch {
            el = new TcpEdgeListener(0, addresses);
          }
        }
        else if (item.type == "udp") {
          try {
            el = new UdpEdgeListener(port, addresses);
          }
          catch {
            el = new UdpEdgeListener(0, addresses);
          }
        }
        else {
          throw new Exception("Unrecognized transport: " + item.type);
        }
        node.AddEdgeListener(el);
      }
      el = new TunnelEdgeListener(node);
      node.AddEdgeListener(el);

      ArrayList RemoteTAs = null;
      if(config.RemoteTAs != null) {
        RemoteTAs = new ArrayList();
        foreach(string ta in config.RemoteTAs) {
          RemoteTAs.Add(TransportAddressFactory.CreateInstance(ta));
        }
        node.RemoteTAs = RemoteTAs;
      }

      return node;
    }

    private static object sync = new object();
    private static DhtServer _ds = null;
    private static XmlRpcManagerServer _xrm = null;

    private static bool dht_reset = false;
    private static bool xml_reset = false;

    public static void StartServices(StructuredNode node, Dht dht, IPRouterConfig config)
    {
      if(config.RpcDht != null && config.RpcDht.Enabled) {
        lock(sync) {
          try {
            if(_ds == null) {
              _ds = new DhtServer(config.RpcDht.Port);
              dht_reset = true;
            }
            if(dht_reset) {
              _ds.Update(dht);
              dht_reset = false;
            }
          }
          catch (Exception e) {Console.WriteLine(e);}
          //catch{}
        }
      }
      if(config.XmlRpcManager != null && config.XmlRpcManager.Enabled) {
        lock(sync) {
          try {
            if(_xrm == null) {
              _xrm = new XmlRpcManagerServer(config.XmlRpcManager.Port);
              xml_reset = true;
            }
            if(xml_reset) {
              _xrm.Update(node);
              xml_reset = false;
            }
          }
          catch (Exception e) {Console.WriteLine(e);}
          //catch {}
        }
      }
    }

    public static Dht RegisterDht(StructuredNode node)
    {
      return new Dht(node, 3, 20);
    }

    public static void RemoveHandlers()
    {
      lock(sync) {
        if(_ds != null) {
          _ds.Stop();
          dht_reset = true;
        }
        if(_xrm != null) {
          _xrm.Stop();
          xml_reset = true;
        }
      }
    }
  }
}
