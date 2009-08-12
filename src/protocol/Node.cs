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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;

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
  abstract public class Node : IDataHandler, ISender
  {
    static Node() {
      SenderFactory.Register("localnode", CreateLocalSender); 
    }

    public static Node CreateLocalSender(Node n, string uri) {
      return n;
    }
    /**
     * Create a node in the realm "global"
     */
    protected Node(Address addr) : this(addr, "global") { }
    /**
     * Create a node with a given local address and
     * a set of Routers.
     * @param addr Address for the local node
     * @param realm the Realm or Namespace this node belongs to
     */

    protected Node(Address addr, string realm)
    {
      //Start with the address hashcode:

      _sync = new Object();
      lock(_sync)
      {
        DemuxHandler = new DemuxHandler();
        /*
         * Make all the hashtables : 
         */
        _local_add = AddressParser.Parse( addr.ToMemBlock() );
        _realm = String.Intern(realm);
        
        /* Set up the heartbeat */
        _heart_period = 500; //500 ms, or 1/2 second.
        _heartbeat_handlers = new Dictionary<EventHandler, Brunet.Util.FuzzyEvent>();

        _task_queue = new NodeTaskQueue(this);
        _packet_queue = new Brunet.Util.LFBlockingQueue<IAction>();

        _running = 0;
        _send_pings = 1;
        _LOG = ProtocolLog.Monitor.Enabled;

        _connection_table = new ConnectionTable(_local_add);
        _connection_table.ConnectionEvent += this.ConnectionHandler;

        //We start off offline.
        _con_state = Node.ConnectionState.Offline;
        
        /* Set up the ReqrepManager as a filter */
        _rrm = new ReqrepManager(Address.ToString());
        DemuxHandler.GetTypeSource(PType.Protocol.ReqRep).Subscribe(_rrm, null);
        _rrm.Subscribe(this, null);
        this.HeartBeatEvent += _rrm.TimeoutChecker;
        /* Set up RPC */
        _rpc = new RpcManager(_rrm);
        DemuxHandler.GetTypeSource( PType.Protocol.Rpc ).Subscribe(_rpc, null);

        /*
         * Where there is a change in the Connections, we might have a state
         * change
         */
        _connection_table.ConnectionEvent += this.CheckForStateChange;
        _connection_table.DisconnectionEvent += this.CheckForStateChange;
        _connection_table.StatusChangedEvent += this.CheckForStateChange;

        _codeinjection = new CodeInjection(this);
        _codeinjection.LoadLocalModules();
        /*
         * We must later make sure the EdgeEvent events from
         * any EdgeListeners are connected to _cph.EdgeHandler
         */
        /**
         * Here are the protocols that every edge must support
         */
        /* Here are the transport addresses */
        _remote_ta = new ArrayList();
        /*@throw ArgumentNullException if the list ( new ArrayList()) is null.
         */
        /* EdgeListener's */
        _edgelistener_list = new ArrayList();
        _edge_factory = new EdgeFactory();
        
        /* Initialize this at 15 seconds */
        _connection_timeout = new TimeSpan(0,0,0,0,15000);
        //Check the edges from time to time
        IAction cec_act = new HeartBeatAction(this, this.CheckEdgesCallback);
        _check_edges = Brunet.Util.FuzzyTimer.Instance.DoEvery(delegate(DateTime dt) {
          this.EnqueueAction(cec_act);
        }, 15000, 1000);
      }
    }
 //////////////
 ///  Inner Classes
 //////////
 
    protected class HeartBeatAction : IAction {
    
      readonly Node _n;
      readonly EventHandler _eh;
    
      public HeartBeatAction(Node n, EventHandler eh) {
        _n = n;
        _eh = eh;
      }
      public void Start() {
        try {
          _eh(_n, EventArgs.Empty);
        }
        catch(Exception x) {
          ProtocolLog.WriteIf(ProtocolLog.Exceptions, String.Format(
            "Exception in heartbeat event : {0}", x));
        }
      }
   }

    protected class LogAction : IAction {
      protected readonly Brunet.Util.LFBlockingQueue<IAction> _q;
      public LogAction(Brunet.Util.LFBlockingQueue<IAction> q) {
        _q = q;
      }

      public void Start() {
        ProtocolLog.Write(
        ProtocolLog.Monitor, String.Format("I am alive: {0}, packet_queue_length: {1}", 
                                                            DateTime.UtcNow, _q.Count));
      }
    }

    /**
     * When we do announces using the seperate thread, this is
     * what we pass
     */
    private class AnnounceState : IAction {
      public readonly MemBlock Data;
      public readonly ISender From;
      public readonly Node LocalNode;
      public AnnounceState(Node n, MemBlock p, ISender from) {
        LocalNode = n;
        Data = p;
        From = from;
      }
      
      /**
       * Perform the action of announing a packet
       */
      public void Start() {
        LocalNode.Announce(Data, From);
      }
      public override string ToString() {
        try {
          return Data.GetString(System.Text.Encoding.ASCII);
        }
        catch {
          return "AnnounceState: could not get string as ASCII";
        }
      }
    }
    private class EdgeCloseAction : IAction {
      protected Edge EdgeToClose;
      public EdgeCloseAction(Edge e) {
        EdgeToClose = e;
      }
      public void Start() {
        EdgeToClose.Close();
      }
      public override string ToString() {
        return "EdgeCloseAction: " + EdgeToClose.ToString();
      }
    }

    public class GracefulCloseAction : Brunet.Util.Triple<Node, Edge, string>, IAction {
      public GracefulCloseAction(Node n, Edge e, string r) : base(n, e, r) { }
      public void Start() {
        First.GracefullyClose(Second, Third);
      }
    }

    /**
     * This is a TaskQueue where new TaskWorkers are started
     * by EnqueueAction, so they are executed in the announce thread
     * and without the call stack growing arbitrarily
     */
    protected class NodeTaskQueue : TaskQueue {
      protected readonly Node LocalNode;
      public NodeTaskQueue(Node n) {
        LocalNode = n;
      }
      protected override void Start(TaskWorker tw) {
        LocalNode.EnqueueAction(tw);
      }
    }

//////
// End of inner classes
/////

    /**
     * This represents the Connection state of the node.
     * We use different words for each state to reduce the
     * liklihood of typos causing problems in the code.
     */
    public enum ConnectionState {
      Offline, /// Not yet called Node.Connect
      Joining, /// Called Node.Connect but IsConnected has never been true
      Connected, /// IsConnected is true.
      SeekingConnections, /// We were previously Connected, but lost connections.
      Leaving, /// Node.Disconnect has been called, but we haven't closed all edges.
      Disconnected /// We are completely disconnected and have no active Edges.
    }
    public delegate void StateChangeHandler(Node n, ConnectionState newstate);
    /**
     * This event is called every time Node.ConState changes.  The new state
     * is passed with the event.
     */
    public event StateChangeHandler StateChangeEvent;
    public ConnectionState ConState {
      get {
        lock( _sync ) {
          return _con_state;
        }
      }
    }
    protected ConnectionState _con_state;

    protected readonly Address _local_add;
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
    protected readonly EdgeFactory _edge_factory;
    /**
     *  my EdgeFactory
     */
    public EdgeFactory EdgeFactory { get { return _edge_factory; } }

    /**
     * Here are all the EdgeListener objects for this Node
     */
    protected ArrayList _edgelistener_list;
    public ArrayList EdgeListenerList {
      get {
        return (ArrayList) _edgelistener_list.Clone();
      }
    }

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
    protected readonly Brunet.Util.LFBlockingQueue<IAction> _packet_queue;
    /** The IAction that was most recently started */
    protected IAction _current_action;
    protected float _packet_queue_exp_avg = 0.0f;

    //Getting from a BooleanSwitch is strangely very expensive, do it once
    protected bool _LOG;
    //If we get this big, we just throw an exception, and not enqueue it
    protected static readonly int MAX_QUEUE_LENGTH = 8192;
    /**
     * This number should be more thoroughly tested, but my system and dht
     * never surpassed 105.
     */
    public static readonly int MAX_AVG_QUEUE_LENGTH = 4096;
    public static readonly float PACKET_QUEUE_RETAIN = 0.99f;
    public bool DisconnectOnOverload {
      get { return _disconnect_on_overload; }
      set { _disconnect_on_overload = value; }
    }

    public bool _disconnect_on_overload = false;

    protected readonly string _realm;
    /**
     * Each Brunet Node is in exactly 1 realm.  This is 
     * a namespacing feature.  This allows you to create
     * Brunets which are separate from other Brunets.
     *
     * The default is "global" which is the standard
     * namespace.
     */
    public string Realm { get { return _realm; } }
    
    protected readonly ArrayList _remote_ta;
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
        UpdateRemoteTAs(value);
      }
    }

    /**
     * This is true after Connect is called and false after
     * Disconnect is called.
     */
    protected int _running;
    protected int _send_pings;
    protected Util.FuzzyEvent _check_edges;

    /** Object which we lock for thread safety */
    protected readonly object _sync;

    /**  <summary>Handles subscriptions for the different types of packets
    that come to this node.</summary>*/
    public readonly DemuxHandler DemuxHandler;

    /**
    <summary>All packets that come to this are demultiplexed according to t.
    To subscribe or unsubscribe, get the ISource for the type you want and
    subscribe to it,</summary>
    <param name="t">The key for the MultiSource.</param>
    @deprecated Use Node.DemuxHandler.GetTypeSource
    */
    public ISource GetTypeSource(Object t) {
      return DemuxHandler.GetTypeSource(t);
    }

    /**
    <summary>Deletes (and thus unsubscribes) all IDataHandlers for a given key.
    </summary>
    <param name="t">The key for the MultiSource.</param>
    @deprecated Use Node.DemuxHandler.ClearTypeSource
    */
    public void ClearTypeSource(Object t) {
      DemuxHandler.ClearTypeSource(t);
    }

    /**
    <summary>Deletes (and thus unsubscribes) all IDataHandlers for the table.
    </summary>
    @deprecated Use Node.DemuxHandler.Clear
    */
    public void Clear() {
      DemuxHandler.Clear();
    }

    protected readonly ConnectionTable _connection_table;

    /**
     * Manages the various mappings associated with connections
     */
    public virtual ConnectionTable ConnectionTable { get { return _connection_table; } }
    /**
     * Brunet IPHandler service!
     */
    public IPHandler IPHandler { get { return _iphandler; } }
    protected IPHandler _iphandler;
    protected CodeInjection _codeinjection;
    
    protected readonly ReqrepManager _rrm;
    public ReqrepManager Rrm { get { return _rrm; } }
    protected readonly RpcManager _rpc;
    public RpcManager Rpc { get { return _rpc; } }
    protected MapReduceHandler _mr_handler;
    public MapReduceHandler MapReduce { get { return _mr_handler; } }


    /**
     * This is true if the Node is properly connected in the network.
     * If you want to know when it is safe to assume you are connected,
     * listen to all for Node.ConnectionTable.ConnectionEvent and
     * Node.ConnectionTable.DisconnectionEvent and then check
     * this property.  If it is true, you should probably wait
     * until it is false if you need the Node to be connected
     */
    public abstract bool IsConnected { get; }
    protected readonly NodeTaskQueue _task_queue;
    /**
     * This is the TaskQueue for this Node
     */
    public TaskQueue TaskQueue { get { return _task_queue; } }

    protected int _heart_period;
    ///how many milliseconds between heartbeats
    public int HeartPeriod { get { return _heart_period; } }

    ///If we don't hear anything from a *CONNECTION* in this time, ping it.
    protected TimeSpan _connection_timeout;
    ///This is the maximum value we allow _connection_timeout to grow to
    protected static readonly TimeSpan MAX_CONNECTION_TIMEOUT = new TimeSpan(0,0,0,15,0);
    //Give edges this long to get connected, then drop them
    protected static readonly TimeSpan _unconnected_timeout = new TimeSpan(0,0,0,30,0);
    /**
     * Maximum number of TAs we keep in both for local and remote.
     * This does not control how many we send to our neighbors.
     */
    static protected readonly int _MAX_RECORDED_TAS = 10000;

    ///after each HeartPeriod, the HeartBeat event is fired
    public event EventHandler HeartBeatEvent {
      add {
        IAction hba = new HeartBeatAction(this, value);
        Action<DateTime> torun = delegate(DateTime now) {
          //Execute the code in the node's thread
          this.EnqueueAction(hba);
        };
        //every period +/- half a period, run this event
        var fe = Brunet.Util.FuzzyTimer.Instance.DoEvery(torun, _heart_period, _heart_period / 2 + 1);
        lock( _sync ) {
          _heartbeat_handlers[ value ] = fe;
        }
      }

      remove {
        Brunet.Util.FuzzyEvent fe = null;
        lock( _sync ) {
          if(_heartbeat_handlers.TryGetValue(value, out fe) ) {
            _heartbeat_handlers.Remove(value);
          }
        }
        if( fe != null ) {
          fe.TryCancel();
        }
      }
    }

    protected Dictionary<EventHandler, Brunet.Util.FuzzyEvent> _heartbeat_handlers;
    
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
      el.EdgeCloseRequestEvent += delegate(object elsender, EventArgs args) {
        EdgeCloseRequestArgs ecra = (EdgeCloseRequestArgs)args;
        Close(ecra.Edge);
      };
    }

    /** Immediately stop the node. ONLY USE FOR TESTING!!!!!!!
     * This simulates a node being disconnected immediately from the network,
     * as in a crash, or a network outage.  It should NEVER be used by a
     * well-behaved node.  It is only for test programs.
     *
     * To gracefully disconnect, you the Node.Disconnect() method, which
     * closes edges gracefully and informs its neighbors before going offline.
     */
    public abstract void Abort();
    
    /**
     * Called when there is a connection or disconnection.  Send a StateChange
     * event if need be.
     * We could be transitioning from:
     *   Joining -> Connected
     *   Connected -> SeekingConnections
     */
    protected void CheckForStateChange(object ct, EventArgs ce_args) {
      bool con = this.IsConnected; 
      ConnectionState new_state;
      if( con ) {
        new_state = Node.ConnectionState.Connected;
      }
      else {
        /*
         * The only other state change that is triggered by a Connection
         * or Disconnection event is SeekingConnections
         */
        new_state = Node.ConnectionState.SeekingConnections;
      }
      bool success;
      SetConState(new_state, out success);
      if( success ) {
        SendStateChange(new_state);
      }
    }

    protected void Close(Edge e) {
      try {
        //This can throw an exception if the _packet_queue is closed
        EnqueueAction(new EdgeCloseAction(e));
      }
      catch {
        e.Close();
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
      else if( ttld > (double)Int16.MaxValue ) {
        ttl = Int16.MaxValue;
      }
      else {
        ttl = (short)( ttld );
      }
      //When the network is very small this could happen, at least give it one
      //hop:
      if( ttl < 1 ) { ttl = 1; }
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
      try {
        _connection_table.AddUnconnected(e);
        e.Subscribe(this, e);
      }
      catch(TableClosedException) {
        /*
         * Close this edge immediately, before any packets
         * have a chance to be received.  We are shutting down,
         * and it is best that we stop getting new packets
         */
        e.Close();
      }
    }

    /**
     * Put this IAction object into the announce thread and call start on it
     * there.
     */
    public void EnqueueAction(IAction a) {
#if BRUNET_SIMULATOR
      a.Start();
      return;
#else
      int queue_size = _packet_queue.Count;
      if( queue_size > MAX_QUEUE_LENGTH ) {
        /*
         * Disconnect actually assumes the _packet_queue is being processed
         * if it is not, due to a blocking operation, or deadlock, we could
         * still be adding things into the queue.  This is here to prevent
         * a memory explosion
         */
//        throw new Exception(String.Format("Queue is too long: {0}", queue_size));
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, String.Format("Queue is too long: {0}", queue_size));
      }
      int count = _packet_queue.Enqueue(a);
      _packet_queue_exp_avg = (PACKET_QUEUE_RETAIN * _packet_queue_exp_avg)
          + ((1 - PACKET_QUEUE_RETAIN) * count);

      if(_packet_queue_exp_avg > MAX_AVG_QUEUE_LENGTH) {
        if(_LOG) {
          String top_string = String.Empty;
          try {
            top_string = _current_action.ToString();
          }
          catch {}
          ProtocolLog.Write(ProtocolLog.Monitor, String.Format(
            "Packet Queue Average too high: {0} at {1}.  Actual length:  {2}\n\tTop most action: {3}",
            _packet_queue_exp_avg, DateTime.UtcNow, count, top_string));
        }
        if(_disconnect_on_overload) {
          Disconnect();
        }
      }
#endif
    }

    /**
     * Send the StateChange event
     */
    protected void SendStateChange(ConnectionState new_state) {
      if( new_state == Node.ConnectionState.Joining && ArrivalEvent != null) {
        ArrivalEvent(this, null);
      }
      if( new_state == Node.ConnectionState.Leaving && DepartureEvent != null) {
        DepartureEvent(this, null);
      }
      StateChangeEvent(this, new_state);
    }
    /**
     * This sets the ConState to new_cs and returns the old
     * ConState.
     *
     * This method knows about the allowable state transitions.
     * @param success is set to false if we can't do the state transition.
     * @return the value of ConState prior to the method being called
     */
    protected ConnectionState SetConState(ConnectionState new_cs, out bool success) {
      ConnectionState old_state;
      success = false;
      lock( _sync ) {
        old_state = _con_state;
        if( old_state == new_cs ) {
          //This is not a state change
          return old_state;
        }
        if( new_cs == Node.ConnectionState.Joining ) {
          success = (old_state == Node.ConnectionState.Offline);
        }
        else if( new_cs == Node.ConnectionState.Connected ) {
          success = (old_state == Node.ConnectionState.Joining) ||
                    (old_state == Node.ConnectionState.SeekingConnections);
        }
        else if( new_cs == Node.ConnectionState.SeekingConnections ) {
          success = (old_state == Node.ConnectionState.Connected);
        }
        else if( new_cs == Node.ConnectionState.Leaving ) {
          success = (old_state != Node.ConnectionState.Disconnected);
        }
        else if( new_cs == Node.ConnectionState.Disconnected ) {
          success = (old_state == Node.ConnectionState.Leaving );
        }
        else if( new_cs == Node.ConnectionState.Offline ) {
          // We can never move into the Offline state.
          success = false;
        }
        /*
         * Now let's update _con_state
         */
        if( success ) {
          _con_state = new_cs;
        }
      }
      return old_state;
    }

    /**
     * Starts all edge listeners for the node.
     * Useful for connect/disconnect operations
     */
    protected virtual void StartAllEdgeListeners()
    {
      foreach(EdgeListener el in _edgelistener_list) {
        ProtocolLog.WriteIf(ProtocolLog.NodeLog, String.Format(
          "{0} starting {1}", Address, el));

        el.Start();
      }
      Interlocked.Exchange(ref _running, 1);
    }

    /**
     * Stops all edge listeners for the node.
     * Useful for connect/disconnect operations
     */
    protected virtual void StopAllEdgeListeners()
    {
      bool changed = false;
      try {
        SetConState(Node.ConnectionState.Disconnected, out changed);
        foreach(EdgeListener el in _edgelistener_list) {
          el.Stop();
        }
        _edgelistener_list.Clear();
        Interlocked.Exchange(ref _running, 0);
        //This makes sure we don't block forever on the last packet
        _packet_queue.Enqueue(NullAction.Instance);
      }
      finally {
        if( changed ) {
          SendStateChange(Node.ConnectionState.Disconnected);
          Dictionary<EventHandler, Brunet.Util.FuzzyEvent> hbhands = null;
          lock(_sync) {
            hbhands = _heartbeat_handlers;
            _heartbeat_handlers = null;
            //Clear out the subscription table
            DemuxHandler.Clear();
          }
          foreach(KeyValuePair<EventHandler, Brunet.Util.FuzzyEvent> de in hbhands) {
            //Stop running the event
            de.Value.TryCancel();
          }
          _check_edges.TryCancel();
        }
      }
    }
    /**
     * There can only safely be one of these threads running
     */
    protected void AnnounceThread() {
      Brunet.Util.FuzzyEvent fe = null;
      try {
        int millisec_timeout = 5000; //log every 5 seconds.
        IAction queue_item = null;
        bool timedout = false;
        if( ProtocolLog.Monitor.Enabled ) {
          IAction log_act = new LogAction(_packet_queue);
          Action<DateTime> log_todo = delegate(DateTime dt) {
            EnqueueAction(log_act);
          };
          fe = Brunet.Util.FuzzyTimer.Instance.DoEvery(log_todo, millisec_timeout, millisec_timeout/2);
        }
        while( 1 == _running ) {
          queue_item = _packet_queue.Dequeue(millisec_timeout, out timedout);
          if (!timedout) {
            _current_action = queue_item;
            queue_item.Start();
          }
        }
      }
      catch(System.InvalidOperationException x) {
        //This is thrown when Dequeue is called on an empty queue
        //which happens when the BlockingQueue is closed, which
        //happens on Disconnect
        if(1 == _running) {
          ProtocolLog.WriteIf(ProtocolLog.Exceptions, String.Format(
            "Running in AnnounceThread got Exception: {0}", x));
        }
      }
      catch(Exception x) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, String.Format(
        "ERROR: Exception in AnnounceThread: {0}", x));
      }
      finally {
        //Make sure we stop logging:
        if( fe != null ) { fe.TryCancel(); }
      }
      ProtocolLog.Write(ProtocolLog.Monitor,
                        String.Format("Node: {0} leaving AnnounceThread",
                                      this.Address));

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
    protected virtual void Announce(MemBlock b, ISender from)
    {
      //When Subscribe or unsubscribe are called,
      //they make copies of the ArrayList, thus we
      //only need to hold the sync while we are
      //getting the list of handlers.

      /* 
       * Note that getting from Hashtable is threadsafe, multiple
       * threads writing is a problem
       */
      MemBlock payload = null;
      int handlers = 0;
      MultiSource ns = null;
      PType t = null;
      try {
        t = PType.Parse(b, out payload);
        ns = (MultiSource)DemuxHandler.GetTypeSource(t);
        handlers = ns.Announce(payload, from);
        /**
         * @todo if no one handled the packet, we might want to send some
         * ICMP-like message.
         */
        if( handlers == 0 ) {
          string p_s = payload.GetString(System.Text.Encoding.ASCII);
          ProtocolLog.WriteIf(ProtocolLog.Exceptions, String.Format(
            "No Handler for packet type: {0} from: {2}\n{1} :: {3}", t, p_s, from, b.ToBase16String()));
        }
      }
      catch(Exception x) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, String.Format(
          "Packet Handling Exception"));
        string nodeSource = "null";
        if (ns != null) {
          nodeSource = ns.ToString();
        }
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, String.Format(
          "Handler: {0}\tEdge: {1}", nodeSource, from));
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, String.Format(
          "Exception: {0}", x));
      }
    }
    /**
     * This method is called when the Node should connect to the
     * network
     */
    public virtual void Connect() {
      if (Thread.CurrentThread.Name == null) {
        Thread.CurrentThread.Name = "announce_thread";
      }
      bool changed_state = false;
      try {
        SetConState(Node.ConnectionState.Joining, out changed_state);
        if( !changed_state ) {
          throw new Exception("Already called Connect");
        }
        ProtocolLog.Enable();
      }
      finally {
        if( changed_state ) {
          SendStateChange(Node.ConnectionState.Joining);
        }
      }
    }

    /**
     * Disconnect from the network.
     */
    public void Disconnect() {
      if(ProtocolLog.NodeLog.Enabled) {
        ProtocolLog.Write(ProtocolLog.NodeLog, String.Format(
          "Called Node.Disconnect: {0}", this.Address));
      }
      bool changed_state = false;
      try {
        SetConState(Node.ConnectionState.Leaving, out changed_state);
        if( changed_state ) {
          ProtocolLog.WriteIf(ProtocolLog.NodeLog, String.Format(
            "[Connect: {0}] deactivating task queue", _local_add));
          _task_queue.IsActive = false;
          Interlocked.Exchange( ref _send_pings, 0);
          _connection_table.Close();
        }
      }
      finally {
        if( changed_state ) {
          SendStateChange(Node.ConnectionState.Leaving);
        }
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
      /*
       * Make a copy so that _remote_ta never changes while
       * someone is using it
       */
      ArrayList new_remote_ta = new ArrayList();
      foreach(EdgeListener el in _edgelistener_list) {
        //Update our local list:
        el.UpdateLocalTAs(edge, reported_ta);
        el.UpdateRemoteTAs( new_remote_ta, edge, remote_ta);
      }
      UpdateRemoteTAs(new_remote_ta);
    }

    /**
     * Called by ConnectionEvent and the LocalConnectionOverlord to update
     * the remote ta list.
     */
    public void UpdateRemoteTAs(ArrayList tas_to_add)
    {
      //Make a copy so as not to mess up tas_to_add:
      ArrayList new_remote_ta = new ArrayList(tas_to_add);
      //Build a set that can be quickly checked for membership
      Hashtable new_addr = new Hashtable(new_remote_ta.Count);
      foreach(TransportAddress ta in new_remote_ta) {
        new_addr[ta] = ta;
      }
      lock( _remote_ta ) {
        //Now append all the items in _remote_ta *NOT* already present:
        foreach(TransportAddress ta in _remote_ta) {
          if( false == new_addr.ContainsKey(ta)) {
            //We should add it to the end:
            new_remote_ta.Add(ta);
          }
        }
        //Now make sure we don'tkeep too many:
        int count = new_remote_ta.Count;
        if( count > _MAX_RECORDED_TAS ) {
          int rm_count = count - _MAX_RECORDED_TAS;
          new_remote_ta.RemoveRange(_MAX_RECORDED_TAS, rm_count);
        }
        //Now fill up _remote_ta with new_remote_ta:
        _remote_ta.Clear();
        _remote_ta.AddRange(new_remote_ta);
      }
    }

    /**
     * Return a NodeInfo object for this node containing
     * at most max_local local Transport addresses
     */
    virtual public NodeInfo GetNodeInfo(int max_local) {
      return GetNodeInfo(max_local, null);
    }

    virtual public NodeInfo GetNodeInfo(int max_local, TAAuthorizer ta_auth) {
      ArrayList l = new ArrayList( this.LocalTAs );
      if(ta_auth != null) {
        ArrayList ta_authed = new ArrayList();
        foreach(TransportAddress ta in l) {
          if(ta_auth.Authorize(ta) != TAAuthorizer.Decision.Deny) {
            ta_authed.Add(ta);
          }
        }
        l = ta_authed;
      }

      if( l.Count > max_local ) {
        int rm_count = l.Count - max_local;
        l.RemoveRange( max_local, rm_count );
      }

      return NodeInfo.CreateInstance( this.Address, l);
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
      /*
       * Send the list of all neighbors of this type.
       * @todo make sure we are not sending more than
       * will fit in a single packet.
       */
      ConnectionType ct = Connection.StringToMainType( con_type_string );
      foreach(Connection c in _connection_table.GetConnections( ct ) ) {
        neighbors.Add(NodeInfo.CreateInstance(c.Address));
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
      GracefullyClose(e, String.Empty);
    }
    /**
     * @param e Edge to close
     * @param cm message to send to other node
     * This method is used if we want to use a particular CloseMessage
     * If not, we can use the method with the same name with one fewer
     * parameters
     */
    public void GracefullyClose(Edge e, string message)
    {
      /**
       * Close any connection on this edge, and
       * put the edge into the list of unconnected edges
       */
      _connection_table.Disconnect(e);
      
      ListDictionary close_info = new ListDictionary();
      string reason = message;
      if( reason != String.Empty ) {
        close_info["reason"] = reason;
      }
      ProtocolLog.WriteIf(ProtocolLog.EdgeClose, String.Format(
                          "GracefulCLose - " + e + ": " + reason));

      Channel results = new Channel();
      results.CloseAfterEnqueue();
      EventHandler close_eh = delegate(object o, EventArgs args) {
        e.Close(); 
      };
      results.CloseEvent += close_eh;
      RpcManager rpc = RpcManager.GetInstance(this);
      try {
        rpc.Invoke(e, results, "sys:link.Close", close_info);
      }
      catch { Close(e); }
    }

    /**
     * Implements the IDataHandler interface
     */
    public void HandleData(MemBlock data, ISender return_path, object state) {
      AnnounceState astate = new AnnounceState(this, data, return_path);
      EnqueueAction(astate);
    }

    protected TimeSpan ComputeDynamicTimeout() {
      TimeSpan timeout;
      //Compute the mean and stddev of LastInPacketDateTime:
      double sum = 0.0;
      double sum2 = 0.0;
      int count = 0;
      DateTime now = DateTime.UtcNow;
      foreach(Connection con in _connection_table) {
        Edge e = con.Edge;
        double this_int = (now - e.LastInPacketDateTime).TotalMilliseconds;
        sum += this_int;
        sum2 += this_int * this_int;
        count++;
      }
      /*
       * Compute the mean and std.dev:
       */
      if( count > 1 ) {
        double mean = sum / count;
        double s2 = sum2 - count * mean * mean;
        double stddev = Math.Sqrt( s2 /(count - 1) );
        double timeout_d = mean + stddev;
        ProtocolLog.WriteIf(ProtocolLog.NodeLog, String.Format(
          "Connection timeout: {0}, mean: {1} stdev: {2}", timeout_d, 
          mean, stddev));
        timeout = TimeSpan.FromMilliseconds( timeout_d );
        if( timeout > MAX_CONNECTION_TIMEOUT ) {
          timeout = MAX_CONNECTION_TIMEOUT;
        }
      }
      else {
        //Keep the old timeout.  Don't let small number statistics bias us
        timeout = _connection_timeout;
      }
      return timeout;
    }
    /**
     * Check all the edges in the ConnectionTable and see if any of them
     * need to be pinged or closed.
     * This method is connected to the heartbeat event.
     */
    virtual protected void CheckEdgesCallback(object node, EventArgs args)
    {
      DateTime now = DateTime.UtcNow;
        
      //_connection_timeout = ComputeDynamicTimeout();
      _connection_timeout = MAX_CONNECTION_TIMEOUT;
      /*
       * If we haven't heard from any of these people in this time,
       * we ping them, and if we don't get a response, we close them
       */
      RpcManager rpc = RpcManager.GetInstance(this);
      foreach(Connection c in _connection_table) {
        Edge e = c.Edge;
        TimeSpan since_last_in = now - e.LastInPacketDateTime; 
        if( (1 == _send_pings) && ( since_last_in > _connection_timeout ) ) {

          object ping_arg = String.Empty;
          DateTime start = DateTime.UtcNow;
          EventHandler on_close = delegate(object q, EventArgs cargs) {
            Channel qu = (Channel)q;
            if( qu.Count == 0 ) {
              /* we never got a response! */
              if( !e.IsClosed ) {
                //We are going to close it after waiting:
                ProtocolLog.WriteIf(ProtocolLog.NodeLog, String.Format(
	                "On an edge timeout({1}), closing connection: {0}",
                  c, DateTime.UtcNow - start));
                //Make sure it is indeed closed.
                e.Close();
              }
              else {
                //The edge was closed somewhere else, so it
                //didn't timeout.
              }
            }
            else {
              //We got a response, let's make sure it's not an exception:
              bool close = false;
              try {
                RpcResult r = (RpcResult)qu.Dequeue();
                object o = r.Result; //This will throw an exception if there was a problem
                if( !o.Equals( ping_arg ) ) {
                  //Something is wrong with the other node:
                  ProtocolLog.WriteIf(ProtocolLog.NodeLog, String.Format(
                    "Ping({0}) != {1} on {2}", ping_arg, o, c));
                  close = true;
                }
              }
              catch(Exception x) {
                ProtocolLog.WriteIf(ProtocolLog.Exceptions, String.Format(
                  "Ping on {0}: resulted in: {1}", c, x));
                close = true;
              }
              if( close ) { e.Close(); }
            }
          };
          Channel tmp_queue = new Channel(1);
          tmp_queue.CloseEvent += on_close;
          //Do the ping
          try {
            rpc.Invoke(e, tmp_queue, "sys:link.Ping", ping_arg);
          }
          catch(EdgeClosedException) {
            //Just ignore closed edges, clearly we can't ping them
          }
          catch(EdgeException x) {
            if(!x.IsTransient) {
              //Go ahead and close the Edge
              e.Close();
            }
          }
        }
      }
      foreach(Edge e in _connection_table.GetUnconnectedEdges() ) {
        if( now - e.LastInPacketDateTime > _unconnected_timeout ) {
          if(ProtocolLog.Connections.Enabled)
            ProtocolLog.Write(ProtocolLog.Connections, String.Format(
              "Closed an unconnected edge: {0}", e));
          e.Close();
        }
      }
    }

    /**
     * This just announces the data with the current node
     * as the return path
     */
    public void Send(ICopyable data) {
      MemBlock mb = data as MemBlock;
      if(mb == null) {
        mb = MemBlock.Copy(data);
      }
      this.HandleData(mb, this, null);
    }
    
    public string ToUri() {
      return "sender:localnode";
    }
  }
}
