using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Security.Cryptography;

namespace Brunet {
  public class BrokenRingTester {
    static SortedList nodes = new SortedList();
    static int network_size;
    static int base_port = 45111;
    static int max_range;
    static Random rand = new Random();
    static string brunet_namespace = "testing";
    static Hashtable taken_ports = new Hashtable();
    static ArrayList RemoteTA = new ArrayList();

    public static void Main(string []args) {
      if (args.Length < 1) {
        Console.WriteLine("please specify the number of p2p nodes."); 
        Environment.Exit(0);
      }
      else if(args.Length < 2) {
        Console.WriteLine("PLease specify the amount of iterations you would like to run.");
        Environment.Exit(0);
      }

      network_size = Int32.Parse(args[0]);
      max_range = network_size * 10;
      int count = Int32.Parse(args[1]);
      Console.WriteLine("Initializing...");

      for(int i = 0; i < max_range; i++) {
        RemoteTA.Add(TransportAddressFactory.CreateInstance("brunet.udp://localhost:" + (base_port + i)));
      }

      for(int i = 0; i < network_size; i++) {
        add_node();
      }

      int current_iter = 0;
      while(current_iter++ < count || count == 0) {
        Console.WriteLine("Iteration: " + current_iter);
        check_ring();
        for(int i = 0; i < 5; i++) {
          remove_node();
          while(true) {
            try {
              add_node();
              break;
            }
            catch {}
          }
        }
      }

      foreach(DictionaryEntry de in nodes) {
        Node node = (Node)de.Value;
        node.Disconnect();
      }
    }

    private static void remove_node() {
      int local_port = 0;
      while(!taken_ports.Contains(local_port = rand.Next(0, max_range) + base_port));
      Node node = (Node) taken_ports[local_port];
      node.Disconnect();
      nodes.RemoveAt(nodes.IndexOfValue(node));
      taken_ports.Remove(local_port);
    }

    private static void add_node() {
      int local_port = 0;
      while(taken_ports.Contains(local_port = rand.Next(0, max_range) + base_port));
      AHAddress address = new AHAddress(new RNGCryptoServiceProvider());
      Node node = new StructuredNode(address, brunet_namespace);
      ArrayList arr_tas = new ArrayList();
      for(int j = 0; j < max_range / 10; j++) {
        int remote_port = 0;
        do {
          remote_port = rand.Next(0, max_range) + base_port;
        } while(remote_port == local_port);
        PortTAAuthorizer port_auth = new PortTAAuthorizer(remote_port);
        arr_tas.Add(port_auth);
      }
      arr_tas.Add(new ConstantAuthorizer(TAAuthorizer.Decision.Allow));
      TAAuthorizer ta_auth = new SeriesTAAuthorizer(arr_tas);
      node.AddEdgeListener(new UdpEdgeListener(local_port, null, ta_auth));
      node.AddEdgeListener(new TunnelEdgeListener(node));
      node.RemoteTAs = RemoteTA;
      node.Connect();
      taken_ports[local_port] = node;
      nodes.Add((Address) address, node);
    }

    private static void check_ring() {
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
    }
  }
}
