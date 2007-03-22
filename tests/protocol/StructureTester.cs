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
#define PACKET_LOG
//#define MEMORY_LOG
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

//[assembly:log4net.Config.DOMConfigurator(Watch = true)]

namespace Brunet
{
#if ECHO
  public class StructureTester:IAHPacketHandler
#else
  public class StructureTester
#endif			     
  {

    ///This tester simply establishes the Brunet network and log the edges made
     private BrunetLogger bl;
#if ECHO
     private static int echo_uid;
#endif

     public StructureTester(int port, AHAddress local_add, bool net_stream, String server_ipadd, 
		     int server_port, int offset, StreamWriter fs)
     {
          bl = new BrunetLogger(port, local_add, net_stream, server_ipadd, server_port, offset, fs); 
     }
    
     public void SignalCatcher(int v)
     {
          Console.WriteLine("Signal received: " + v);
	  bl.GracefullyCloseStream();
          Environment.Exit(0);
     }  
     
     public void SignalCatcherStartRDP(int v)
     {
          Console.WriteLine("Signal received: " + v);
     }  
#if ECHO
/*    public static Hashtable uid_starttime = new Hashtable();
    public static Hashtable uid_brunetpingtime = new Hashtable();
    public static Hashtable uid_pingtime = new Hashtable();
    public static Hashtable seq_uid = new Hashtable();

    private long _message_count=0;
    
    public long MessageCount
    {
      get
      {
        return _message_count;
      }
    }*/
    public void HandleAHPacket(object node, AHPacket packet, Edge from)
    {
      //_message_count++;

      Node node_handler = (Node) node;
      long stop_time, rt_ticks = -10000;

      if (!node_handler.Address.Equals(packet.Source)) {
        byte[] payload = packet.PayloadStream.ToArray();

/*        if (payload[0] == 0) {
        //log.Debug("Echo Response:");
	  stop_time = System.DateTime.Now.Ticks;
	  int received_uid = NumberSerializer.ReadInt(payload, 1);
          if(uid_starttime.ContainsKey(received_uid)){
		rt_ticks = stop_time - (long)uid_starttime[received_uid];
	  }
	  double rt_ms = (double) rt_ticks/10000.0;
	  uid_brunetpingtime.Add(received_uid, rt_ms);
	  Console.WriteLine("Packet ID = {0}, Round-trip = {1}", received_uid, rt_ms); 	  
        }
        else {
        //log.Debug("Echo Request:");
        }*/

        //log.Debug(packet.ToString());

        //System.Console.WriteLine("{0}", packet.ToString());

        if (payload[0] > 0 && packet.Destination.Equals(node_handler.Address)) {
          //Send a reply back, this is a request  
          payload[0] = (byte) 0;
          AHPacket resp = new AHPacket( 0,
			                packet.Ttl, node_handler.Address,
			                packet.Source, packet.PayloadType,
					payload);

          node_handler.Send(resp);
        }
	else if(payload[0] == 0 && packet.Destination.Equals(node_handler.Address)){
          echo_uid = NumberSerializer.ReadInt(packet.PayloadStream.ToArray(), 2);
	}
      }
    }

    public bool HandlesAHProtocol(string type)
    {
      return (type == AHPacket.Protocol.Echo);
    }
#endif

    static void Main(string[] args)
    {

      String config_file = args[0];
      NetworkConfiguration network_configuration = NetworkConfiguration.Deserialize(config_file);

      int port_selection = Convert.ToInt32(args[1]); //There will be 10 different ports available for use: 0, 1, 2..
      //for example, node 0 on a machine will use port_selection # 0, node 1 on a machine will use port_selection # 1
      
      string host_ip = "";
      if(args.Length > 2){
        host_ip = args[2];
	Console.WriteLine(host_ip);
      }

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
	
      ///The line below is used when there is only one node per machine
      //int local_host_index = network_configuration.GetLocalHostIndex();                                                                
	
      int desired_port = port_list[port_selection];

      int local_host_index;
      if(host_ip != ""){
        local_host_index = network_configuration.GetLocalHostIndex(desired_port,host_ip); 
	Console.WriteLine("Using host ip, index is: {0}", local_host_index);
      }
      else{
        local_host_index = network_configuration.GetLocalHostIndex(desired_port); 
	Console.WriteLine("Not using host ip, index is: {0}", local_host_index);
      }

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
      Node this_node = new HybridNode( _local_ahaddress );
      //Node this_node = new StructuredNode( _local_ahaddress );
      ///Node this_node = new HybridNode( new AHAddress( new BigInteger( 2*(local_host_index+1) ) ) );      

/*      String file_string = "./data/brunetadd" + Convert.ToString(desired_port) + ".log";
      StreamWriter sw = new StreamWriter(file_string, false);
      sw.WriteLine( "local_address " + this_node.Address.ToBigInteger().ToString() + " " + Dns.GetHostName() + ":" + desired_port);
      sw.Flush();
      sw.Close();  */
#if PACKET_LOG
      String file_packet = "./data/packet" + Convert.ToString(desired_port) + ".log";
      StreamWriter packet_sw = new StreamWriter(file_packet, false);
      packet_sw.WriteLine("Local_node: {0}:{1} start_time_GMT: {2}:{3} local_address {4}", Dns.GetHostName(), 
		      desired_port, DateTime.Now.ToUniversalTime().ToString("MM'/'dd'/'yyyy' 'HH':'mm':'ss"), 
		      DateTime.Now.ToUniversalTime().Millisecond, this_node.Address.ToBigInteger().ToString() ); 
      packet_sw.Close(); 
#endif
      
      if ( local_ta_configuration.Protocol == "tcp" ) {
        this_node.AddEdgeListener( new TcpEdgeListener(port) );
      } 
      else if( local_ta_configuration.Protocol == "udp" ) {
        this_node.AddEdgeListener( new UdpEdgeListener(port) );        
      }

      int remote_node_index = local_host_index-1;
      int num_remote_ta = 150; //20 nodes on the list to try to bootstrap to

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

        //System.Console.WriteLine("Adding {0}", remote_ta);

          remote_node_index--;
          num_remote_ta--;
        }

#if PLAB_LOG
      int time_diff = 0;
      String td_file_string = "~/joe/time_diff.txt";
      if(File.Exists(td_file_string)){
         StreamReader sr = new StreamReader(td_file_string);
         time_diff = Convert.ToInt32( sr.ReadLine() );     
	 sr.Close();
      }           
      String _connection_log_file = "./data/" + "brunetadd" + Convert.ToString(desired_port) + ".log";
      if(File.Exists(_connection_log_file)){
	 File.Delete(_connection_log_file);
      }
      StreamWriter fs = new StreamWriter(_connection_log_file, true);
      fs.AutoFlush = true;
      
      ///Initialize Brunet logger      
      //bl = new BrunetLogger(desired_port, (AHAddress)this_node.Address, true, "tcp://cantor.ee.ucla.edu:8003");
      //The line below is for cantor
      StructureTester st = new StructureTester(desired_port, (AHAddress)this_node.Address, 
		      false, "128.97.88.154", 8002, time_diff, fs); 
      //The line below is for cobweb
      //StructureTester st = new StructureTester(desired_port, (AHAddress)this_node.Address, false, "164.67.194.45", 8002); 
      //set true for network stream
      this_node.Logger = st.bl;

#if ECHO
      this_node.Subscribe(AHPacket.Protocol.Echo, st);
#endif
           
      //Syscall.signal(31, new Syscall.sighandler_t(st.SignalCatcher));
      //Syscall.signal(18, new Syscall.sighandler_t(st.SignalCatcherStartRDP));      
#endif

      this_node.Connect();   

#if MEMORY_LOG
      //first, remove the log file
      String file_memory = "./data/memory" + Convert.ToString(desired_port) + ".log";
      if(File.Exists(file_memory)){
	File.Delete(file_memory);
      }
      StreamWriter memory_sw = new StreamWriter(file_memory, true);
      memory_sw.WriteLine("Local_node: {0}:{1} start_time_GMT: {2}:{3} local_address {4}", Dns.GetHostName(), 
		      desired_port, DateTime.Now.ToUniversalTime().ToString("MM'/'dd'/'yyyy' 'HH':'mm':'ss"), 
		      DateTime.Now.ToUniversalTime().Millisecond, this_node.Address.ToBigInteger().ToString() ); 
      memory_sw.WriteLine("Time \t \t RSS(KB) \t VSZ(KB)");
      memory_sw.Flush();
      PsWrapper psw = new PsWrapper();
      int interval_min = 1;
      TimeSpan elapsed_time;
      DateTime start_time = DateTime.Now;
      while(true){ //we log for 3000 seconds
	elapsed_time = System.DateTime.Now - start_time;
	try{
	   psw.Ps();
	   memory_sw.WriteLine("{0} \t {1} \t \t {2}", elapsed_time.TotalMilliseconds, psw.Rss, psw.Vsz );
	   memory_sw.Flush();
	   System.Threading.Thread.Sleep(interval_min*60*1000);               
	}
	catch(Exception e){
           memory_sw.WriteLine("Fail to start the ps process");
	   memory_sw.Flush();
           break;
	}
      }
      //memory_sw.Close();
#endif
      
#if ECHO 
      ///The following is a while-loop for the local node to Brunet-ping all other nodes in the network
      int sleep_time_min = 240;
      System.Threading.Thread.Sleep(sleep_time_min*60*1000);  ///IMPORTANT: change this parameter so we wait longer for larger network
      /*
      while(!st.log_rdp){
         System.Threading.Thread.Sleep(5000);
      }
      */
      Random uid_generator = new Random( DateTime.Now.Millisecond + local_ta.GetHashCode() + port);
      byte[] bytes = new byte[6];
      int target_index = 0, num_pings = 10, wait_time = 2000, uid; //the wait_time is in ms
      double ping_time;
      PingWrapper pw = new PingWrapper();    
      AHPacket p;
      while( target_index < network_configuration.Nodes.Count ){
	  NodeConfiguration target_node_configuration = (NodeConfiguration)network_configuration.Nodes[target_index];
	  TransportAddressConfiguration target_ta_configuration = (TransportAddressConfiguration)target_node_configuration.TransportAddresses[0];
          if(target_index != local_host_index && target_ta_configuration.Address != local_ta_configuration.Address){///we do not ping the local machine
	      short target_port = target_ta_configuration.Port;
	      uid = uid_generator.Next(); //this is the unique id of the packet
	      // update the payload
	      // This is a request, so the first byte is greater than zero
	      sha = new SHA1CryptoServiceProvider();
	      String target_ta = target_ta_configuration.GetTransportAddressURI();
	      //We take the transport address plus the port number to be hashed to obtain a random AHAddress
	      hashedbytes = sha.ComputeHash(Encoding.UTF8.GetBytes(target_ta + target_port));
	      //inforce type 0
	      hashedbytes[Address.MemSize - 1] &= 0xFE;
	      AHAddress _target_ahaddress = new AHAddress(hashedbytes);	
	      bytes[0] = (byte) 1;
	      bytes[1] = (byte) 1; /// 1 for testing to see node up or not
	      NumberSerializer.WriteInt(uid, bytes, 2);
	      p = new AHPacket(0, 30, this_node.Address, _target_ahaddress, AHPacket.Protocol.Echo, bytes);
              this_node.Send(p);
	      Console.WriteLine("Brunet-pinging {0} on port {1} with uid {2}", target_ta_configuration.Address, target_port, uid);
	      bool received = false;
	      int receive_counter = 0;
	      while(!received && receive_counter < 10){
                 receive_counter++;
                 System.Threading.Thread.Sleep(1000);
		 if(echo_uid == uid){
                    received = true;
		    Console.WriteLine("Got echo packet with uid {0} on loop {1}", echo_uid, receive_counter);
		 }	
	      }
	      double ping1 = pw.Ping(target_ta_configuration.Address, wait_time);
	      double ping2 = pw.Ping(target_ta_configuration.Address, wait_time);
	      if(received && (ping1 >= 0 || ping2 >= 0)){ //we gather the data only when the node is ping-able      
#if PLAB_LOG
		  ///Write the header to a log file  
		  st.bl.LogBPHeader(local_ta_configuration.Address, local_ta_configuration.Port, 
				  target_ta_configuration.Address, target_ta_configuration.Port);  
		  st.bl.LogPingHeader(local_ta_configuration.Address, local_ta_configuration.Port, 
				  target_ta_configuration.Address, target_ta_configuration.Port);
#endif
		  for(int i = 0; i < num_pings; i++){
		    //ping and Brunet-ping the target node for a number of times
		    uid = uid_generator.Next(); //this is the unique id of the packet
		    // update the payload
		    // This is a request, so the first byte is greater than zero
		    bytes[0] = (byte) 1;
		    bytes[1] = (byte) 0; /// 0 for regular brunet-pinging
		    NumberSerializer.WriteInt(uid, bytes, 2);
		    p = new AHPacket(0, 30, this_node.Address, _target_ahaddress, AHPacket.Protocol.Echo, bytes);

		    this_node.Send(p);
		    ping_time = pw.Ping(target_ta_configuration.Address, wait_time); //wait wait_time number of ms
#if PLAB_LOG
		    st.bl.LogPing(ping_time);	
#endif		   
		    System.Threading.Thread.Sleep(1000); 
		  }//end of for-loop 		  
	        }
	        received = false;
	        receive_counter = 0;
	        while(!received && receive_counter < 5){
                  receive_counter++;
                  System.Threading.Thread.Sleep(1000);
		  if(echo_uid == uid){
                    received = true;
		    Console.WriteLine("Got echo packet with uid {0} on loop {1}", echo_uid, receive_counter);
		  }	
	        }
          }//end of if-loop        
    	  target_index++;
       }//end of while-loop
#endif
    }//end of Main fcn

  }

}
