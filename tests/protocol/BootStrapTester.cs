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
//using log4net;
//using log4net.Config;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;


//[assembly: log4net.Config.DOMConfigurator(Watch=true)]
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
    BootStrapTester(ArrayList adds, ArrayList nodes)
    {
      _node_list = nodes;
      _sorted_adds = adds;
      _ctable_to_node = new Hashtable();
      lock(this) {
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

    private ArrayList _sorted_adds;
    /* holds all the nodes */
    private ArrayList _node_list;
    public ArrayList NodeList {
	    get { return _node_list; }
    }
    
    /**
     * When any connectionTable changes, an event is fired.  This method
     * is called, and the global graph is written out
     */
    void ConnectionTableChangeHandler(object o, EventArgs arg) {
      lock( this ) {
        _idx++;
	Node n = (Node)_ctable_to_node[o];
#if EVERY_20
       if( _idx % 20 == 0 ) { 
	      //Only print every 20'th change.  This is a hack...
          ToDotFile(_sorted_adds, _node_list, _idx);
       }
#else
          ToDotFile(_sorted_adds, _node_list, _idx);
#endif
	  Console.WriteLine("Node({0}).IsConnected == {1}", n.Address, n.IsConnected);
      }
    }
    
    static void ToDotFile(ArrayList all_adds, ArrayList node_list, int index)
    {
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
            string graph_line = String.Format("{0} -> {1}" + color + ";",
                //item.Address.ToBigInteger().IntValue(),
                //con.Address.ToBigInteger().IntValue()
		item_idx,
		con_idx
		);
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
    ArrayList adds = new ArrayList();
    Hashtable add_to_node = new Hashtable();
    
    for (int loop=0;loop<net_size;loop++)
    {
      //create and initialize new host
      //create one new node for each host
      AHAddress tmp_add = new AHAddress(rng);
      Node tmp_node = new StructuredNode(tmp_add, "bstland");
      //Node tmp_node = new HybridNode(tmp_add, "bstland");
      
      adds.Add(tmp_add);
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
        case "udp-as":
		tmp_node.AddEdgeListener(new ASUdpEdgeListener(port+loop));
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
    //Now sort the address list and make a list of nodes in that order:
    adds.Sort(new AHAddressComparer());
    ArrayList node_list = new ArrayList();
    foreach(AHAddress addr in adds) {
      node_list.Add( add_to_node[addr] );
    }
    
    //This logs the changes in connection table
    BootStrapTester bst = new BootStrapTester(adds, node_list);
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
    
    foreach( Node item in rnd_list)
    {
      item.Connect();
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

    //Node anode = (Node)node_list[0];
    //anode.Connect();

    //Gnucla.FunctionEdge.simulate();

    //We are connected now, stop the threads:
    //t.Abort(); 
    System.Console.Out.WriteLine("Finished with BootStrapTester.Main");
    string this_command = "Q";
    if( wait_after_connect) {
      Console.WriteLine("Enter Q to stop");
      this_command = Console.ReadLine();
    }
    while( this_command != "Q" ) {
      int node = -1;
      try {
        node = Int32.Parse(this_command);
        Node to_disconnect = (Node)node_list[node];
	to_disconnect.Disconnect();
      }
      catch(Exception) {

      }
      if( wait_after_connect ) {
        this_command = Console.ReadLine();
      }
    }
    
    foreach(Node n in node_list)
    {
      n.Disconnect();
    }

  }
  }
}
