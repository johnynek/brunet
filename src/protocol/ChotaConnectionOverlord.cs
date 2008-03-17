/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005-2006  University of Florida

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
using System.Diagnostics;

namespace Brunet {
  public class NodeRankComparer : System.Collections.IComparer {
    public int Compare(object x, object y) {
      if( x == y ) {
        //This is trivial, but we need to deal with it:
        return 0;
      }
      NodeRankInformation x1 = (NodeRankInformation) x;
      NodeRankInformation y1 = (NodeRankInformation) y;
      if (x1.Equals(y1) && x1.Count == y1.Count) {
        /*
         * Since each Address is in our list at most once,
         * this is an Error, so lets print it out and hope
         * someone sees it.
         */
        Console.Error.WriteLine("NodeRankComparer.Comparer: Equality: {0} == {1}", x1, y1);
	return 0;
      }
      if (x1.Count <= y1.Count) {
	return 1;
      }
      if (x1.Count > y1.Count) {
	return -1;
      }
      return -1;
    }
  }
  public class NodeRankInformation { 
    //address of the node
    private Address _addr;
    //rank - score is a better name though
    private int _count;

     
    
    //when was the last retry made
    private DateTime _last_retry = DateTime.MinValue;

    //constructor
    public NodeRankInformation(Address addr) {
      _addr = addr;
      _count = 0;
    }
    public int Count {
      get {
	return _count;
      }
      set {
	_count = value;
      }
    }
    public DateTime LastRetryInstant {
      get {
        return _last_retry;
      }
      set {
       _last_retry = value; 
      }
    }
    public Address Addr {
      get {
	return _addr;
      }
    }

    override public bool Equals(Object other ) {
      if( Object.ReferenceEquals(other, this) ) { return true; }
      NodeRankInformation other1 =  other as NodeRankInformation;
      if( Object.ReferenceEquals(other1, null) ) { return false; }
      if (_addr.Equals(other1.Addr)) {
	//Console.Error.WriteLine("equal ranks");
	return true;
      }
      //Console.Error.WriteLine("ranks not equal");
      return false;
    }
    override public int GetHashCode() {
      return _addr.GetHashCode();
    }
    override public string ToString() {
      return _addr.ToString() + ": " + _count + ": " + _last_retry;
    }
  }


/** The class maintains the state of prospective ChotaConnections. 
 *  This is used by ChotaConnectionOverlord to decide if we should make
 *  connection attempt.
 */

  public class ChotaConnectionState {
    /** target we are keeping state about. */
    protected Address _target;

    /** connector associated with the state. */
    protected Connector _con = null;
    
    //boolean flag indicating we got a packet back from the node
    //we initiate ChotaConnections only if we observe bidirectional connectivity
    private bool _received;

    public bool Received {
      get {
	return _received;
      }
      set {
#if ARI_CHOTA_DEBUG
	if (!_received && value) {
	  Console.Error.WriteLine("ChotaConnectionState: Recording bidirectional connectivity");
	}
#endif
	_received = value;
      }
    }

    
    public Address Address {
      get {
	return _target;
      }
    }

    /** default constructor. */
    public ChotaConnectionState(Address target) {
      _target = target;
    }
 
    /** whether we should make a connection attempt. 
     *  only when there are no active connectors. 
     */
    public bool CanConnect {
      get {
#if ARI_CHOTA_DEBUG
	if (_con != null) {
	  Console.Error.WriteLine("ChotaConnectionState:  Active connector exists. (Don't reattempt)");
	}
	if (!_received) {
	  Console.Error.WriteLine("ChotaConnectionState:  No bidirectional connectivity (Don't reattempt)");
	}
#endif
	if (_con == null && _received) {
	  return true;
	}
	return false;
      }
    }
    /** ChotaConnectionOverlord just created a new connector. 
     *  We keep its state here.
     */
    public Connector Connector {
      set {
	_con = value;
      } 
      get {
	return _con;
      }
    }
  }

