using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;

using Brunet;
using Brunet.Dht;


/* The SimpleNode just works for a p2p router
* (Doesn't generate or sink any packets)
* Could sink; in case no route to destination is available!
*/
namespace Ipop {
  public class BrokenRingTester {
    public static void Main(string []args) {
      OSDependent.DetectOS();
      if (args.Length < 1) {
        Console.WriteLine("please specify the SimpleNode configuration " + 
          "file... ");
        Environment.Exit(0);
      }
      else if (args.Length < 2) {
        Console.WriteLine("please specify the number of p2p nodes."); 
        Environment.Exit(0);
      }
      else if (args.Length < 3) {
        Console.WriteLine("please specify the number of missing edges."); 
        Environment.Exit(0);
      }
      IPRouterConfig config = IPRouterConfigHandler.Read(args[0]);

      int node_count = Int32.Parse(args[1]) + 10;
      ArrayList addresses = new ArrayList();
      Hashtable address_to_node = new Hashtable();

      for (int count = 0; count < node_count; count++) { 
        AHAddress address = IPOP_Common.GenerateAHAddress();
        Node node = new StructuredNode(address, config.brunet_namespace);
        addresses.Add(address);
        address_to_node.Add(address, node);
      }

      int missing_count = Int32.Parse(args[2]);
      ArrayList missing_edges = new ArrayList();
      Random rand = new Random();
      for (int i = 0; i < missing_count; i++) {
        int idx = -1;
        do {
          idx = rand.Next(10, node_count);
          Console.WriteLine("Will drop a left edge on idx {0}: ", idx);
        } while (missing_edges.Contains(idx));
        missing_edges.Add(idx);
      }
      ArrayList sorted_addresses = (ArrayList) addresses.Clone();
      sorted_addresses.Sort();
      for (int idx = 0; idx < node_count; idx++) {
          AHAddress address = (AHAddress) sorted_addresses[idx];
          Node brunetNode = (Node) address_to_node[address];

          //Where do we listen 
          foreach(EdgeListener item in config.EdgeListeners) {
            int port = Int32.Parse(item.port) + idx;
            TAAuthorizer ta_auth = null;
            if (missing_edges.Contains(idx)) {
              int remote_port = Int32.Parse(item.port) + (idx + 1)%node_count;
              PortTAAuthorizer port_auth = new PortTAAuthorizer(remote_port);
              Console.WriteLine("Adding a port TA authorizer at: {0} for remote port: {1}", port, remote_port);
              ArrayList arr_tas = new ArrayList();
              arr_tas.Add(port_auth);
              arr_tas.Add(new ConstantAuthorizer(TAAuthorizer.Decision.Allow));
              ta_auth = new SeriesTAAuthorizer(arr_tas);
            }

            Brunet.EdgeListener el = null;
            Console.WriteLine("{0} : {1}", brunetNode.Address, port);
            if (ta_auth == null) {
              Console.WriteLine("No port TA authorizer for local port: {0}", port);
            }
            else {
              Console.WriteLine("Attaching TA authorizer for local port: {0}", port);
            }
            if(config.DevicesToBind == null) {
              if (item.type =="tcp")
                el = new TcpEdgeListener(port, null, ta_auth);
              else if (item.type == "udp")
                el = new UdpEdgeListener(port, null, ta_auth);
              else if (item.type == "udp-as")
                el = new ASUdpEdgeListener(port, null, ta_auth);
              else
                throw new Exception("Unrecognized transport: " + item.type);
            }
            else {
              if (item.type == "udp")
                el = new UdpEdgeListener(port, OSDependent.GetIPAddresses(config.DevicesToBind), ta_auth);
              else
                throw new Exception("Unrecognized transport: " + item.type);
            }
            brunetNode.AddEdgeListener(el);
          }

          brunetNode.AddEdgeListener(new TunnelEdgeListener(brunetNode));
          //Here is where we connect to some well-known Brunet endpoints
          ArrayList RemoteTAs = new ArrayList();
          foreach(string ta in config.RemoteTAs)
            RemoteTAs.Add(TransportAddressFactory.CreateInstance(ta));
          brunetNode.RemoteTAs = RemoteTAs;
      }
      Console.WriteLine("Starting nodes");
      foreach (Node node in address_to_node.Values) {
        try {
          System.Console.WriteLine("Calling Connect");
          node.Connect();
        } catch(Exception) {
          Console.WriteLine("Unable to start node: " + node.Address);
        }
      }
      //wait for 60 more seconds
      while(true) {
        Console.WriteLine("Going to sleep for 60 seconds.");
        System.Threading.Thread.Sleep(1000*60);
        bool complete = true;
        for (int idx = 0; idx < node_count; idx++) {
          Address addr1  = (Address) sorted_addresses[idx];
          Address addr2  = (Address) sorted_addresses[(idx + 1)%node_count];
          Node n1 = (Node) address_to_node[addr1];
          Node n2 = (Node) address_to_node[addr2];

          Connection con = n1.ConnectionTable.GetConnection(ConnectionType.Structured, n2.Address);
          if (con != null) {
            Console.WriteLine("Found connection (forward) at: {0} -> {1}", n1.Address, con);
          } else {
            complete = false;
            Console.WriteLine("Missing connection (forward) between: {0} and {1}", n1.Address, n2.Address);
          }
          con = n2.ConnectionTable.GetConnection(ConnectionType.Structured, n1.Address);
          if (con != null) {
            Console.WriteLine("Found connection (reverse) at: {0} -> {1}", n2.Address, con);
          } else {
            complete = false;
            Console.WriteLine("Missing connection (reverse) between: {0} and {1}", n2.Address, n1.Address);
          }	
        }
        if (complete) {
          Console.WriteLine("Ring status: complete");
        } else {
          Console.WriteLine("Ring status: incomplete");
        }
      }
    }
  }
}
