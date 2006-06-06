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
//#define LOWER_PORTS
//#define ECHO
//#define PACKET_LOG
//#define MEMORY_LOG
#define ALIVE_LOG
///WARNING: ECHO and MEMORY_LOG cannot be turned on at the same time
///To-do: implement memory logging on a separate thread

using System;
using System.Text;
using System.Collections;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

//[assembly:log4net.Config.DOMConfigurator(Watch = true)]

namespace Brunet
{
#if ECHO
  public class BrunetTester:IAHPacketHandler
#else
  public class BrunetTester
#endif			     
  {

    ///This tester simply establishes the Brunet network and log the edges made
    //private BrunetLogger bl;
    //private bool log_rdp;
    protected Node node;
    protected short _port;
    protected StreamWriter _sw;

    public BrunetTester(int p, NetworkConfiguration nc, StreamWriter fs)
    { 
      int desired_port = p;
      _port = (short)p;
      _sw = fs;
      int local_host_index = nc.GetLocalHostIndex(desired_port); 
      NodeConfiguration this_node_configuration = (NodeConfiguration)nc.Nodes[local_host_index];

      TransportAddressConfiguration local_ta_configuration = 
        (TransportAddressConfiguration)this_node_configuration.TransportAddresses[0];
      short this_port = local_ta_configuration.Port;

      SHA1 sha = new SHA1CryptoServiceProvider();
      String local_ta = local_ta_configuration.GetTransportAddressURI();
      //We take the local transport address plus the port number to be hashed to obtain a random AHAddress
      byte[] hashedbytes = sha.ComputeHash(Encoding.UTF8.GetBytes(local_ta + desired_port));
      //inforce type 0
      hashedbytes[Address.MemSize - 1] &= 0xFE;
      AHAddress _local_ahaddress = new AHAddress(hashedbytes);
      //Node this_node = new HybridNode( _local_ahaddress );
      Node this_node = new StructuredNode( _local_ahaddress );

      node = this_node;
      if ( local_ta_configuration.Protocol == "tcp" ) {
        node.AddEdgeListener( new TcpEdgeListener(this_port) );
      } 
      else if( local_ta_configuration.Protocol == "udp" ) {
        node.AddEdgeListener( new UdpEdgeListener(this_port) );        
      }

      int remote_node_index = local_host_index-1;
      int num_remote_ta = 150; //20 nodes on the list to try to bootstrap to

      if (local_host_index!=0) {
        NodeConfiguration remote_node_configuration = (NodeConfiguration)nc.Nodes[0];
        TransportAddressConfiguration remote_ta_configuration = 
          (TransportAddressConfiguration)remote_node_configuration.TransportAddresses[0];

        String remote_ta = remote_ta_configuration.GetTransportAddressURI(); 
        node.RemoteTAs.Add( new TransportAddress( remote_ta  ) );
      }

      while ( (remote_node_index>=0) && (num_remote_ta>=0) ) { 
        NodeConfiguration remote_node_configuration = (NodeConfiguration)nc.Nodes[remote_node_index];
        TransportAddressConfiguration remote_ta_configuration = 
          (TransportAddressConfiguration)remote_node_configuration.TransportAddresses[0];

        String remote_ta = remote_ta_configuration.GetTransportAddressURI(); 
        node.RemoteTAs.Add( new TransportAddress( remote_ta  ) );

        //System.Console.WriteLine("Adding {0}", remote_ta);

        remote_node_index--;
        num_remote_ta--;
      }

#if PLAB_LOG
      bool net_stream = false;
      String server_ipadd = "cantor.ee.ucla.edu";
      int server_port = 8002;
      int time_diff = 0;
      String td_file_string = "~/joe/time_diff.txt";
      if(File.Exists(td_file_string)){
        StreamReader sr = new StreamReader(td_file_string);
        time_diff = Convert.ToInt32( sr.ReadLine() );     
        sr.Close();
      }  
#endif
      //String file_string = "./data/brunetadd" + Convert.ToString(desired_port) + ".log";
      fs.WriteLine( "local_address " + node.Address.ToBigInteger().ToString() 
          + " " + Dns.GetHostName() + ":" + desired_port);
      fs.Write( DateTime.Now.ToUniversalTime().ToString("MM'/'dd'/'yyyy' 'HH':'mm':'ss") + 
          ":" + DateTime.Now.ToUniversalTime().Millisecond +
          "  Start  Start  " + node.Address.ToBigInteger().ToString() + '\n'); 
      fs.Flush(); 
#if PLAB_LOG
      BrunetLogger bl = new BrunetLogger(desired_port, (AHAddress)node.Address, 
          net_stream, server_ipadd, server_port, time_diff, fs); 

      node.Logger = bl;
#endif
      //bool log_rdp = false;	  

#if PACKET_LOG
      String file_packet = "./data/packet" + Convert.ToString(desired_port) + ".log";
      StreamWriter packet_sw = new StreamWriter(file_packet, false);
      packet_sw.WriteLine("Local_node: {0}:{1} start_time_GMT: {2}:{3} local_address {4}", Dns.GetHostName(), 
          desired_port, DateTime.Now.ToUniversalTime().ToString("MM'/'dd'/'yyyy' 'HH':'mm':'ss"), 
          DateTime.Now.ToUniversalTime().Millisecond, node.Address.ToBigInteger().ToString() ); 
      packet_sw.Close(); 
#endif

    }

    public void StartBrunet()
    {
      node.Connect();          
    }

    static void Main(string[] args)
    {

      String config_file = args[0];
      NetworkConfiguration network_configuration = NetworkConfiguration.Deserialize(config_file);

      int start_index = Convert.ToInt32(args[1]);
      int start_ip;
      //int num_instances = 10;  ///10 nodes in one mono instance is the standard
      int num_instances = Convert.ToInt32(args[2]); 

      ///There will be multiple BruNet nodes on the same machine. The following is a list of possible ports used
      int list_size = 900;
      int [] port_list = new int[list_size];
      for(int i = 0; i < list_size; i++){
#if LOWER_PORTS
        port_list[i] = 5000 + i;
#else	      
        port_list[i] = 25000 + i;
#endif
      }
      start_ip = port_list[start_index];

      //ArrayList thread_arr = new ArrayList();
      //ArrayList bt_arr = new ArrayList();
      Hashtable port_thread = new Hashtable();
      Hashtable port_bt = new Hashtable();
      for(int i = start_index; i < start_index + num_instances; i++)
      {
        int desired_port = port_list[i];        
        String _connection_log_file = "./data/" + "brunetadd" + Convert.ToString(desired_port) + ".log";
        if(File.Exists(_connection_log_file)){
          File.Delete(_connection_log_file);
        }
        StreamWriter fs = new StreamWriter(_connection_log_file, true);
        fs.AutoFlush = true;

        BrunetTester bt = new BrunetTester(desired_port, network_configuration, fs); 

        Thread backgroundThread = new Thread(new ThreadStart(bt.StartBrunet));
        //thread_arr.Add( backgroundThread );
        //bt_arr.Add( bt );
        port_thread[desired_port] = backgroundThread;
        port_bt[desired_port] = bt;
        ///need to implement the hashtable, port to thread/brunettester
      }	     

      int sleep_sec = Convert.ToInt32(args[3]);
      foreach( int this_port in port_thread.Keys ){
        ((Thread)port_thread[this_port]).Start();
        System.Threading.Thread.Sleep(sleep_sec*1000);
      }

      int interval_sec = 5; //this determines the frequency to check for kill_file
      String _kill_file = "kill" + Convert.ToString(start_index) + ".txt";
#if ALIVE_LOG
      ///WARNING: num_instance must be one for this code block
      String _alive_log_file = "./data/" + "alive" + Convert.ToString(start_ip) + ".log";
      if(File.Exists(_alive_log_file)){
          File.Delete(_alive_log_file);
      }
      StreamWriter alive_sw = new StreamWriter(_alive_log_file, true);
#endif
      ///The standard is to have 10 nodes running per mono instance
      ///For example, the file kill20.txt take care of the nodes in the port range of
      ///25020-25029.  
      while(true){
#if ALIVE_LOG
        alive_sw.Write("{0}:{1} Alive \n", DateTime.Now.ToUniversalTime().ToString("MM'/'dd'/'yyyy' 'HH':'mm':'ss"), 
          DateTime.Now.ToUniversalTime().Millisecond ); 
        alive_sw.Flush();
#endif
        
        if(File.Exists(_kill_file)){
          ArrayList kill_list = new ArrayList();
          using( StreamReader sr = new StreamReader(_kill_file) ){
            while (sr.Peek() >= 0){
              int port_to_kill = Convert.ToInt32(sr.ReadLine().Trim());
              kill_list.Add( port_to_kill );
            }  
          }
          File.Delete(_kill_file);

          foreach( int port in kill_list ){
            ///use the hashtable to lookup BrunetTester's and Thread's
            if( port_bt.ContainsKey(port) && port_thread.ContainsKey(port) ){
              BrunetTester bt = (BrunetTester)port_bt[port];
              bt.node.Disconnect();
              bt._sw.Write( DateTime.Now.ToUniversalTime().ToString("MM'/'dd'/'yyyy' 'HH':'mm':'ss") + 
                  ":" + DateTime.Now.ToUniversalTime().Millisecond +
                  "  deletion  deletion  " + bt.node.Address.ToBigInteger().ToString() + '\n');
              Thread thr = (Thread)port_thread[port];
              try{
                thr.Abort();
              }
              catch( ThreadAbortException e){

              }
              port_bt.Remove(port);
              port_thread.Remove(port);                   
            }
          }             
        }
        System.Threading.Thread.Sleep(interval_sec*1000);
      }

    }

  }
}
