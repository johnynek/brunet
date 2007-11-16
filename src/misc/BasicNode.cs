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
  public class BasicNode {
    public static int Main(string []args)
    {
      // Get the config loaded
      IPRouterConfig config = null;
      try {
        config = IPRouterConfigHandler.Read(args[0]);
      }
      catch {
        Console.WriteLine("Invalid or missing configuration file.");
        return -1;
      }

      // Setup the enumeration of ip addresses if the user specifies it
      OSDependent.DetectOS();
      IEnumerable addresses = null;
      if(config.DevicesToBind != null) {
        addresses = OSDependent.GetIPAddresses(config.DevicesToBind);
      }

      int sleep = 60, sleep_min = 60, sleep_max = 3600;
      DateTime runtime = DateTime.UtcNow;

      // Keep creating new nodes no matter what!
      while(true) {
        try {
          StructuredNode node = new StructuredNode(IPOP_Common.GenerateAHAddress(),
                                                  config.brunet_namespace);
          // Set up end points
          Brunet.EdgeListener el = null;
          foreach(EdgeListener item in config.EdgeListeners) {
            int port = item.port;
            if (item.type =="tcp")
              el = new TcpEdgeListener(port, addresses);
            else if (item.type == "udp")
              el = new UdpEdgeListener(port, addresses);
            else
              throw new Exception("Unrecognized transport: " + item.type);
            node.AddEdgeListener(el);
          }
          el = new TunnelEdgeListener(node);
          node.AddEdgeListener(el);

          // Setup a list of well known end points
          if(config.RemoteTAs != null) {
            ArrayList RemoteTAs = new ArrayList();
            foreach(string ta in config.RemoteTAs)
              RemoteTAs.Add(TransportAddressFactory.CreateInstance(ta));
            node.RemoteTAs = RemoteTAs;
          }

          new Dht(node, 3, 20);
          Console.Error.WriteLine("I am connected to {0} as {1}",
                                  config.brunet_namespace, node.Address.ToString());
          node.disconnect_on_overload = true;
          node.ConnectReturnOnDisconnect();
        }
        catch (Exception e) {
          Console.Error.WriteLine(e);
        }
        finally {
          // Assist in garbage collection
          DateTime now = DateTime.UtcNow;
          Thread.Sleep(sleep);
          if(now - runtime > TimeSpan.FromSeconds(sleep_max)) {
            sleep *= 2;
            sleep = (sleep > sleep_max) ? sleep_max : sleep;
          }
          else {
            sleep /= 2;
            sleep = (sleep < sleep_min) ? sleep_min : sleep;
          }
        }
      }
    }
  }
}