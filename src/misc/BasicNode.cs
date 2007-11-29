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

      int sleep = 60, sleep_min = 60, sleep_max = 3600;
      DateTime runtime = DateTime.UtcNow;

      // Keep creating new nodes no matter what!
      while(true) {
        try {
          if(config.NodeAddress == null) {
              String address = (IPOP_Common.GenerateAHAddress()).ToString();
              //remember addresss for future incarnations.
              config.NodeAddress = address.ToString();
          }
          StructuredNode node = Brunet_Common.CreateStructuredNode(config);
          Dht dht = Brunet_Common.RegisterDht(node);
          Brunet_Common.StartServices(node, dht, config);
          new IpopInformation(node, "BasicNode");

          Console.Error.WriteLine("I am connected to {0} as {1}.  Current time is {2}.",
            node.Realm, node.Address.ToString(), DateTime.UtcNow);
          node.DisconnectOnOverload = true;
          node.Connect();
          Brunet_Common.RemoveHandlers();
        }
        catch (Exception e) {
          Console.Error.WriteLine(e);
        }
        finally {
          // Assist in garbage collection
          DateTime now = DateTime.UtcNow;
          Console.Error.WriteLine("Going to sleep for {0} seconds. Current time is: {1}", sleep, now);
          Thread.Sleep(sleep * 1000);
          if(now - runtime < TimeSpan.FromSeconds(sleep_max)) {
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
