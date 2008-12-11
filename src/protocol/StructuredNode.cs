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

using System;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;

namespace Brunet
{

  /**
   * A node that only makes connections on the structured system
   * and only routes structured address packets.
   */

  public class StructuredNode:Node
  {
    /**
     * Here are the ConnectionOverlords for this type of Node
     */
    protected ConnectionOverlord _leafco;
    protected ConnectionOverlord _sco;
    //give access to the Structured connection overlord
    public StructuredConnectionOverlord Sco {
      get {
        return _sco as StructuredConnectionOverlord;
      }
    }
    protected ConnectionOverlord _cco;
    protected ConnectionOverlord _localco;
    protected ManagedConnectionOverlord _mco;
    public ManagedConnectionOverlord ManagedCO { get { return _mco; } }

    //maximum number of neighbors we report in our status
    protected static readonly int MAX_NEIGHBORS = 4;
    public ConnectionPacketHandler sys_link;


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
    //           return !(_leafco.NeedConnection || _sco.NeedConnection);
    //         }
    //       }
    //     }

    public override bool IsConnected {
      get {
	return _sco.IsConnected;
      }

    }
    public StructuredNode(AHAddress add, string realm):base(add,realm)
    {
      // Instantiate rpc early!
      RpcManager rpc = RpcManager.GetInstance(this);
      /**
       * Here are the ConnectionOverlords
       */ 
      _leafco = new LeafConnectionOverlord(this);
      _sco = new StructuredConnectionOverlord(this);
      _cco = new ChotaConnectionOverlord(this);
      _mco = new ManagedConnectionOverlord(this);
#if !BRUNET_SIMULATOR
      _localco = new LocalConnectionOverlord(this);
      _iphandler = new IPHandler();
      _iphandler.Subscribe(this, null);
#endif

      /**
       * Turn on some protocol support : 
       */
      /// Turn on Packet Forwarding Support :
      GetTypeSource(PType.Protocol.Forwarding).Subscribe(new PacketForwarder(this), null);
      //Handles AHRouting:
      GetTypeSource(PType.Protocol.AH).Subscribe(new AHHandler(this), this);
      GetTypeSource(PType.Protocol.Echo).Subscribe(new EchoHandler(), this);
      
      //Add the standard RPC handlers:
      rpc.AddHandler("sys:ctm", new CtmRequestHandler(this));
      sys_link = new ConnectionPacketHandler(this);
      rpc.AddHandler("sys:link", sys_link);
      rpc.AddHandler("trace", new TraceRpcHandler(this));

      //Add a map-reduce handlers:
      _mr_handler = new MapReduceHandler(this);
      //Subscribe it with the RPC handler:
      rpc.AddHandler("mapreduce", _mr_handler);

      //Subscribe map-reduce tasks
      _mr_handler.SubscribeTask(new MapReduceTrace(this));
      _mr_handler.SubscribeTask(new MapReduceRangeCounter(this));

      
      /*
       * Handle Node state changes.
       */
      StateChangeEvent += delegate(Node n, Node.ConnectionState s) {
        if( s == Node.ConnectionState.Leaving ) {
          //Start our StructuredNode specific leaving:
          Leave();
        }
      };

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
    public StructuredNode(AHAddress addr) : this(addr, "global")
    {
    
    }

    protected int _netsize = -1;
    override public int NetworkSize {
      get {
        return _netsize;
      }
    }

    override public void Abort() {
      if(ProtocolLog.NodeLog.Enabled) {
        ProtocolLog.Write(ProtocolLog.NodeLog, String.Format(
          "In StructuredNode.Abort: {0}", this.Address));
      }

#if !BRUNET_SIMULATOR
      _localco.IsActive = false;
      _iphandler.Stop();
#endif

      _leafco.IsActive = false;
      _sco.IsActive = false;
      _cco.IsActive = false;
      _mco.IsActive = false;
      StopAllEdgeListeners();
    }

    /**
     * Connect to the network.  This informs all the ConnectionOverlord objects
     * to do their thing.  Announce runs in a new thread returning context back
     * to the caller.
     */
    override public void Connect()
    {
      base.Connect();
      StartAllEdgeListeners();

      _leafco.IsActive = true;
      _sco.IsActive = true;
      _cco.IsActive = true;
      _mco.IsActive = true;

#if !BRUNET_SIMULATOR
      _localco.IsActive = true;
      _leafco.Activate();
      _sco.Activate();
      _cco.Activate();
      _localco.Activate();
      _mco.Activate();
      AnnounceThread();
#endif
    }

    /**
     * This informs all the ConnectionOverlord objects
     * to not respond to loss of edges, then to issue
     * close messages to all the edges
     * 
     */
    protected void Leave()
    {
      if(ProtocolLog.NodeLog.Enabled) {
        ProtocolLog.Write(ProtocolLog.NodeLog, String.Format(
          "In StructuredNode.Leave: {0}", this.Address));
      }

#if !BRUNET_SIMULATOR
      _iphandler.Stop();
      _localco.IsActive = false;
#endif
      _leafco.IsActive = false;
      _sco.IsActive = false;
      _cco.IsActive = false;
      _mco.IsActive = false;

      //Gracefully close all the edges:
      _connection_table.Close(); //This makes sure we can't add any new connections.
      ArrayList edges_to_close = new ArrayList();
      foreach(Edge e in _connection_table.GetUnconnectedEdges() ) {
        edges_to_close.Add( e );
      }
      //There is no way unconnected edges could have become Connections,
      //so we should put the connections in last.
      foreach(Connection c in _connection_table) {
        edges_to_close.Add( c.Edge );
      }
      //edges_to_close has all the connections and unconnected edges.
      IList copy = edges_to_close.ToArray();

      //Make sure multiple readers and writers won't have problems:
      edges_to_close = ArrayList.Synchronized( edges_to_close );

      EventHandler ch = delegate(object o, EventArgs a) {
        if(ProtocolLog.NodeLog.Enabled)
          ProtocolLog.Write(ProtocolLog.NodeLog, String.Format(
            "{1} Handling Close of: {0}", o, this.Address));
        edges_to_close.Remove(o);
        if( edges_to_close.Count == 0 ) {
          if(ProtocolLog.NodeLog.Enabled)
            ProtocolLog.Write(ProtocolLog.NodeLog, String.Format(
              "Node({0}) Stopping all EdgeListeners", Address));
          StopAllEdgeListeners();
        }
      };
      RpcManager rpc = RpcManager.GetInstance(this);
      if(ProtocolLog.NodeLog.Enabled)
        ProtocolLog.Write(ProtocolLog.NodeLog, String.Format(
          "{0} About to gracefully close all edges", this.Address));
      for(int i = 0; i < copy.Count; i++) {
        Edge e = (Edge)copy[i];
        if(ProtocolLog.NodeLog.Enabled) {
          ProtocolLog.Write(ProtocolLog.NodeLog, String.Format(
            "{0} Closing: [{1} of {2}]: {3}", this.Address, i, copy.Count, e));
        }
        try {
          e.CloseEvent += ch;
          Channel res_q = new Channel();
          res_q.CloseAfterEnqueue();
          DateTime start_time = DateTime.UtcNow;
          res_q.CloseEvent += delegate(object o, EventArgs arg) {
            if(ProtocolLog.EdgeClose.Enabled) {
              ProtocolLog.Write(ProtocolLog.EdgeClose, String.Format(
                "Close on edge: {0} took: {1}", e, (DateTime.UtcNow - start_time))); 
            }
            e.Close();
          };
          try {
            IDictionary carg = new ListDictionary();
            carg["reason"] = "disconnecting";
            rpc.Invoke(e, res_q, "sys:link.Close", carg);
          }
          catch {
            if(ProtocolLog.Exceptions.Enabled) {
              ProtocolLog.Write(ProtocolLog.Exceptions, String.Format(
                "Closing: {0}", e));
            }
            e.Close();
          }
        }
        catch {
          ch(e,null);
        }
      }
      if( copy.Count == 0 ) {
        //There were no edges, go ahead an Stop
        if(ProtocolLog.NodeLog.Enabled)
          ProtocolLog.Write(ProtocolLog.NodeLog, String.Format(
            "Node({0}) Stopping all EdgeListeners", Address));
        StopAllEdgeListeners();
      }
    }

    /**
     * When the connectiontable changes, we re-estimate
     * the size of the network:
     */
    protected void EstimateSize(object contab, System.EventArgs args)
    {
      try {
        //Estimate the new size:
        int net_size = -1;
        BigInteger least_dist = null;
        BigInteger greatest_dist = null;
        int shorts = 0;
        ConnectionList structs = ((ConnectionEventArgs)args).CList;
  
        if( structs.MainType == ConnectionType.Structured ) {
      	/*
      	 * We know we are in the network, so the network
      	 * has size at least 1.  And all our connections
      	 * plus us is certainly a lower bound.
      	 */
        
          if( structs.Count + 1 > net_size ) {
            net_size = structs.Count + 1;
          }
          /*
      	   * We estimate the density of nodes in the address space,
      	   * and since we know the size of the whole address space,
      	   * we can use the density to estimate the number of nodes.
      	   */
          AHAddress local = (AHAddress)_local_add;
          foreach(Connection c in structs) {
            if( c.ConType == "structured.near") {
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
        }
  
        if(ProtocolLog.NodeLog.Enabled) {
            ProtocolLog.Write(ProtocolLog.NodeLog, String.Format(
              "Network size: {0} at {1}", _netsize,
              DateTime.UtcNow.ToString()));
        }
      }
      catch(Exception x) {
        if(ProtocolLog.Exceptions.Enabled) {
          ProtocolLog.Write(ProtocolLog.Exceptions, x.ToString());
        }
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
	    neighbors.Add( NodeInfo.CreateInstance( c.Address ) );
	  }
	} else {
	//if address is null, we send the list of
	  int count = 0;
	  foreach(Connection c in _connection_table.GetConnections( ct ) ) {
	    neighbors.Add( NodeInfo.CreateInstance( c.Address ) );
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
        Channel stat_res = new Channel();
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
      ConnectionEventArgs cea = (ConnectionEventArgs)args;
      if( cea.ConnectionType != ConnectionType.Structured ) {
        //We don't do anything,
        return;
      }

      //This is the list we had when things changed
      ConnectionList structs = cea.CList;
      //structs is constant
      if( structs.Count == 0 ) {
        //There is no one to talk to
        return;
      }
      /*
       * Get the data we need about this connection:
       */
      ConnectionTable tab = this.ConnectionTable;
      Connection con = cea.Connection;
      string con_type_string = con.ConType;
      AHAddress new_address = (AHAddress)con.Address;

      /*
       * Update the left neighbor:
       */
      Connection lc = structs.GetLeftNeighborOf(new_address);
      try {
        //This edge could ahve been closed, which will
        //cause the Rpc to throw an exception
        CallGetStatus(con_type_string, lc);
      }
      catch(Exception x) {
        if( lc.Edge.IsClosed ) {
          //Make sure this guy is removed in this thread (it may be
          //in the process of being removed in another thread)
          tab.Disconnect(lc.Edge);
        }
        else {
          if(ProtocolLog.Exceptions.Enabled)
            ProtocolLog.Write(ProtocolLog.Exceptions, String.Format(
              "CallGetStatus on {0} failed: {1}", lc, x));
        }
      }
      /*
       * Update the right neighbor:
       */
      Connection rc = structs.GetRightNeighborOf(new_address);
      try {
        if( (lc != rc) ) {
          //This edge could ahve been closed, which will
          //cause the Rpc to throw an exception
          CallGetStatus(con_type_string, rc);
        }
      }
      catch(Exception x) {
        if( rc.Edge.IsClosed ) {
          //Make sure this guy is removed in this thread (it may be
          //in the process of being removed in another thread)
          tab.Disconnect(rc.Edge);
        } 
        else {
          if(ProtocolLog.Exceptions.Enabled)
            ProtocolLog.Write(ProtocolLog.Exceptions, String.Format(
              "CallGetStatus on {0} failed: {1}", rc, x));
	      }
      }
    }
  }
}
