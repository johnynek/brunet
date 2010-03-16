
using System;
using System.Security.Cryptography;
using Brunet;
using Brunet.Util;
using Brunet.Transport;
using Brunet.Messaging;
using Brunet.Symphony;

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
public class ReqrepExample : IDataHandler, IReplyHandler {

  public void HandleData(MemBlock data, ISender return_path, object state)
  {
    /*
     * Write the messages:
     */
    Console.WriteLine("Msg from: {0}", return_path);
    data.ToMemoryStream().WriteTo( System.Console.OpenStandardOutput() );
    Console.WriteLine();
    return_path.Send( new CopyList(PType.Protocol.Chat, MemBlock.Null) );
  }

  public bool HandleReply(ReqrepManager man, ReqrepManager.ReqrepType rt,
		   int mid,
		   PType prot,
		   MemBlock payload,
		   ISender from,
		   ReqrepManager.Statistics s,
		   object state)
  {
    Console.WriteLine("{0} got our message", from);
    return false;
  }

  public void HandleError(ReqrepManager man, int message_number,
		   ReqrepManager.ReqrepError err, ISender from, object state)
  {
    Console.WriteLine("Got Error from: {0}, {1}, {2}", from, message_number, err);
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
    EdgeListener el = null;
    int port = Int32.Parse( args[1] );
    if( args[0].ToLower() == "tcp" ) {
      el = new TcpEdgeListener(port);
    }
    else if( args[0].ToLower() == "udp" ) {
      el = new UdpEdgeListener(port);
    }
    /**
     * Create a random address for our node.
     * Some other application might want to select the address
     * a particular way, or reuse a previously selected random
     * address.  If the addresses are not random (or the output
     * of secure hashes) the network might not behave correctly.
     */
    RandomNumberGenerator rng = new RNGCryptoServiceProvider();
    AHAddress tmp_add = new AHAddress(rng);
    Console.WriteLine("Address: {0}", tmp_add);
    /**
     * Make the node that lives in a particular
using Brunet.Messaging;
     * namespace (or realm) called "testspace"
     */
    Node tmp_node = new StructuredNode(tmp_add, "testspace");
    ReqrepManager rrman = tmp_node.Rrm;
    ReqrepExample irh = new ReqrepExample();
    tmp_node.GetTypeSource(PType.Protocol.Chat).Subscribe(irh, tmp_node);
    /**
     * Add the EdgeListener
     */
    tmp_node.AddEdgeListener( el );
    /**
     * Tell the node who it can connect to:
     */
    for(int i = 2; i < args.Length; i++) {
      tmp_node.RemoteTAs.Add( TransportAddressFactory.CreateInstance( args[i] ) );
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
      ISender sender = new AHSender(tmp_node, dest);
      rrman.SendRequest(sender, ReqrepManager.ReqrepType.Request,
                        new CopyList(PType.Protocol.Chat, MemBlock.Reference(payload)),
			irh , null);
     }
    }
	 
    return 1;
  }
	
}
