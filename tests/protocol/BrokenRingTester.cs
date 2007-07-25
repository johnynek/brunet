using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Security.Cryptography;

namespace Brunet {
  public class BrokenRingTester {
    public static void Main(string []args) {
      if (args.Length < 1) {
        Console.WriteLine("please specify the number of p2p nodes."); 
        Environment.Exit(0);
      }
      else if (args.Length < 2) {
        Console.WriteLine("please specify the number of missing edges."); 
        Environment.Exit(0);
      }

      int base_port = 54111;
      int network_size = Int32.Parse(args[0]);
      int missing_edges_count = Int32.Parse(args[1]);
      string brunet_namespace = "testing";
      SortedList nodes = new SortedList();
      Console.WriteLine("Initializing...");

      ArrayList RemoteTA = new ArrayList();
      for(int i = 0; i < network_size; i++) {
        RemoteTA.Add(TransportAddressFactory.CreateInstance("brunet.udp://localhost:" + (base_port + i)));
      }

      Random rand = new Random();
      for(int i = 0; i < network_size; i++) {
        AHAddress address = new AHAddress(new RNGCryptoServiceProvider());
        Node node = new StructuredNode(address, brunet_namespace);
        ArrayList arr_tas = new ArrayList();
        for(int j = 0; j < missing_edges_count; j++) {
          int remote_port = 0;
          do {
            remote_port = rand.Next(0, network_size - 1) + base_port;
          } while(remote_port == base_port + i);
          PortTAAuthorizer port_auth = new PortTAAuthorizer(remote_port);
          arr_tas.Add(port_auth);
        }
        arr_tas.Add(new ConstantAuthorizer(TAAuthorizer.Decision.Allow));
        TAAuthorizer ta_auth = new SeriesTAAuthorizer(arr_tas);
        node.AddEdgeListener(new UdpEdgeListener(base_port + i, null, ta_auth));
        node.AddEdgeListener(new TunnelEdgeListener(node));
        node.RemoteTAs = RemoteTA;
        node.Connect();
        nodes.Add((Address) address, node);
      }

      //wait for 60 more seconds
      int count = 0;
      while(true) {
        Console.WriteLine("Going to sleep for 5 seconds.");
        System.Threading.Thread.Sleep(5000);

        Console.WriteLine("Checking ring...");
        Address start_addr = (Address) nodes.GetKeyList()[0];
        Address curr_addr = start_addr;

        for (int i = 0; i < network_size; i++) {
          Node node = (Node) nodes[curr_addr];
          ConnectionTable con_table = node.ConnectionTable;
          Connection con = con_table.GetLeftStructuredNeighborOf((AHAddress) curr_addr);
          Console.WriteLine("Hop {2}\t Address {0}\n\t Connection to left {1}\n", curr_addr, con, i);
          Address next_addr = con.Address;

          if (next_addr == null) {
            Console.WriteLine("Found disconnection.");
            break;
          }

          Connection lc = ((Node)nodes[next_addr]).ConnectionTable.GetRightStructuredNeighborOf((AHAddress) next_addr);
          if( (lc == null) || !curr_addr.Equals(lc.Address)) {
            Address left_addr = lc.Address;
            Console.WriteLine(curr_addr + " != " + left_addr);
            Console.WriteLine("Right had edge, but left has no record of it!\n{0} != {1}", con, lc);
            break;
          }
          else if(next_addr.Equals(start_addr) && i != network_size -1) {
            Console.WriteLine("Completed circle too early.  Only {0} nodes in the ring.",
                              (i + 1));
            break;
          }
          curr_addr = next_addr;
        }
        count++;
        if(start_addr.Equals(curr_addr)) {
          Console.WriteLine("Ring properly formed!");
          Console.WriteLine("This only took .... {0} seconds", (count * 5));
          break;
        }
      }

      count = 0;
      while(true) {
        Console.WriteLine("Going to sleep for 5 seconds.");
        System.Threading.Thread.Sleep(5000);

        Console.WriteLine("Checking ring...");
        Address start_addr = (Address) nodes.GetKeyList()[0];
        Address curr_addr = start_addr;

        for (int i = 0; i < network_size; i++) {
          Node node = (Node) nodes[curr_addr];
          ConnectionTable con_table = node.ConnectionTable;
          Connection con = con_table.GetRightStructuredNeighborOf((AHAddress) curr_addr);
          Console.WriteLine("Hop {2}\t Address {0}\n\t Connection to right {1}\n", curr_addr, con, i);
          Address next_addr = con.Address;

          if (next_addr == null) {
            Console.WriteLine("Found disconnection.");
          }
          Connection left_con = ((Node)nodes[next_addr]).ConnectionTable.GetLeftStructuredNeighborOf((AHAddress) next_addr);
          if(left_con == null) {
            Console.WriteLine("Found disconnection.");
          }
          else if(!curr_addr.Equals(left_con.Address)) {
            Address left_addr = left_con.Address;
            Console.WriteLine(curr_addr + " != " + left_addr);
            Console.WriteLine("Left had edge, but right has no record of it! {0}", left_con);
            break;
          }
          else if(next_addr.Equals(start_addr) && i != network_size -1) {
            Console.WriteLine("Completed circle too early.  Only " + count + " nodes in the ring.");
            break;
          }
          curr_addr = next_addr;
        }
        count++;
        if(start_addr.Equals(curr_addr)) {
          Console.WriteLine("Ring properly formed!");
          Console.WriteLine("This only took .... {0} seconds", (count * 5));
          break;
        }
      }

      foreach(DictionaryEntry de in nodes) {
        Node node = (Node)de.Value;
        node.Disconnect();
      }
    }
  }
}
