/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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

#define EVERY_20

using System.IO;
using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;

using Brunet.Concurrent;
using Brunet.Connections;
using Brunet.Collections;
using Brunet.Transport;
using Brunet.Util;

using Brunet.Symphony;
using Brunet.Messaging;
namespace Brunet 
{
  /** 
   * This class tests the bootstrapping of the brunet system.
   *
   */
  public class BootStrapTester
  {
/*
         private static readonly ILog log =
		 LogManager.GetLogger(typeof(BootStrapTester));
		 */
    BootStrapTester(ArrayList nodes)
    {
      //Now sort the address list and make a list of nodes in that order:
      ArrayList adds = new ArrayList();
      foreach(Node n in nodes) {
        adds.Add(n.Address);
      }
      adds.Sort(new AHAddressComparer());

      _node_list = nodes;
      _sorted_adds = adds;
      _ctable_to_node = new Hashtable();
      _sync = new object();
      lock(_sync) {
        foreach(Node n in _node_list) {
          //When the tables change, we want to know about it.
          n.ConnectionTable.ConnectionEvent += new EventHandler(this.ConnectionTableChangeHandler);
          n.ConnectionTable.DisconnectionEvent += new EventHandler(this.ConnectionTableChangeHandler);
          _ctable_to_node[ n.ConnectionTable ] = n; 
        }
      }
    }

    public int _idx = 0;
    
    protected Hashtable _ctable_to_node;
    protected object _sync;

    private ArrayList _sorted_adds;
    /* holds all the nodes */
    private ArrayList _node_list;