  /** The following is what we call a ChotaConnectionOverlord.
   *  This provides high-performance routing by setting up direct
   *  structured connections between pairs of highly communicating nodes.
   *  Chota - in Hindi means small. 
   */
  public class ChotaConnectionOverlord : ConnectionOverlord, IDataHandler {
    //the node we are attached to
    protected Node _node;

    //used for locking
    protected object _sync;
    //our random number generator
    protected Random _rand;

    //if the overlord is active
    protected bool _active;
    
    //minimum score before we start forming chota connections
    private static readonly int MIN_SCORE_THRESHOLD = 5;

    //the maximum number of Chota connections we plan to support
    private static readonly int max_chota = 200;
    
    //maximum number of entries in the node_rank table
    private static readonly int node_rank_capacity = max_chota;

    //retry interval for Chota connections
    //private static readonly double _retry_delay = 5.0;

    
    //hashtable of destinations. for each destination we maintain 
    //how frequently we communicate with it. Just like the LRU in virtual
    // memory context - Arijit Ganguly. 
    protected ArrayList node_rank_list;
    /*
     * Allows us to quickly look up the node rank for a destination
     */
    protected Hashtable _dest_to_node_rank;

    //maintains if bidirectional connectivity and also active linkers and connectors
    protected Hashtable _chota_connection_state;

    //node rank comparer
    protected NodeRankComparer _cmp;
    
