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
using System.Diagnostics;

namespace Brunet {
  public class NodeRankComparer : System.Collections.IComparer {
    public int Compare(object x, object y) {
      NodeRankInformation x1 = (NodeRankInformation) x;
      NodeRankInformation y1 = (NodeRankInformation) y;
      if (x1.Equals(y1) && x1.Count == y1.Count) {
	Console.WriteLine("Comparer: Equality");
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
      NodeRankInformation other1 = (NodeRankInformation) other;
      if (_addr.Equals(other1.Addr)) {
	//Console.WriteLine("equal ranks");
	return true;
      }
      //Console.WriteLine("ranks not equal");
      return false;
    }
    override public string ToString() {
      return _addr.ToString() + ": " + _count + ": " + _last_retry;
    }
  }

  /** The following is what we call a ChotaConnectionOverlord.
   *  This provides high-performance routing by setting up direct
   *  structured connections between pairs of highly communicating nodes.
   *  Chota - in Hindi means small. 
   */
  public class ChotaConnectionOverlord : ConnectionOverlord {
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
    private static readonly double _retry_delay = 5.0;

    
    //hashtable of destinations. for each destination we maintain 
    //how frequently we communicate with it. Just like the LRU in virtual
    // memory context - Arijit Ganguly. 
    protected ArrayList node_rank_list;

    //maintains if bidirectional connectivity and also active linkers and connectors
    protected Hashtable _chota_connection_state;

    //ip packet handler to mark bidirectional connectivity
    protected ChotaConnectionIPPacketHandler _ip_handler;
    
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
      _ip_handler = new ChotaConnectionIPPacketHandler();
      node_rank_list = new ArrayList();

      lock( _sync ) {
	_node.ConnectionTable.ConnectionEvent +=
          new EventHandler(this.ConnectHandler); 

	// we assess trimming/growing situation on every heart beat
        _node.HeartBeatEvent += new EventHandler(this.CheckState);
	_node.SendPacketEvent += new EventHandler(this.UpdateTable);
	
	//subscribe the ip_handler to IP packets
	_node.Subscribe(AHPacket.Protocol.IP, _ip_handler);
	
	//also register the event handler
	_ip_handler.ReceivePacketEvent += new EventHandler(this.ReceivePacketHandler);
      }
#if ARI_EXP_DEBUG
      Console.WriteLine("ChotaConnectionOverlord starting : {0}", DateTime.Now);
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
	Console.WriteLine("ChotaConnectionOverlord is inactive");
#endif
	return;
      }
      //it is now that we do things with connections
      ConnectionTable tab = _node.ConnectionTable;

      //connection setup manager for the node
      ConnectionSetupManager cs_manager = _node.ConnectionSetupManager;

      NodeRankInformation to_add = null;
      Connection to_trim = null;

