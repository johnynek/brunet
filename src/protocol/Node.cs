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

//#define DEBUG
using System;
using System.Collections;
using System.Threading;
//using log4net;
namespace Brunet
{

  /**
   * This class represents endpoint of packet communication.
   * There may be many nodes on each computer on the Brunet
   * Network.  An example may be :  a user wants to chat, and
   * share a file.  The file may be represented as a Node on
   * the Brunet Network, and also the chat user may represent
   * itself as a Node on the network.  Both of these Nodes
   * reside on the same computer host.
   *
   * The Node also keeps itself connected and manages its
   * connections. 
   * 
   */
  abstract public class Node:IPacketSender, IPacketHandler
  {
    /*private static readonly log4net.ILog log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/


#if PLAB_LOG
    protected BrunetLogger _logger;
    virtual public BrunetLogger Logger{
	get{
	  return _logger;
	}
	set
	{
	  _logger = value;
	  //The connection table only has a logger in this case
          _connection_table.Logger = value;
          foreach(EdgeListener el in _edgelistener_list) {
             el.Logger = value;
	  }
	}
    }
#endif
    /**
     * Create a node with a given local address and
     * a set of Routers.
     */
    public Node(Address add)
    {
      //Start with the address hashcode:
      _gracefully_close_edges  = new Hashtable();
      _cmp = new ConnectionMessageParser();

      _sync = new Object();
      lock(_sync)
      {
        /*
         * Make all the hashtables : 
         */
        _local_add = add;
        _subscription_table = new Hashtable();
        _send_subs = new Hashtable();

        _task_queue = new TaskQueue();
        //Here is the thread for announcing packets
        _packet_queue = new BlockingQueue();
        _running = false;
        _announce_thread = new Thread(this.AnnounceThread);
        
        _connection_table = new ConnectionTable(_local_add);
        _connection_table.ConnectionEvent +=
          new EventHandler(this.ConnectionHandler);
        /*
         * We must later make sure the EdgeEvent events from
         * any EdgeListeners are connected to _cph.EdgeHandler
         */
        _cph = new ConnectionPacketHandler(this);
        /* Here are the transport addresses */
        /*@throw ArgumentNullException if the list ( new ArrayList()) is null.
         */
        _remote_ta = new ArrayList();
        /*@throw ArgumentNullException if the list ( new ArrayList()) is null.
         */
        /* EdgeListener's */
        _edgelistener_list = new ArrayList();
        _edge_factory = new EdgeFactory();
        //Put all the Routers in :
        _routers = new IRouter[ 161 ];
        /* Set up the heartbeat */
        _heart_period = 2000; //2000 ms, or 2 second.
        _timer = new Timer(new TimerCallback(this.HeartBeatCallback),
                           null, _heart_period, _heart_period);
        //Check the edges from time to time
        this.HeartBeatEvent += new EventHandler(this.CheckEdgesCallback);
        _last_edge_check = DateTime.UtcNow;
      }
    }

    /**
     * Keeps track of the objects which need to be notified 
     * of certain packets.
     */
    protected Hashtable _subscription_table;
    /**
     * Same thing as the above, except for sends
     */
    protected Hashtable _send_subs;

    /**
     * This object handles new Edge objects, and
     * manages the incoming Link protocol
     */
    protected ConnectionPacketHandler _cph;

    protected Address _local_add;
    /**
     * The Address of this Node
     */
    public Address Address
    {
      get
      {
        return _local_add;
      }
    }
    protected EdgeFactory _edge_factory;
    /**
     *  my EdgeFactory
     */
    public EdgeFactory EdgeFactory { get { return _edge_factory; } }

    /**
     * Here are all the EdgeListener objects for this Node
     */
    protected ArrayList _edgelistener_list;
    /**
     * These are all the local TransportAddress objects that
     * refer to EdgeListener objects attached to this node.
     * This IList is ReadOnly
     */
    public IList LocalTAs {
      get {
        //Make sure we don't keep too many of these things:
        ArrayList local_ta = new ArrayList();
        foreach(EdgeListener el in _edgelistener_list) {
          foreach(TransportAddress ta in el.LocalTAs) {
            local_ta.Add(ta);
            if( local_ta.Count >= _MAX_RECORDED_TAS ) {
              break;
            }
          }
          if( local_ta.Count >= _MAX_RECORDED_TAS ) {
            break;
          }
        }
        return local_ta;
      }
    }

