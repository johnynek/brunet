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
 * Brunet.AHPacket
 * Brunet.Address
 * Brunet.CtmRequestHandler
 * Brunet.ConnectionOverlord
 * Brunet.ConnectionType
 * Brunet.DirectionalRouter
 * Brunet.Edge
 * Brunet.LeafConnectionOverlord
 * Brunet.Linker
 * Brunet.IAHPacketHandler
 * Brunet.Node
 * Brunet.PacketForwarder
 * Brunet.StructuredConnectionOverlord
 */

#define PRODUCTION
//to run the connecttester, make sure you change PRODUCTION to DEBUG

using System;
using System.Collections;
//using log4net;

namespace Brunet
{

  /**
   * A node that only makes connections on the structured system
   * and only routes structured address packets.
   */

  public class StructuredNode:Node
  {
    /*private static readonly log4net.ILog log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/


    /**
     * Here are the ConnectionOverlords for this type of Node
     */
    protected ConnectionOverlord _lco;
    protected ConnectionOverlord _sco;

    public StructuredNode(AHAddress add):base(add)
    {
      /**
       * Here are the routers this node uses : 
       */
      ArrayList routers = new ArrayList();
      routers.Add(new AHRouter(add));
      routers.Add(new DirectionalRouter(add));
      SetRouters(routers);

      /**
       * Here are the ConnectionOverlords
       */ 
      _lco = new LeafConnectionOverlord(this);
      //_sco = new StructuredConnectionOverlord(this);
      _sco = new SimpleConnectionOverlord(this);

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

      _connection_table.ConnectionEvent += new EventHandler(this.EstimateSize);
      _connection_table.DisconnectionEvent += new EventHandler(this.EstimateSize);
    }
    
    /**
     * If you want to create a node in a realm other
     * than the default "global" realm, use this
     * @param add AHAddress of this node
     * @param realm Realm this node is to be a member of
     */
    public StructuredNode(AHAddress add, string realm) : this(add)
    {
      _realm = realm;
    }

    protected int _netsize = -1;
    override public int NetworkSize {
      get {
        return _netsize;
      }
    }

    /**
     * Connect to the network.  This informs all the ConnectionOverlord objects
     * to do their thing.
     */
    override public void Connect()
    {
      StartAllEdgeListeners();

      _lco.IsActive = true;
      _sco.IsActive = true;

      _lco.Activate();
      _sco.Activate();
    }
    /**
     * This informs all the ConnectionOverlord objects
     * to not respond to loss of edges, then to issue
     * close messages to all the edges
     * 
     */
    override public void Disconnect()
    {
      _lco.IsActive = false;
      _sco.IsActive = false;

      //Gracefully close all the edges:
      ArrayList edges_to_close = new ArrayList();
      lock( _connection_table.SyncRoot ) {
        foreach(Connection c in _connection_table) {
          edges_to_close.Add( c.Edge );
        }
      }
      foreach(Edge e in edges_to_close) {
        GracefullyClose(e);
      }
      // stop all edge listeners to prevent other nodes
      // from connecting to us
      StopAllEdgeListeners();
    }

    /**
     * When the connectiontable changes, we re-estimate
     * the size of the network:
     */
    protected void EstimateSize(object contab, System.EventArgs args)
    {
      Console.WriteLine("Estimate size: ");
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
	Console.WriteLine("Network size: {0}", _netsize);
      }
      }catch(Exception x) {
        Console.Error.WriteLine(x.ToString());
      }

    }
  }

}