      lock(tab.SyncRoot) {//lock the connection table
	lock(_sync) { //lock the score table
	  int structured_count = tab.Count(ConnectionType.Structured);
	  //we assume that we are well-connected before ChotaConnections are needed. 
	  if( structured_count < 2 ) {
#if ARI_CHOTA_DEBUG
	    Console.WriteLine("Not sufficient structured connections to bootstrap Chotas.");
#endif
	    //if we do not have sufficient structured connections
	    //we do not;
	    return;
	  }
#if ARI_CHOTA_DEBUG
	  Console.WriteLine("Finding a connection to trim... ");
#endif
	  //find out the lowest score guy to trim
	  for (int i = node_rank_list.Count - 1; i >= max_chota && i > 0; i--)  
	  {
	    NodeRankInformation node_rank = (NodeRankInformation) node_rank_list[i];
	    bool trim = false;
	    foreach(Connection c in tab.GetConnections(struc_chota)) {
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
	  Console.WriteLine("Finding connections to open... ");
#endif
	  //find out the highest score  guy who need connections
	  for (int i = 0; i < node_rank_list.Count && i < max_chota; i++) 
	  {
	    //we are traversing the list in descending order of 
	    bool add = true;
	    NodeRankInformation node_rank = (NodeRankInformation) node_rank_list[i];
#if ARI_CHOTA_DEBUG
	    Console.WriteLine("Testing: {0}", node_rank);
#endif
	    if (node_rank.Count < MIN_SCORE_THRESHOLD ) {
#if ARI_CHOTA_DEBUG
	      Console.WriteLine("To poor score for a connection....");
#endif
	      //too low score to create a connection
	      continue;
	    }
	    //TimeSpan elapsed = DateTime.Now - node_rank.LastRetryInstant;
	    //if (elapsed.TotalSeconds < _retry_delay) {
	    //Console.WriteLine("To early for retry, Now = {0} and {1} < {2}", 
	    //			DateTime.Now, elapsed.TotalSeconds, _retry_delay);
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
	      Console.WriteLine("Creating a new chota connection state."); 
#endif	      
	      state = new ChotaConnectionState(node_rank.Addr);
	      _chota_connection_state[node_rank.Addr] = state;
	    }
	    if (!state.CanConnect)
	    {
#if ARI_CHOTA_DEBUG
	      Console.WriteLine("No point connecting. Active connector or no recorded bidirectionality."); 
#endif
	      continue;
	    }
	    if (cs_manager.IsActive(node_rank.Addr, struc_chota)) 
	    {
#if ARI_CHOTA_DEBUG
	      Console.WriteLine("No point connecting. Active linking is on."); 
#endif	      
	      continue;
	    }


#if ARI_CHOTA_DEBUG
	    Console.WriteLine("{0} looks good chota connection.", node_rank);
#endif
	    //make sure that this guy doesn't have any Structured or Leaf Connection already
	    foreach(Connection c in tab.GetConnections(ConnectionType.Structured)) {
	      if (node_rank.Addr.Equals(c.Address)) {
#if ARI_CHOTA_DEBUG
		Console.WriteLine("{0} already has a structured connection - {1}. ", node_rank, c.ConType);
#endif
		add = false;
		break;
	      }
	    }
	    foreach(Connection c in tab.GetConnections(ConnectionType.Leaf)) {
	      if (node_rank.Addr.Equals(c.Address)) {
#if ARI_CHOTA_DEBUG
		Console.WriteLine("{0} already has a leaf connection. ", node_rank);
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
	  Console.WriteLine("Trying to form a chota connection to addr: {0}", to_add.Addr);
#endif
	  to_add.LastRetryInstant = DateTime.Now;
	  ConnectTo(to_add.Addr, 1024, struc_chota);
	} else {
#if ARI_CHOTA_DEBUG
	  Console.WriteLine("No new connection to add... ");
#endif
	}
	//now pick some guy who can be trimmed off 
	if (to_trim != null) {
	  //lets pick the guy who possibly has the lowest score
#if ARI_CHOTA_DEBUG
	  Console.WriteLine("Trimming chota connection with addr: {0}", to_trim.Address);
#endif
	  _node.GracefullyClose(to_trim.Edge);
	} else {
#if ARI_CHOTA_DEBUG
	  Console.WriteLine("No connection to trim... ");
#endif
	}
      }
    }
    
  override public bool NeedConnection 
    {
      get {
	return true;
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
     * Everytime we the node sends a packet out this method is invoked. 
     * Since multiple invocations may exist, take care of synchronization. 
     */
    public void UpdateTable(object node, EventArgs eargs) {
      //update information in the connection table.
      SendPacketEventArgs speargs = (SendPacketEventArgs) eargs;
      AHPacket p  = speargs.Packet as AHPacket;
      if (p == null) {
	return;
      }
      if (!p.PayloadType.Equals(AHPacket.Protocol.IP)) {
	return;
      }
#if ARI_CHOTA_DEBUG
      Console.WriteLine("Receiving an IP-packet send event...");
      Console.WriteLine("IP packet: update table");
#endif
      lock(_sync) {
	NodeRankInformation node_rank = new NodeRankInformation(p.Destination);
#if ARI_CHOTA_DEBUG
	Console.WriteLine("Before, List size: {0}", node_rank_list.Count);
#endif
	int index = node_rank_list.IndexOf(node_rank);
#if ARI_CHOTA_DEBUG
	Console.WriteLine("IndexOf: {0}", index);
#endif
	if (index >= 0) {
	  node_rank = (NodeRankInformation) node_rank_list[index];
	  node_rank_list.RemoveAt(index);
#if ARI_CHOTA_DEBUG
	  Console.WriteLine("Post-removal, List size: {0}", node_rank_list.Count);
#endif
	} 
#if ARI_CHOTA_DEBUG
	Console.WriteLine("After, List size: {0}", node_rank_list.Count);
	Console.WriteLine("Pre-increment -> SendPacket: {0}", node_rank);
#endif
	int count = node_rank.Count;
	node_rank.Count = count + 1;
#if ARI_CHOTA_DEBUG
	Console.WriteLine("Post-increment -> SendPacket: {0}", node_rank);
#endif
	//find a suitable place to put this back
	index = node_rank_list.BinarySearch(node_rank, _cmp);
	if (index < 0) {
	  index = ~index;
	  node_rank_list.Insert(index, node_rank);
	  if (node_rank_list.Count > node_rank_capacity) {//we are exceeding capacity
            //trim the list
	    node_rank_list.RemoveAt(node_rank_list.Count - 1);    
	  }
	} else {
#if ARI_CHOTA_DEBUG
	  Console.WriteLine("Not supposed to happen");
#endif
	  Debug.Assert(false);
	}
      }
    }
    /**
     * On every heartbeat this method is invoked.
     * We decide which edge to trim and which one to add
     */ 
    public void CheckState(object node, EventArgs eargs) {
#if ARI_CHOTA_DEBUG
      Console.WriteLine("Receiving a heart beat event...");
      debug_counter++;
#endif
      //in this case we decrement the rank
      //update information in the connection table.
      lock(_sync) {
	IEnumerator ie = node_rank_list.GetEnumerator();
	while (ie.MoveNext()) {
	  NodeRankInformation node_rank = (NodeRankInformation) ie.Current;
#if ARI_CHOTA_DEBUG
	  Console.WriteLine("Pre-decrement -> Heartbeat: {0}", node_rank);
#endif
	  int count = node_rank.Count;
	  if (node_rank.Count > 0) {
	    node_rank.Count = count - 1;
	  }
#if ARI_CHOTA_DEBUG
	  Console.WriteLine("Post-decrement -> Heartbeat: {0}", node_rank);
#endif
	  //should also forget connectivity issues once we fall below MIN_SCORE_THRESHOLD
	  if (node_rank.Count < MIN_SCORE_THRESHOLD) {
	    if (_chota_connection_state.ContainsKey(node_rank.Addr)) {
	      ChotaConnectionState state = 
		(ChotaConnectionState) _chota_connection_state[node_rank.Addr];
	      state.Received = false;
#if ARI_CHOTA_DEBUG
	      Console.WriteLine("ChotaConnectionState -  Reverting to unidirectional: {0}", node_rank.Addr);
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
	      Console.WriteLine("ChotaConnectionState: {0} => Active Connector; Connectivity: {1}"
				, addr_key, state.Received);
	    } else {
	      Console.WriteLine("ChotaConnectionState: {0} => No connector; Connectivity: {1}"
				, addr_key, state.Received);
	    }
	  }
	}
	debug_counter = 0;
      }
#endif

#if ARI_CHOTA_DEBUG
      Console.WriteLine("Calling activate... ");
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
	Address addr_key = null;
	while(ide.MoveNext()) {
	  ChotaConnectionState state = (ChotaConnectionState) ide.Value;
	  if (state.Connector != null && state.Connector.Equals(ctr)) {
	    addr_key = (Address) ide.Key;
#if ARI_CHOTA_DEBUG
	    Console.WriteLine("ConnectorEndHandler: Connector (Chota) ended for target: {0}", 
			      addr_key);
#endif
	    //set the associated connector to null;
	    state.Connector = null;
	    break;
	  }
	}
#if ARI_CHOTA_DEBUG
	if (addr_key == null) {
	  Console.WriteLine("Finshed connector not in our records. We may have trimmed this info before.");
	}
#endif
      }
    }

    /**
     * Everytime we the node receives a packet this method is invoked. 
     * All this does is to update the ChotaConnectionState "bidirectional connectivity"
     * flag.
     */
    public void ReceivePacketHandler(object node, EventArgs eargs) {
      //update information in chota_connection_state.
      SendPacketEventArgs speargs = (SendPacketEventArgs) eargs;
      AHPacket p  = speargs.Packet as AHPacket;


#if ARI_CHOTA_DEBUG
      Console.WriteLine("Got an IP packet from src: {0} ", p.Source);
#endif

      lock(_sync) {
	if (_chota_connection_state.ContainsKey(p.Source)) {
	  ChotaConnectionState state = 
	    (ChotaConnectionState) _chota_connection_state[p.Source];
	  state.Received = true;
	}
      }
    }

    /**
     * This method is called when a new Connection is added
     * to the ConnectionTable; currently just for debugging. 
     */
    protected void ConnectHandler(object contab, EventArgs eargs)
    {
      ConnectionEventArgs args = (ConnectionEventArgs)eargs;
      Connection new_con = args.Connection;
      
#if ARI_CHOTA_DEBUG
      Console.WriteLine("Forming a connection: {0}", new_con);
#endif
#if ARI_EXP_DEBUG
      if (new_con.ConType.Equals(struc_chota)) {
	Console.WriteLine("Forming a chota connection: {0} at :{1}", new_con, DateTime.Now);
      }
#endif
    }
    /**
     * This method is called when a we disconnect
     */
    protected void DisconnectHandler(object contab, EventArgs eargs)
    {
      ConnectionEventArgs args = (ConnectionEventArgs)eargs;
      Connection new_con = args.Connection;
      
#if ARI_CHOTA_DEBUG
      Console.WriteLine("Disconnect connection: {0}", new_con);
#endif
#if ARI_EXP_DEBUG
      if (new_con.ConType.Equals(struc_chota)) {
	Console.WriteLine("Disconnect a chota connection: {0} at: {1}", new_con, DateTime.Now);
      }
#endif
    }
   
    protected void ConnectTo(Address target,
			     short t_ttl, string contype)
    {
      //If we already have a connection to this node,
      //don't try to get another one, it is a waste of 
      //time.
      ConnectionType mt = Connection.StringToMainType(contype);
      if( _node.ConnectionTable.Contains( mt, target ) ) {
#if ARI_CHOTA_DEBUG
	Console.WriteLine("Looks like we are already connected to the target: {0}"
			  , target);
#endif
        return; 
      }
      short t_hops = 0;
      ConnectToMessage ctm =
        new ConnectToMessage(contype, new NodeInfo(_node.Address, _node.LocalTAs) );
      ctm.Id = _rand.Next(1, Int32.MaxValue);
      ctm.Dir = ConnectionMessage.Direction.Request;

      AHPacket ctm_pack =
        new AHPacket(t_hops, t_ttl, _node.Address, target, AHPacket.AHOptions.Exact,
                     AHPacket.Protocol.Connection, ctm.ToByteArray());

      Connector con = new Connector(_node);
      lock( _sync ) {
	ChotaConnectionState state = null;
	if (!_chota_connection_state.ContainsKey(target)) {
	  //state = new ChotaConnectionState(target);
	  //_chota_connection_state[target] = state;
#if ARI_CHOTA_DEBUG
	  Console.WriteLine("We can't be asked to connect without ChotaConnectionState (Shouldn't have happened).");
	  return;
#endif
	} else {
	  state = (ChotaConnectionState) _chota_connection_state[target];
	}
	if (!state.CanConnect) 
	{ 
#if ARI_CHOTA_DEBUG
	  Console.WriteLine("Can't connect: Active connector or no recorded bidirectionality (Shouldn't have got here).");
	  return;
#endif	  
	}
	if (_node.ConnectionSetupManager.IsActive(target, struc_chota)) 
	{
#if ARI_CHOTA_DEBUG
	  Console.WriteLine("Active linking is on (Shouldn't have got here).");
	  return;
#endif	  
	}
	state.Connector = con;
      }
      con.FinishEvent += new EventHandler(this.ConnectorEndHandler);
      
#if ARI_CHOTA_DEBUG
      Console.WriteLine("ChotaConnectionOverlord: Starting a real chota connection attempt to: {0}", target);
#endif

#if ARI_EXP_DEBUG
      Console.WriteLine("ChotaConnectionOverlord: Starting a real chota connection attempt to: {0} at {1}", target, DateTime.Now);
#endif

      con.Connect(_node, ctm_pack, ctm.Id);
    }
  }
}