    /**
     * This is an estimate of the current
     * network size.  It is not an exact
     * value.
     *
     * A value of -1 means there is not
     * enough information to make a meaningful
     * estimate.
     */
    virtual public int NetworkSize {
      get { return -1; }
    }
    protected BlockingQueue _packet_queue;

    protected string _realm = "global";
    /**
     * Each Brunet Node is in exactly 1 realm.  This is 
     * a namespacing feature.  This allows you to create
     * Brunets which are separate from other Brunets.
     *
     * The default is "global" which is the standard
     * namespace.
     */
    public string Realm { get { return _realm; } }
    
    protected ArrayList _remote_ta;
    /**
     * These are all the remote TransportAddress objects that
     * this Node may use to connect to remote Nodes
     *
     * This can be shared between nodes or not.
     *
     * This is the ONLY proper way to set the RemoteTAs for this
     * node.
     */
    public ArrayList RemoteTAs {
      get {
        return _remote_ta;
      }
      set {
        _remote_ta = value;
      }
    }

    /**
     * Holds the Router for each Address type
     */
    protected IRouter[] _routers;
    /**
     * This is true after Connect is called and false after
     * Disconnect is called.
     */
    protected bool _running;

    /** Object which we lock for thread safety */
    protected Object _sync;

    protected Thread _announce_thread;

    protected ConnectionTable _connection_table;


    protected Hashtable _gracefully_close_edges;

    protected ConnectionMessageParser _cmp;
    /**
     * Manages the various mappings associated with connections
     */
    public virtual ConnectionTable ConnectionTable { get { return _connection_table; } }

    /**
     * This is true if the Node is properly connected in the network.
     * If you want to know when it is safe to assume you are connected,
     * listen to all for Node.ConnectionTable.ConnectionEvent and
     * Node.ConnectionTable.DisconnectionEvent and then check
     * this property.  If it is true, you should probably wait
     * until it is false if you need the Node to be connected
     */
    public abstract bool IsConnected { get; }
    protected TaskQueue _task_queue;
    /**
     * This is the TaskQueue for this Node
     */
    public TaskQueue TaskQueue { get { return _task_queue; } }

    //The timer that tells us when to HeartBeatEvent
    protected Timer _timer;

    protected int _heart_period;
    ///how many milliseconds between heartbeats
    public int HeartPeriod { get { return _heart_period; } }

    ///If we don't hear anything from a *CONNECTION* in this time, ping it.
    static protected readonly TimeSpan _CONNECTION_TIMEOUT = new TimeSpan(0,0,0,0,15000);
    ///If we don't hear anything from any *EDGE* in this time, close it, 45 seconds is now
    ///the close timeout
    static protected readonly TimeSpan _EDGE_CLOSE_TIMEOUT = new TimeSpan(0,0,0,0,45000);
    /**
     * Maximum number of TAs we keep in both for local and remote.
     * This does not control how many we send to our neighbors.
     */
    static protected readonly int _MAX_RECORDED_TAS = 10000;
    ///The DateTime that we last checked the edges.  @see CheckEdgesCallback
    protected DateTime _last_edge_check;

    ///after each HeartPeriod, the HeartBeat event is fired
    public event EventHandler HeartBeatEvent;
    
    //add an event handler which conveys the fact that Disconnect has been called on the node
    public event EventHandler DepartureEvent;

    //add an event handler which conveys the fact that Cconnect has been called on the node
    public event EventHandler ArrivalEvent;

    public virtual void AddEdgeListener(EdgeListener el)
    {
      /* The EdgeFactory needs to be made aware of all EdgeListeners */
      _edge_factory.AddListener(el);
      _edgelistener_list.Add(el);
      /**
       * It is ESSENTIAL that the EdgeEvent of EdgeListener objects
       * be connected to the EdgeHandler method of ConnectionPacketHandler
       */
      el.EdgeEvent += new EventHandler(_cph.EdgeHandler);
    }
    
