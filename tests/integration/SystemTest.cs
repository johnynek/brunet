/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Threading;
using Brunet;
using Brunet.DistributedServices;

namespace Test {
  public class SystemTest {
    static SortedList nodes = new SortedList();
    static SortedList TakenPorts = new SortedList();

    static int network_size = 0;
    static int time_interval = -1;
    static int add_remove_interval = 0;
    static int dht_put_interval = 0;
    static int dht_get_interval = 0;
    static int add_remove_delta = 0;

    static string edge_type = "function";

    static Random rand = new Random();
    static string brunet_namespace = "testing";

    static bool dht_enabled = false;
    static bool discovery = false;

    public static void Main(string []args) {
      int starting_network_size = 10;
      brunet_namespace += rand.Next();

      int carg = 0;
      while(carg < args.Length) {
        String[] parts = args[carg++].Split('=');
        try {
          switch(parts[0]) {
            case "--n":
              starting_network_size = Int32.Parse(parts[1]);
              break;
            case "--td":
              time_interval = Int32.Parse(parts[1]);
              break;
            case "--ari":
              add_remove_interval = Int32.Parse(parts[1]);
              break;
            case "--ard":
              add_remove_delta = Int32.Parse(parts[1]);
              break;
            case "--dp":
              dht_enabled = true;
              dht_put_interval = Int32.Parse(parts[1]);
              break;
            case "--dg":
              dht_enabled = true;
              dht_get_interval = Int32.Parse(parts[1]);
              break;
            case "--et":
              edge_type = parts[1];
              if(edge_type != "udp" && edge_type != "tcp" && edge_type != "function") {
                throw new Exception();
              }
              break;
            case "--discovery":
              discovery = true;
              break;
            case "--help":
              PrintHelp();
              break;
            default:
              PrintHelp();
              break;
          }
        }
        catch {
          PrintHelp();
        }
      }

      Console.WriteLine("Initializing...");

      for(int i = 0; i < starting_network_size; i++) {
        Console.WriteLine("Setting up node: " + i);
        add_node(false);
      }

      Console.WriteLine("Done setting up...\n");
      Thread system_thread = null;
      if(time_interval > 0) {
        system_thread = new Thread(system);
        system_thread.IsBackground = true;
        system_thread.Start();
      }

      string command = String.Empty;
      Console.WriteLine("Type HELP for a list of commands.\n");
      while (command != "Q") {
        Console.Write("#: ");
        // Commands can have parameters separated by spaces
        string[] parts = Console.ReadLine().Split(' ');
        command = parts[0];

        if(command.Equals("C")) {
          check_ring();
        }
        else if(command.Equals("P")) {
          PrintConnections();
        }
        else if(command.Equals("M")) {
          Console.WriteLine("Memory Usage: " + GC.GetTotalMemory(true));
        }
        else if(command.Equals("G")) {
          if(dht_enabled) {
            NodeMapping nm = (NodeMapping) nodes.GetByIndex(rand.Next(0, nodes.Count));
            Dht dht = nm.Dht;
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
        }
        else if(command.Equals("CR")) {
          Crawl();
        }
        else if(command.Equals("A")) {
          add_node(true);
        }
        else if(command.Equals("R")) {
          remove_node(true);
        }
        else if(command.Equals("ST")) {
          int count = 1024;
          if(parts.Length > 1) {
            count = Int32.Parse(parts[1]);
          }
          SenderSpeedTest(count);
        }
        else if(command.Equals("HELP")) {
          Console.WriteLine("Commands: \n");
          Console.WriteLine("A - add a node");
          Console.WriteLine("R - remove a node");
          Console.WriteLine("C - check the ring using ConnectionTables");
          Console.WriteLine("P - Print connections for each node to the screen");
          Console.WriteLine("M - Current memory usage according to the garbage collector");
          Console.WriteLine("G - Retrieve total dht entries");
          Console.WriteLine("CR - Perform a crawl of the network using RPC");
          Console.WriteLine("ST - Speed test, parameter - integer - times to end data");
          Console.WriteLine("Q - Quit");
        }
        Console.WriteLine();
      }

      if(system_thread != null) {
        system_thread.Abort();
        system_thread.Join();
      }

      foreach(DictionaryEntry de in nodes) {
        Node node = ((NodeMapping) de.Value).Node;
        node.Disconnect();
      }
    }

    public static void system() {
      try {
        int interval = 1;
        while(true) {
          Thread.Sleep(time_interval * 1000);
          if(add_remove_interval != 0 && interval % add_remove_interval == 0) {
            Console.Error.WriteLine("System.Test::add / removing...");
            for(int i = 0; i < add_remove_delta; i++) {
              remove_node(false);
              add_node(false);
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
      catch (ThreadAbortException){}
      catch (Exception e){
        Console.WriteLine(e);
      }
    }

    private static void Crawl() {
      int count = 0, consistency = 0;
      NodeMapping nm = (NodeMapping) nodes.GetByIndex(0);
      Node lnode = nm.Node;
      Address rem_addr = lnode.Address, prev = null, first_left = null;
      bool failed = false;
      try {
        do {
          Console.WriteLine("Current address: " + rem_addr);
          ISender sender = new AHGreedySender(lnode, rem_addr);
          BlockingQueue q = new BlockingQueue();
          lnode.Rpc.Invoke(sender, q, "sys:link.GetNeighbors");
          RpcResult res = (RpcResult) q.Dequeue();
          Hashtable ht = (Hashtable) res.Result;

          Address tmp = AddressParser.Parse((String) ht["left"]);
          Address next = AddressParser.Parse((String) ht["right"]);
          if(prev != null && tmp.Equals(prev)) {
            consistency++;
          }
          else {
            first_left = tmp;
          }
          if(next == lnode.Address && first_left == rem_addr) {
            consistency++;
          }
          prev = rem_addr;
          rem_addr = next;
          q.Close();
          count++;
        } while((rem_addr != lnode.Address) && (count < nodes.Count));
      }
      catch(Exception e) {
        failed = true;
        Console.WriteLine("Crawl failed due to exception...");
        Console.WriteLine(e);
      }
      if(!failed) {
        if(count != nodes.Count) {
          Console.WriteLine("Crawl failed due to missing nodes!");
          Console.WriteLine("Expected nodes: {0}, found: {1}.", nodes.Count, count);
        }
        else if(consistency != count) {
          Console.WriteLine("Crawl failed due to bad consistency!");
          Console.WriteLine("Expected consistency: {0}, actual: {1}.", count, consistency);
        }
        else {
          Console.WriteLine("Crawl succeeded!");
        }
      }
    }

    //Sets up and performs the SenderSpeedTest
    private static void SenderSpeedTest(int max_count) {
      SenderReceiver sender = new SenderReceiver((NodeMapping) nodes.GetByIndex(0), max_count);
      NodeMapping rem_nm = (NodeMapping) nodes.GetByIndex(1);
      SenderReceiver receiver = new SenderReceiver(rem_nm, max_count);

      Console.WriteLine("Total time: " + sender.Send(rem_nm.Node.Address));
    }

    // Not implemented yet!  Maybe we can get an independent study project to fill this out
    private static void RpcSpeedTest() {
    }

    // removes a node from the pool
    private static void remove_node(bool output) {
      int index = rand.Next(0, nodes.Count);
      NodeMapping nm = (NodeMapping) nodes.GetByIndex(index);
      if(output) {
        Console.WriteLine("Removing: " + nm.Node.Address);
      }
      nm.Node.Disconnect();
      TakenPorts.Remove(nm.Port);
      nodes.RemoveAt(index);
      network_size--;
    }

    // adds a node to the pool
    private static void add_node(bool output) {
      AHAddress address = new AHAddress(new RNGCryptoServiceProvider());
      Node node = new StructuredNode(address, brunet_namespace);
      NodeMapping nm = new NodeMapping();
      nm.Node = node;
      nodes.Add((Address) address, nm);

      nm.Port = rand.Next(1024, 65535);
      while(TakenPorts.Contains(nm.Port)) {
        nm.Port = rand.Next(1024, 65535);
      }

      EdgeListener el = null;
      if(edge_type.Equals("function")) {
        el = new FunctionEdgeListener(nm.Port, 0, null);
      }
      else if(edge_type.Equals("tcp")) {
        el = new TcpEdgeListener(nm.Port);
      }
      else if(edge_type.Equals("udp")) {
        el = new UdpEdgeListener(nm.Port);
      }
      node.AddEdgeListener(el);

      if(!discovery) {
        ArrayList RemoteTAs = new ArrayList();
        for(int i = 0; i < 5 && i < TakenPorts.Count; i++) {
          int rport = (int) TakenPorts.GetByIndex(rand.Next(0, TakenPorts.Count));
          RemoteTAs.Add(TransportAddressFactory.CreateInstance("brunet." + edge_type + "://127.0.0.1:" + rport));
        }
        node.RemoteTAs = RemoteTAs;
      }
      TakenPorts[nm.Port] = nm.Port;

      if(dht_enabled) {
        nm.Dht = new Dht(node, 3);
      }

      if(output) {
        Console.WriteLine("Adding: " + nm.Node.Address);
      }
      (new Thread(node.Connect)).Start();
      network_size++;
    }

    // Each nodes dht places a piece of data into the same dht key
    private static void dht_put() {
      foreach(DictionaryEntry de in nodes) {
        NodeMapping nm = (NodeMapping) de.Value;
        Dht dht = nm.Dht;
        Node node = nm.Node;
        if(!dht.Activated)
          continue;
        Channel returns = new Channel();
        dht.AsPut("tester", node.Address.ToString(), 2 * time_interval * dht_put_interval, returns);
      }
    }

    // Performs network_size / 25 random dht gets
    private static void dht_get() {
      for(int i = 0; i < network_size / 25; i++) {
        int index = rand.Next(0, network_size);
        Dht dht = ((NodeMapping) nodes.GetByIndex(index)).Dht;
        if(!dht.Activated)
          continue;
        Channel returns = new Channel();
        dht.AsGet("tester", returns);
      }
    }

    // Performs a crawl of the network using the ConnectionTable of each node.
    private static bool check_ring() {
      Console.WriteLine("Checking ring...");
      Address start_addr = (Address) nodes.GetKeyList()[0];
      Address curr_addr = start_addr;

      for (int i = 0; i < network_size; i++) {
        Node node = ((NodeMapping) nodes[curr_addr]).Node;
        ConnectionTable con_table = node.ConnectionTable;

        Connection con = null;
        try {
          con = con_table.GetLeftStructuredNeighborOf((AHAddress) curr_addr);
        }
        catch {}

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

        Connection lc = null;
        try {
          Node tnode = ((NodeMapping)nodes[next_addr]).Node;
          lc = tnode.ConnectionTable.GetRightStructuredNeighborOf((AHAddress) next_addr);
        }
        catch {}

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

    private static void PrintConnections() {
      foreach(DictionaryEntry de in nodes) {
        Node node = ((NodeMapping)de.Value).Node;
        IEnumerable ie = node.ConnectionTable.GetConnections(ConnectionType.Structured);
        Console.WriteLine("Connections for Node: " + node.Address);
        foreach(Connection c in ie) {
          Console.WriteLine(c);
        }
        Console.WriteLine("==============================================================");
      }
    }

    private static void PrintHelp() {
      Console.WriteLine("Usage: SystemTest.exe --option[=value]...\n");
      Console.WriteLine("Options:");
      Console.WriteLine("--n=int - network size");
      Console.WriteLine("--et=str - edge type - udp, tcp, or function");
      Console.WriteLine("--td=int - time interval - base time between events");
      Console.WriteLine("--ari=int - add remove interval - td * ari is how often nodes are added/removed");
      Console.WriteLine("--ard=int - amount of nodes to add and remove at each td * ari");
      Console.WriteLine("--dg=int - dht get performed at every dg*td");
      Console.WriteLine("--dp=int - dht put performed at every dp*td");
      Console.WriteLine("--discovery - enables discovery");
      Console.WriteLine("--help - this menu");
      Console.WriteLine();
      Environment.Exit(0);
    }

    public class NodeMapping {
      public int Port;
      public Dht Dht;
      public Node Node;
    }

    /*
     * The sender test!  Node sends data to remote node and then waits for a
     * signal to acknowledge the completion of data sending.  Data sending
     * is complete when the remote end has received the end of transfer packet.
     * Just in case of packet loss, there is a timeout on the AutoResetEvent.
     * Due to overload on the system, the Sender will send 1024 packets, wait
     * for an ACK via an AutoResetEvent and then send another 1024.
     */
    public class SenderReceiver: IDataHandler {
      NodeMapping _nm;
      int count = 0;
      static AutoResetEvent _all_done = new AutoResetEvent(false);
      // Length of the data packet
      int length = 1024;
      int _max_count;
      // This is used for synchronization
      static readonly ICopyable eos;
      static readonly MemBlock eos_data;
      static readonly PType testing;

      static SenderReceiver() {
        testing = new PType("g");
        byte[] end_of_send = new byte[10];
        for(int i = 0; i < end_of_send.Length; i++) {
          end_of_send[i] = 254;
        }
        eos_data = MemBlock.Reference(end_of_send);
        eos = new CopyList(testing, eos_data);
      }

      public SenderReceiver(NodeMapping nm, int max_count) {
        _nm = nm;
        _nm.Node.GetTypeSource(testing).Subscribe(this, null);
        _max_count = max_count;
        _all_done.Reset();
      }

      // The the packet is an eos, set _all_done otherwise ignore it...
      // This could have a counter on it to match a counter on send to ensure
      // all packets arrive prior to calling _all_done...
      public void HandleData(MemBlock b, ISender return_path, object state) {
        if(b.Equals(eos_data)) {
          _all_done.Set();
        }
      }

      // This is called once per test and performs max_count as defined in the
      // constructor
      public int Send(Address rem_addr) {
        // Get a random set of data
        byte[]data = new byte[length];
        RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        rng.GetBytes(data);
        // Add the appropriate header to the data block
        testing.ToMemBlock().CopyTo(data, 0);
        MemBlock to_send = MemBlock.Reference(data);
        // Setup the sender
        ISender sender = new AHExactSender(_nm.Node, rem_addr);

        // perform the send test
        DateTime now = DateTime.UtcNow;
        for(int i = 1; i <= _max_count; i++) {
          // at every 102400 packet, we syncrhonize and force a GC otherwise
          // memory tends to explode
          if(i % (102400) != 0) {
            sender.Send(MemBlock.Reference(to_send));
          }
          else {
            sender.Send(eos);
            _all_done.WaitOne();
            Thread.Sleep(500);
            GC.Collect();
          }
        }

        // All done, now let's send and wait!
        sender.Send(eos);
        if(!_all_done.WaitOne(10000, false)) {
          Console.WriteLine("Failed");
        }
        return (int) (DateTime.UtcNow - now).TotalMilliseconds;
      }
    }
  }
}
