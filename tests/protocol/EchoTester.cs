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
//using log4net;
//using log4net.Config;

//[assembly:log4net.Config.DOMConfigurator(Watch = true)]

namespace Brunet
{
  public class EchoTester:IAHPacketHandler
  {
  /*private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.
                                        GetCurrentMethod().
                                        DeclaringType);*/
    public static Hashtable uid_starttime = new Hashtable();
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
    }

    public void HandleAHPacket(object node, AHPacket packet, Edge from)
    {
      _message_count++;

      Node node_handler = (Node) node;
      long stop_time, rt_ticks = -10000;

      if (!node_handler.Address.Equals(packet.Source)) {
        byte[] payload = packet.PayloadStream.ToArray();

        if (payload[0] == 0) {
        //log.Debug("Echo Response:");
	  stop_time = System.DateTime.Now.Ticks;
	  int received_uid = NumberSerializer.ReadInt(payload, 1);
          if(uid_starttime.ContainsKey(received_uid)){
		rt_ticks = stop_time - (long)EchoTester.uid_starttime[received_uid];
	  }
	  double rt_ms = (double) rt_ticks/10000.0;
	  uid_brunetpingtime.Add(received_uid, rt_ms);
	  Console.WriteLine("Packet ID = {0}, Round-trip = {1}", received_uid, rt_ms); 	  
        }
        else {
        //log.Debug("Echo Request:");
        }

        //log.Debug(packet.ToString());

        //System.Console.WriteLine("{0}", packet.ToString());

        if (payload[0] > 0) {
          //Send a reply back, this is a request  
          payload[0] = (byte) 0;
          AHPacket resp = new AHPacket( 0,
			                packet.Ttl, node_handler.Address,
			                packet.Source, packet.PayloadType,
					payload);

          node_handler.Send(resp);
        }
      }
    }

    public bool HandlesAHProtocol(AHPacket.Protocol type)
    {
      return (type == AHPacket.Protocol.Echo);
    }

    static void Main(string[] args)
    {
    //log.Debug( "Starting the Brunet Echo Tester" );
      Process _rm_process;
      _rm_process = new Process();  
      _rm_process.StartInfo.FileName = "rm";
      _rm_process.StartInfo.Arguments = "time_stamp.log";
      _rm_process.StartInfo.UseShellExecute = false;
      _rm_process.StartInfo.RedirectStandardInput = false;
      _rm_process.StartInfo.RedirectStandardOutput = false;
      _rm_process.StartInfo.RedirectStandardError = false;
      _rm_process.Start();
      _rm_process.WaitForExit(500);

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
      Node this_node = new HybridNode( _local_ahaddress );
      ///Node this_node = new HybridNode( new AHAddress( new BigInteger( 2*(local_host_index+1) ) ) );      

      String file_string = "brunetadd" + Convert.ToString(desired_port) + ".log";
      StreamWriter sw = new StreamWriter(file_string, false);
      sw.WriteLine( "local_address " + this_node.Address.ToBigInteger().ToString() + " " + Dns.GetHostName()); 
      sw.Close();

#if PLAB_LOG      
      ///Initialize Brunet logger
      BrunetLogger bl = new BrunetLogger(file_string);
#endif
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
        this_node.RemoteTAs.Add( new TransportAddress( remote_ta  ) );
      }
      
      while ( (remote_node_index>=0) && (num_remote_ta>=0) ) { 
        NodeConfiguration remote_node_configuration = (NodeConfiguration)network_configuration.Nodes[remote_node_index];
        TransportAddressConfiguration remote_ta_configuration = (TransportAddressConfiguration)remote_node_configuration.TransportAddresses[0];

        String remote_ta = remote_ta_configuration.GetTransportAddressURI(); 
        this_node.RemoteTAs.Add( new TransportAddress( remote_ta  ) );

        System.Console.WriteLine("Adding {0}", remote_ta);

          remote_node_index--;
          num_remote_ta--;
        }

     /* NodeConfiguration remote_node_configuration = (NodeConfiguration)network_configuration.Nodes[remote_node_index];
      TransportAddressConfiguration remote_ta_configuration = (TransportAddressConfiguration)remote_node_configuration.TransportAddresses[0];

      String remote_ta = remote_ta_configuration.GetTransportAddressURI(); 
      this_node.RemoteTAs.Add( new TransportAddress( remote_ta  ) );*/
 
      EchoTester echo_printer = new EchoTester();
      this_node.Subscribe(AHPacket.Protocol.Echo, echo_printer);

      this_node.Connect();
#if PLAB_LOG      
      this_node.Logger = bl;
#endif 
      //Send a "hello message" to a random neighbor

      int trial = 0;
      ASCIIEncoding ascii = new ASCIIEncoding();

      //Make the target addresses      
      AHAddress target  = new AHAddress( new BigInteger( 2*(remote_node_index+1) ) );

      string hello_msg = "hello, brunet";
      int byteCount = ascii.GetByteCount(hello_msg);
      byte[] bytes = new byte[byteCount + 1];
      int bytesEncodedCount = ascii.GetBytes(hello_msg,
                                                    0,
                                                    hello_msg.Length,
                                                    bytes,
                                                    1);

      // update the payload
      // This is a request, so the first byte is greater than zero
      bytes[0] = (byte) 1;
      AHPacket p = new AHPacket(0, 30,   this_node.Address,
                                     target,
                                     AHPacket.Protocol.Echo, bytes);

