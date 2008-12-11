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
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Threading;
using Brunet.Security;

namespace Brunet {
  public class SystemTest {
    static SortedList nodes = new SortedList();
    static SortedList TakenPorts = new SortedList();

    static int network_size = 0;
    static Random rand = new Random();
    static string brunet_namespace = "testing";
    static double broken = 0;
    static bool complete = false;

    static bool secure_edges = false;
    static bool secure_senders = false;
    static RSACryptoServiceProvider SEKey;
    static Certificate CACert;

    public static void Main(string []args) {
      int starting_network_size = 10;
      brunet_namespace += rand.Next();
      string dataset_filename = String.Empty;

      int carg = 0;
      while(carg < args.Length) {
        String[] parts = args[carg++].Split('=');
        try {
          switch(parts[0]) {
            case "--n":
              starting_network_size = Int32.Parse(parts[1]);
              break;
            case "--broken":
              broken = Double.Parse(parts[1]);
              break;
            case "--complete":
              complete = true;
              break;
            case "--se":
              secure_edges = true;
              SecureStartup();
              LinkProtocolState.EdgeVerifyMethod = EdgeVerify.AddressInSubjectAltName;
              break;
            case "--ss":
              secure_senders = true;
              SecureStartup();
              break;
            case "--dataset":
              dataset_filename = parts[1];
              break;
            case "--help":
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

      if(dataset_filename != String.Empty) {
        List<List<int>> latency = new List<List<int>>();
        using(StreamReader fs = new StreamReader(new FileStream(dataset_filename, FileMode.Open))) {
          string line = null;
          while((line = fs.ReadLine()) != null) {
            string[] points = line.Split(' ');
            List<int> current = new List<int>(points.Length);
            foreach(string point in points) {
              int val;
              if(!Int32.TryParse(point, out val)) {
                continue;
              }
              current.Add(Int32.Parse(point));
            }
            latency.Add(current);
          }
        }
        starting_network_size = latency.Count;
        FunctionEdgeListener.LatencyMap = latency;
      }

      for(int i = 0; i < starting_network_size; i++) {
        Console.WriteLine("Setting up node: " + i);
        add_node(false);
      }

      Console.WriteLine("Done setting up...\n");

      if(complete) {
        DateTime start = DateTime.UtcNow;
        while(!Crawl(secure_senders, false)) {
          RunStep();
        }
        Console.WriteLine("It took {0} to complete the ring", DateTime.UtcNow - start);
        return;
      }

      string command = String.Empty;
      Console.WriteLine("Type HELP for a list of commands.\n");
      while (command != "Q") {
        bool secure = false;
        Console.Write("#: ");
        // Commands can have parameters separated by spaces
        string[] parts = Console.ReadLine().Split(' ');
        command = parts[0];

        try {
          switch(command) {
            case "C":
              check_ring();
              break;
            case "P":
              PrintConnections();
              break;
            case "M":
              Console.WriteLine("Memory Usage: " + GC.GetTotalMemory(true));
              break;
            case "CR":
              Crawl(true, secure);
              break;
            case "SCR":
              secure = true;
              goto case "CR";
            case "A2A":
              AllToAll(secure);
              break;
            case "SA2A":
              secure = true;
              goto case "A2A";
            case "A":
              add_node(true);
              break;
            case "D":
              remove_node(true, false);
              break;
            case "R":
              remove_node(true, false);
              break;
            case "RUN":
              int steps = (parts.Length >= 2) ? Int32.Parse(parts[1]) : 0;
              if(steps > 0) {
                RunSteps(steps);
              } else {
                RunStep();
              }
              break;
            case "Q":
              break;
            case "H":
              Console.WriteLine("Commands: \n");
              Console.WriteLine("A - add a node");
              Console.WriteLine("R - remove a node");
              Console.WriteLine("C - check the ring using ConnectionTables");
              Console.WriteLine("P - Print connections for each node to the screen");
              Console.WriteLine("M - Current memory usage according to the garbage collector");
              Console.WriteLine("G - Retrieve total dht entries");
              Console.WriteLine("CR - Perform a crawl of the network using RPC");
              Console.WriteLine("ST - Speed test, parameter - integer - times to end data");
              Console.WriteLine("CM - ManagedCO test");
              Console.WriteLine("Q - Quit");
              break;
            default:
              Console.WriteLine("Invalid command");
              break;
          }
        } catch(Exception e) {
          Console.WriteLine("Error: " + e);
        }
        Console.WriteLine();
      }

      foreach(DictionaryEntry de in nodes) {
        Node node = ((NodeMapping) de.Value).Node;
        node.Disconnect();
      }
    }

    protected static void AllToAll(bool secure) {
      AllToAllHelper a2ah = new AllToAllHelper(nodes, secure);
      a2ah.Start();
      while(a2ah.Done == 0) {
        RunStep();
      }
    }

    protected static bool Crawl(bool log, bool secure) {
      NodeMapping nm = (NodeMapping) nodes.GetByIndex(0);
      BrunetSecurityOverlord bso = null;
      if(secure) {
        bso = nm.BSO;
      }

      CrawlHelper ch = new CrawlHelper(nm.Node, nodes.Count, bso, log);
      ch.Start();
      while(ch.Done == 0) {
        RunStep();
      }

      return ch.Success;
    }

    protected static void RunSteps(int cycles) {
      long cycle = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
      long diff = cycle + cycles;
      cycle = BrunetTimer.Minimum / TimeSpan.TicksPerMillisecond;

      System.DateTime last = System.DateTime.UtcNow;
      while(diff > cycle) {
        System.DateTime now = System.DateTime.UtcNow;
        if(last.AddSeconds(5) < now) {
          last = now;
          Console.WriteLine(now + ": " + DateTime.UtcNow);
        }
        Brunet.DateTime.SetTime(cycle);
        cycle = BrunetTimer.Run() / TimeSpan.TicksPerMillisecond;
      }
    }

    protected static void RunStep() {
      long next = BrunetTimer.Minimum;
      Brunet.DateTime.SetTime(next / TimeSpan.TicksPerMillisecond);
      BrunetTimer.Run();
    }

    public class AllToAllHelper {
      protected long _total_latency;
      protected long _count;
      protected SortedList _nodes;
      protected int _done;
      protected long _waiting_on;
      public int Done { get { return _done; } }
      protected long _start_time;
      protected object _sync;
      protected bool _secure;

      public AllToAllHelper(SortedList nodes, bool secure) {
        _nodes = nodes;
        _count = 0;
        _total_latency = 0;
        _waiting_on = 0;
        _start_time = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        _done = 0;
        _sync = new object();
        _secure = secure;
      }

      public void Callback(object o, EventArgs ea) {
        Channel q = o as Channel;
        try {
          RpcResult res = (RpcResult) q.Dequeue();
          int result = (int) res.Result;
          if(result != 0) {
            throw new Exception(res.Result.ToString());
          }

          _total_latency += (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond) - _start_time;
        } catch(Exception e) {
          Console.WriteLine(e);
        }
        if(Interlocked.Decrement(ref _waiting_on) == 0) {
          Interlocked.Exchange(ref _done, 1);
          Console.WriteLine("Performed {0} tests on {1} nodes", _count, _nodes.Count);
          Console.WriteLine("Latency avg: {0}", _total_latency / _count);
          DateTime start = new DateTime(_start_time * TimeSpan.TicksPerMillisecond);
          Console.WriteLine("Finished in: {0}", (DateTime.UtcNow - start));
        }
      }

      public void Start() {
        foreach(NodeMapping nm_from in _nodes.Values) {
          foreach(NodeMapping nm_to in _nodes.Values) {
            if(nm_from == nm_to) {
              continue;
            }

            ISender sender = null;
            if(_secure) {
              sender = nm_from.BSO.GetSecureSender(nm_to.Node.Address);
            } else {
              sender = new AHGreedySender(nm_from.Node, nm_to.Node.Address);
            }

            Channel q = new Channel(1);
            q.CloseEvent += Callback;
            try {
              nm_from.Node.Rpc.Invoke(sender, q, "sys:link.Ping", 0);
              lock(_sync) {
                _count++;
                _waiting_on++;
              }
            } catch(Exception e) {
              Console.WriteLine(e);
            }
          }
        }
      }
    }

    public class CrawlHelper {
      protected int _count;
      protected Hashtable _crawled;
      protected DateTime _start;
      protected Node _node;
      protected int _done;
      public int Done { get { return _done; } }
      protected int _consistency;
      protected bool _log;
      protected Address _first_left;
      protected Address _previous;
      public bool Success { get { return _crawled.Count == _count; } }
      protected BrunetSecurityOverlord _bso;

      public CrawlHelper(Node node, int count, BrunetSecurityOverlord bso, bool log) {
        _count = count;
        _node = node;
        Interlocked.Exchange(ref _done, 0);
        _crawled = new Hashtable(count);
        _log = log;
        _bso = bso;
      }

      protected void CrawlNext(Address addr) {
        bool finished = false;
        if(_log && _crawled.Count < _count) {
          Console.WriteLine("Current address: " + addr);
        }
        if(_crawled.Contains(addr)) {
          finished = true;
        } else {
          _crawled.Add(addr, true);
          try {
            ISender sender = null;
            if(_bso != null) {
              sender = _bso.GetSecureSender(addr);
            } else {
              sender = new AHGreedySender(_node, addr);
            }

            Channel q = new Channel(1);
            q.CloseEvent += CrawlHandler;
            _node.Rpc.Invoke(sender, q, "sys:link.GetNeighbors");
          } catch(Exception e) {
            if(_log) {
              Console.WriteLine("Crawl failed" + e);
            }
            finished = true;
          }
        }

        if(finished) {
          Interlocked.Exchange(ref _done, 1);
          if(_log) {
            Console.WriteLine("Crawl stats: {0}/{1}", _crawled.Count, _count);
            Console.WriteLine("Consistency: {0}/{1}", _consistency, _crawled.Count);
            Console.WriteLine("Finished in: {0}", (DateTime.UtcNow - _start));
          }
        }
      }

      public void Start() {
        _start = DateTime.UtcNow;
        CrawlNext(_node.Address);
      }

      protected void CrawlHandler(object o, EventArgs ea) {
        Address addr = _node.Address;
        Channel q = (Channel) o;
        try {
          RpcResult res = (RpcResult) q.Dequeue();
          Hashtable ht = (Hashtable) res.Result;

          Address left = AddressParser.Parse((String) ht["left"]);
          Address next = AddressParser.Parse((String) ht["right"]);
          Address current = AddressParser.Parse((String) ht["self"]);
          if(left.Equals(_previous)) {
            _consistency++;
          } else if(_previous == null) {
            _first_left = left;
          }

          if(current.Equals(_first_left) && _node.Address.Equals(next)) {
            _consistency++;
          }

          _previous = current;
          addr = next;
        } catch(Exception e) {
          if(_log) {
            Console.WriteLine("Crawl failed due to exception...");
            Console.WriteLine(e);
          }
        }
        CrawlNext(addr);
      }
    }

    // removes a node from the pool
    protected static void remove_node(bool output, bool cleanly) {
      int index = rand.Next(0, nodes.Count);
      NodeMapping nm = (NodeMapping) nodes.GetByIndex(index);
      if(output) {
        Console.WriteLine("Removing: " + nm.Node.Address);
      }
      if(cleanly) {
        nm.Node.Disconnect();
      } else {
        nm.Node.Abort();
      }
      TakenPorts.Remove(nm.Port);
      nodes.RemoveAt(index);
      network_size--;
    }

    public static void SecureStartup() {
      if(SEKey != null) {
        return;
      }
      SEKey = new RSACryptoServiceProvider();
      byte[] blob = SEKey.ExportCspBlob(false);
      RSACryptoServiceProvider rsa_pub = new RSACryptoServiceProvider();
      rsa_pub.ImportCspBlob(blob);
      CertificateMaker cm = new CertificateMaker("United States", "UFL", 
          "ACIS", "David Wolinsky", "davidiw@ufl.edu", rsa_pub,
          "brunet:node:abcdefghijklmnopqrs");
      Certificate cert = cm.Sign(cm, SEKey);
      CACert = cert;
    }

    // adds a node to the pool
    protected static void add_node(bool output) {
      AHAddress address = new AHAddress(new RNGCryptoServiceProvider());
      Node node = new StructuredNode(address, brunet_namespace);
      NodeMapping nm = new NodeMapping();
      nm.Node = node;
      nodes.Add((Address) address, nm);

      nm.Port = TakenPorts.Count;
      while(TakenPorts.Contains(nm.Port)) {
        nm.Port = rand.Next(0, 65535);
      }

      TAAuthorizer auth = null;
      if(broken != 0) {
        auth = new BrokenTAAuth(broken);
      }

      EdgeListener el = new FunctionEdgeListener(nm.Port, 0, auth, true);

      if(secure_edges || secure_senders) {
        byte[] blob = SEKey.ExportCspBlob(true);
        RSACryptoServiceProvider rsa_copy = new RSACryptoServiceProvider();
        rsa_copy.ImportCspBlob(blob);

        CertificateMaker cm = new CertificateMaker("United States", "UFL", 
          "ACIS", "David Wolinsky", "davidiw@ufl.edu", rsa_copy,
          address.ToString());
        Certificate cert = cm.Sign(CACert, SEKey);

        CertificateHandler ch = new CertificateHandler();
        ch.AddCACertificate(CACert.X509);
        ch.AddSignedCertificate(cert.X509);

        BrunetSecurityOverlord so = new BrunetSecurityOverlord(node, rsa_copy, node.Rrm, ch);
        so.Subscribe(node, null);
        node.GetTypeSource(SecurityOverlord.Security).Subscribe(so, null);
        nm.BSO = so;
        node.HeartBeatEvent += so.Heartbeat;
      }
      if(secure_edges) {
        el = new SecureEdgeListener(el, nm.BSO);
      }

      node.AddEdgeListener(el);

      if(broken != 0) {
        el = new TunnelEdgeListener(node);
        node.AddEdgeListener(el);
      }

      ArrayList RemoteTAs = new ArrayList();
      for(int i = 0; i < 5 && i < TakenPorts.Count; i++) {
        int rport = (int) TakenPorts.GetByIndex(rand.Next(0, TakenPorts.Count));
        RemoteTAs.Add(TransportAddressFactory.CreateInstance("brunet.function://127.0.0.1:" + rport));
      }
      node.RemoteTAs = RemoteTAs;

      TakenPorts[nm.Port] = nm.Port;

      if(output) {
        Console.WriteLine("Adding: " + nm.Node.Address);
      }
      node.Connect();
      network_size++;
    }

    public class BrokenTAAuth : TAAuthorizer {
      double _prob;
      Hashtable _allowed;
      object _sync;
      Random _rand;

      public BrokenTAAuth(double probability) {
        _prob = probability;
        _allowed = new Hashtable();
        _sync = new object();
        _rand = new Random();
      }

      public override TAAuthorizer.Decision Authorize(TransportAddress a) {
        int port = ((IPTransportAddress) a).Port;
        lock(_sync) {
          if(!_allowed.Contains(port)) {
            if(_rand.NextDouble() > _prob) {
              _allowed[port] = TAAuthorizer.Decision.Allow;
            } else {
              _allowed[port] = TAAuthorizer.Decision.Deny;
            }
          }
        }
        return (TAAuthorizer.Decision) _allowed[port];
      }
    }

    // Performs a crawl of the network using the ConnectionTable of each node.
    protected static bool check_ring() {
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

    protected static void PrintConnections() {
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

    protected static void PrintHelp() {
      Console.WriteLine("Usage: SystemTest.exe --option[=value]...\n");
      Console.WriteLine("Options:");
      Console.WriteLine("--n=int - network size");
      Console.WriteLine("--et=str - edge type - udp, tcp, or function");
      Console.WriteLine("--broken - broken system test");
      Console.WriteLine("--complete -- run until fully connected network");
      Console.WriteLine("--help - this menu");
      Console.WriteLine();
      Environment.Exit(0);
    }

    public class NodeMapping {
      public int Port;
      public Node Node;
      public BrunetSecurityOverlord BSO;
    }
  }
}
