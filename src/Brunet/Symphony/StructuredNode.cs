/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;

using Brunet.Connections;
using Brunet.Util;
using Brunet.Concurrent;
using Brunet.Transport;

using Brunet.Messaging;
using Brunet.Services.MapReduce;
namespace Brunet.Symphony
{

  /**
   * A node that only makes connections on the structured system
   * and only routes structured address packets.
   */

  public class StructuredNode:Node
  {

// /////////////////////////////
// Member variables
// /////////////////////////////
    /**
     * Here are the ConnectionOverlords for this type of Node
     */
    protected readonly LeafConnectionOverlord _leafco;
    protected readonly StructuredNearConnectionOverlord _snco;
    protected readonly StructuredShortcutConnectionOverlord _ssco;
    //give access to the Structured connection overlord
    public StructuredShortcutConnectionOverlord Ssco {
      get {
        return _ssco;
      }
    }

    //maximum number of neighbors we report in our status
    protected static readonly int MAX_NEIGHBORS = 4;
    public readonly ConnectionPacketHandler sys_link;

    protected readonly IPHandler _iphandler;
    public override IPHandler IPHandler { get { return _iphandler; } }

    public override bool IsConnected {
      get {
        return _snco.IsConnected;
      }
    }
    
    protected int _netsize = -1;
    override public int NetworkSize {
      get {
        return _netsize;
      }
    }

// /////////////////////////////
// Constructors 
// /////////////////////////////

