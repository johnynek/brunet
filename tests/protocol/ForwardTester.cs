/**
 * Dependencies : 
 * Brunet.Address
 * Brunet.AHAddress
 * Brunet.AHPacket
 * Brunet.Edge
 * Brunet.EdgeListener
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
 * Brunet.ConnectToMessage
 * Brunet.Connector
 * Brunet.ConnectionMessage
 * Brunet.PacketForwarder
 * Brunet.PingMessage
 */

using System;
using System.Collections;
using log4net;
using log4net.Config;
[assembly: log4net.Config.DOMConfigurator(Watch=true)]
namespace Brunet
{
  /** 
   * This class tests the bootstrapping of the brunet system.
   *
   */
  public class ForwardTester : IAHPacketHandler
  {
    ForwardTester()
    {

    }
  
  
         public bool HandlesAHProtocol(AHPacket.Protocol type)
         {
           return (type == AHPacket.Protocol.Echo);
         }
    
    public void HandleAHPacket(object node, AHPacket p,
                                    Edge from)
    {
      Node n = (Node)node;
      Console.WriteLine("Node: " + n.Address.ToString() + " got:");
      Console.WriteLine( p.ToString() );     
    }
    
  static void Main(string[] args)  
  {
    //NodeList
    ArrayList node_list = new ArrayList();
      
    Random my_rand = new Random( unchecked((int)DateTime.Now.Ticks) ); 
    
    //Initialize hosts
    Console.WriteLine("\n\n---------------------------------------\n\n");
    int port1 = 22876;
    int port2 = 22877;
    int port3 = 22878;
    int port4 = 22879;

      //create and initialize new host
      //create one new node for each host
      //Set the last bit to be zero so it will be class 0
      byte[] address = new byte[Address.MemSize];
      my_rand.NextBytes(address);
      address[Address.MemSize - 1] &= 0xFE;
      Node node1 = new StructuredNode(new AHAddress(address));
      node1.AddEdgeListener( new TcpEdgeListener(port1));
      node1.RemoteTAs.Add(
          new TransportAddress("brunet.tcp://127.0.0.1:" + port2 ));
      node1.RemoteTAs.Add(
          new TransportAddress("brunet.tcp://127.0.0.1:" + port3 ));
      
      my_rand.NextBytes(address);
      address[Address.MemSize - 1] &= 0xFE;
      Node node2 = new StructuredNode(new AHAddress(address));
      node2.AddEdgeListener(new TcpEdgeListener(port2));
      node2.RemoteTAs.Add(
          new TransportAddress("brunet.tcp://127.0.0.1:" + port3 ));
      node2.RemoteTAs.Add(
          new TransportAddress("brunet.tcp://127.0.0.1:" + port4 ));
      
      my_rand.NextBytes(address);
      address[Address.MemSize - 1] &= 0xFE;
      Node node3 = new StructuredNode(new AHAddress(address));
      node3.AddEdgeListener(new TcpEdgeListener(port3));
      node3.RemoteTAs.Add(
          new TransportAddress("brunet.tcp://127.0.0.1:" + port4 ));
      node3.RemoteTAs.Add(
          new TransportAddress("brunet.tcp://127.0.0.1:" + port1 ));
          
      my_rand.NextBytes(address);
      address[Address.MemSize - 1] &= 0xFE;
      Node node4 = new StructuredNode(new AHAddress(address));
      node4.AddEdgeListener(new TcpEdgeListener(port4));
      node4.RemoteTAs.Add(
          new TransportAddress("brunet.tcp://127.0.0.1:" + port1 ));
      node4.RemoteTAs.Add(
          new TransportAddress("brunet.tcp://127.0.0.1:" + port2 ));
      
      node_list.Add(node1);
      node_list.Add(node2);
      node_list.Add(node3);
      node_list.Add(node4);

    ForwardTester echo_printer = new ForwardTester();
    foreach( Node item in node_list)
    {
      item.Subscribe(AHPacket.Protocol.Echo, echo_printer);
      item.Connect();
    }

    while (true)
    {
      System.Threading.Thread.Sleep(500);
      Console.WriteLine();
      int intact_leaves =1;
      foreach( Node item in node_list)
      {
	Console.WriteLine("Node: " + item.Address.ToString() );
	Console.WriteLine(item.ConnectionTable.ToString() );
        int leaves = item.ConnectionTable.Count(ConnectionType.Leaf);
        int structs = item.ConnectionTable.Count(ConnectionType.Structured);
        Console.Write(leaves+" ");
        
        if ( 0 < leaves || 0 < structs)
          Console.Write("<");
        
        if (0 < leaves)
        {
          intact_leaves *= 1;
          Console.Write( leaves );
        }
        else
        {
          intact_leaves *=0;
        }
        
        if ( 0 < leaves && 0 < structs)
          Console.Write(".");
        
        if (0 < structs )
        {
           Console.Write("."+structs );
        }
        if ( 0 < leaves || 0 < structs)
        Console.Write("> ");
        
      }
      
      if ( intact_leaves == 1 )
      {
      PingMessage ping =
        new PingMessage();
      short t_hops = 0;
      short t_ttl = 3;

      AHPacket ping_pack =
        new AHPacket(t_hops, t_ttl, node1.Address, node3.Address,
          AHPacket.Protocol.Echo,
          ping.ToByteArray());
      AHPacket forward_pack =
        PacketForwarder.WrapPacket(node2.Address, 5, ping_pack);
      Console.WriteLine( forward_pack.ToString() );
  
      Console.WriteLine("Node: " + node1.Address.ToString() + " sending:");
      node1.Send(forward_pack);
      }
    }
    
  }
  }
}
