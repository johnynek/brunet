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
   * This class tests the bootstrapping of the gnucla system.
   *
   */
  public class BootStrapTester
  {
/*
         private static readonly ILog log =
		 LogManager.GetLogger(typeof(BootStrapTester));
		 */
    BootStrapTester()
    {
    }

    public string l_u_f = null;
    public string l_u_h = null;
    public int _unchanged_loops = 0;
    

    /* holds all the nodes */
    private ArrayList _node_list;
    public ArrayList NodeList {
	    get { return _node_list; }
	    set { _node_list = value; }
    }
    
    void DotThread()
    {
      ArrayList node_list = _node_list;
      int idx = 0;
      //Stop after we have not seen a change in 1000 steps
      while ( _unchanged_loops < 1000 )
      {
        System.Threading.Thread.Sleep(50);
	int threads, comp_ports;
        if (node_list != null)
        {
          ToDotFile(node_list, idx);
	  /*
	  ThreadPool.GetAvailableThreads (out threads, out comp_ports);
	  Console.WriteLine ("Available threads: {0} ports: {1}", threads,
			     comp_ports); 
	  */
          idx++;
        }
      } 
      System.Console.Out.WriteLine("Done with DotThread");
    }
    
    void ToDotFile(ArrayList node_list, int index)
    {
      string file_name = string.Format("BootGraph_{0:000000}",index); 
      StreamWriter sw = File.CreateText(file_name);
      sw.WriteLine("digraph bootgraph { ");
      //sw.WriteLine("size=\"8,8\";");
      sw.WriteLine("graph [bb=\"0,0,800,800\"];");
      Process dot_proc;
      
      AHAddress add = new AHAddress(new BigInteger(0));
      ArrayList all_adds = new ArrayList();
      AHAddressComparer cmp = new AHAddressComparer(add);
      foreach( Node item in node_list)
      {
        int ins_index = all_adds.BinarySearch(item.Address, cmp);
        if (ins_index < 0)
        {
	        ins_index = ~ins_index;
          all_adds.Insert(ins_index,item.Address);
        }
      }
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

         string node_line = 
           String.Format("{0} [pos=\"{1:D},{2:D}\", width=\"{3:F2}\",height=\"{4:F2}\"];",
                item.ToBigInteger().IntValue(),
                ringlayoutx,
                ringlayouty,
                nodesize,
                nodesize);
          sw.WriteLine(node_line);
         position++;
      }
      
      foreach( Node item in node_list)
      {
        lock( item.ConnectionTable.SyncRoot ) {
          
        if (0 < item.ConnectionTable.Count(ConnectionType.Leaf) )
        {
          ArrayList leaf_adds = 
            item.ConnectionTable.GetAddressesOfType(ConnectionType.Leaf);
          foreach ( Address leaf_item in leaf_adds  )
          {
            string graph_line = String.Format("{0} -> {1} [color= blue];",
                item.Address.ToBigInteger().IntValue(),
                leaf_item.ToBigInteger().IntValue() );
            sw.WriteLine(graph_line);
          }
        }
        if (0 < item.ConnectionTable.Count(ConnectionType.Structured) )
        {
          ArrayList struct_adds = 
            item.ConnectionTable.GetAddressesOfType(ConnectionType.Structured);
          foreach ( Address struct_item in struct_adds  )
          {
            string graph_line = String.Format("{0} -> {1} [color= red];",
                item.Address.ToBigInteger().IntValue(),
                struct_item.ToBigInteger().IntValue() );
            sw.WriteLine(graph_line);
          }
        }
       }
      }
      sw.WriteLine("}");
      sw.Close();
      SHA1 sha1 = (SHA1) CryptoConfig.CreateFromName("SHA1");
      
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
      FileStream fs_this = new FileStream (file_name,
		                           FileMode.Open,
					   FileAccess.Read);
      if ( l_u_f != null )
      {
        string this_hash_string = Base32.Encode(sha1.ComputeHash (fs_this));
        if( l_u_h == this_hash_string )
        {
          _unchanged_loops++;
          if (File.Exists(file_name))
          {
             File.Delete(file_name);
          }
	  
        }
        else
        {
          _unchanged_loops = 0;
          ProgramRunner(dot_command,dot_args);
          ProgramRunner(neato_command,neato_args);
          l_u_f = file_name;
          FileStream fs_last = new FileStream (l_u_f, FileMode.Open, FileAccess.Read);
          l_u_h = Base32.Encode(sha1.ComputeHash (fs_last));
          fs_last.Close ();
          //ProgramRunner(cat_cmd,cat_args);
          //ProgramRunner(ps2ps_cmd,ps2ps_args);
        }
  
      }
      else
      {
        _unchanged_loops = 0;
        //ProgramRunner(touch_cmd,touch_args);
        ProgramRunner(dot_command,dot_args);
        ProgramRunner(neato_command,neato_args);
        l_u_f = file_name;
        FileStream fs_last = new FileStream (
            l_u_f, FileMode.Open, FileAccess.Read);
        l_u_h = Base32.Encode(sha1.ComputeHash (fs_last)) ;
        fs_last.Close ();
        //ProgramRunner(cat_cmd,cat_args);
        //ProgramRunner(ps2ps_cmd,ps2ps_args);
      }
  
    fs_this.Close ();
    }
      
    static void ProgramRunner(string cmd, string cmd_args)
    {
      //There is some bug I have not diagnosed using mono 0.31.  POB
#if false
      if (File.Exists(cmd))
      {
        //ProcessStartInfo proc_start_info = new ProcessStartInfo(cmd,cmd_args);
        //Process.Start(proc_start_info);
	System.Console.Out.WriteLine("starting: " + cmd + " " + cmd_args);
	Process.Start(cmd, cmd_args);
      }
#endif
    }
    
  static void Main(string[] args)  
  {
   
    BootStrapTester bst = new BootStrapTester();
    //NodeList
    ArrayList node_list = new ArrayList();
    bst.NodeList = node_list;
    ArrayList all_ta_list = new ArrayList();  
    Random my_rand = new Random( unchecked((int)DateTime.Now.Ticks) ); 
    
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
    
    for (int loop=0;loop<net_size;loop++)
    {
      //create and initialize new host
      //create one new node for each host
      //Set the last bit to be zero so it will be class 0
      
      //byte[] address = new byte[Address.MemSize];
      //my_rand.NextBytes(address);
      //address[Address.MemSize - 1] &= 0xFE;
      //Node tmp_node = new StructuredNode(new AHAddress(address));
      long small_add = 2*(loop+1);
      Node tmp_node = new StructuredNode(new AHAddress( new BigInteger(small_add)) );
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
	  TransportAddress this_ta = null;
          switch(net_type) {
            case "tcp":
		this_ta = new TransportAddress("gnucla.tcp://127.0.0.1:"
				               + other_port );
	        break;
        case "udp":
		this_ta = new TransportAddress("gnucla.udp://127.0.0.1:"
				               + other_port );
	        break;
	    case "function":
		this_ta = new TransportAddress("gnucla.function://localhost:"
				               + other_port );
                break;
	    default:
		throw new Exception("Unknown net type: " + net_type);
          }
          tmp_node.RemoteTAs.Add(this_ta);
          }
      }
      node_list.Add(tmp_node);
    }

    //Start the dot thread:
    System.Threading.Thread t = new System.Threading.Thread(
		                    new System.Threading.ThreadStart(bst.DotThread));
    t.Start();
    //Get Connected:
    foreach( Node item in node_list)
    {
      item.Connect();
      Console.WriteLine(item.Address.ToString()
		      + " RemoteTAs count: " + item.RemoteTAs.Count);

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
  }
  }
}