    public void Add(Node n) {
      lock( _sync ) {
        _node_list.Add(n);  
        _sorted_adds.Add(n.Address);
        _sorted_adds.Sort(new AHAddressComparer());
        //monitor the connection table:
        ConnectionTable ct = n.ConnectionTable;
        _ctable_to_node[ ct ] = n; 
        ct.ConnectionEvent += new EventHandler(this.ConnectionTableChangeHandler);
        ct.DisconnectionEvent += new EventHandler(this.ConnectionTableChangeHandler);
      }
    }
    public void BenchmarkHops(int reps) {
      List<int> hops = new List<int>();
      Random my_r = new Random();
      ArrayList nodes = null;
      lock(_sync) {
        //Make a copy
        nodes = new ArrayList(_node_list);
      }
      Stack<Action> pings = new Stack<Action>();
      for(int i = 0; i < reps; i++) {
        int idx0 = my_r.Next(0, nodes.Count);
        int idx1 = my_r.Next(0, nodes.Count);
        Node n0 = (Node)nodes[idx0];
        Node n1 = (Node)nodes[idx1];
        Action ping = delegate() {
          RpcManager pinger = RpcManager.GetInstance( n0 );
          Channel results = new Channel(1);
          results.CloseEvent += delegate(object q, EventArgs a) {
            try {
              object result = results.Dequeue();
	      RpcResult r = (RpcResult)result;
	      IList data = (IList)r.Result;
              hops.Add(data.Count - 1);
            }
            catch(Exception x) {
              Console.WriteLine("target: {0}, Exception: {1}", n1.Address, x);
            }
            if( pings.Count > 0 ) {
              var next = pings.Pop();
              next();
            }
            else {
              double ave_rtt = 0;
              foreach(int s in hops) {
                ave_rtt += (double)s;     
              }
              ave_rtt /= hops.Count;
              double var = 0;
              foreach(int s in hops) {
                var += (ave_rtt - (double)s) * (ave_rtt - (double)s);
              }
              var /= hops.Count;
              var stdev = Math.Sqrt(var);
              Console.WriteLine("Average: {0} Stdev: {1} Samples: {2} Reps: {3}", ave_rtt, stdev, hops.Count, reps); 
            }
          };
          try {
            pinger.Invoke(n0, results, "trace.GetRouteTo", n1.Address.ToString());
          }
          catch(Exception x) {
            Console.WriteLine("Exception: {0}", x);
            if( pings.Count > 0 ) {
              var next = pings.Pop();
              next();
            }
            else {
              double ave_rtt = 0;
              foreach(int s in hops) {
                ave_rtt += (double)s;     
              }
              ave_rtt /= hops.Count;
              double var = 0;
              foreach(int s in hops) {
                var += (ave_rtt - (double)s) * (ave_rtt - (double)s);
              }
              var /= hops.Count;
              var stdev = Math.Sqrt(var);
              Console.WriteLine("Average: {0} Stdev: {1} Samples: {2} Reps: {3}", ave_rtt, stdev, hops.Count, reps); 
            }
          }
        };
        pings.Push(ping);
      }
      //Now pop off the first one and go:
      var first = pings.Pop();
      first();
    }
    public void BenchmarkPing(int reps) {
      List<int> mu_sec_pings = new List<int>();
      Random my_r = new Random();
      ArrayList nodes = null;
      lock(_sync) {
        //Make a copy
        nodes = new ArrayList(_node_list);
      }
      Stack<Action> pings = new Stack<Action>();
      for(int i = 0; i < reps; i++) {
        int idx0 = my_r.Next(0, nodes.Count);
        int idx1 = my_r.Next(0, nodes.Count);
        Node n0 = (Node)nodes[idx0];
        Node n1 = (Node)nodes[idx1];
        Action ping = delegate() {
          RpcManager pinger = RpcManager.GetInstance( n0 );
          Channel results = new Channel(1);
          results.CloseEvent += delegate(object q, EventArgs a) {
            try {
              object result = results.Dequeue();
	      RpcResult r = (RpcResult)result;
	      IDictionary data = (IDictionary)r.Result;
              int rtt = (int)data["musec"];
	      //Console.WriteLine("target: {0}, rtt: {1}", data["target"], data["musec"]);
              mu_sec_pings.Add(rtt);
            }
            catch(Exception x) {
              Console.WriteLine("target: {0}, Exception: {1}", n1.Address, x);
            }
            if( pings.Count > 0 ) {
              var next = pings.Pop();
              next();
            }
            else {
              double ave_rtt = 0;
              foreach(int s in mu_sec_pings) {
                ave_rtt += (double)s;     
              }
              ave_rtt /= mu_sec_pings.Count;
              double var = 0;
              foreach(int s in mu_sec_pings) {
                var += (ave_rtt - (double)s) * (ave_rtt - (double)s);
              }
              var /= mu_sec_pings.Count;
              var stdev = Math.Sqrt(var);
              mu_sec_pings.Sort();
              var median = mu_sec_pings[ mu_sec_pings.Count / 2];
              Console.WriteLine("Average: {0} Median: {1} Stdev: {2} Samples: {3} Reps: {4}",
                                ave_rtt, median, stdev, mu_sec_pings.Count, reps); 
            }
          };
          try {
            pinger.Invoke(n0, results, "trace.GetRttTo", n1.Address.ToString());
          }
          catch(Exception x) {
            Console.WriteLine("Exception: {0}", x);
            if( pings.Count > 0 ) {
              var next = pings.Pop();
              next();
            }
            else {
              double ave_rtt = 0;
              foreach(int s in mu_sec_pings) {
                ave_rtt += (double)s;     
              }
              ave_rtt /= mu_sec_pings.Count;
              double var = 0;
              foreach(int s in mu_sec_pings) {
                var += (ave_rtt - (double)s) * (ave_rtt - (double)s);
              }
              var /= mu_sec_pings.Count;
              var stdev = Math.Sqrt(var);
              mu_sec_pings.Sort();
              var median = mu_sec_pings[ mu_sec_pings.Count / 2];
              Console.WriteLine("Average: {0} Median: {1} Stdev: {2} Samples: {3} Reps: {4}",
                                ave_rtt, median, stdev, mu_sec_pings.Count, reps); 
            }
          }
        };
        pings.Push(ping);
      }
      //Now pop off the first one and go:
      var first = pings.Pop();
      first();
    }

    public void Remove(Node n) {
      lock(_sync) {
        _node_list.Remove(n);
        _sorted_adds.Remove(n.Address);
      }
    }
    
    /**
     * When any connectionTable changes, an event is fired.  This method
     * is called, and the global graph is written out
     */
    void ConnectionTableChangeHandler(object o, EventArgs arg) {
      lock( _sync ) {
        _idx++;
#if EVERY_20
        if( _idx % 20 == 0 ) { 
	      //Only print every 20'th change.  This is a hack...
          ToDotFile();
        }
#else
        ToDotFile();
#endif
	//Node n = (Node)_ctable_to_node[o];
	//Console.Error.WriteLine("Node({0}).IsConnected == {1}", n.Address, n.IsConnected);
      }
    }
    
