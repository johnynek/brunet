/**
 * Dependencies : 
 * Brunet.AHAddress
 * Brunet.AHRouter
 * Brunet.RwpRouter
 * Brunet.RwtaRouter
 * Brunet.AHPacket
 * Brunet.Address
 * Brunet.CtmRequestHandler
 * Brunet.ConnectionOverlord
 * Brunet.ConnectionType
 * Brunet.DirectionalRouter
 * Brunet.LeafConnectionOverlord
 * Brunet.IAHPacketHandler
 * Brunet.Node
 * Brunet.PacketForwarder
 * Brunet.StructuredConnectionOverlord
 * Brunet.UnstructuredConnectionOverlord
 */

#define PRODUCTION
//to run the connecttester, make sure you change PRODUCTION to DEBUG

using System;
using System.Collections;

namespace Brunet
{

  /**
   * A node that only makes connections on the structured system
   * and only routes structured address packets.
   */

  public class HybridNode:Node
  {

    protected Hashtable _connectionoverlords;

    public HybridNode(AHAddress add):base(add)
    {

      /**
       * Here are the routers this node uses : 
       */
      ArrayList routers = new ArrayList();
      routers.Add(new AHRouter(add));
      routers.Add(new DirectionalRouter(add));
      routers.Add(new RwpRouter());
      routers.Add(new RwtaRouter());

      SetRouters(routers);

      /**
       * Here are the ConnectionOverlords
       */ 
      _connectionoverlords = new Hashtable();
      ConnectionOverlord co = new LeafConnectionOverlord(this);
      _connectionoverlords[ co.ConnectionType ] = co;
      //DEBUG: forget about structured connections for now
      co = new StructuredConnectionOverlord(this);
      _connectionoverlords[ co.ConnectionType ] = co;

      co = new UnstructuredConnectionOverlord(this);
      _connectionoverlords[ co.ConnectionType ] = co;

      /**
       * Turn on some protocol support : 
       */
      /// Turn on Packet Forwarding Support :
      IAHPacketHandler h = new PacketForwarder(add);
      Subscribe(AHPacket.Protocol.Forwarding, h);
      /**
       * Here is how we handle ConnectToMessages : 
       */
      h = new CtmRequestHandler();
      Subscribe(AHPacket.Protocol.Connection, h);
    }

    /**
     * Connect to the network.  This informs all the ConnectionOverlord objects
     * to do their thing.
     */
    //ATTENTION: (Debug) To run the connecttester, do the following:
    override public void Connect()
    {
      StartAllEdgeListeners();

      lock(_sync) {
	    #if DEBUG
        Console.WriteLine("I am in DEBUG!!");
        Console.ReadLine();
        ConnectionType t = ConnectionType.Leaf;
        LeafConnectionOverlord co = (LeafConnectionOverlord) _connectionoverlords[t];
        if (co != null) {
          co.IsActive = true;
        }

	    #elif PRODUCTION	
        //Console.WriteLine("I am in PRODUCTION!!");

        //And then, comment out the following up till ***
        foreach(ConnectionType t
                in System.Enum.GetValues(typeof(ConnectionType)))
        {
          ConnectionOverlord co = (ConnectionOverlord) _connectionoverlords[t];
          //Make sure we have this kind of ConnectionOverlord
          if (co != null) {
            co.IsActive = true;
          }
        }
        foreach(ConnectionType t
                in System.Enum.GetValues(typeof(ConnectionType)))
        {
          ConnectionOverlord co = (ConnectionOverlord) _connectionoverlords[t];
          //Make sure we have this kind of ConnectionOverlord
          if (co != null) {
            co.Activate();
          }
        }
	  #endif
      }
    }
    /**
     * This informs all the ConnectionOverlord objects
     * to not respond to loss of edges, then to issue close messages to all the edges
     * 
     */
    override public void Disconnect()
    {
      lock(_sync) {
        foreach(ConnectionType t
                in System.Enum.GetValues(typeof(ConnectionType))) {
          ConnectionOverlord co =
            (ConnectionOverlord) _connectionoverlords[t];
          //Make sure we have this kind of ConnectionOverlord
          if (co != null) {
            co.IsActive = false;
          }
        }

        // close and remove all edges for the node
        //ConnectionTable.CloseAllEdges();

        // stop all edge listeners to prevent other nodes
        // from connecting to us
        StopAllEdgeListeners();
      }
    }
  }

}


