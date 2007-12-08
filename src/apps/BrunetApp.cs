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

/**
 * Dependencies : 
 * Brunet.Address
 * Brunet.AHAddress
 * Brunet.AHAddressComparer;
 * Brunet.AHPacket
 * Brunet.Edge
 * Brunet.EdgeListener
 * Brunet.FunctionEdgeListener
 * Brunet.Node
 * Brunet.StructuredNode
 * Brunet.NumberSerializer
 * Brunet.IAHPacketHandler
 * Brunet.TcpEdgeListener
 * Brunet.TransportAddress
 * Brunet.LeafConnectionOverlord
 * Brunet.StructuredConnectionOverlord
 * Brunet.ConnectionType;
 * Brunet.ConnectionTable;
 * Brunet.BigInteger;
 * Brunet.Base32;
 */

using System.IO;
using System;
using System.Collections;
using log4net;
using log4net.Config;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;

[assembly: log4net.Config.DOMConfigurator(Watch=true)]
namespace Brunet
{
  /**
   * This class tests the bootstrapping of the brunet system.
   *
   */
  public class BootStrapTester
  {

    private static readonly log4net.ILog log =
      log4net.LogManager.GetLogger(System.Reflection.MethodBase.
                                   GetCurrentMethod().
                                   DeclaringType);
    BootStrapTester()
    {
    }

    public string l_u_f = null;
    public string l_u_h = null;


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
      while (true)
      {
        System.Threading.Thread.Sleep(50);
        if (node_list != null)
        {
          ToDotFile(node_list, idx);
          idx++;
        }
      }
    }

    public static void testAddressComparer(ArrayList node_list)
    {
      AHAddress add = new AHAddress(new BigInteger(0));
      ArrayList all_adds = new ArrayList();
      AHAddressComparer cmp = new AHAddressComparer(add);

      System.Console.WriteLine("Address comparison test:");
      AHAddress a0 = (AHAddress)(((Node)node_list[0]).Address);
      AHAddress a1 = (AHAddress)(((Node)node_list[1]).Address);
      System.Console.WriteLine("Address: {0}", a0);
      System.Console.WriteLine("Address: {0}", a1);
      System.Console.WriteLine("Distance between a0 and a1:{0}", a0.DistanceTo(a1));
      System.Console.WriteLine("Compare a0 and a1:{0}", cmp.Compare(a0,a1));
      System.Console.WriteLine("------------------------");

      foreach( Node item in node_list)
      {
        int ins_index = all_adds.BinarySearch(item.Address, cmp);
        if (ins_index < 0)
        {
          ins_index = ~ins_index;
          all_adds.Insert(ins_index,item.Address);
        }
      }

      for (int i=0; i<all_adds.Count; i++)
      {
        Address item = (Address)all_adds[i];
        System.Console.WriteLine("Adress is {0}", item);
      }

    }

    void ToDotFile(ArrayList node_list, int index)
    {
      string file_name = string.Format("BootGraph_{0:000000}",index);
      StreamWriter sw = File.CreateText(file_name);
      sw.WriteLine("digraph bootgraph { ");
      //sw.WriteLine("size=\"8,8\";");
      sw.WriteLine("graph [bb=\"0,0,800,800\"];");

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
          ConnectionList leafs = item.ConnectionTable.GetConnections(ConnectionType.Leaf);
          foreach(Connection con in leafs)
	  {
	     Address leaf_item = con.Address;
              string graph_line = String.Format("{0} -> {1} [color= blue];",
                                                item.Address.ToBigInteger().IntValue(),
                                                leaf_item.ToBigInteger().IntValue() );
              sw.WriteLine(graph_line);
          }
          ConnectionList structs = item.ConnectionTable.GetConnections(ConnectionType.Structured);
          foreach(Connection con in structs)
	  {
	     Address struct_item = con.Address;
              string graph_line = String.Format("{0} -> {1} [color= red];",
                                                item.Address.ToBigInteger().IntValue(),
                                                struct_item.ToBigInteger().IntValue() );
              sw.WriteLine(graph_line);
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
      FileStream fs_this = new FileStream (file_name, FileMode.Open, FileAccess.Read);
      if ( l_u_f != null )
      {
        string this_hash_string = Base32.Encode(sha1.ComputeHash (fs_this));
        if( l_u_h == this_hash_string )
        {
          if (File.Exists(file_name))
          {
            File.Delete(file_name);
          }
        }
        else
        {
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
      if (File.Exists(cmd))
      {
        ProcessStartInfo proc_start_info = new ProcessStartInfo(cmd,cmd_args);
        Process.Start(proc_start_info);
      }

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
      //Console.WriteLine("\n\n---------------------------------------\n\n");
      int port = 20287;
      int net_size = Int32.Parse( args[0] );

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
        //tmp_node.AddEdgeListener(new TcpEdgeListener(port+loop));
        tmp_node.AddEdgeListener(new FunctionEdgeListener(port+loop));
        for (int loop2=0;loop2<loop;loop2++)
        {
          if (loop != loop2)
          {
            int other_port = port+loop2;
            tmp_node.RemoteTAs.Add(
              //new TransportAddress("brunet.tcp://128.97.89.79:" + other_port )
              TransportAddressFactory.CreateInstance("brunet.function://localhost:" + other_port )
            );
          }
        }
        node_list.Add(tmp_node);
      }

      //testAddressComparer(node_list);

      //Start the dot thread:
      System.Threading.Thread t = new System.Threading.Thread(
                                    new System.Threading.ThreadStart(bst.DotThread));
      t.Start();
      //Get Connected:
      int i=0;
      foreach( Node item in node_list)
      {
        System.Console.WriteLine("{0}: CONNECTING NODE: {1}", ++i, item.Address);
        System.Console.WriteLine("--------------------------");
        item.Connect();
        /*Console.WriteLine(item.Address.ToString()
          + " RemoteTAs count: " + item.RemoteTAs.Count);*/

        //Console.ReadLine();

        //foreach (TransportAddress item2 in item.RemoteTAs)
        //  Console.WriteLine(item2);

      }

      Console.WriteLine("BeforeDisconnect");
      Console.ReadLine();
      Node anode = (Node)node_list[5];
      anode.Disconnect();

      //We are connected now, stop the threads:
      //t.Abort();
    }
  }
}