    /*
     * We don't want to risk mistyping these strings.
     */
    static protected readonly string struc_chota = "structured.chota";

#if ARI_CHOTA_DEBUG
    protected int debug_counter = 0;
#endif
    
    
    public ChotaConnectionOverlord(Node n)
    {
      _node = n;
      _cmp = new NodeRankComparer();
      _sync = new object();
      _rand = new Random();
      _chota_connection_state = new Hashtable();
      node_rank_list = new ArrayList();
      _dest_to_node_rank = new Hashtable();

      lock( _sync ) {
	_node.ConnectionTable.ConnectionEvent += this.ConnectHandler; 

	// we assess trimming/growing situation on every heart beat
        _node.HeartBeatEvent += this.CheckState;
        //_node.SubscribeToSends(AHPacket.Protocol.IP, this);
	//subscribe the ip_handler to IP packets
        ISource source = _node.GetTypeSource(PType.Protocol.IP);
        source.Subscribe(this, AHPacket.Protocol.IP);
      }
#if ARI_EXP_DEBUG
      Console.Error.WriteLine("ChotaConnectionOverlord starting : {0}", DateTime.UtcNow);
#endif
      
    }
    /**
     * On every activation, the ChotaConnectionOverlord trims any connections
     * that are unused, and also creates any new connections of needed
     * 
     */
    override public void Activate() {
      if (!IsActive) {
#if ARI_CHOTA_DEBUG || ARI_EXP_DEBUG
	Console.Error.WriteLine("ChotaConnectionOverlord is inactive");
#endif
	return;
      }

      NodeRankInformation to_add = null;
      Connection to_trim = null;
       
      ConnectionTable tab = _node.ConnectionTable;
      IEnumerable chota_cons = tab.GetConnections(struc_chota);
      ConnectionList struct_cons = tab.GetConnections(ConnectionType.Structured);
      ConnectionList leaf_cons = tab.GetConnections(ConnectionType.Leaf);
      int structured_count = struct_cons.Count;
      //we assume that we are well-connected before ChotaConnections are needed. 
      if( structured_count < 2 ) {
#if ARI_CHOTA_DEBUG
        Console.Error.WriteLine("Not sufficient structured connections to bootstrap Chotas.");
#endif
        //if we do not have sufficient structured connections
        //we do not;
        return;
      }

	lock(_sync) { //lock the score table
#if ARI_CHOTA_DEBUG
	  Console.Error.WriteLine("Finding a connection to trim... ");
#endif
	  //find out the lowest score guy to trim
          SortTable();
	  for (int i = node_rank_list.Count - 1; i >= max_chota && i > 0; i--)  
	  {
	    NodeRankInformation node_rank = (NodeRankInformation) node_rank_list[i];
	    bool trim = false;
	    foreach(Connection c in chota_cons) {
	      if (node_rank.Addr.Equals(c.Address)) {
		to_trim = c;
		trim = true;
		break;
	      }
	      if (trim) {
		break;
	      }
	    }
	  }
#if ARI_CHOTA_DEBUG
	  Console.Error.WriteLine("Finding connections to open... ");
#endif
	  //find out the highest score  guy who need connections
	  for (int i = 0; i < node_rank_list.Count && i < max_chota; i++) 
	  {
	    //we are traversing the list in descending order of 
	    bool add = true;
	    NodeRankInformation node_rank = (NodeRankInformation) node_rank_list[i];
#if ARI_CHOTA_DEBUG
	    Console.Error.WriteLine("Testing: {0}", node_rank);
#endif
	    if (node_rank.Count < MIN_SCORE_THRESHOLD ) {
#if ARI_CHOTA_DEBUG
	      Console.Error.WriteLine("To poor score for a connection....");
#endif
	      //too low score to create a connection
	      continue;
	    }
	    //TimeSpan elapsed = DateTime.UtcNow - node_rank.LastRetryInstant;
	    //if (elapsed.TotalSeconds < _retry_delay) {
	    //Console.Error.WriteLine("To early for retry, Now = {0} and {1} < {2}", 
	    //			DateTime.UtcNow, elapsed.TotalSeconds, _retry_delay);
	      //wait for some time before sending a connection request again
	      //continue;
	    //}
	    //check if there is an active connector/linker or lacking bidirectional 
	    //connectivity

	    ChotaConnectionState state = null;
	    if (_chota_connection_state.ContainsKey(node_rank.Addr)) {
	      state = (ChotaConnectionState) _chota_connection_state[node_rank.Addr];
	    } else {
#if ARI_CHOTA_DEBUG
	      Console.Error.WriteLine("Creating a new chota connection state."); 
#endif	      
	      state = new ChotaConnectionState(node_rank.Addr);
	      _chota_connection_state[node_rank.Addr] = state;
	    }
	    if (!state.CanConnect)
	    {
#if ARI_CHOTA_DEBUG
	      Console.Error.WriteLine("No point connecting. Active connector or no recorded bidirectionality."); 
#endif
	      continue;
	    }

#if ARI_CHOTA_DEBUG
	    Console.Error.WriteLine("{0} looks good chota connection.", node_rank);
#endif
	    //make sure that this guy doesn't have any Structured or Leaf Connection already
	    foreach(Connection c in struct_cons) {
	      if (node_rank.Addr.Equals(c.Address)) {
#if ARI_CHOTA_DEBUG
		Console.Error.WriteLine("{0} already has a structured connection - {1}. ", node_rank, c.ConType);
#endif
		add = false;
		break;
	      }
	    }
	    foreach(Connection c in leaf_cons) {
	      if (node_rank.Addr.Equals(c.Address)) {
#if ARI_CHOTA_DEBUG
		Console.Error.WriteLine("{0} already has a leaf connection. ", node_rank);
#endif
		add = false;
		break;
	      }
	    }
	    if (add) {
	      to_add = node_rank;
	      break;
	    }
	  }
	}
	
	//connection to add
	if (to_add != null) {
	  //the first connection would have the highest score
#if ARI_CHOTA_DEBUG
	  Console.Error.WriteLine("Trying to form a chota connection to addr: {0}", to_add.Addr);
#endif
	  to_add.LastRetryInstant = DateTime.UtcNow;
	  ConnectTo(to_add.Addr, 1024, struc_chota);
	} else {
#if ARI_CHOTA_DEBUG
	  Console.Error.WriteLine("No new connection to add... ");
#endif
	}
	//now pick some guy who can be trimmed off 
	if (to_trim != null) {
	  //lets pick the guy who possibly has the lowest score
#if ARI_CHOTA_DEBUG
	  Console.Error.WriteLine("Trimming chota connection with addr: {0}", to_trim.Address);
#endif
	  _node.GracefullyClose(to_trim.Edge);
	} else {
#if ARI_CHOTA_DEBUG
	  Console.Error.WriteLine("No connection to trim... ");
#endif
	}
    }
    
