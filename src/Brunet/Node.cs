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
using System.Threading;

using Brunet.Collections;
using Brunet.Connections;
using BCon = Brunet.Concurrent;
using Brunet.Util;
using Brunet.Transport;
using MR = Brunet.Services.MapReduce;
using Brunet.Messaging;

using Brunet.Concurrent;
using Brunet.Symphony;
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
  abstract public class Node : IDataHandler, ISender, IActionQueue, ITAHandler
  {
 // /////////////////
 // Static methods
 // /////////////////
    static Node() {
      SenderFactory.Register("localnode", CreateLocalSender); 
    }

    public static Node CreateLocalSender(object n, string uri) {
      return (Node)n;
    }
 // /////////////////
 // Constructors 
 // /////////////////
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
        _packet_queue = new BCon.LFBlockingQueue<IAction>();

        _running = 0;
        _send_pings = 1;
        _LOG = ProtocolLog.Monitor.Enabled;

        //The default is pretty good, but add fallback handling of Relay:
        var erp = DefaultERPolicy.Create(Brunet.Relay.RelayERPolicy.Instance,
                                         addr,
                                           typeof(Brunet.Transport.UdpEdge),
                                           typeof(Brunet.Transport.TcpEdge),
                                           typeof(Brunet.Relay.RelayEdge)
                                        );
        _connection_table = new ConnectionTable(erp);
        _connection_table.ConnectionEvent += this.ConnectionHandler;
        LockMgr = new ConnectionLockManager(_connection_table);

        //We start off offline.
        _con_state = Node.ConnectionState.Offline;
        
        /* Set up the ReqrepManager as a filter */
        _rrm = new ReqrepManager(this.ToString());
        DemuxHandler.GetTypeSource(PType.Protocol.ReqRep).Subscribe(_rrm, null);
        _rrm.Subscribe(this, null);
        this.HeartBeatEvent += _rrm.TimeoutChecker;
        /* Set up RPC */
        _rpc = new RpcManager(_rrm);
        DemuxHandler.GetTypeSource( PType.Protocol.Rpc ).Subscribe(_rpc, null);
        //Add a map-reduce handlers:
        _mr_handler = new MR.MapReduceHandler(this);
        //Subscribe it with the RPC handler:
        _rpc.AddHandler("mapreduce", _mr_handler);
        //Set up Fragmenting Handler:
        var fh = new FragmentingHandler(1024); //cache at most 1024 fragments
        DemuxHandler.GetTypeSource(
          FragmentingSender.FragPType ).Subscribe(fh, this);
        fh.Subscribe(this, fh);
        /*
         * Where there is a change in the Connections, we might have a state
         * change
         */
        _connection_table.ConnectionEvent += this.CheckForStateChange;
        _connection_table.DisconnectionEvent += this.CheckForStateChange;

#if !BRUNET_SIMULATOR
        _codeinjection = new Brunet.Services.CodeInjection(this);
        _codeinjection.LoadLocalModules();
#endif
        /*
         * We must later make sure the EdgeEvent events from
         * any EdgeListeners are connected to _cph.EdgeHandler
         */
        /**
         * Here are the protocols that every edge must support
         */
        /* Here are the transport addresses */
        _remote_ta = ImmutableList<TransportAddress>.Empty;
        /*@throw ArgumentNullException if the list ( new ArrayList()) is null.
         */
        /* EdgeListener's */
        _edgelistener_list = new ArrayList();
        _co_list = new List<ConnectionOverlord>();
        _edge_factory = new EdgeFactory();
        _ta_discovery = ImmutableList<Discovery>.Empty;
        StateChangeEvent += HandleTADiscoveryState;
        
        /* Initialize this at 15 seconds */
        _connection_timeout = new TimeSpan(0,0,0,0,15000);
        //Check the edges from time to time
        IAction cec_act = new HeartBeatAction(this, this.CheckEdgesCallback);
        _check_edges = Brunet.Util.FuzzyTimer.Instance.DoEvery(delegate(DateTime dt) {
          this.EnqueueAction(cec_act);
        }, 5000, 500);
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
      public void OnFuzzy(DateTime runtime) {
        //Put us into the node's queue so we run in that thread
        _n.EnqueueAction(this);
      }
   }

    protected class LogAction : IAction {
      protected readonly BCon.LFBlockingQueue<IAction> _q;
      public LogAction(BCon.LFBlockingQueue<IAction> q) {
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

// ////
// Inner types, delegates 
// ///

#if BRUNET_SIMULATOR
    public static Random SimulatorRandom = new Random();
#endif
    public delegate bool EdgeVerifier(Node node, Edge e, Address addr);

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
 
 // /////////////////
 // Immutable Member variables 
 // /////////////////

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
    
    protected readonly BCon.LFBlockingQueue<IAction> _packet_queue;
    
    //If we get this big, we just throw an exception, and not enqueue it
    protected static readonly int MAX_QUEUE_LENGTH = 8192;
    /**
     * This number should be more thoroughly tested, but my system and dht
     * never surpassed 105.
     */
    public static readonly int MAX_AVG_QUEUE_LENGTH = 4096;
    public static readonly float PACKET_QUEUE_RETAIN = 0.99f;
    
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
    
    protected readonly Util.FuzzyEvent _check_edges;

    /** Object which we lock for thread safety */
    protected readonly object _sync;

    /**  <summary>Handles subscriptions for the different types of packets
    that come to this node.</summary>*/
    public readonly DemuxHandler DemuxHandler;


    protected readonly ConnectionTable _connection_table;

    /**
     * Manages the various mappings associated with connections
     */
    public virtual ConnectionTable ConnectionTable { get { return _connection_table; } }
    public readonly ConnectionLockManager LockMgr;
    /**
     * Brunet IPHandler service!
     */
    public virtual IPHandler IPHandler { get { return null; } }
    protected Brunet.Services.CodeInjection _codeinjection;
    
    protected readonly ReqrepManager _rrm;
    public ReqrepManager Rrm { get { return _rrm; } }
    protected readonly RpcManager _rpc;
    public RpcManager Rpc { get { return _rpc; } }
    protected readonly MR.MapReduceHandler _mr_handler;
    public MR.MapReduceHandler MapReduce { get { return _mr_handler; } }
    
    protected readonly NodeTaskQueue _task_queue;
    /**
     * This is the TaskQueue for this Node
     */
    public TaskQueue TaskQueue { get { return _task_queue; } }

    protected readonly int _heart_period;
    ///how many milliseconds between heartbeats
    public int HeartPeriod { get { return _heart_period; } }
    
    protected readonly Dictionary<EventHandler, Brunet.Util.FuzzyEvent> _heartbeat_handlers;
    
    ///This is the maximum value we allow _connection_timeout to grow to
    protected static readonly TimeSpan MAX_CONNECTION_TIMEOUT = new TimeSpan(0,0,0,15,0);
    //Give edges this long to get connected, then drop them
    protected static readonly TimeSpan _unconnected_timeout = new TimeSpan(0,0,0,30,0);
    /**
     * Maximum number of TAs we keep in both for local and remote.
     * This does not control how many we send to our neighbors.
     */
    static protected readonly int _MAX_RECORDED_TAS = 10000;
    //Getting from a BooleanSwitch is strangely very expensive, do it once
    protected readonly bool _LOG;
    

 // /////////////////
 // Mutable Member variables 
 // /////////////////

    public ConnectionState ConState {
      get {
        lock( _sync ) {
          return _con_state;
        }
      }
    }
    protected ConnectionState _con_state;

    protected ImmutableList<Discovery> _ta_discovery;
    /**
     * Here are all the EdgeListener objects for this Node
     */
    protected readonly ArrayList _edgelistener_list;
    public ArrayList EdgeListenerList {
      get {
        return ArrayList.ReadOnly(_edgelistener_list);
      }
    }

    /// List of ConnectionOverlords managed by this node
    protected List<ConnectionOverlord> _co_list;

    /**
     * These are all the local TransportAddress objects that
     * refer to EdgeListener objects attached to this node.
     * This IList is ReadOnly
     */
    public IList<TransportAddress> LocalTAs {
      get {
        var local_ta = new List<TransportAddress>();
        var enums = new List<IEnumerable>();
        foreach(EdgeListener el in _edgelistener_list) {
          enums.Add( el.LocalTAs );
        }
        //Go round robin through all the LocalTAs:
        var uncast_tas = new Functional.Interleave(enums);
        //Cast the resulting IEnumerable into IEnumerable<TransportAddress>
        var all_tas = new Functional.CastEnumerable<TransportAddress>(uncast_tas);
        //Make sure we don't keep too many of these things:
        local_ta.AddRange(new Functional.Take<TransportAddress>(all_tas, _MAX_RECORDED_TAS));
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
    /** The IAction that was most recently started */
    protected IAction _current_action;
    protected float _packet_queue_exp_avg = 0.0f;

    public bool DisconnectOnOverload {
      get { return _disconnect_on_overload; }
      set { _disconnect_on_overload = value; }
    }

    public bool _disconnect_on_overload = false;

    protected ImmutableList<TransportAddress> _remote_ta;
    /**
     * These are all the remote TransportAddress objects that
     * this Node may use to connect to remote Nodes
     *
     * This can be shared between nodes or not.
     *
     * This is the ONLY proper way to set the RemoteTAs for this
     * node.
     */
    public IList<TransportAddress> RemoteTAs {
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

    /**
     * This is true if the Node is properly connected in the network.
     * If you want to know when it is safe to assume you are connected,
     * listen to all for Node.ConnectionTable.ConnectionEvent and
     * Node.ConnectionTable.DisconnectionEvent and then check
     * this property.  If it is true, you should probably wait
     * until it is false if you need the Node to be connected
     */
    public abstract bool IsConnected { get; }
    ///If we don't hear anything from a *CONNECTION* in this time, ping it.
    protected TimeSpan _connection_timeout;

// ///////////////
//  Events and Delegates
// ///////////////
    /**
     * This is used to verify new incoming edges
     * @todo this should probably be a property
     */ 
    public EdgeVerifier EdgeVerifyMethod;

    ///after each HeartPeriod, the HeartBeat event is fired
    public event EventHandler HeartBeatEvent {
      add {
        var hba = new HeartBeatAction(this, value);
        //every period +/- half a period, run this event
        var fe = Brunet.Util.FuzzyTimer.Instance.DoEvery(hba.OnFuzzy,
                                                         _heart_period,
                                                         _heart_period / 2 + 1);
        bool disconnected = false;
        lock( _sync ) {
          if(_con_state == ConnectionState.Disconnected) {
            disconnected = true;
          } else {
            _heartbeat_handlers[ value ] = fe;
          }
        }
        if(disconnected) {
          fe.TryCancel();
          throw new Exception("Node is disconnected");
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
    //add an event handler which conveys the fact that Disconnect has been called on the node
    public event EventHandler DepartureEvent;

    //add an event handler which conveys the fact that Connect has been called on the node
    public event EventHandler ArrivalEvent;
    /**
     * This event is called every time Node.ConState changes.  The new state
     * is passed with the event.
     */
    public event StateChangeHandler StateChangeEvent;

 // ///////////////////
 // Methods
 // ///////////////////
   
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

    public virtual void AddConnectionOverlord(ConnectionOverlord co)
    {
      _co_list.Add(co);
    }

    /// <summary>Add a TA discovery agent.</summary>
    public virtual void AddTADiscovery(Discovery disc)
    {
      lock(_sync) {
        _ta_discovery = _ta_discovery.PushIntoNew(disc);
      }

      HandleTADiscoveryState(this, ConState);
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


    /// <summary>If the node is connecting, we need TAs, if its in any other
    /// state, we'll deal with what we have.</summary>
    protected void HandleTADiscoveryState(Node n, ConnectionState newstate)
    {
      ImmutableList<Discovery> discs = _ta_discovery;
      if(newstate == ConnectionState.Joining ||
          newstate == ConnectionState.SeekingConnections) {
        foreach(Discovery disc in discs) {
          disc.BeginFindingTAs();
        }
      } else if(newstate == ConnectionState.Leaving ||
          newstate == ConnectionState.Disconnected)
      {
        foreach(Discovery disc in discs) {
          disc.Stop();
        }
      } else {
        foreach(Discovery disc in discs) {
          disc.EndFindingTAs();
        }
      }
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
        EnqueueAction(new Edge.CloseAction(e));
      }
      catch {
        e.Close();
      }
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
      SimpleTimer.Enqueue(a, 0, 0);
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

    protected virtual void StartConnectionOverlords() {
      foreach(ConnectionOverlord co in _co_list) {
        co.Start();
        co.Activate();
      }
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
            //Get a copy of all the heartbeat events so we can stop them:
            hbhands = new Dictionary<EventHandler, Brunet.Util.FuzzyEvent>(_heartbeat_handlers);
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

    protected virtual void StopConnectionOverlords() {
      foreach(ConnectionOverlord co in _co_list) {
        co.Stop();
      }
    }

    public override string ToString() {
      return String.Format("Node({0})", Address);
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
      var con = ce_args.Connection;
      var edge = ce_args.ConnectionState.Edge;
      con.StateChangeEvent +=
        delegate(Connection c,
                 Pair<Brunet.Connections.ConnectionState,
                      Brunet.Connections.ConnectionState> oldnew) {
          //Need to check if we we have to change state:
          this.CheckForStateChange(null, null); 
        };
      edge.Subscribe(this, edge);
      //Our peer's remote is us
      var cs = ce_args.Connection.State;
      TransportAddress reported_ta = cs.PeerLinkMessage.Remote.FirstTA;
      //Our peer's local is them
      TransportAddress remote_ta = cs.PeerLinkMessage.Local.FirstTA;
      /*
       * Make a copy so that _remote_ta never changes while
       * someone is using it
       */
      var new_remote_ta = new List<TransportAddress>();
      foreach(EdgeListener el in _edgelistener_list) {
        //Update our local list:
        el.UpdateLocalTAs(edge, reported_ta);
        el.UpdateRemoteTAs( new_remote_ta, edge, remote_ta);
      }
      UpdateRemoteTAs(new_remote_ta);
    }

    /**
     * Updates the RemoteTA list hosted by the Node.
     */
    virtual public void UpdateRemoteTAs(IList<TransportAddress> tas_to_add)
    {
      IList<TransportAddress> local_tas = LocalTAs;

      lock(_sync) {
        // Remove duplicates in tas_to_add
        var dup_test = new Dictionary<TransportAddress, bool>();
        foreach(TransportAddress ta in tas_to_add) {
          if(dup_test.ContainsKey(ta) || local_tas.Contains(ta)) {
            continue;
          }
          dup_test.Add(ta, true);
        }

        // Remove duplicates found in tas_to_add and _remote_ta.  If we don't,
        // we could be flooded by a single node and lose track of good TAs.
        foreach(TransportAddress ta in _remote_ta) {
          if(dup_test.ContainsKey(ta)) {
            dup_test.Remove(ta);
          }
        }
        
        // Add in the remaining TAs
        foreach(TransportAddress ta in dup_test.Keys) {
          _remote_ta = _remote_ta.PushIntoNew(ta);
        }

        // Remove older TAs after _MAX_RECORDED_TAS
        int count = _remote_ta.Count;

        if(count > _MAX_RECORDED_TAS) {
          for(int i = _MAX_RECORDED_TAS; i < count; i++) {
            _remote_ta = _remote_ta.RemoveAtFromNew(_MAX_RECORDED_TAS);
          }
        }
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
      var l = new List<TransportAddress>(LocalTAs);
      if(ta_auth != null) {
        var ta_authed = new List<TransportAddress>();
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
        Edge e = con.State.Edge;
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
      foreach(Connection c in _connection_table) {
        Edge e = c.State.Edge;
        TimeSpan since_last_in = now - e.LastInPacketDateTime; 
        if( (1 == _send_pings) && ( since_last_in > _connection_timeout ) ) {

          object ping_arg = String.Empty;
          DateTime start = DateTime.UtcNow;
          EventHandler on_close = delegate(object q, EventArgs cargs) {
            BCon.Channel qu = (BCon.Channel)q;
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
          BCon.Channel tmp_queue = new BCon.Channel(1);
          tmp_queue.CloseEvent += on_close;
          //Do the ping
          try {
            _rpc.Invoke(e, tmp_queue, "sys:link.Ping", ping_arg);
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
