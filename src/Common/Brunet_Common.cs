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
          el = new TcpEdgeListener(port, addresses);
        }
        else if (item.type == "udp") {
          el = new UdpEdgeListener(port, addresses);
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
    public static void StartServices(StructuredNode node, Dht dht, IPRouterConfig config)
    {
      if(config.RpcDht != null && config.RpcDht.Enabled) {
        lock(sync) {
          try {
            if(_ds == null) {
              _ds = new DhtServer(config.RpcDht.Port);
            }
            if(_ds != null) {
              _ds.Update(dht);
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
            }
            if(_xrm != null) {
              RpcManager rpc = RpcManager.GetInstance(node);
              _xrm.Update(rpc);
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

    public static void DisconnectNode(StructuredNode node, bool disconnected)
    {
      if(!disconnected) {
        node.Disconnect();
      }
      if(_ds != null) {
        _ds.Stop();
      }
      if(_xrm != null) {
        _xrm.Stop();
      }
    }
  }
}