    public StructuredNode(AHAddress add, string realm):base(add,realm)
    {
      /**
       * Here are the ConnectionOverlords
       */ 
      _leafco = new LeafConnectionOverlord(this);
      AddConnectionOverlord(_leafco);
      _snco = new StructuredNearConnectionOverlord(this);
      AddConnectionOverlord(_snco);
      _ssco = new StructuredShortcutConnectionOverlord(this);
      AddConnectionOverlord(_ssco);
#if !BRUNET_SIMULATOR
      _iphandler = new IPHandler();
      _iphandler.Subscribe(this, null);
      AddTADiscovery(new LocalDiscovery(this, Realm, _rpc, _iphandler));
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
      _rpc.AddHandler("sys:ctm", new CtmRequestHandler(this));
      sys_link = new ConnectionPacketHandler(this);
      _rpc.AddHandler("sys:link", sys_link);
      _rpc.AddHandler("trace", new TraceRpcHandler(this));
      //Serve some public information about our ConnectionTable
      _rpc.AddHandler("ConnectionTable", new ConnectionTableRpc(ConnectionTable, _rpc));
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


// /////////////////////////////
// Methods 
// /////////////////////////////

    override public void Abort() {
      if(ProtocolLog.NodeLog.Enabled) {
        ProtocolLog.Write(ProtocolLog.NodeLog, String.Format(
          "In StructuredNode.Abort: {0}", this.Address));
      }

#if !BRUNET_SIMULATOR
      _iphandler.Stop();
#endif
      StopConnectionOverlords();
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
      StartConnectionOverlords();
#if !BRUNET_SIMULATOR
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
#endif
      StopConnectionOverlords();
      //Stop notifying neighbors of disconnection, we are the one leaving
      _connection_table.DisconnectionEvent -= this.UpdateNeighborStatus;

      //Gracefully close all the edges:
      _connection_table.Close(); //This makes sure we can't add any new connections.
      ArrayList edges_to_close = new ArrayList();
      foreach(Edge e in _connection_table.GetUnconnectedEdges() ) {
        edges_to_close.Add( e );
      }
      //There is no way unconnected edges could have become Connections,
      //so we should put the connections in last.
      foreach(Connection c in _connection_table) {
        edges_to_close.Add( c.State.Edge );
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
      if(ProtocolLog.NodeLog.Enabled)
        ProtocolLog.Write(ProtocolLog.NodeLog, String.Format(
          "{0} About to gracefully close all edges", this.Address));
      //Use just one of these for all the calls:
      IDictionary carg = new ListDictionary();
      carg["reason"] = "disconnecting";
      for(int i = 0; i < copy.Count; i++) {
        Edge e = (Edge)copy[i];
        if(ProtocolLog.NodeLog.Enabled) {
          ProtocolLog.Write(ProtocolLog.NodeLog, String.Format(
            "{0} Closing: [{1} of {2}]: {3}", this.Address, i, copy.Count, e));
        }
        try {
          e.CloseEvent += ch;
          Channel res_q = new Channel(1);
          DateTime start_time = DateTime.UtcNow;
          res_q.CloseEvent += delegate(object o, EventArgs arg) {
            if(ProtocolLog.NodeLog.Enabled)
              ProtocolLog.Write(ProtocolLog.NodeLog, String.Format(
                "Close on edge: {0} took: {1}", e, (DateTime.UtcNow - start_time))); 
            e.Close();
          };
          try {
            _rpc.Invoke(e, res_q, "sys:link.Close", carg);
          }
          catch(EdgeException) {
            /*
             * It is not strange for the other side to have potentially
             * closed, or some other error be in progress which is why
             * we might have been shutting down in the first place
             * No need to print a message
             */
            e.Close();
          }
          catch(Exception x) {
            if(ProtocolLog.NodeLog.Enabled)
              ProtocolLog.Write(ProtocolLog.Exceptions, String.Format(
                "sys:link.Close({0}) threw: {1}", e, x));
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
        foreach(Connection c in  _connection_table.GetNearestTo(ah_addr, MAX_NEIGHBORS)) {
          neighbors.Add(NodeInfo.CreateInstance(c.Address));
        }
      } else {
        //if address is null, we send the list of
        int count = 0;
        foreach(Connection c in _connection_table.GetConnections( ct ) ) {
          neighbors.Add(NodeInfo.CreateInstance(c.Address));
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
      if( c != null ) {
        StatusMessage req = GetStatus(type, c.Address);
        Channel stat_res = new Channel(1);
        EventHandler handle_result = delegate(object q, EventArgs eargs) {
          try {
            RpcResult r = (RpcResult)stat_res.Dequeue();
            c.SetStatus(new StatusMessage( (IDictionary)r.Result ));
          }
          catch(Exception) {
            //Looks like lc disappeared before we could update it
          }
        };
        stat_res.CloseEvent += handle_result;
        _rpc.Invoke(c, stat_res, "sys:link.GetStatus", req.ToDictionary() );
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
      catch(EdgeClosedException) {
        //Just ignore this connection if it is closed
      }
      catch(EdgeException ex) {
        if( !ex.IsTransient ) {
          //Make sure this Edge is closed before going forward
          lc.State.Edge.Close();
        }
      }
      catch(Exception x) {
        if(ProtocolLog.Exceptions.Enabled) {
            ProtocolLog.Write(ProtocolLog.Exceptions, String.Format(
              "CallGetStatus(left) on {0} failed: {1}", lc, x));
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
      catch(EdgeClosedException) {
        //Just ignore this connection if it is closed
      }
      catch(EdgeException ex) {
        if( !ex.IsTransient ) {
          //Make sure this Edge is closed before going forward
          rc.State.Edge.Close();
        }
      }
      catch(Exception x) {
        if(ProtocolLog.Exceptions.Enabled) {
            ProtocolLog.Write(ProtocolLog.Exceptions, String.Format(
              "CallGetStatus(right) on {0} failed: {1}", rc, x));
        }
      }
    }

    override public void UpdateRemoteTAs(IList<TransportAddress> tas_to_add)
    {
      base.UpdateRemoteTAs(tas_to_add);
      ConnectionState cs = ConState;
      if(cs == ConnectionState.SeekingConnections || cs == ConnectionState.Joining) {
        _leafco.Activate();
      }
    }
  }
}
