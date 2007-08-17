using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Threading;
using Brunet;
using Brunet.Dht;

namespace Test {
  public class SystemTest {
    static SortedList nodes = new SortedList();
    static Hashtable dhts = new Hashtable();
    static int network_size, base_time, add_remove_interval, dht_put_interval,
      dht_get_interval, max_range, add_remove_delta;
    static int base_port = 45111;
    static int DEGREE = 3;
    static Random rand = new Random();
    static string brunet_namespace = "testing";
    static Hashtable taken_ports = new Hashtable();
    static ArrayList RemoteTA = new ArrayList();

    public static void Main(string []args) {
      if (args.Length < 6) {
        Console.WriteLine("Input format %1 %2 %3 %4 %5 %6");
        Console.WriteLine("\t%1 = [network size]");
        Console.WriteLine("\t%2 = [base time]");
        Console.WriteLine("\t%3 = [add/remove interval]");
        Console.WriteLine("\t%4 = [add/remove delta]");
        Console.WriteLine("\t%5 = [dht put interval]");
        Console.WriteLine("\t%6 = [dht get interval]");
        Console.WriteLine("Specifying 3, 4, 5, 6 disables the event.");
        Environment.Exit(0);
      }

      int starting_network_size = Int32.Parse(args[0]);
      max_range = starting_network_size * 10;

      base_time = Int32.Parse(args[1]);
      add_remove_interval = Int32.Parse(args[2]);
      add_remove_delta = Int32.Parse(args[3]);
      dht_put_interval = Int32.Parse(args[4]);
      dht_get_interval = Int32.Parse(args[5]);
      Console.WriteLine("Initializing...");

      for(int i = 0; i < max_range; i++) {
        RemoteTA.Add(TransportAddressFactory.CreateInstance("brunet.udp://localhost:" + (base_port + i)));
      }

      for(int i = 0; i < starting_network_size; i++) {
        Console.WriteLine("Setting up node: " + i);
        add_node();
      }
      Console.WriteLine("Done setting up...\n");

      Thread system_thread = new Thread(system);
      system_thread.IsBackground = true;
      system_thread.Start();

      string command = String.Empty;
      while (command != "Q") {
        Console.WriteLine("Enter command (M/C/G/Q)");
        command = Console.ReadLine();
        if(command.Equals("C")) {
          check_ring();
        }
        else if(command.Equals("M")) {
          Console.WriteLine("Memory Usage: " + GC.GetTotalMemory(true));
        }
        else if(command.Equals("G")) {
          Node node = (Node) nodes.GetByIndex(rand.Next(0, network_size));
          Dht dht = (Dht) dhts[node];
          if(!dht.Activated)
            continue;
          BlockingQueue returns = new BlockingQueue();
          dht.AsGet("tester", returns);
          int count = 0;
          try {
            while(true) {
              returns.Dequeue();
              count++;
            }
          }
          catch {}
          Console.WriteLine("Count: " + count);
        }
        Console.WriteLine();
      }

      system_thread.Abort();

      int lcount = 0;
      foreach(DictionaryEntry de in nodes) {
        Console.WriteLine(lcount++);
        Node node = (Node)de.Value;
        node.Disconnect();
      }
    }

    public static void system() {
      try {
        int interval = 1;
        while(true) {
          Thread.Sleep(base_time * 1000);
          if(add_remove_interval != 0 && interval % add_remove_interval == 0) {
            Console.Error.WriteLine("System.Test::add / removing...");
            for(int i = 0; i < add_remove_delta; i++) {
              remove_node();
              while(true) {
                try {
                  add_node();
                  break;
                }
                catch (ThreadAbortException) { return; }
                catch {}
              }
            }
          }
          if(dht_put_interval != 0 && interval % dht_put_interval == 0) {
            Console.Error.WriteLine("System.Test::Dht put.");
            dht_put();
          }
          if(dht_get_interval != 0 && interval % dht_get_interval == 0) {
            Console.Error.WriteLine("System.Test::Dht get.");
            dht_get();
          }
          interval++;
        }
      }
      catch (Exception e){
       Console.WriteLine(e);
      }
    }

    private static void dht_put() {
      foreach(DictionaryEntry de in nodes) {
        Node node = (Node)de.Value;
        Dht dht = (Dht) dhts[node];
        if(!dht.Activated)
          continue;
        Channel returns = new Channel();
        dht.AsPut("tester", node.Address.ToString(), 2 * base_time * dht_put_interval, returns);
      }
    }

    private static void dht_get() {
      for(int i = 0; i < network_size / 25; i++) {
        int index = rand.Next(0, network_size);
        Node node = (Node) nodes.GetByIndex(index);
        Dht dht = (Dht) dhts[node];
        if(!dht.Activated)
          continue;
        Channel returns = new Channel();
        dht.AsGet("tester", returns);
      }
    }

    private static void remove_node() {
      int local_port = 0;
      while(!taken_ports.Contains(local_port = rand.Next(0, max_range) + base_port));
      Node node = (Node) taken_ports[local_port];
      node.Disconnect();
      nodes.RemoveAt(nodes.IndexOfValue(node));
      taken_ports.Remove(local_port);
      dhts.Remove(node);
      network_size--;
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
      dhts.Add(node, new Dht(node, DEGREE));
      network_size++;
    }

    private static bool check_ring() {
      Console.WriteLine("Checking ring...");
      Address start_addr = (Address) nodes.GetKeyList()[0];
      Address curr_addr = start_addr;

      for (int i = 0; i < network_size; i++) {
        Node node = (Node) nodes[curr_addr];
        ConnectionTable con_table = node.ConnectionTable;
        Connection con = con_table.GetLeftStructuredNeighborOf((AHAddress) curr_addr);
        if(con == null) {
          Console.WriteLine("Found no connection.");
          return false;
        }
        Console.WriteLine("Hop {2}\t Address {0}\n\t Connection to left {1}\n", curr_addr, con, i);
        Address next_addr = con.Address;

        if (next_addr == null) {
          Console.WriteLine("Found no connection.");
          return false;
        }

        Connection lc = ((Node)nodes[next_addr]).ConnectionTable.GetRightStructuredNeighborOf((AHAddress) next_addr);
        if( (lc == null) || !curr_addr.Equals(lc.Address)) {
          Address left_addr = lc.Address;
          Console.WriteLine(curr_addr + " != " + left_addr);
          Console.WriteLine("Right had edge, but left has no record of it!\n{0} != {1}", con, lc);
          return false;
        }
        else if(next_addr.Equals(start_addr) && i != network_size -1) {
          Console.WriteLine("Completed circle too early.  Only {0} nodes in the ring.",
                            (i + 1));
          return false;
        }
        curr_addr = next_addr;
      }

      if(start_addr.Equals(curr_addr)) {
        Console.WriteLine("Ring properly formed!");
        return true;
      }
      return false;
    }
  }
}
