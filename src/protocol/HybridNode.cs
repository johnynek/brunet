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

//#define PRODUCTION
//to run the connecttester, make sure you change PRODUCTION to DEBUG
#define DEBUG  //Unstructured network is not formed

using System;
using System.Collections;

namespace Brunet
{

  /**
   * A node that makes connections on the structured and unstructured system 
   */

  public class HybridNode:Node
  {

    protected Hashtable _connectionoverlords;

    protected int _netsize = -1;
    override public int NetworkSize {
      get {
        return _netsize;
      }
    }
    
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
      _connectionoverlords[ ConnectionType.Leaf ] = co;
      //DEBUG: forget about structured connections for now
      co = new StructuredConnectionOverlord(this);
#if PLAB_LOG
      co.Logger = this.Logger;
#endif
      _connectionoverlords[ ConnectionType.Structured ] = co;

      co = new UnstructuredConnectionOverlord(this);
      _connectionoverlords[ ConnectionType.Unstructured ] = co;

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

      /*
       * When the ConnectionTable changes,
       * reestimate the size of the network
       */
      _connection_table.ConnectionEvent += new EventHandler(this.EstimateSize);
      _connection_table.DisconnectionEvent += new EventHandler(this.EstimateSize);
      
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

	//	Console.WriteLine("I am in DEBUG!!");
	//	Console.ReadLine();
	    ConnectionType tl = ConnectionType.Leaf;
	    LeafConnectionOverlord col = (LeafConnectionOverlord) _connectionoverlords[tl];
            if (col != null) {
              col.IsActive = true;
            }

	    ConnectionType ts = ConnectionType.Structured;
	    StructuredConnectionOverlord cos = (StructuredConnectionOverlord) _connectionoverlords[ts];
            if (cos != null) {
              cos.IsActive = true;
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

    /**
     * When the connectiontable changes, we re-estimate
     * the size of the network:
     */
    protected void EstimateSize(object contab, System.EventArgs args)
    {
      //Console.WriteLine("Estimate size: ");
      try {
      ConnectionTable tab = (ConnectionTable)contab;
      int net_size = -1;
      lock( tab.SyncRoot ) {
	int leafs = tab.Count(ConnectionType.Leaf);
        if( leafs > net_size ) {
          net_size = leafs;
	}
	int structs = tab.Count(ConnectionType.Structured);
        if( structs > net_size ) {
          net_size = structs;
	}
        /*
	 * We estimate the density of nodes in the address space,
	 * and since we know the size of the whole address space,
	 * we can use the density to estimate the number of nodes.
	 */
        BigInteger least_dist = 0;
	BigInteger greatest_dist = 0;
	AHAddress local = (AHAddress)_local_add;
	int shorts = 0;
        foreach(Connection c in tab.GetConnections("structured.near")) {
          BigInteger dist = local.DistanceTo( (AHAddress)c.Address );
          
	  if( shorts == 0 ) {
            //This is the first one
            least_dist = dist;
	    greatest_dist = dist;
	  }
	  else {
            if( dist > greatest_dist ) {
              greatest_dist = dist;
	    }
	    if( dist < least_dist ) {
              least_dist = dist;
	    }
	  } 
	  shorts++;
	}
	/*
	 * Now we have the distance between the range of our neighbors
	 */
	BigInteger width = greatest_dist - least_dist;
	if( shorts > 0 && width > 0 ) {
	  //Here is our estimate of the inverse density:
	  BigInteger inv_density = width/(shorts);
          //The density times the full address space is the number
	  BigInteger total = Address.Full / inv_density;
	  int total_int = total.IntValue();
	  if( total_int > net_size ) {
            net_size = total_int;
	  }
	}

	//Now we have our estimate:
	_netsize = net_size;
	//Console.WriteLine("Network size: {0}", _netsize);
      }
      }catch(Exception x) {
        Console.Error.WriteLine(x.ToString());
      }

    }
    
  }

}


