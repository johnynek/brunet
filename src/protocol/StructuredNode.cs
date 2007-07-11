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

#define PRODUCTION
//to run the connecttester, make sure you change PRODUCTION to DEBUG

using System;
using System.Collections;
using System.Collections.Specialized;
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
    protected static readonly int MAX_NEIGHBORS = 4;


    /**
     * Right now, this just asks if the main ConnectionOverlords
     * are looking for connections, with the assumption being
     * that if they are, we are not correctly connected.
     *
     * In the future, it might use a smarter algorithm
     */
    //     public override bool IsConnected {
    //       get {
    //         lock( _sync ) {
    //           //To be routable, 
    //           return !(_lco.NeedConnection || _sco.NeedConnection);
    //         }
    //       }
    //     }

    public override bool IsConnected {
      get {
	return _sco.IsConnected;
      }

    }
    public StructuredNode(AHAddress add):base(add)
    {
      /**
       * Here are the ConnectionOverlords
       */ 
      _lco = new LeafConnectionOverlord(this);
      _sco = new StructuredConnectionOverlord(this);
      //ChotaConnectionOverlord
      _cco = new ChotaConnectionOverlord(this);

      /**
       * Turn on some protocol support : 
       */
      /// Turn on Packet Forwarding Support :
      GetTypeSource(PType.Protocol.Forwarding).Subscribe(new PacketForwarder(this), null);
      //Handles AHRouting:
      GetTypeSource(PType.Protocol.AH).Subscribe(new AHHandler(this), this);
      GetTypeSource(PType.Protocol.Echo).Subscribe(new EchoHandler(), this);
      
      //Add the standard RPC handlers:
      RpcManager rpc = RpcManager.GetInstance(this);
      rpc.AddHandler("sys:ctm", new CtmRequestHandler(this));
      rpc.AddHandlerWithSender("sys:link", new ConnectionPacketHandler(this));
      rpc.AddHandler("trace", new TraceRpcHandler(this));

      _connection_table.ConnectionEvent += new EventHandler(this.EstimateSize);
      _connection_table.ConnectionEvent += new EventHandler(this.UpdateNeighborStatus);
      _connection_table.DisconnectionEvent += new EventHandler(this.EstimateSize);
      _connection_table.DisconnectionEvent += new EventHandler(this.UpdateNeighborStatus);
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
      base.Connect();
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
      base.Disconnect();
      _lco.IsActive = false;
      _sco.IsActive = false;
      _cco.IsActive = false;
      
      //Gracefully close all the edges:
      _connection_table.Close(); //This makes sure we can't add any new connections.
      ArrayList edges_to_close = ArrayList.Synchronized( new ArrayList() );
      foreach(Edge e in _connection_table.GetUnconnectedEdges() ) {
        edges_to_close.Add( e );
      }
      //There is no way unconnected edges could have become Connections,
      //so we should put the connections in last.
      foreach(Connection c in _connection_table) {
        edges_to_close.Add( c.Edge );
      }
      //edges_to_close has all the connections and unconnected edges.
      ArrayList copy = new ArrayList(edges_to_close);
      EventHandler ch = delegate(object o, EventArgs a) {
        Edge e = (Edge)o;
        edges_to_close.Remove(e);
        if( edges_to_close.Count == 0 ) {
          Console.Error.WriteLine("Node({0}) Stopping all EdgeListeners", Address);
          StopAllEdgeListeners();
        }
      };
      RpcManager rpc = RpcManager.GetInstance(this);
      foreach(Edge e in copy) {
        e.CloseEvent += ch;
        if( e.IsClosed ) { ch(e, null); }
        else {
          BlockingQueue res_q = new BlockingQueue();
          res_q.CloseAfterEnqueue();
          DateTime start_time = DateTime.UtcNow;
          res_q.CloseEvent += delegate(object o, EventArgs arg) {
            Console.Error.WriteLine("Close on edge: {0} took: {1}", e, (DateTime.UtcNow - start_time)); 
            e.Close();
          };
          try {
            IDictionary carg = new ListDictionary();
            carg["reason"] = "disconnecting";
            rpc.Invoke(e, res_q, "sys:link.Close", carg);
          } catch { e.Close(); }
        }
      }
    }

    /**
     * When the connectiontable changes, we re-estimate
     * the size of the network:
     */
    protected void EstimateSize(object contab, System.EventArgs args)
    {
      //Console.Error.WriteLine("Estimate size: ");
      try {
      //Estimate the new size:
      ConnectionTable tab = (ConnectionTable)contab;
      int net_size = -1;
      BigInteger least_dist = null;
      BigInteger greatest_dist = null;
      int shorts = 0;
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
	AHAddress local = (AHAddress)_local_add;
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
      }
	/*
	 * Now we have the distance between the range of our neighbors
	 */
	if( shorts > 0 ) {
          if ( greatest_dist > least_dist ) {
	    BigInteger width = greatest_dist - least_dist;
	    //Here is our estimate of the inverse density:
	    BigInteger inv_density = width/(shorts);
            //The density times the full address space is the number
	    BigInteger total = Address.Full / inv_density;
	    int total_int = total.IntValue();
	    if( total_int > net_size ) {
              net_size = total_int;
	    }
          }
	}

	//Now we have our estimate:
	lock( _sync ) {
	  _netsize = net_size;
        }
	Console.Error.WriteLine("Network size: {0} at {1}:{2}", _netsize, 
			DateTime.UtcNow.ToString("MM'/'dd'/'yyyy' 'HH':'mm':'ss"),
		        DateTime.UtcNow.Millisecond);
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
	    if (count >= MAX_NEIGHBORS) {
	      break;
	    }
	  }
        }
      return new StatusMessage( con_type_string, neighbors );
    }
    /**
     * Call the GetStatus method on the given connection
     */
    protected void CallGetStatus(string type, Connection c) {
      ConnectionTable tab = this.ConnectionTable;
      if( c != null ) {
        StatusMessage req = GetStatus(type, c.Address);
        BlockingQueue stat_res = new BlockingQueue();
        EventHandler handle_result = delegate(object q, EventArgs eargs) {
          try {
            RpcResult r = (RpcResult)stat_res.Dequeue();
            StatusMessage sm = new StatusMessage( (IDictionary)r.Result );
            tab.UpdateStatus(c, sm);
          }
          catch(Exception) {
            //Looks like lc disappeared before we could update it
          }
          stat_res.Close();
        };
        stat_res.EnqueueEvent += handle_result;
        RpcManager rpc = RpcManager.GetInstance(this);
        rpc.Invoke(c.Edge, stat_res, "sys:link.GetStatus", req.ToDictionary() );
      }
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
        
        bool done = false;
        Connection lc = null;
        int trials = 0;
        do {
          trials++;
          lc = tab.GetLeftStructuredNeighborOf(new_address);
          try {
            if( lc != null ) {
              CallGetStatus(con_type_string, lc);
              done = true;
            }
            else {
              //There are no more neighbors
              done = true;
            }
          }
          catch(Exception x) {
            Console.Error.WriteLine("CallGetStatus trial {2} on {0} failed: {1}", lc, x, trials); 
            if( lc.Edge.IsClosed ) {
              //Make sure this guy is removed in this thread (it may be
              //in the process of being removed in another thread)
              tab.Disconnect(lc.Edge);
            }
          }
          done = done || (trials > 3); //Don't try forever
        } while(!done);
        Connection rc = null;
        done = false;
        trials = 0;
        do {
          trials++;
          rc = tab.GetRightStructuredNeighborOf(new_address);
          try {
            if( rc != null && (lc != rc) ) {
              CallGetStatus(con_type_string, rc);
              done = true;
            }
            else {
              //There are no more neighbors
              done = true;
            }
          }
          catch(Exception x) {
            Console.Error.WriteLine("CallGetStatus trial {2} on {0} failed: {1}", rc, x, trials); 
            if( rc.Edge.IsClosed ) {
              //Make sure this guy is removed in this thread (it may be
              //in the process of being removed in another thread)
              tab.Disconnect(rc.Edge);
            }
          }
          done = done || (trials > 3); //Don't try forever
        } while(!done);
      } catch(Exception x) {
        Console.Error.WriteLine(x.ToString());
      }
    }
    
  }
}


