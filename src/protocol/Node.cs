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
  abstract public class Node : IDataHandler
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
    public Node(Address addr)
    {
      //Start with the address hashcode:
      _gracefully_close_edges  = new Hashtable();

      _sync = new Object();
      lock(_sync)
      {
        /*
         * Make all the hashtables : 
         */
        _local_add = addr;
        _subscription_table = new Hashtable();

        _task_queue = new TaskQueue();
        //Here is the thread for announcing packets
        _packet_queue = new BlockingQueue();
        _running = false;
        _announce_thread = new Thread(this.AnnounceThread);
        
        _connection_table = new ConnectionTable(_local_add);
        _connection_table.ConnectionEvent += this.ConnectionHandler;
        /*
         * We must later make sure the EdgeEvent events from
         * any EdgeListeners are connected to _cph.EdgeHandler
         */
        /**
         * Here are the protocols that every edge must support
         */
        //Handles linking:
        GetTypeSource(PType.Protocol.Linking).Subscribe(new ConnectionPacketHandler(), this);

        /* Here are the transport addresses */
        _remote_ta = new ArrayList();
        /*@throw ArgumentNullException if the list ( new ArrayList()) is null.
         */
        /* EdgeListener's */
        _edgelistener_list = new ArrayList();
        _edge_factory = new EdgeFactory();

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
     * This class represents the demultiplexing of each
     * type of data to different handlers
     */
    protected class NodeSource : ISource {
      protected object _sync; //The object to lock before changing the subscriptions
      protected volatile ArrayList _subs;
      protected volatile ArrayList _states;

      public NodeSource(object sync) {
        _subs = new ArrayList(); 
        _states = new ArrayList(); 
        _sync = sync;
      }

      public void Subscribe(IDataHandler h, object state) {
        lock( _sync ) {
          _subs = Functional.Add(_subs, h);
          _states = Functional.Add(_states, state);
        }
      }
      public void Unsubscribe(IDataHandler h) {
        lock( _sync ) {
          int idx = _subs.IndexOf(h);
          _subs = Functional.RemoveAt(_subs, idx);
          _states = Functional.RemoveAt(_subs, idx);
        }
      }
      /**
       * @return the number of Handlers that saw this data
       */
      public int Announce(MemBlock b, ISender return_path) {
        ArrayList subs = null;
        ArrayList states = null;
        lock( _sync ) {
          subs = _subs;
          states = _states;
        }
        int handlers = 0;
        for(int i = 0; i < subs.Count; i++) {
          IDataHandler dh = (IDataHandler)subs[i];
          dh.HandleData(b, return_path, states[i]);
          handlers++;
        }
        return handlers;
      }
    }
    /**
     * Keeps track of the objects which need to be notified 
     * of certain packets.
     */
    protected Hashtable _subscription_table;

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
     * This is true after Connect is called and false after
     * Disconnect is called.
     */
    volatile protected bool _running;

    /** Object which we lock for thread safety */
    protected Object _sync;

    protected Thread _announce_thread;

    protected ConnectionTable _connection_table;


    protected Hashtable _gracefully_close_edges;

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

    //add an event handler which conveys the fact that Connect has been called on the node
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
      el.EdgeEvent += this.EdgeHandler;
    }
    /**
     * Unsubscribe all IDataHandlers for a given
     * type
     */
    protected void ClearTypeSource(PType t) {
      lock( _sync ) {
        //Reset it to a new source
        _subscription_table[t] = new NodeSource(_sync);
      }
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
     * This Handler should be connected to incoming EdgeEvent
     * events.  If it is not, it cannot hear the new edges.
     *
     * When a new edge is created, we make sure we can hear
     * the packets from it.  Also, we make sure we can hear
     * the CloseEvent.
     *
     * @param edge the new Edge
     */
    protected void EdgeHandler(object edge, EventArgs args)
    {
      Edge e = (Edge)edge;
      e.Subscribe(this, e);
      _connection_table.AddUnconnected(e);
    }

    /**
     * All packets that come to this node are demultiplexed according to
     * type.  To subscribe, get the ISource for the type you want, and
     * subscribe to it.  Similarly for the unsubscribe.
     */
    public ISource GetTypeSource(PType t) {
      ISource s;
      lock( _sync ) {
        s = (ISource)_subscription_table[t];
        if( s == null ) {
          s = new NodeSource(_sync);
          _subscription_table[t] = s;
        }
      }
      return s;
    }
    /**
     * Starts all edge listeners for the node.
     * Useful for connect/disconnect operations
     */
    protected virtual void StartAllEdgeListeners()
    {
      foreach(EdgeListener el in _edgelistener_list) {
#if DEBUG
        Console.Error.WriteLine("{0} starting {1}", Address, el);
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
      public MemBlock Data;
      public Edge From;
      public AnnounceState(MemBlock p, Edge from) {
        Data = p;
        From = from;
      }
    }
    private void AnnounceThread() {
      try {
       while( _running ) {
        AnnounceState a_state = (AnnounceState)_packet_queue.Dequeue();
        Announce(a_state.Data, a_state.From);
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
    public virtual void Announce(MemBlock b, ISender from)
    {

      //System.Console.Error.WriteLine("Announcing packet: {0}:", p.ToString() );
      //System.Console.Error.WriteLine("PayloadType: {0}:", p.PayloadType );

      //When Subscribe or unsubscribe are called,
      //they make copies of the ArrayList, thus we
      //only need to hold the sync while we are
      //getting the list of handlers.

      /* 
       * Note that getting from Hashtable is threadsafe, multiple
       * threads writing is a problem
       */
      MemBlock payload = null;
      PType t = PType.Parse(b, out payload);
      NodeSource ns = (NodeSource)GetTypeSource(t);
      int handlers = 0;
      try {
        handlers = ns.Announce(payload, from);
      }
      catch(Exception x) {
        System.Console.Error.WriteLine("ERROR: Packet Handling Exception");
        System.Console.Error.WriteLine("Hander: {0}\tEdge: {1}\tPacket: {2}", ns, from, b);
        System.Console.Error.WriteLine("Exception: {0}", x);
      }
      /**
       * @todo if no one handled the packet, we might want to send some
       * ICMP-like message.
       */
      if( handlers == 0 ) {
        string p_s = payload.GetString(System.Text.Encoding.ASCII);
        System.Console.Error.WriteLine("No Handler for packet type: {0}\n{1}", t, p_s);
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
#if DEBUG
      Console.Error.WriteLine("[Connect: {0}] deactivating task queue", _local_add);
#endif
      _task_queue.IsActive = false;
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
      edge.Subscribe(this, edge);
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
     * This is an enner class to handle Graceful closing
     * @see GracefullyClose
     */
    protected class GracefulCloser : IDataHandler {
      public CloseMessage CM;
      public Edge Edge;
      protected ConnectionTable _connection_table;
      protected ConnectionMessageParser _cmp;

      public GracefulCloser(Edge e, CloseMessage cm, ConnectionTable ct) {
        Edge = e;
        CM = cm;
        _connection_table = ct;
        _cmp = new ConnectionMessageParser();
        e.Subscribe(this, null);
      }

    /**
     * When we close gracefully, we wait for a response Close message
     * before closing.  This method is waiting for such a response
     */
      public void HandleData(MemBlock data, ISender return_path, object state) {
        bool remove = false;
        CloseMessage close_req = CM;
        
        ConnectionMessage cm = null;
        try {
          MemBlock payload;
          PType pt = PType.Parse(data, out payload);
          //Ignore non-link protocol packets
          if( !pt.Equals( PType.Protocol.Linking ) ) { return; }
          cm = _cmp.Parse(payload);
  #if DEBUG
          Console.Error.WriteLine("Got cm: {0}\nfrom: {1}", cm, from);
  #endif
          if( cm.Dir == ConnectionMessage.Direction.Response ) {
            //We expect a response to our close request:
            if( cm is CloseMessage ) {
              /*
               * Make sure we do not accept any more packets from
               * this Edge:
               */
              Edge.Unsubscribe(this);
              remove = true;
            }
            else {
              //This is some kind of other response.  We don't expect this.
              //Resend the close request:
              Send();
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
              return_path.Send( close_res.ToPacket() );
              /**
               * In order to make sure that we close gracefully, we simply
               * move this edge to the unconnected list.  The node will
               * close edges that have been there for some time
               */
              lock( _connection_table.SyncRoot ) {
                if( !_connection_table.IsUnconnected(Edge) ) {
                  _connection_table.Disconnect(Edge);
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
              return_path.Send( error_message.ToPacket() );
              //Re-request that the edge be closed:
              return_path.Send( close_req.ToPacket() );
            }
          }
        }
        catch (InvalidCastException x) {
          Console.Error.WriteLine( "Bad cast in node: " + x.ToString() );
        }
        catch( EdgeException ) {
          //Make sure the edge is closed:
          Edge.Close();
        }
        finally {
          if( remove ) { Edge.Close(); }
        }
      }
        
      public void Send() {
        Edge.Send( CM.ToPacket() );
      }

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
        e.CloseEvent += this.GracefulCloseHandler;
        GracefulCloser gc = new GracefulCloser(e, cm, _connection_table);
        lock( _sync ) {
          _gracefully_close_edges[e] = gc;
        }
        /**
         * Close any connection on this edge, and
         * put the edge into the list of unconnected edges
         */
        _connection_table.Disconnect(e);
        gc.Send();
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
     * Implements the IDataHandler interface
     */
    public void HandleData(MemBlock data, ISender return_path, object state) {
      AnnounceState astate = new AnnounceState(data, return_path as Edge);
      _packet_queue.Enqueue(astate);
      //Announce(data, return_path);
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
	      Console.Error.WriteLine("On an edge timeout, closing connection: {0}", con);
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
	      Console.Error.WriteLine("Close an unconnected edge: {0}", e);
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
            Console.Error.WriteLine("Sending ping to: {0}", e);
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
              GracefulCloser gc = (GracefulCloser)grace_close_enum.Value;
              gc.Send();
#if DEBUG
            Console.Error.WriteLine("Sending close to: {0}", e);
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
          Console.Error.WriteLine("{1} Timeout Close: {0}", e, Address);
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
        if( HeartBeatEvent != null ) {
          HeartBeatEvent(this, EventArgs.Empty);
        }
      }
      catch(Exception x) {
        Console.Error.WriteLine("Exception in heartbeat: {0}", x.ToString() );
      }
    }
  }

  /**
   * This is a sender that just announces data at the local node.
   */
  public class LocalSender : ISender {
    protected Node _n;

    public LocalSender(Node n) {
      _n = n;
    }

    public void Send(ICopyable data) {
      MemBlock b = MemBlock.Copy(data);
      _n.Announce(b, this);
    }
  }
}
