/**
 * Dependencies : 
 * Brunet.Address
 * Brunet.AHAddress
 * Brunet.AHPacket
 * Brunet.Edge
 * Brunet.EdgeListener
 * Brunet.Node
 * Brunet.ConnectionType
 * Brunet.ConnectionMessage
 * Brunet.IAHPacketHandler
 * Brunet.StructuredNode
 * Brunet.TcpEdgeListener
 * Brunet.UdpEdgeListener
 * Brunet.TransportAddress
 * Brunet.Connector
 * Brunet.CtmRequestHandler
 * Brunet.ConnectToMessage
 * Brunet.PingMessage
 * Brunet.LeafConnectionOverlord
 * Brunet.StructuredConnectionOverlord
 */

using System;
using System.Text;
using System.Collections;
//using log4net;
//using log4net.Config;
//[assembly:log4net.Config.DOMConfigurator(Watch = true)]
namespace Brunet
{
  /** 
   * This class tests the connector and the ctmrequesthandler
   * of the brunet system.  Basically, I will start two nodes, node A with the
   * connector and node B with the ctmrequesthandler.  Then, node A will send
   * ctm packet to node B and node B responds.   
   */
  public class ConnectTester : IAHPacketHandler
  {
  
  ConnectTester()
  {

  }

  public bool HandlesAHProtocol(AHPacket.Protocol type)
  {
    return (type == AHPacket.Protocol.Connection);
  }

  public void HandleAHPacket(object node, AHPacket p, Edge from)
  {
    Node n = (Node)node;
    Console.WriteLine("Node: " + n.Address.ToString() + " got:");
    Console.WriteLine( p.ToString() );
  }
  
  static void Main(string[] args)  
  {
    Console.WriteLine("Warning: before running this test, make sure that " +
		    "you have set the #define directive to DEBUG in structurednode.cs");
    Console.WriteLine("Press the enter key to continue.");
    Console.ReadLine();
    //NodeList
    ArrayList node_list = new ArrayList();

    Random my_rand = new Random( unchecked((int)DateTime.Now.Ticks) ); 
        
    byte[] byte_arr_A = new byte[Address.MemSize];
    byte[] byte_arr_B = new byte[Address.MemSize];

    my_rand.NextBytes(byte_arr_A);
    my_rand.NextBytes(byte_arr_B);
    byte_arr_A[Address.MemSize - 1] &= 0xFE;
    byte_arr_B[Address.MemSize - 1] &= 0xFE;
    AHAddress address_A = new AHAddress(byte_arr_A);
    AHAddress address_B = new AHAddress(byte_arr_B);

    Console.WriteLine("Welcome to the Connect tester!!");
    
    int port_A = 8000;
    int port_B = 8008;
    int dummyport = 8888;

    Node node_A = new StructuredNode(address_A);
    node_A.AddEdgeListener(new TcpEdgeListener(port_A));
    node_A.RemoteTAs.Add(new TransportAddress("brunet.tcp://127.0.0.1:" +
                     port_B));
    //node_A.RemoteTAs.Add(new TransportAddress("brunet.tcp://127.0.0.1:" +
      //               dummyport));

    Node node_B = new StructuredNode(address_B);
    node_B.AddEdgeListener(new TcpEdgeListener(port_B));
    node_B.RemoteTAs.Add(new TransportAddress("brunet.tcp://127.0.0.1:" +
				port_A));
    //node_B.RemoteTAs.Add(new TransportAddress("brunet.tcp://127.0.0.1:" +
      //               dummyport));

    node_list.Add(node_A);
    node_list.Add(node_B);

    ConnectTester connection_printer = new ConnectTester();
    foreach( Node item in node_list)
    {
      item.Subscribe(AHPacket.Protocol.Echo, connection_printer);
      item.Connect();
      //Only establish leaf connection here
    }

    while (true)
    {
      System.Threading.Thread.Sleep(500);
      Console.WriteLine();
      int intact_leaves = 1;
      foreach( Node item in node_list)
      {
	Console.WriteLine("Node: " + item.Address.ToString() );
	Console.WriteLine(item.ConnectionTable.ToString() );
        int leaves = item.ConnectionTable.Count(ConnectionType.Leaf);
        
        Console.Write(leaves+" ");
        
        if (0 < leaves)
        {
          intact_leaves *= 1;
          Console.Write( leaves );
          Console.Write(".");
          Console.Write("> ");
        }
        else
        {
          intact_leaves *=0;
        }
                             
      }//end of foreach loop
      
      if ( intact_leaves == 1 ) //this is the case that both nodes have leaves
      {
	      Console.WriteLine("We are ready!");
	      System.Threading.Thread.Sleep(2500);
	      Connector my_con = new Connector(node_A);
	      ArrayList ta_list_B = new ArrayList();
	      ta_list_B.Add(new TransportAddress("brunet.tcp://127.0.0.1:" +
				port_B));

	      CtmRequestHandler my_handler = new CtmRequestHandler();
	      System.Threading.Thread.Sleep(500);
	      
	      ArrayList ta_list_A = new ArrayList();
	      ta_list_A.Add(new TransportAddress("brunet.tcp://127.0.0.1:" +
				port_A));
	      ConnectToMessage ctm = new ConnectToMessage(
			      ConnectionType.Structured, address_A, ta_list_A);
	      ctm.Id = my_rand.Next(1, Int32.MaxValue);
              ctm.Dir = ConnectionMessage.Direction.Request;
              AHPacket ctm_pack = new AHPacket(0, 8, address_A, address_B, 
			AHPacket.Protocol.Connection, ctm.ToByteArray() );
	      my_con.Connect(ctm_pack, ctm.Id);
	      Console.WriteLine("Sending the following packet: ");
              Console.WriteLine( ctm_pack.ToString() );            

	      //my_handler.HandleAHPacket(node_B, ctm_pack, from);//???  don't need this?
	      
      	      foreach( Node item in node_list)
              {
		      int leaves = item.ConnectionTable.Count(ConnectionType.Leaf);
              	      int structs = item.ConnectionTable.Count(ConnectionType.Structured);
		      Console.WriteLine("This node has " + leaves + " leaf connections");
		      Console.WriteLine("This node has " + structs + " structured connections");
		      System.Threading.Thread.Sleep(1500);
	      }
      }
      
    } //end of while loop
    
 
  }// end of Main function

  }

}