    /**
     * The default TTL for this destination 
     */
    public virtual short DefaultTTLFor(Address destination)
    {
      short ttl;
      double ttld;
      if( destination is StructuredAddress ) {
	 //This is from the original papers on
	 //small world routing.  The maximum distance
	 //is almost certainly less than log^3 N
        ttld = Math.Log( NetworkSize );
        ttld = ttld * ttld * ttld;
      }
      else {
	//Most random networks have diameter
	//of size order Log N
        ttld = Math.Log( NetworkSize, 2.0 );
        ttld = 2.0 * ttld;
      }
      
      if( ttld < 2.0 ) {
        //Don't send too short a distance
	ttl = 2;
      }
      else if( ttld > (double)AHPacket.MaxTtl ) {
        ttl = AHPacket.MaxTtl;
      }
      else {
        ttl = (short)( ttld );
      }
      return ttl;
    }
    /**
     * Starts all edge listeners for the node.
     * Useful for connect/disconnect operations
     */
    protected virtual void StartAllEdgeListeners()
    {
      foreach(EdgeListener el in _edgelistener_list) {
#if DEBUG
        Console.WriteLine("{0} starting {1}", Address, el);
#endif
        el.Start();
      }
      _running = true;
      _announce_thread.Start();
    }

    /**
     * Stops all edge listeners for the node.
     * Useful for connect/disconnect operations
     */
    protected virtual void StopAllEdgeListeners()
    {
      foreach(EdgeListener el in _edgelistener_list) {
        el.Stop();
      }
      _running = false;
      //This makes sure we don't block forever on the last packet
      _packet_queue.Close();
    }
    /**
     * When we do announces using the seperate thread, this is
     * what we pass
     */
    private class AnnounceState {
      public AHPacket Pack;
      public Edge From;
      public AnnounceState(AHPacket p, Edge from) {
        Pack = p;
        From = from;
      }
    }
    private void AnnounceThread() {
      try {
       while( _running ) {
        AnnounceState a_state = (AnnounceState)_packet_queue.Dequeue();
        Announce(a_state.Pack, a_state.From);
       }
      }
      catch(System.InvalidOperationException) {
        //This is thrown when Dequeue is called on an empty queue
        //which happens when the BlockingQueue is closed, which
        //happens on Disconnect
      }
    }
    /**
     * When a packet is to be delivered to this node,
     * this method is called.  This method is public so that
     * we can chain protocols through the node.  For instance,
     * after a packet is handled, it may be a wrapped packet
     * which actually contains another packet inside.  Thus,
     * the unwrapped packet could be "Announced" by the handler
     *
     * One needs to be careful to prevent an infinite loop of
     * a Handler announcing the packet it is supposed to handle.
     */
    public virtual void Announce(AHPacket p, Edge from)
    {

      //System.Console.WriteLine("Announcing packet: {0}:", p.ToString() );
      //System.Console.WriteLine("PayloadType: {0}:", p.PayloadType );

      //When Subscribe or unsubscribe are called,
      //they make copies of the ArrayList, thus we
      //only need to hold the sync while we are
      //getting the list of handlers.

      /* 
       * Note that getting from Hashtable is threadsafe, multiple
       * threads writing is a problem
       */
      ArrayList handlers = _subscription_table[p.PayloadType] as ArrayList;
      if (handlers != null) {
        //log.Info("Announcing Packet: " + p.ToString() );
        /*
         * Ordinarily I prefer foreach, but for is faster in this
         * case and this code gets called for every packet we receive
         */
        int count = handlers.Count;
        for(int i = 0; i < count; i++) {
          IAHPacketHandler hand = null;
          try {
            hand = (IAHPacketHandler)handlers[i];
            //System.Console.WriteLine("Handler: {0}", hand);
            hand.HandleAHPacket(this, p, from);
          }
          catch(Exception x) {
            System.Console.WriteLine("ERROR: Packet Handling Exception");
            System.Console.WriteLine("Hander: {0}\tEdge: {1}\tPacket: {2}",hand, from, p);
            System.Console.WriteLine("Exception: {0}", x);
          }
        }
      }
      else {
        /**
         * @todo we should send some kind of ICMP message to the sender
         * and let them know we don't know about this protocol
         */
        //log.Error("No Handler for: " + p.ToString());
      }
    }
    /**
     * This method is called when the Node should connect to the
     * network
     */
    public virtual void Connect() {
      if (ArrivalEvent != null) {
	ArrivalEvent(this, null);
      } 
    }
    /**
     * Disconnect to the network.
     */
    public virtual void Disconnect() {
      if (DepartureEvent != null) {
	DepartureEvent(this, null);      
      }
    }

