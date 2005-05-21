
using System;
using System.Security.Cryptography;
using Brunet;

/**
 * Simple Brunet program that starts a node and potentially
 * connects to other nodes.
 * 
 * Compile this code with:
 * mcs ReqrepExample.cs -r Brunet.dll
 * 
 * you should make a copy of Brunet.dll into the local directory,
 * so the program can use the Brunet library.
 * 
 * Should be helpful for people who want to create applications
 * which use Brunet
 *
 * This Node is a StructuredNode, it only lives on the Structured system.
 *
 * You can do basically the same thing with a HybridNode.
 */
public class ReqrepExample : Brunet.IRequestHandler, Brunet.IReplyHandler {

  public void HandleRequest(ReqrepManager man, ReqrepManager.ReqrepType rt,
		   object req,
		   string prot,
		   System.IO.MemoryStream payload, AHPacket packet)
  {
    /*
     * Write the messages:
     */
    Console.WriteLine("Msg from: {0}", packet.Source);
    payload.WriteTo( System.Console.OpenStandardOutput() );
    Console.WriteLine();
    man.SendReply(req, new byte[0]);
  }

  public bool HandleReply(ReqrepManager man, ReqrepManager.ReqrepType rt,
		   int mid,
		   string prot,
		   System.IO.MemoryStream payload, AHPacket packet,
		   object state)
  {
    Console.WriteLine("{0} got our message", packet.Source);
    return false;
  }

  public void HandleError(ReqrepManager man, int message_number,
		   ReqrepManager.ReqrepError err, object state)
  {
    Console.WriteLine("Got Error from: {0}, {1}", message_number, err);
  }
  
  public static int Main(string[] args) {

    /**
     * Get the arguments
     */
    if( args.Length < 2 ) {
      Console.Error.WriteLine("usage: SNodeExample.exe [tcp|udp] port remota_ta0 remote_ta1 ...");
      return 0;
    }

    /**
     * Make the edge listener:
     */
    Brunet.EdgeListener el = null;
    int port = Int32.Parse( args[1] );
    if( args[0].ToLower() == "tcp" ) {
      el = new Brunet.TcpEdgeListener(port);
    }
    else if( args[0].ToLower() == "udp" ) {
      el = new Brunet.UdpEdgeListener(port);
    }
    /**
     * Create a random address for our node.
     * Some other application might want to select the address
     * a particular way, or reuse a previously selected random
     * address.  If the addresses are not random (or the output
     * of secure hashes) the network might not behave correctly.
     */
    RandomNumberGenerator rng = new RNGCryptoServiceProvider();
    Brunet.AHAddress tmp_add = new Brunet.AHAddress(rng);
    Console.WriteLine("Address: {0}", tmp_add);
    /**
     * Make the node that lives in a particular
     * namespace (or realm) called "testspace"
     */
    Brunet.Node tmp_node = new Brunet.StructuredNode(tmp_add, "testspace");
    Brunet.ReqrepManager rrman = new Brunet.ReqrepManager(tmp_node);
    ReqrepExample irh = new ReqrepExample();
    rrman.Bind(AHPacket.Protocol.Chat, irh);
    /**
     * Add the EdgeListener
     */
    tmp_node.AddEdgeListener( el );
    /**
     * Tell the node who it can connect to:
     */
    for(int i = 2; i < args.Length; i++) {
      tmp_node.RemoteTAs.Add( new Brunet.TransportAddress( args[i] ) );
    }
    /**
     * Now we connect
     */
    tmp_node.Connect();
    Console.WriteLine("Connected");
    /**
     * In a real application, we would create some IAHPacketHandler
     * objects and do:
     * tmp_node.Subscribe( )
     * finally, we could send packets using tmp_node.Send( ) or
     * tmp_node.SendTo( )
     */
    string msg = "";
    System.Text.Encoding coder = new System.Text.ASCIIEncoding();
    while( true ) {
     Console.Write("To: ");
     msg = Console.ReadLine();
     if ( msg == "q" ) { break; }
     Address dest = AddressParser.Parse(msg);
     while( msg != "." ) {
      msg = Console.ReadLine();
      int length = coder.GetByteCount(msg);
      byte[] payload = new byte[length];
      coder.GetBytes(msg, 0, msg.Length, payload, 0);
      rrman.SendRequest(dest, ReqrepManager.ReqrepType.Request, AHPacket.Protocol.Chat,
		        payload, irh , null);
     }
    }
	 
    return 1;
  }
	
}