    public void ToDotFile()
    {
      ArrayList node_list = _node_list;
      int index = _idx;
      //Make the list of all addresses:
      ArrayList all_adds = new ArrayList();
      Hashtable addresses = new Hashtable();
      foreach(Node item in node_list) {
        Address a = item.Address;
        if( addresses.Contains(a) == false ) {
          all_adds.Add(a);
          addresses[a] = a;
        }
        foreach(Connection con in item.ConnectionTable) {
          a = con.Address;
          if( addresses.Contains(a) == false ) {
            all_adds.Add(a);
            addresses[a] = a;
          }
        }
      }
      all_adds.Sort(new AHAddressComparer());

      string file_name = string.Format("BootGraph_{0:000000}",index); 
      StreamWriter sw = File.CreateText(file_name);
      sw.WriteLine("digraph bootgraph { ");
      //sw.WriteLine("size=\"8,8\";");
      sw.WriteLine("graph [bb=\"0,0,800,800\"];");
      
      double nodesize = .50;
      int canvassize = 576;
      double r = (double)canvassize/2.0 - 1.0 -36.0*nodesize;
      int c = canvassize/2;
      int nodes = all_adds.Count;
      int position = 0;
      double phi = Math.PI/(2*((double)nodes));
      //double r = Math.Cos(phi)/(2.0*nodesize);
      //double r =((double)(canvassize-1-2-Math.Ceiling(nodesize )))/2.0;
      double theta = 0.0;
      int ringlayoutx = 0;
      int ringlayouty = 0;
      
      foreach( Address item in all_adds)
      {
         theta = (double)(4*(position))*phi;
         ringlayoutx = c + (int)(r*Math.Sin(theta));
         ringlayouty = c - (int)(r*Math.Cos(theta));
	 
	 //Find the index of this address:
	 int idx = all_adds.IndexOf( item );

         string node_line = 
           String.Format("{0} [pos=\"{1:D},{2:D}\", width=\"{3:F2}\",height=\"{4:F2}\"];",
                //item.ToBigInteger().IntValue(),
		idx,
                ringlayoutx,
                ringlayouty,
                nodesize,
                nodesize);
          sw.WriteLine(node_line);
	  sw.WriteLine("//{0} = {1}",idx, item);
         position++;
      }
      
      foreach( Node item in node_list)
      {
          string color = "";
	  int item_idx = all_adds.IndexOf( item.Address );
          foreach(Connection con in item.ConnectionTable) {
	    int con_idx = all_adds.IndexOf( con.Address );
            if( con.MainType == ConnectionType.Leaf ) {
              color = " [color= blue]";
	    }
	    else if (con.MainType == ConnectionType.Structured ) {
              color = " [color= red]";
	    }
	    else if (con.MainType == ConnectionType.Unstructured ) {
              color = " [color= green]";
	    }
           string graph_line = String.Format("{0} -> {1}{2};",
		                                          item_idx, con_idx, color);
            sw.WriteLine(graph_line);
	    
	  }
      }
      sw.WriteLine("}");
      sw.Close();
      //We just wrote the file out.
#if USE_GRAPHVIZ
      string neato_command = String.Format("/usr/bin/neato");
      string neato_args =String.Format("-Tps -o {0}_circle.ps -n -s72 {0}",file_name );
      string dot_command = String.Format("/usr/bin/dot");
      string dot_args = String.Format("-Tps -o {0}.ps {1}",file_name,file_name);
      //string touch_cmd = String.Format("/usr/bin/touch");
      //string touch_args = String.Format(" t_movie.ps movie.ps");
      //string cat_cmd = String.Format("/bin/cat");
      //string cat_args = String.Format(" t_movie.ps {0}.ps > t_movie.ps",file_name);
      //string ps2ps_cmd = String.Format("/usr/bin/ps2ps");
      //string ps2ps_args = String.Format("t_movie.ps movie.ps");
#endif
    }
    static void Ping(IList nodes) {
      Random my_r = new Random();
      int idx0 = my_r.Next(0, nodes.Count);
      int idx1 = my_r.Next(0, nodes.Count);
      Node n0 = (Node)nodes[idx0];
      Node n1 = (Node)nodes[idx1];
      RpcManager pinger = RpcManager.GetInstance( n0 );
      Channel results = new Channel();
      results.EnqueueEvent += delegate(object q, EventArgs a) {
        object result = results.Dequeue();
	RpcResult r = (RpcResult)result;
        try {
	  IDictionary data = (IDictionary)r.Result;
	  Console.WriteLine("target: {0}, rtt: {1}", data["target"], data["musec"]);
        }
        catch(Exception x) {
          Console.WriteLine("target: {0}, Exception: {1}", n1.Address, x);
        }
      };
      Console.WriteLine("Pinging: {0} -> {1}", n0.Address, n1.Address);
      try {
        pinger.Invoke(n0, results, "trace.GetRttTo", n1.Address.ToString());
      }
      catch(Exception x) {
        Console.WriteLine("Exception: {0}", x);
      }
    }
    static void TraceRoute(IList nodes) {
      Random my_r = new Random();
      int idx0 = my_r.Next(0, nodes.Count);
      int idx1 = my_r.Next(0, nodes.Count);
      Node n0 = (Node)nodes[idx0];
      Node n1 = (Node)nodes[idx1];
      RpcManager pinger = RpcManager.GetInstance( n0 );
      Channel results = new Channel();
      Console.WriteLine("Traceroute: {0} -> {1}", n0.Address, n1.Address);
      results.EnqueueEvent += delegate(object q, EventArgs a) {
        object result = results.Dequeue();
	RpcResult r = (RpcResult)result;
	IList data = (IList)r.Result;
	int hop = 0;
	foreach(IDictionary d in data) {
          Console.WriteLine("Hop: {0} :: {1}\n  :: {2}", hop, d["node"], d["next_con"]);
	  hop++;
	}
      };
      try {
        pinger.Invoke(n0, results, "trace.GetRouteTo", n1.Address.ToString());
      }
      catch(Exception x) {
        Console.WriteLine("Exception: {0}", x);
      }
    }

#if USE_GRAPHVIZ
    static void ProgramRunner(string cmd, string cmd_args)
    {
      //There is some bug I have not diagnosed using mono 0.31.  POB
      if (File.Exists(cmd))
      {
        //ProcessStartInfo proc_start_info = new ProcessStartInfo(cmd,cmd_args);
        //Process.Start(proc_start_info);
	System.Console.Out.WriteLine("starting: " + cmd + " " + cmd_args);
	Process.Start(cmd, cmd_args);
      }
    }
#endif
    