    /**
     * When a ConnectionEvent occurs, this handler registers the
     * information with the node
     */
    public virtual void ConnectionHandler(object ct, EventArgs args)
    {
      ConnectionEventArgs ce_args = (ConnectionEventArgs) args;
      Edge edge = ce_args.Edge;
      edge.SetCallback(Packet.ProtType.AH, this);
      //by Arijit Ganguly
      //also register a callback for direct packets which we may get
#if ARI_DIRECT_ENABLE
      edge.SetCallback(Packet.ProtType.Direct, this);
#endif      
      //Our peer's remote is us
      TransportAddress reported_ta =
            ce_args.Connection.PeerLinkMessage.Remote.FirstTA;
      //Our peer's local is them
      TransportAddress remote_ta =
            ce_args.Connection.PeerLinkMessage.Local.FirstTA;
      lock( _sync ) {
        foreach(EdgeListener el in _edgelistener_list) {
          //Update our local list:
          el.UpdateLocalTAs(edge, reported_ta);
          el.UpdateRemoteTAs( _remote_ta, edge, remote_ta);
        }
        int count = _remote_ta.Count;
        if( count > _MAX_RECORDED_TAS ) {
          int rm_count = count - _MAX_RECORDED_TAS;
          _remote_ta.RemoveRange(_MAX_RECORDED_TAS, rm_count);
        }

      }
    }
    /**
     * Return a NodeInfo object for this node containing
     * at most max_local local Transport addresses
     */
    virtual public NodeInfo GetNodeInfo(int max_local) {
      ArrayList l = new ArrayList( this.LocalTAs );
      if( l.Count > max_local ) {
        int rm_count = l.Count - max_local;
        l.RemoveRange( max_local, rm_count );
      }
      return new NodeInfo( this.Address, l);
    }
    /**
     * return a status message for this node.
     * Currently this provides neighbor list exchange
     * but may be used for other features in the future
     * such as network size estimate sharing.
     * @param con_type_string string representation of the desired type.
     * @param addr address of the new node we just connected to.
     */
    virtual public StatusMessage GetStatus(string con_type_string, Address addr)
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
        foreach(Connection c in _connection_table.GetConnections( ct ) ) {
          neighbors.Add( new NodeInfo( c.Address, c.Edge.RemoteTA ) );
        }
      }	  
      return new StatusMessage( con_type_string, neighbors );
    }
    
    /**
     * Close the edge after we get a response CloseMessage
     * from the node on the other end.
     * This method is to try to make sure both sides of an edge
     * know that the edge is closing.
     * @param e Edge to close
     */
    public void GracefullyClose(Edge e)
    {
      CloseMessage cm = new CloseMessage();
      cm.Dir = ConnectionMessage.Direction.Request;
      GracefullyClose(e, cm);
    }
    /**
     * @param e Edge to close
     * @param cm CloseMessage to send to other node
     * This method is used if we want to use a particular CloseMessage
     * If not, we can use the method with the same name with one fewer
     * parameters
     */
    public void GracefullyClose(Edge e, CloseMessage cm)
    {
      try {
        e.CloseEvent += new EventHandler(this.GracefulCloseHandler);
        e.SetCallback(Packet.ProtType.Connection, this);
        e.Send( cm.ToPacket() );
        lock( _sync ) {
          _gracefully_close_edges[e] = cm;
        }
        /**
         * Close any connection on this edge, and
         * put the edge into the list of unconnected edges
         */
        _connection_table.Disconnect(e);
      }
      catch(EdgeException) {
        //If the edge has some problem, don't do anything
        e.Close();
      }
    }

    /*
     * When an edge we are gracefully closing closes, this cleans up
     */
    protected void GracefulCloseHandler(object sender, EventArgs args)
    {
      lock( _sync ) {
        _gracefully_close_edges.Remove(sender);
      }
    }

    /**
     * When we close gracefully, we wait for a response Close message
     * before closing.  This method is waiting for such a response
     */
    protected void GracefulClosePacketCallback(Packet p, Edge from)
    {
      bool remove = false;
      CloseMessage close_req;
      lock( _sync ) {
        close_req = (CloseMessage)_gracefully_close_edges[from];
      }
      ConnectionMessage cm = null;
      try {
        cm = _cmp.Parse((ConnectionPacket)p);
#if DEBUG
        Console.WriteLine("Got cm: {0}\nfrom: {1}", cm, from);
#endif
        if( cm.Dir == ConnectionMessage.Direction.Response ) {
          //We expect a response to our close request:
          if( cm is CloseMessage ) {
            /*
             * Make sure we do not accept any more packets from
             * this Edge:
             */
            from.ClearCallback(Packet.ProtType.Connection);
            remove = true;
          }
          else {
            //This is some kind of other response.  We don't expect this.
            //Resend the close request:
            if( close_req != null ) from.Send( close_req.ToPacket() );
          }
        }
        else {
          if( cm is CloseMessage ) {
            //Somehow this is a Close Request.  We were expecting
            //a close response.  In this case.  We just respond
            //to his request:
            CloseMessage close_res = new CloseMessage();
            close_res.Id = cm.Id;
            close_res.Dir = ConnectionMessage.Direction.Response;
            from.Send( close_res.ToPacket() );
            /**
             * In order to make sure that we close gracefully, we simply
             * move this edge to the unconnected list.  The node will
             * close edges that have been there for some time
             */
            lock( _connection_table.SyncRoot ) {
              if( !_connection_table.IsUnconnected(from) ) {
                _connection_table.Disconnect(from);
              }
            }
          }
          else {
            //This is a request, we did not expect this
            ErrorMessage error_message =
              new ErrorMessage(ErrorMessage.ErrorCode.UnexpectedRequest,
                               "Got Expected Response");
            error_message.Id = cm.Id;
            error_message.Dir = ConnectionMessage.Direction.Response;
            from.Send( error_message.ToPacket() );
            //Re-request that the edge be closed:
            if( close_req != null ) from.Send( close_req.ToPacket() );
          }
        }
      }
      catch (InvalidCastException x) {
        Console.WriteLine( "Bad cast in node: " + x.ToString() );
      }
      catch( EdgeException ) {
        //Make sure the edge is closed:
        from.Close();
      }
      finally {
        if( remove ) {
            lock( _sync ) {
              _gracefully_close_edges.Remove(from);
            }
#if DEBUG
            Console.WriteLine("{0} Got a response {2} to our close request, closing: {1}",
                              Address,
                              from,
                              cm);
#endif
            from.Close();
          }
      }
    }

    /**
     * Implements the IPacketHandler interface
     */
    public void HandlePacket(Packet p, Edge from)
    {
      if( p.type == Packet.ProtType.AH ) {
        Send(p, from);
      }
#if ARI_DIRECT_ENABLE
      //added by Arijit Ganguly for handling direct packets
      if (p.type == Packet.ProtType.Direct) {
	HandleDirectPacket(from, (DirectPacket) p);
      }
#endif      
      else if( p.type == Packet.ProtType.Connection ) {
        GracefulClosePacketCallback(p, from); 
      }
    }
    
    /**
     * Check all the edges in the ConnectionTable and see if any of them
     * need to be pinged or closed.
     * This method is connected to the heartbeat event.
     */
    virtual protected void CheckEdgesCallback(object node, EventArgs args)
    {
      if( DateTime.UtcNow - _last_edge_check > _CONNECTION_TIMEOUT ) {
        //We are checking the edges now:
        _last_edge_check = DateTime.UtcNow;
        ArrayList edges_to_ping = new ArrayList();
        ArrayList edges_to_close = new ArrayList();
        lock( _connection_table.SyncRoot ) {
          foreach(Connection con in _connection_table) {
	    Edge e = con.Edge;
            if( _last_edge_check - e.LastInPacketDateTime  > _EDGE_CLOSE_TIMEOUT ) {
              //After this period of time, we close the edge no matter what.
              edges_to_close.Add(e);
            }
            else if( _last_edge_check - e.LastInPacketDateTime  > _CONNECTION_TIMEOUT ) {
              //Check to see if this connection is still active by pinging it
              edges_to_ping.Add(e);
            }
          }
          foreach(Edge e in _connection_table.GetUnconnectedEdges() ) {
            if( _last_edge_check - e.LastInPacketDateTime > _EDGE_CLOSE_TIMEOUT ) {
              edges_to_close.Add(e);
              lock( _sync ) {
                if( _gracefully_close_edges.Contains(e) ) {
                  _gracefully_close_edges.Remove(e);
                }
              }
            }
          }
        }
        //We release the lock before we start messing with the edges:
        int id = GetHashCode(); //This can be any number.
        foreach(Edge e in edges_to_ping) {
          try {
            PingMessage pm = new PingMessage();
            pm.Dir = ConnectionMessage.Direction.Request;
            pm.Id = id++;
            e.Send( pm.ToPacket() );
#if DEBUG
            Console.WriteLine("Sending ping to: {0}", e);
#endif
          }
          catch(EdgeException) {
            //This should only happen when the edge is closed.
            edges_to_close.Add(e);
          }
        }
        //Now we resend our close message to edges we are gracefully closing:
        lock( _sync ) {
          ArrayList edges_to_remove = new ArrayList();
          IDictionaryEnumerator grace_close_enum =
            _gracefully_close_edges.GetEnumerator();
          while( grace_close_enum.MoveNext() ) {
            Edge e = (Edge)grace_close_enum.Key;
            try {
              CloseMessage cm = (CloseMessage)grace_close_enum.Value;
              e.Send( cm.ToPacket() );
#if DEBUG
            Console.WriteLine("Sending close to: {0}", e);
#endif
            }
            catch(EdgeException) {
              //This edge is goofy, remove it:
              edges_to_remove.Add(e);
              edges_to_close.Add(e);
            }
          }
          //Now remove any bad edges:
          //We can't do it above because we would invalidate
          //the iterator.
          foreach(Edge erm in edges_to_remove) {
            _gracefully_close_edges.Remove(erm);
          }
          //Now it is safe to release lock
        }
        foreach(Edge e in edges_to_close) {
#if DEBUG
          Console.WriteLine("{1} Timeout Close: {0}", e, Address);
#endif
          //This guy is dead, close him down
          e.Close();
        }
      }
      else {
        //Don't do anything for now.
      }
    }
    /**
     * A TimerCallback to send the HeartBeatEvent
     */
    protected void HeartBeatCallback(object state)
    {
      ///Just send the event:
      try {
        if( HeartBeatEvent != null )
          HeartBeatEvent(this, EventArgs.Empty);
      }
      catch(Exception x) {
        Console.WriteLine("Exception in heartbeat: {0}", x.ToString() );
      }
    }

    /**
     * Send the packet to the next hop AVOIDING edge f
     * This is used by HandlePacket, and some AHPacketHandlers
     * @param p the packet to send on
     * @param f the edge to AVOID sending to
     */
    virtual public void Send(Packet p, Edge from)
    {

      //System.Console.WriteLine("Entering Send for node {0} packet {1}", this.Address, p.ToString() );

      int sent = 0;

      AHPacket packet = (AHPacket) p;
      Address dest = packet.Destination;
      IRouter router = (IRouter)_routers[dest.Class];
      
      bool deliver_locally = false;


      //System.Console.WriteLine("Sending with Router: {0}", router.ToString());

      if (router != null) {
        sent = router.Route(from, packet, out deliver_locally);
      }
      else {
        /*log.Error(Address.ToString() + " No router for packet: "
          + p.ToString());*/
      }

      if( deliver_locally ) {
        //This one's for us!
        /*log.Info(Address.ToString() + " Delivering Locally: "
          + packet.ToString());*/

        //#if DEBUG
#if false 
        System.Console.WriteLine("Delivering locally to node {0} packet {1}", this.Address, p.ToString() );
        //System.Console.ReadLine();
#endif  
        /*
        Announce(packet, from);
        */
        AnnounceState astate = new AnnounceState(packet, from);
        _packet_queue.Enqueue(astate);
      }

      if (sent <= 0) {
        //No edges got it
        /*log.Warn(Address.ToString() + " Could not send to: "
          + packet.Destination.ToString() );*/
        if( !deliver_locally ) {
          /*log.Warn(Address.ToString() + " Packet not delivered: "
            + packet.ToString() );*/
        }
      }
      else
      {
        //We did send it
        /*log.Info(Address.ToString() + " *_COULD_* send to: "
          + packet.Destination.ToString() );*/
      }
    }
    /**
     * This may be used when you have a complete packet to send
     * 
     * @param p the packet to send (including destination information)
     */
    virtual public void Send(Packet p)
    {
      //Send without avoiding any edges
      bool directly_routable = false;
#if ARI_DIRECT_ENABLE
      AHPacket packet = (AHPacket) p;
      //directly routable packets enabled.
      DirectlyRoute(packet, out directly_routable);
#endif      
      if (!directly_routable) {
#if ARI_DIRECT_DEBUG
	Console.WriteLine("Packet not routed directly. Using regular router means.");
#endif
	Send(p, null);
      } else {
#if ARI_DIRECT_DEBUG
	Console.WriteLine("Packet routed directlty. Bypassing all routers. ");
#endif
      }


      //Like Announce:
      AHPacket ahp = p as AHPacket;
      if( ahp != null ) {
        //Note that reading from an Hashtable is threadsafe
        ArrayList handlers = (ArrayList)_send_subs[ahp.PayloadType];
        if( handlers != null ) {
        /*
         * Ordinarily I prefer foreach, but for is faster in this
         * case and this code gets called for every packet we send
         */
          int count = handlers.Count;
          for(int i = 0; i < count; i++) {
            IAHPacketHandler hand = (IAHPacketHandler)handlers[i];
            //System.Console.WriteLine("Handler: {0}", hand);
            hand.HandleAHPacket(this, ahp, null);
          }
        }
      }
    }

    /**
     * Sends a packet to the address given.  The node keeps track of
     * which values to set for the TTL.
     */
    virtual public void SendTo(Address destination,
                               short ttl,
                               string p,
                               byte[] payload)
    {
      AHPacket packet = new AHPacket(0, ttl, _local_add, destination, p, payload);
      Send(packet);
    }

    /**
     * Sends a packet to the given address.  Estimates
     * the correct TTL to use, so users of this library
     * don't need to concern themselves with that.
     *
     * This is the recommended way for users of the library
     * to send packets.
     *
     * By default it sets the TTL to be Ln^3 N for StructuredAddress
     * types, and 2 Log_2 N, for UnstructuredAddress types.
     */
    virtual public void SendTo(Address destination,
		               string p,
			       byte[] payload)
    {
      short ttl = DefaultTTLFor(destination);
      SendTo(destination, ttl, p, payload);
    }

    /**
     * This replaces any existing IRouter objects with
     * ones given as arguments
     */
    virtual protected void SetRouters(IEnumerable routers)
    {
      lock(_sync) {
        foreach(IRouter r in routers) {
          foreach(int addclass in r.RoutedAddressClasses) {
            _routers[addclass] = r;
            r.ConnectionTable = _connection_table;
          }
        }
      }
    }

    /**
     * Where should we send these packets?
     */
    virtual public void Subscribe(string prot, IAHPacketHandler hand)
    {
      lock(_sync) {
        if (!_subscription_table.Contains(prot)) {
          _subscription_table[prot] = new ArrayList();
        }
        /*
         * We COPY the list because otherwise, one thread
         * could be in announce, and then try to modify
         * the list.  This would invalidate iterators in Announce
         */
        ArrayList a = new ArrayList( (ArrayList) _subscription_table[prot] );
        a.Add(hand);
        _subscription_table[prot] = a;
      }
    }
    /**
     * Subscribe to outgoing packets.  Use Subscribe to get incoming
     * packets
     */
    virtual public void SubscribeToSends(string prot, IAHPacketHandler hand)
    {
      lock(_sync) {
        ArrayList a = (ArrayList)_send_subs[prot];
        /*
         * We COPY the list because otherwise, one thread
         * could be in announce, and then try to modify
         * the list.  This would invalidate iterators in Announce
         */
        if (a == null) {
          a = new ArrayList();
        }
        else {
          a = new ArrayList(a);
        }
        a.Add(hand);
        _send_subs[prot] = a;
      }
    }

    /**
     * This handler is going to stop listening
     */
    virtual public void Unsubscribe(string prot, IAHPacketHandler hand)
    {
      lock(_sync) {
        if (_subscription_table.Contains(prot)) {
          //Make a copy to make sure there are no thread safety issues
          //with any Announces that might be going on.
          ArrayList a = new ArrayList( (ArrayList) _subscription_table[prot] );
          a.Remove(hand);
          _subscription_table[prot] = a;
        }
      }
    }
    /**
     * This handler is going to stop listening to sends of a given
     * type
     */
    virtual public void UnsubscribeToSends(string prot, IAHPacketHandler hand)
    {
      lock(_sync) {
        ArrayList a = (ArrayList)_send_subs[prot];
        if ( a != null) {
          //Make a copy for thread safety
          a = new ArrayList(a);
          a.Remove(hand);
          _send_subs[prot] = a;
        }
      }
    }