    override public bool NeedConnection 
    {
      get {
	return true;
      } 
    }
    /**
     * @return true if we have sufficient connections for functionality
     */
    override public bool IsConnected
    {
      get {
        throw new Exception("Not implemented! Chota connection overlord (IsConnected)");
      }
    }
    public override bool IsActive 
    {
      get {
	return _active;
      }
      set {
	_active = value;
      }
    }
    /**
     * When we get ConnectToMessage responses the connector tells us.
     */
    override public bool HandleCtmResponse(Connector c, ISender ret_path,
                                           ConnectToMessage ctm_resp)
    {
      /**
       * Time to start linking:
       */

      Linker l = new Linker(_node, ctm_resp.Target.Address,
                            ctm_resp.Target.Transports,
                            ctm_resp.ConnectionType,
                            ctm_resp.Token);
      _node.TaskQueue.Enqueue( l );
      //We only want to connect to one node, so we are done connecting now:
      return true;
    }
    /**
     * We count incoming IP packets here
     */
    public void HandleData(MemBlock p, ISender from, object state) {
      AHSender ahs = from as AHSender;
      if( ahs != null ) {
        Address dest = ahs.Destination;
        UpdateTable(dest, p);
        //This is an incoming packet
        ///@todo fix this
#if ARI_CHOTA_DEBUG
        Console.Error.WriteLine("Got an IP packet from src: {0} ", dest);
#endif
        //Getting from a Hashtable is threadsafe... no need to lock
        ChotaConnectionState ccs = (ChotaConnectionState) _chota_connection_state[dest];
        if ( ccs != null ) {
	  ccs.Received = true;
        }
      }
    }
    /**
     * Everytime we the node sends a packet out this method is invoked. 
     * Since multiple invocations may exist, take care of synchronization. 
     */
    public void UpdateTable(Address a, MemBlock p) {
    /*
     * We know the following conditions are never true because
     * we are only subscribed to IP packets, and the Node will
     * not send null packets
      
      if (p == null) {
	return;
      }
      if (!p.PayloadType.Equals(AHPacket.Protocol.IP)) {
	return;
      }
      */
#if ARI_CHOTA_DEBUG
      Console.Error.WriteLine("Receiving an IP-packet send event...");
      Console.Error.WriteLine("IP packet: update table");
#endif
      /*
       * We don't need to keep a perfectly accurate count.
       * As an optimization, we could just sample:
       */
      if( _rand.Next(4) != 0 ) {
        return;
      }
      lock(_sync) {
        /*
         * We have to lock here to make the following an atomic
         * operation, otherwise we could leave this table inconsistent
         */
        NodeRankInformation node_rank =
          (NodeRankInformation)_dest_to_node_rank[a];
        if( node_rank == null ) {
          //This is a new guy:
	  node_rank = new NodeRankInformation(a);
          node_rank_list.Add( node_rank );
          _dest_to_node_rank[a] = node_rank;
        }
        //Since we only update once every fourth time, go ahead
        //and bump the count by 4 each time, so the count represents
        //the expected number of packets we have sent.
        node_rank.Count = node_rank.Count + 4;
        //There, we have updated the node_rank
      }
    }
    /**
     * We only need to do this before we take action based on the
     * table, in the mean time, it can get disordered
     */
    protected void SortTable() {
      lock( _sync ) {
        //Keep the table sorted according to _cmp
        node_rank_list.Sort( _cmp );
	if (node_rank_list.Count > node_rank_capacity) {
          //we are exceeding capacity
          //trim the list
          int rmv_idx = node_rank_list.Count - 1;
          NodeRankInformation nr = (NodeRankInformation)node_rank_list[ rmv_idx ];
	  node_rank_list.RemoveAt(rmv_idx);    
          _dest_to_node_rank.Remove( nr.Addr );
	}
      }
    }
    /**
     * On every heartbeat this method is invoked.
     * We decide which edge to trim and which one to add
     */ 
    public void CheckState(object node, EventArgs eargs) {
#if ARI_CHOTA_DEBUG
      Console.Error.WriteLine("Receiving a heart beat event...");
      debug_counter++;
#endif
      //in this case we decrement the rank
      //update information in the connection table.
      lock(_sync) {
        SortTable();
        foreach(NodeRankInformation node_rank in node_rank_list) {
#if ARI_CHOTA_DEBUG
	  Console.Error.WriteLine("Pre-decrement -> Heartbeat: {0}", node_rank);
#endif
	  int count = node_rank.Count;
	  if (count > 0) {
	    node_rank.Count = count - 1;
	  }
#if ARI_CHOTA_DEBUG
	  Console.Error.WriteLine("Post-decrement -> Heartbeat: {0}", node_rank);
#endif
	  //should also forget connectivity issues once we fall below MIN_SCORE_THRESHOLD
	  if (node_rank.Count < MIN_SCORE_THRESHOLD) {
	    if (_chota_connection_state.ContainsKey(node_rank.Addr)) {
	      ChotaConnectionState state = 
		(ChotaConnectionState) _chota_connection_state[node_rank.Addr];
	      state.Received = false;
#if ARI_CHOTA_DEBUG
	      Console.Error.WriteLine("ChotaConnectionState -  Reverting to unidirectional: {0}", node_rank.Addr);
#endif
	    }
	  }
	}
      }

#if ARI_CHOTA_DEBUG
      //periodically print out ChotaConnectionState as we know
      if (debug_counter >= 5) {
	lock(_sync) {
	  IDictionaryEnumerator ide = _chota_connection_state.GetEnumerator();
	  while(ide.MoveNext()) {
	    ChotaConnectionState state = (ChotaConnectionState) ide.Value;
	    Address addr_key = (Address) ide.Key;
	    if (state.Connector != null) {
	      Console.Error.WriteLine("ChotaConnectionState: {0} => Active Connector; Connectivity: {1}"
				, addr_key, state.Received);
	    } else {
	      Console.Error.WriteLine("ChotaConnectionState: {0} => No connector; Connectivity: {1}"
				, addr_key, state.Received);
	    }
	  }
	}
	debug_counter = 0;
      }
#endif

#if ARI_CHOTA_DEBUG
      Console.Error.WriteLine("Calling activate... ");
#endif
      //everything fine now take a look at connections
      //let us see which connections are to trim
      Activate();
    }

