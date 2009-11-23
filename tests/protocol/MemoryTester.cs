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

using System;
using System.Text;
using System.Collections;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Diagnostics;


//[assembly:log4net.Config.DOMConfigurator(Watch = true)]

namespace Brunet
{
  public class MemoryTester //:IAHPacketHandler
  {
    /**
     * @param elapsed_time the time past since the first log
     * @param received true if the packet was received, false if the packet was sent, at 
     * the time of logging
    */
    public static void LogMemoryUsage(TimeSpan elapsed_time)
    {   	
 	PsWrapper psw = new PsWrapper();
	psw.Ps();
	StreamWriter memory_sw = new StreamWriter("memory_usage.log", true);
	memory_sw.WriteLine("{0} \t {1} \t \t {2}", elapsed_time.TotalMilliseconds, psw.Rss, psw.Vsz );	
        memory_sw.Close();      
    }

    static void Main(string[] args)
    {
      //first, remove the log file
      if(File.Exists("memory_usage.log")){
	File.Delete("memory_usage.log");
      }

      String config_file = args[0];
      NetworkConfiguration network_configuration = NetworkConfiguration.Deserialize(config_file);

      int port_selection = Convert.ToInt32(args[1]); //There will be 10 different ports available for use: 0, 1, 2..
      //for example, node 0 on a machine will use port_selection # 0, node 1 on a machine will use port_selection # 1

      ///There will be multiple BruNet nodes on the same machine. The following is a list of possible ports used
      int list_size = 10;
      int [] port_list = new int[list_size];
      for(int i = 0; i < list_size; i++){
	port_list[i] = 25010 + i*10;
      }
	
      ///The line below is used when there is only one node per machine
      //int local_host_index = network_configuration.GetLocalHostIndex();                                                                
	
      int desired_port = port_list[port_selection];
      int local_host_index = network_configuration.GetLocalHostIndex(desired_port); 

      NodeConfiguration this_node_configuration = (NodeConfiguration)network_configuration.Nodes[local_host_index];
      TransportAddressConfiguration local_ta_configuration = (TransportAddressConfiguration)this_node_configuration.TransportAddresses[0];
      short port = local_ta_configuration.Port;

      SHA1 sha = new SHA1CryptoServiceProvider();
      String local_ta = local_ta_configuration.GetTransportAddressURI();
      //We take the local transport address plus the port number to be hashed to obtain a random AHAddress
      byte[] hashedbytes = sha.ComputeHash(Encoding.UTF8.GetBytes(local_ta + port));
      //inforce type 0
      hashedbytes[Address.MemSize - 1] &= 0xFE;
      AHAddress _local_ahaddress = new AHAddress(hashedbytes);
      Node this_node = new StructuredNode( _local_ahaddress );
      ///Node this_node = new HybridNode( new AHAddress( new BigInteger( 2*(local_host_index+1) ) ) );      

      String file_string = "brunetadd" + Convert.ToString(desired_port) + ".log";
      StreamWriter sw = new StreamWriter(file_string, false);
      sw.WriteLine( "local_address " + this_node.Address.ToBigInteger().ToString() + " " + Dns.GetHostName()); 
      sw.Close();      

      if ( local_ta_configuration.Protocol == "tcp" ) {
        this_node.AddEdgeListener( new TcpEdgeListener(port) );
      } 
      else if( local_ta_configuration.Protocol == "udp" ) {
        this_node.AddEdgeListener( new UdpEdgeListener(port) );        
      }

      int remote_node_index = local_host_index-1;
      int num_remote_ta = 20; //20 nodes on the list to try to bootstrap to

      if (local_host_index!=0) {
        NodeConfiguration remote_node_configuration = (NodeConfiguration)network_configuration.Nodes[0];
        TransportAddressConfiguration remote_ta_configuration = (TransportAddressConfiguration)remote_node_configuration.TransportAddresses[0];

        String remote_ta = remote_ta_configuration.GetTransportAddressURI(); 
        this_node.RemoteTAs.Add( TransportAddressFactory.CreateInstance( remote_ta  ) );
      }
      
      while ( (remote_node_index>=0) && (num_remote_ta>=0) ) { 
        NodeConfiguration remote_node_configuration = (NodeConfiguration)network_configuration.Nodes[remote_node_index];
        TransportAddressConfiguration remote_ta_configuration = (TransportAddressConfiguration)remote_node_configuration.TransportAddresses[0];

        String remote_ta = remote_ta_configuration.GetTransportAddressURI(); 
        this_node.RemoteTAs.Add( TransportAddressFactory.CreateInstance( remote_ta  ) );

        System.Console.WriteLine("Adding {0}", remote_ta);

          remote_node_index--;
          num_remote_ta--;
        }

      //EchoTester echo_printer = new EchoTester();
      //this_node.Subscribe(AHPacket.Protocol.Echo, echo_printer);

      this_node.Connect();
      DateTime start_time = DateTime.Now;
      TimeSpan elapsed_time = System.DateTime.Now - start_time;
      StreamWriter memory_sw = new StreamWriter("memory_usage.log", true);
      memory_sw.WriteLine("Local_node: {0} start_time_GMT: {1}:{2}", Dns.GetHostName(), DateTime.Now.ToUniversalTime().ToString(),
			 DateTime.Now.ToUniversalTime().Millisecond );
      memory_sw.WriteLine("Time \t \t RSS(KB) \t VSZ(KB)");
      memory_sw.Close();
      while(elapsed_time.TotalSeconds < 3000){ //we log for 3000 seconds
	elapsed_time = System.DateTime.Now - start_time; 
	LogMemoryUsage(elapsed_time);
	System.Threading.Thread.Sleep(4960);               
      }


    }//end of Main fcn

  }

}
