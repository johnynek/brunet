using System;
using System.Text;
using System.Collections;
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

      if (!node_handler.Address.Equals(packet.Source)) {
        byte[] payload = packet.PayloadStream.ToArray();

        if (payload[0] == 0) {
        //log.Debug("Echo Response:");
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

    static void Main(string[]args)
    {
    //log.Debug( "Starting the Brunet Echo Tester" );

      String config_file = args[0];
      NetworkConfiguration network_configuration = NetworkConfiguration.Deserialize(config_file);

      int local_host_index = Int16.Parse( args[1] );                                                                
      NodeConfiguration this_node_configuration = (NodeConfiguration)network_configuration.Nodes[local_host_index];
      TransportAddressConfiguration local_ta_configuration = (TransportAddressConfiguration)this_node_configuration.TransportAddresses[0];
      short port = local_ta_configuration.Port;

      Node this_node = new HybridNode( new AHAddress( new BigInteger( 2*(local_host_index+1) ) ) );      
      if ( local_ta_configuration.Protocol == "tcp" ) {
        this_node.AddEdgeListener( new TcpEdgeListener(port) );
      } 
      else if( local_ta_configuration.Protocol == "udp" ) {
        this_node.AddEdgeListener( new UdpEdgeListener(port) );        
      }

      int remote_node_index = Int16.Parse( args[2] );
      NodeConfiguration remote_node_configuration = (NodeConfiguration)network_configuration.Nodes[remote_node_index];
      TransportAddressConfiguration remote_ta_configuration = (TransportAddressConfiguration)remote_node_configuration.TransportAddresses[0];

      String remote_ta = remote_ta_configuration.GetTransportAddressURI(); 
      this_node.RemoteTAs.Add( new TransportAddress( remote_ta  ) );
 
      EchoTester echo_printer = new EchoTester();
      this_node.Subscribe(AHPacket.Protocol.Echo, echo_printer);

      this_node.Connect();
 
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

      bool loop;

      if (args.Length == 4) {
        loop = (args[3] == "loop") ;
      } 
      else {
        loop = false;
      }

      System.Console.ReadLine();

      System.DateTime start_time = System.DateTime.Now;
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
      } while (loop);

    }
  }

}
