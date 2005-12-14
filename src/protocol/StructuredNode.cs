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
    //added the new ChotaConnectionOverlord
    protected ConnectionOverlord _cco;

    //maximum number of neighbors we report in our status
    protected static readonly int MAX_NEIGHBORS = 6;

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
      //ChotaConnectionOverlord
      _cco = new ChotaConnectionOverlord(this);

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
      _connection_table.ConnectionEvent += new EventHandler(this.UpdateNeighborStatus);
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

#if PLAB_LOG
    override public BrunetLogger Logger{
      get{
        return _logger;
      }
      set
      {
        _logger = value;
        //The connection table only has a logger in this case
        _connection_table.Logger = value;
        _sco.Logger = value;
        foreach(EdgeListener el in _edgelistener_list) {
          el.Logger = value;
        }
        foreach(AHPacket.Protocol prot in _subscription_table.Keys) {
          if(prot == AHPacket.Protocol.Forwarding){
            ArrayList handlers = (ArrayList)_subscription_table[prot];
            foreach(IAHPacketHandler handler in handlers){ 
              try{
                ((PacketForwarder)handler).Logger = Logger;
              }
              catch(InvalidCastException e){}             
            }
          }
          else if(prot == AHPacket.Protocol.Connection){
            ArrayList handlers = (ArrayList)_subscription_table[prot];
            foreach(IAHPacketHandler handler in handlers){ 
              try{
                ((CtmRequestHandler)handler).Logger = Logger;
              }
              catch(InvalidCastException e){}             
            }
          }

        }
      }
    } 
#endif


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
#if PLAB_LOG
      //_sco.Logger = this.Logger;
#endif
      StartAllEdgeListeners();

      _lco.IsActive = true;
      _sco.IsActive = true;
      _cco.IsActive = true;

      _lco.Activate();
      _sco.Activate();
      _cco.Activate();
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
      _cco.IsActive = false;

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
      //Console.WriteLine("Estimate size: ");
      try {
      //Estimate the new size:
      ConnectionTable tab = (ConnectionTable)contab;
      int net_size = -1;
      lock( tab.SyncRoot ) {
	/*
	 * We know we are in the network, so the network
	 * has size at least 1.  And all our connections
	 * plus us is certainly a lower bound.
	 */
	int leafs = tab.Count(ConnectionType.Leaf);
        if( leafs + 1 > net_size ) {
          net_size = leafs + 1;
	}
	int structs = tab.Count(ConnectionType.Structured);
        if( structs + 1 > net_size ) {
          net_size = structs + 1;
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
	Console.WriteLine("Network size: {0} at {1}:{2}", _netsize, 
			DateTime.Now.ToUniversalTime().ToString("MM'/'dd'/'yyyy' 'HH':'mm':'ss"),
		        DateTime.Now.ToUniversalTime().Millisecond);
      }
      }catch(Exception x) {
        Console.Error.WriteLine(x.ToString());
      }

    }
    /**
     * return a status message for this node.
     * Currently this provides neighbor list exchange
     * but may be used for other features in the future
     * such as network size estimate sharing.
     * @param con_type_string string representation of the desired type.
     * @param addr address of the new node we just connected to.
     */
    override public StatusMessage GetStatus(string con_type_string, Address addr)
    {
      ArrayList neighbors = new ArrayList();
      //Get the neighbors of this type:
      lock( _connection_table.SyncRoot ) {
        /*
         * Send the list of all neighbors of this type.
         * @todo make sure we are not sending more than
         * will fit in a single packet.
         */
        ConnectionType ct = Connection.StringToMainType( con_type_string );
	AHAddress ah_addr = addr as AHAddress;
	if (ah_addr != null) {
	  //we need to find the MAX_NEIGHBORS closest guys to addr
	  foreach(Connection c in  _connection_table.GetNearestTo(ah_addr, MAX_NEIGHBORS))
	  {
	    neighbors.Add( new NodeInfo( c.Address, c.Edge.RemoteTA ) );
	  }
	} else {
	//if address is null, we send the list of
	  int count = 0;
	  foreach(Connection c in _connection_table.GetConnections( ct ) ) {
	    neighbors.Add( new NodeInfo( c.Address, c.Edge.RemoteTA ) );
	    count++;
	    if (count == MAX_NEIGHBORS) {
	      break;
	    }
	  }
        }
      }	  
      return new StatusMessage( con_type_string, neighbors );
    }

    /**
     * Sends a StatusMessage request (local node) to the nearest right and 
     * left neighbors (in the local node's ConnectionTable) of the new Connection.
     */
    protected void UpdateNeighborStatus(object contab, EventArgs args)
    {
     try {
      //our request for the new connections' neighbors
      ConnectionTable tab = this.ConnectionTable;
      if( tab.Count(ConnectionType.Structured) <= 1 ) {
        //There is only one neighbor, no communication will help:
	return;
      }
      //Update the relevant neighbors on the status:
      Connection con = ((ConnectionEventArgs)args).Connection;
      
      string con_type_string = con.ConType;
      AHAddress new_address = (AHAddress)con.Address;
      
      Connection lc = tab.GetLeftStructuredNeighborOf(new_address);
      Connection rc = tab.GetRightStructuredNeighborOf(new_address);
      Packet tp = null;
      int id = 0; 
      if( lc != null ) {
        StatusMessage req = GetStatus(con_type_string, lc.Address);
        req.Dir = ConnectionMessage.Direction.Request;
        req.Id = id++;
        tp = req.ToPacket();
        lc.Edge.Send(tp);
      }
      if( rc != null && (lc != rc) ) {
        StatusMessage req = GetStatus(con_type_string, rc.Address);
        req.Dir = ConnectionMessage.Direction.Request;
        req.Id = id++;
        tp = req.ToPacket();
        rc.Edge.Send(tp);
      }      
     } catch(Exception x) {
        Console.Error.WriteLine(x.ToString());
     }
    }
    
  }
}