      /*bool loop;

      if (args.Length == 4) {
        loop = (args[3] == "loop") ;
      } 
      else {
        loop = false;
      }

      //System.Console.ReadLine();

      System.DateTime start_time;
      System.DateTime current_time;
      TimeSpan elapsed_time;

      do{
	this_node.Send(p);
        if ( ( echo_printer.MessageCount % 500 ) == 0 ) {
          current_time = System.DateTime.Now;
          elapsed_time = current_time - start_time;
          
          double rate = echo_printer.MessageCount / elapsed_time.TotalSeconds;
          System.Console.WriteLine("++++++++++++++++++++++++");
          System.Console.WriteLine("Rate: {0}", rate);
          System.Console.WriteLine("++++++++++++++++++++++++");
        }
      } while (loop);*/

      ///RDP Experiment: sending the echo packet periodically
/*      int seq = 0;
      while(true){
	int start_time = System.DateTime.Now.Millisecond;
	this_node.Send(p);
	Console.WriteLine("Seq = {0}, Start Time = {1}", seq, start_time);
        System.Threading.Thread.Sleep(10000);
	seq++;
      }*/


///The following is a while-loop for the local node to Brunet-ping all other nodes in the network
      System.Threading.Thread.Sleep(30000);  ///IMPORTANT: change this parameter so we wait longer for larger network
      Random uid_generator = new Random( local_ta.GetHashCode() );
      bytes = new byte[5];
      int target_index = 0, num_pings = 3, wait_time = 3000; //the wait_time is in ms
      double ping_time;
      PingWrapper pw = new PingWrapper();
      String rdp_file = "rdp.log";
      StreamWriter rdp_sw = new StreamWriter(rdp_file, false); //boolean variable 'append' is set to false
      while( target_index < network_configuration.Nodes.Count ){
 
 	  if(target_index != local_host_index){///we do not ping the local machine
	      NodeConfiguration target_node_configuration = (NodeConfiguration)network_configuration.Nodes[target_index];
	      TransportAddressConfiguration target_ta_configuration = (TransportAddressConfiguration)target_node_configuration.TransportAddresses[0];
	      short target_port = target_ta_configuration.Port;

	      sha = new SHA1CryptoServiceProvider();
	      String target_ta = target_ta_configuration.GetTransportAddressURI();
	      //We take the transport address plus the port number to be hashed to obtain a random AHAddress
	      hashedbytes = sha.ComputeHash(Encoding.UTF8.GetBytes(target_ta + target_port));
	      //inforce type 0
	      hashedbytes[Address.MemSize - 1] &= 0xFE;
	      AHAddress _target_ahaddress = new AHAddress(hashedbytes);	      
	      	          
	      for(int i = 0; i < num_pings; i++){
		//ping and Brunet-ping the target node for a number of times
                int uid = uid_generator.Next(); //this is the unique id of the packet
		EchoTester.seq_uid.Add(i, uid);
      		// update the payload
      		// This is a request, so the first byte is greater than zero
      		bytes[0] = (byte) 1;
		NumberSerializer.WriteInt(uid, bytes, 1);
      		p = new AHPacket(0, 30, this_node.Address, _target_ahaddress, AHPacket.Protocol.Echo, bytes);
		long start_time = System.DateTime.Now.Ticks;
		EchoTester.uid_starttime.Add(uid, start_time);
		this_node.Send(p);
		ping_time = pw.Ping(target_ta_configuration.Address, wait_time); //wait wait_time number of ms 
		EchoTester.uid_pingtime.Add(uid, ping_time);
		
        	System.Threading.Thread.Sleep(wait_time); 
	      }//end of for-loop
	      System.Threading.Thread.Sleep(10000); //Let the program sleep for 10 seconds before write to file
    	      
    	      ///Write the rdp results to a log file
             
              rdp_sw.WriteLine( "local_address " + Dns.GetHostName() + " remote_address " + target_ta_configuration.Address); 
	      rdp_sw.WriteLine( "Seq" + '\t' + "UID" + '\t' + "Ping" + '\t' + "BNPing" + '\t' + "RDP" );
	      int temp_uid;
	      for(int i = 0; i < seq_uid.Count; i++){
		temp_uid = (int)seq_uid[i];
		double rdp = -1.0;
		if( (double)uid_brunetpingtime[temp_uid] > 0 && (double)uid_pingtime[temp_uid] > 0){
			rdp = (double)uid_brunetpingtime[temp_uid] / (double)uid_pingtime[temp_uid];
		}

		if( (double)uid_brunetpingtime[temp_uid] > 0 && (double)uid_pingtime[temp_uid] > 0 && rdp > 0){
			rdp_sw.WriteLine( "{0} \t {1} \t {2} \t {3} \t {4}", i, seq_uid[i], uid_pingtime[temp_uid],
					   uid_brunetpingtime[temp_uid], rdp);
		}
		else{
			rdp_sw.WriteLine( "{0} \t FAIL", i );
		}
	      }//end of for-loop
	      seq_uid.Clear();
              Console.WriteLine("Finished pinging host: {0}", target_index);

          }//end of if-loop    

    
    	  target_index++;
       }//end of while-loop
       rdp_sw.Close();
       Console.WriteLine("We are done logging.");


    }
  }

}