#if ARI_DIRECT_ENABLE
    /** Method to convert DirectPacket into an AHPacket
     *  @param edge edge the packet came from 
     *  @param direct_packet direct_packet which we just received
     *  @param ah_packet corresponding AHPacket (out paramater)
     */
    void HandleDirectPacket(Edge edge, DirectPacket direct_packet) {
#if ARI_DIRECT_DEBUG
      Console.WriteLine("Received a direct packet. Looking to handle it..");
#endif

      //to find out the source address, we simply have to lookup the connection table
      lock(_connection_table.SyncRoot) {
	Connection c = _connection_table.GetConnection(edge);
	string payload_prot = direct_packet.PayloadType;
	if (c.Address is AHAddress && _local_add is AHAddress && 
	    payload_prot == AHPacket.Protocol.IP) {
#if ARI_DIRECT_DEBUG
	  Console.WriteLine("Sanity check (address type checks, payload protocol) successfull, deflating packet");
#endif

	  Address source = c.Address;
	  Address dest = _local_add;
	  short ttl = 0;
	  short hops = 1;
	  AHPacket packet = new AHPacket(hops, ttl, source, dest, payload_prot, 
					 direct_packet.Payload);
	  //deliver the packet locally, wihout going through any routing hastle
#if ARI_DIRECT_DEBUG
	  Console.WriteLine("Inflated packet from {0} to {1} bytes", direct_packet.Length, 
			    packet.Length);
      Console.WriteLine("Announcing packet reception....");
#endif

	  Announce(packet, edge);
	} else {
	  //protocol is only restricted to structured nodes; why did we get this packet
	  //from this edge
	  ; //ignore
#if ARI_DIRECT_DEBUG
      Console.WriteLine("Sanity check (address type checks, payload protocol) failed, ignoring packet");
#endif
	}
      }
    }
    /** 
     * Method that checks if the packet is routable as a direct packet.
     * Please note that we do such tricks only for AHAddress packets
     */
    void DirectlyRoute(AHPacket packet, out bool directly_routable) {
#if ARI_DIRECT_DEBUG
      Console.WriteLine("Testing if packet is routable directly. ");
#endif
      directly_routable = false;
      Address dest = packet.Destination as AHAddress;
      string payload_prot = packet.PayloadType;

      //only if destination is a structured node. and payload protocol is IP
      if (dest != null && payload_prot == AHPacket.Protocol.IP) {
#if ARI_DIRECT_DEBUG
	Console.WriteLine("Packet is addressed to a structured address, and has IP-payload ");
#endif
	Connection next_con = null;
	lock(_connection_table.SyncRoot)
	  {
	    //check if there is a leaf connection
	    foreach(Connection c in _connection_table.GetConnections(ConnectionType.Leaf)) {
	      if( c.Address.Equals(dest) ) {
		//We can route it to this 
		next_con = c;
#if ARI_DIRECT_DEBUG
		Console.WriteLine("Found a leaf connection to send packet on.. ");
#endif
		break;
	      }
	    }
	    //otherwise look for a structured connection
	    if (next_con == null) {
	      //check if there is a structured connection
	      foreach(Connection c in _connection_table.GetConnections(ConnectionType.Structured)) {
		if( c.Address.Equals(dest) ) {
		  //We can route it to this 
		  next_con = c;
#if ARI_DIRECT_DEBUG
		  Console.WriteLine("Found a structured connection to send packet on.. ");
#endif
		  break;
		}
	      }
	    }
	  }//end of connection table lock;
	if (next_con != null) {//directly routable
	  directly_routable = true;
#if ARI_DIRECT_DEBUG
	  Console.WriteLine("Converting to direct packet.... ");
#endif
	  DirectPacket direct_packet = null;
	  packet.ToDirectPacket(out direct_packet);
#if ARI_DIRECT_DEBUG
	  Console.WriteLine("Converted AHPacket: {0} to DirectPacket {1} bytes", packet.Length, 
			    direct_packet.Length);
	  Console.WriteLine("Using the edge directly to send away the direct packet. ");
#endif
	  next_con.Edge.Send(direct_packet);
	} else {
#if ARI_DIRECT_DEBUG
	  Console.WriteLine("Couldn't find an edge to send packet on. ");
#endif  
	}
      } else {
#if ARI_DIRECT_DEBUG
	Console.WriteLine("Packet not suitable for direct delivery ");
#endif  
      }
    }
#endif
  }
}