    /**
     * When a Connector finishes his job, this method is called to
     * clean up the connector but at the same time;
     * we record all unfinished linkers and subscribe to finish events.
     * This ensures we do not make a connection attempt in the meanwhile.
     */
    protected void ConnectorEndHandler(object connector, EventArgs args)
    {
      lock( _sync ) {
	Connector ctr = (Connector)connector;
	//we do not need to lock the connector; since it is already over
	IDictionaryEnumerator ide = _chota_connection_state.GetEnumerator();
#if ARI_CHOTA_DEBUG
	Address addr_key = null;
#endif
	while(ide.MoveNext()) {
	  ChotaConnectionState state = (ChotaConnectionState) ide.Value;
	  if (state.Connector != null && state.Connector.Equals(ctr)) {
#if ARI_CHOTA_DEBUG
	    addr_key = (Address) ide.Key;
	    Console.Error.WriteLine("ConnectorEndHandler: Connector (Chota) ended for target: {0}", 
			      addr_key);
#endif
	    //set the associated connector to null;
	    state.Connector = null;
	    break;
	  }
	}
#if ARI_CHOTA_DEBUG
	if (addr_key == null) {
	  Console.Error.WriteLine("Finshed connector not in our records. We may have trimmed this info before.");
	}
#endif
      }
    }