  static void Main(string[] args)  
  {
   
    RandomNumberGenerator rng = new RNGCryptoServiceProvider();
    
    //Initialize hosts
    Console.WriteLine("\n\n---------------------------------------\n\n");
    int port = 20287;
    int net_size = 3;
    string net_type = "function";
    if( args.Length > 0 ) {
      net_size = Int32.Parse(args[0]);
    }
    if( args.Length > 1 ) {
      net_type = args[1];
    }
    int ms_sleep = 0;
    if( args.Length > 2 ) {
      ms_sleep = Int32.Parse(args[2]);
    }
    bool wait_after_connect = true;
    if( args.Length > 3 ) {
      ///@todo we really need better option parsing here
      wait_after_connect = false;
    }
    ArrayList node_list = new ArrayList();
    Hashtable add_to_node = new Hashtable();
    
    for (int loop=0;loop<net_size;loop++)
    {
      //create and initialize new host
      //create one new node for each host
      AHAddress tmp_add = new AHAddress(rng);
      Node tmp_node = new StructuredNode(tmp_add, "bstland");
      //Node tmp_node = new HybridNode(tmp_add, "bstland");
      
      node_list.Add(tmp_node);
      add_to_node[tmp_add] = tmp_node;
      
      //long small_add = 2*(loop+1);
      //Node tmp_node = new StructuredNode(new AHAddress( new BigInteger(small_add)) );
      switch(net_type) {
        case "tcp":
		tmp_node.AddEdgeListener(new TcpEdgeListener(port+loop));
	        break;
        case "udp":
		tmp_node.AddEdgeListener(new UdpEdgeListener(port+loop));
		break;
	case "function":
                tmp_node.AddEdgeListener(new FunctionEdgeListener(port+loop));
                break;
	default:
		throw new Exception("Unknown net type: " + net_type);
      }
      //tmp_node.AddEdgeListener(new FunctionEdgeListener(port+loop));
      for (int loop2=0;loop2<net_size;loop2++)
      {
      if (loop != loop2)
      {
          int other_port = port+loop2;
	  string ta_str = null;
          switch(net_type) {
            case "tcp":
		ta_str = "brunet.tcp://127.0.0.1:";
	        break;
            case "udp":
		ta_str = "brunet.udp://127.0.0.1:";
	        break;
            case "udp-as":
		ta_str = "brunet.udp://127.0.0.1:";
	        break;
	    case "function":
		ta_str = "brunet.function://localhost:";
                break;
	    default:
		throw new Exception("Unknown net type: " + net_type);
          }
	  ta_str = ta_str + other_port.ToString();
	  TransportAddress this_ta =
	                    TransportAddressFactory.CreateInstance(ta_str);
          tmp_node.RemoteTAs.Add(this_ta);
          }
      }
    }
    
    //This logs the changes in connection table
    BootStrapTester bst = new BootStrapTester(node_list);
    if( bst != null ) {
    //This is just here to prevent a warning for
    //not using bst, which is just an observer
    }
    //Get Connected:
    int total_started = 0;
    ArrayList rnd_list = (ArrayList)node_list.Clone();
    Random rnd = new Random();
    for(int j = 0; j < rnd_list.Count; j++) {
          //Swap the j^th position with this position:
          int i = rnd.Next(j, rnd_list.Count);
          if( i != j ) {
            object o = rnd_list[i];
            rnd_list[i] = rnd_list[j];
            rnd_list[j] = o;
          }
    }
    ArrayList c_threads = new ArrayList(); 
    //var xrms = new Brunet.Rpc.XmlRpcManagerServer(20000);
    int cnt = 0;
    foreach( Node item in rnd_list)
    {
      Thread t = new Thread( item.Connect );
      c_threads.Add(t);
      t.Start();
      //xrms.Add(item, "xm" + cnt++ + ".rem");
      Console.WriteLine(item.Address.ToString()
		      + " RemoteTAs count: " + item.RemoteTAs.Count);
      total_started++;
      Console.WriteLine("Started: " + total_started.ToString());
      //Thread.Sleep(10000);
      Thread.Sleep(ms_sleep);
      //Console.ReadLine();
      //foreach (TransportAddress item2 in item.RemoteTAs)
      //  Console.WriteLine(item2);
    
    }

    System.Console.Out.WriteLine("Finished with BootStrapTester.Main");
    string[] this_command = new string[] { "Q" };
    if( wait_after_connect) {
      Console.WriteLine("Enter Q to stop");
      this_command = Console.ReadLine().Split(' ');
    }
    while( this_command[0] != "Q" ) {
      if( this_command[0] == "D" ) { 
        //Disconnect a node:
        int node = -1;
        try {
          node = Int32.Parse(this_command[1]);
          Node to_disconnect = (Node)node_list[node];
          Console.WriteLine("About to Disconnect: {0}", to_disconnect.Address);
	        to_disconnect.Disconnect();
          bst.Remove(to_disconnect);
        }
        catch(Exception) {
 
        }
      }
      if( this_command[0] == "abort" ) { 
        //Disconnect a node:
        int node = -1;
        try {
          node = Int32.Parse(this_command[1]);
          Node to_abort = (Node)node_list[node];
          Console.WriteLine("About to Abort: {0}", to_abort.Address);
	        to_abort.Abort();
          bst.Remove(to_abort);
        }
        catch(Exception) {
 
        }
      }
      if( this_command[0] == "P" ) {
        //Pick a random pair of nodes to ping:
	Ping(node_list);
      }
      if( this_command[0] == "BP" ) {
        try {
          int reps = Int32.Parse(this_command[1]);
          bst.BenchmarkPing(reps);
        }
        catch(Exception x) {
          Console.WriteLine(x);
        }
      }
      if( this_command[0] == "BH" ) {
        try {
          int reps = Int32.Parse(this_command[1]);
          bst.BenchmarkHops(reps);
        }
        catch(Exception x) {
          Console.WriteLine(x);
        }
      }
      if( this_command[0] == "T" ) {
        //Pick a random pair of nodes to ping:
	TraceRoute(node_list);
      }
      if( wait_after_connect ) {
        this_command = Console.ReadLine().Split(' ');
      }
    }
    
    foreach(Node n in node_list)
    {
      n.Disconnect();
    }
    //Block until all Connect threads finish.
    //foreach(Thread t in c_threads) {
    //  t.Join();
    //}

  }
  }
}