    /**
     * This method is called when a new Connection is added
     * to the ConnectionTable; currently just for debugging. 
     */
    protected void ConnectHandler(object contab, EventArgs eargs)
    {
#if ARI_CHOTA_DEBUG
      Connection new_con1 = ((ConnectionEventArgs)eargs).Connection; 
      Console.Error.WriteLine("Forming a connection: {0}", new_con1);
#endif
#if ARI_EXP_DEBUG
      Connection new_con2 = ((ConnectionEventArgs)eargs).Connection; 
      if (new_con2.ConType.Equals(struc_chota)) {
	Console.Error.WriteLine("Forming a chota connection: {0} at :{1}",
	                  new_con2, DateTime.UtcNow);
      }
#endif
    }
    /**
     * This method is called when a we disconnect
     */
    protected void DisconnectHandler(object contab, EventArgs eargs)
    {
      
#if ARI_CHOTA_DEBUG
      Connection new_con1 = ((ConnectionEventArgs)eargs).Connection;
      Console.Error.WriteLine("Disconnect connection: {0}", new_con1);
#endif
#if ARI_EXP_DEBUG
      Connection new_con2 = ((ConnectionEventArgs)eargs).Connection;
      if (new_con2.ConType.Equals(struc_chota)) {
	Console.Error.WriteLine("Disconnect a chota connection: {0} at: {1}",
	                  new_con2, DateTime.UtcNow);
      }
#endif
    }
   
    protected void ConnectTo(Address target,
			     short t_ttl, string contype)
    {
      ConnectionType mt = Connection.StringToMainType(contype);
      /*
       * This is an anonymous delegate which is called before
       * the Connector starts.  If it returns true, the Connector
       * will finish immediately without sending an ConnectToMessage
       */
      Linker l = new Linker(_node, target, null, contype, _node.Address.ToString());
      object link_task = l.Task;
      Connector.AbortCheck abort = delegate(Connector c) {
        bool stop = _node.ConnectionTable.Contains( mt, target );
        if (!stop ) {
            /*
             * Make a linker to get the task.  We won't use
             * this linker.
             * No need in sending a ConnectToMessage if we
             * already have a linker going.
             */
          stop = _node.TaskQueue.HasTask( link_task );
        }
        return stop;
      };
      if ( abort(null) ) {
#if ARI_CHOTA_DEBUG
	Console.Error.WriteLine("Looks like we are already connected to the target: {0}"
			  , target);
#endif
        return;
      }
      //Send the 4 neighbors closest to this node:
      ArrayList nearest = _node.ConnectionTable.GetNearestTo(
							 (AHAddress)_node.Address, 4);
      NodeInfo[] near_ni = new NodeInfo[nearest.Count];
      int i = 0;
      foreach(Connection cons in nearest) {
	near_ni[i] = NodeInfo.CreateInstance(cons.Address, cons.Edge.RemoteTA);
	i++;
      }

      ConnectToMessage ctm =
        new ConnectToMessage(contype, _node.GetNodeInfo(8), near_ni, _node.Address.ToString());

      ISender send = new AHSender(_node, target, AHPacket.AHOptions.Exact);
      Connector con = new Connector(_node, send, ctm, this);
      lock( _sync ) {
	ChotaConnectionState state = null;
	if (!_chota_connection_state.ContainsKey(target)) {
	  //state = new ChotaConnectionState(target);
	  //_chota_connection_state[target] = state;
#if ARI_CHOTA_DEBUG
	  Console.Error.WriteLine("We can't be asked to connect without ChotaConnectionState (Shouldn't have happened).");
	  return;
#endif
	} else {
	  state = (ChotaConnectionState) _chota_connection_state[target];
	}
	if (!state.CanConnect) 
	{ 
#if ARI_CHOTA_DEBUG
	  Console.Error.WriteLine("Can't connect: Active connector or no recorded bidirectionality (Shouldn't have got here).");
	  return;
#endif	  
	}
	state.Connector = con;
      }
      con.FinishEvent += new EventHandler(this.ConnectorEndHandler);
      
#if ARI_CHOTA_DEBUG
      Console.Error.WriteLine("ChotaConnectionOverlord: Starting a real chota connection attempt to: {0}", target);
#endif

#if ARI_EXP_DEBUG
      Console.Error.WriteLine("ChotaConnectionOverlord: Starting a real chota connection attempt to: {0} at {1}", target, DateTime.UtcNow);
#endif

      //Start work on connecting
      con.AbortIf = abort;
      _node.TaskQueue.Enqueue( con );
    }
  }
}
