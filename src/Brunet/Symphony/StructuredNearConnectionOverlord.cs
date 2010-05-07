/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

//#define POB_DEBUG

using System;
using System.Threading;
using System.Collections;
using Brunet.Util;
using Brunet.Connections;
using Brunet.Messaging;
using Brunet.Transport;

namespace Brunet.Symphony {

  /**
   * This is an attempt to write a simple version of
   * StructuredNearConnectionOverlord which is currently quite complex,
   * difficult to understand, and difficult to debug.
   */
  public class StructuredNearConnectionOverlord : Brunet.Connections.ConnectionOverlord {
    
    public StructuredNearConnectionOverlord(Node n)
    {
      _sync = new Object();
      lock( _sync ) {
        _node = n;
        _rand = new Random();
        _connectors = new Hashtable();
        _last_connection_time = DateTime.UtcNow;
      /**
       * Every heartbeat we assess the trimming situation.
       * If we have excess edges and it has been more than
       * _trim_wait_time heartbeats then we trim.
       */
        _last_retry_time = DateTime.UtcNow;
        _current_retry_interval = _DEFAULT_RETRY_INTERVAL;

        /**
         * Information related to the target selector feature.
         * Includes statistics such as trim rate and connection lifetimes.
         */
        
        _start_time = DateTime.UtcNow;
        
        /*
         * Register event handlers after everything else is set
         */
        //Listen for connection events:
        _node.ConnectionTable.DisconnectionEvent += DisconnectHandler;
        _node.ConnectionTable.ConnectionEvent += ConnectHandler;
        _node.ConnectionTable.StatusChangedEvent += StatusChangedHandler;
        
        _node.HeartBeatEvent += CheckState;
      }
    }

    ///////  Attributes /////////////////

    protected readonly Random _rand;

    protected int _active;
    //We use this to make sure we don't trim connections
    //too fast.  We want to only trim in the "steady state"
    protected DateTime _last_connection_time;
    protected object _sync;

    protected readonly Hashtable _connectors;
    
    protected TimeSpan _current_retry_interval;
    protected DateTime _last_retry_time;

    /** Checks logging is enabled. */
    protected int _log_enabled = -1;
    protected bool LogEnabled {
      get {
        lock(_sync) {
          if (_log_enabled == -1) {
            _log_enabled = ProtocolLog.SCO.Enabled ? 1 : 0;
          }
          return (_log_enabled == 1);
        }
      }
    }
    
    protected readonly DateTime _start_time;

    /*
     * In between connections or disconnections there is no
     * need to recompute whether we need connections.
     * So after each connection or disconnection, this becomes
     * false.
     *
     * This is just an optimization, however, running many nodes
     * on one computer seems to benefit from this optimization
     * (reducing cpu usage and thus the likely of timeouts).
     */
    protected int _need_left;
    protected int _need_right;

    /*
     * These are parameters of the Overlord.  These govern
     * the way it reacts and works.
     */
    
    ///How many neighbors do we want (same value for left and right)
    static public readonly int DESIRED_NEIGHBORS = 2;
    ///How many seconds to wait between connections/disconnections to trim
    static protected readonly double TRIM_DELAY = 30.0;
    ///By default, we only wake up every 10 seconds, but we back off exponentially
    static protected readonly TimeSpan _DEFAULT_RETRY_INTERVAL = TimeSpan.FromSeconds(10);
    static protected readonly TimeSpan _MAX_RETRY_INTERVAL = TimeSpan.FromSeconds(60);
    /*
     * We don't want to risk mistyping these strings.
     */
    static protected readonly string STRUC_NEAR = "structured.near";
    
    /// If we are active, we check to see if we need to make new neighbors
    override public bool IsActive
    {
      get { return 1 ==_active; }
      set { Interlocked.Exchange(ref _active, value ? 1 : 0); }
    }    
    
    override public bool NeedConnection 
    {
      get {
        int structs = _node.ConnectionTable.Count(ConnectionType.Structured);
        if( structs < (2 * DESIRED_NEIGHBORS) ) {
          //We don't have enough connections for what we need:
          return true;
        }
        else {
          //The total is enough, but we may be missing some edges
          return NeedLeftNeighbor || NeedRightNeighbor;
        }
      }
    }

    /**
     * @return true if we have sufficient connections for functionality
     */
    override public bool IsConnected
    {
      get {
        AHAddress our_addr = _node.Address as AHAddress;
        Connection lc = null; 
        Connection rc = null;
          
        ConnectionList structs =
            _node.ConnectionTable.GetConnections(ConnectionType.Structured);
        try {
          lc = structs.GetLeftNeighborOf(our_addr);
          rc = structs.GetRightNeighborOf(our_addr);
        }
        catch(Exception) { }
        
        if (rc == null || lc == null) {
          if(LogEnabled) {
            ProtocolLog.Write(ProtocolLog.SCO, String.Format(
              "{0}: No left or right neighbor (false)", our_addr));
          }
          return false;
        }
        if (rc == lc) {
            /*
             * In this case, we only have one neighbor.
             * If the network only has two nodes, us and the other
             * guy, we might be stuck in this case.
             * We will consider ourselves connected, if the other
             * guy has no neighbors other than us
             */
            bool only_us = true;
            foreach(NodeInfo ni in rc.Status.Neighbors) {
              only_us = only_us && ni.Address.Equals(our_addr);
            }
            return only_us;
        }
        else {
          //now make sure things are good about Status Messages
          AHAddress left_addr = lc.Address as AHAddress;
          AHAddress right_addr = rc.Address as AHAddress;
          
          //we have to make sure than nothing is between us and left
          foreach (NodeInfo n_info in lc.Status.Neighbors) {
            AHAddress stat_addr = n_info.Address as AHAddress;
            if (stat_addr.IsBetweenFromLeft(our_addr, left_addr)) {
              //we are expecting a better candidate for left neighbor!
              if(LogEnabled)
                ProtocolLog.Write(ProtocolLog.SCO, String.Format(
                  "{0}: Better left: {1} (false)", our_addr, stat_addr));
              return false;
            }
          }
          
          //we have to make sure than nothing is between us and left
          foreach (NodeInfo n_info in rc.Status.Neighbors) {
            AHAddress stat_addr = n_info.Address as AHAddress;
            if (stat_addr.IsBetweenFromRight(our_addr, right_addr)) {
              //we are expecting a better candidate for left neighbor!
              if(LogEnabled)
                ProtocolLog.Write(ProtocolLog.SCO, String.Format(
                  "{0}: Better right: {1} (false)", our_addr, stat_addr));
              return false;
            }
          }
          if(LogEnabled)
            ProtocolLog.Write(ProtocolLog.SCO, String.Format(
              "{0}:  Returning (true)", our_addr));
          return true;
        }
      }
    }
    
    /**
     * We know a left neighbor from a right neighbor because it is closer
     * on the left hand side.  Strictly speaking, this is not necessarily true
     * but for networks greated than size 10 it is very likely:
     *
     * The probabililty that there are less than k neighbors on one half of the ring:
     * \frac{1}{2^N} \sum_{i=0}^{k-1} {N \choose k}
     * for k=2: (1+N)/(2^N)
     * For N=10, the probability that a node is in this boat is already ~1/100.  For
     * N ~ 100 this will never happen in the life of the universe.
     */

    /**
     * 
     * @returns true if we have too few left neighbor connections
     */
    protected bool NeedLeftNeighbor {
      get {
        int left = 0;
        lock( _sync ) {
          if( _need_left != -1 ) {
            return (_need_left == 1);
          }
          ConnectionTable tab = _node.ConnectionTable;
          ConnectionList cl = tab.GetConnections(ConnectionType.Structured);
          int left_pos;
          foreach(Connection c in cl) {
            left_pos = cl.LeftInclusiveCount(_node.Address, c.Address);
#if POB_DEBUG
            AHAddress local = (AHAddress)_node.Address;
            Console.Error.WriteLine( "{0} -> {1}, lidx: {2}, is_left: {3}",
            _node.Address, adr, left_pos, adr.IsLeftOf( local ) );
#endif
            if( left_pos < DESIRED_NEIGHBORS ) {
              //This is left neighbor:
              left++; 
            }
          }
#if POB_DEBUG
          Console.Error.WriteLine("{0} left: {1}" , _node.Address, left);
#endif
          if( left < DESIRED_NEIGHBORS ) {
            _need_left = 1;
            return true;
          } 
          else {
            _need_left = 0;
            return false;
          }
        }
      }
    }

    /**
     * @returns true if we have too few right neighbor connections
     */
    protected bool NeedRightNeighbor {
      get {
        int right = 0;
        lock( _sync ) {
          if( _need_right != -1 ) {
            return (_need_right == 1);
          }
          ConnectionTable tab = _node.ConnectionTable;
          ConnectionList cl = tab.GetConnections(ConnectionType.Structured);
          //foreach(Connection c in _node.ConnectionTable.GetConnections(STRUC_NEAR)) {
          int rp;
          foreach(Connection c in cl) {
            rp = cl.RightInclusiveCount(_node.Address, c.Address);
#if POB_DEBUG
            AHAddress local = (AHAddress)_node.Address;
            Console.Error.WriteLine("{0} -> {1}, ridx: {2}, is_right: {3}",
                            _node.Address, adr, rp, adr.IsRightOf( local) );
#endif
            if( rp < DESIRED_NEIGHBORS ) {
              //This is right neighbor:
              right++; 
            }
          }
          if( right < DESIRED_NEIGHBORS ) {
            _need_right = 1;
            return true;
          } 
          else {
            _need_right = 0;
            return false;
          }
        }
      }
    }
    
    public override TAAuthorizer TAAuth { get { return _ta_auth;} }
    protected readonly static TAAuthorizer _ta_auth = new TATypeAuthorizer(
          new TransportAddress.TAType[]{TransportAddress.TAType.Subring},
          TAAuthorizer.Decision.Deny,
          TAAuthorizer.Decision.None);

    ///////////////// Methods //////////////////////
    
    /**
     * Starts the Overlord if we are active
     *
     * This method is called by the CheckState method
     * IF we have not seen any connections in a while
     * AND we still need some connections
     *
     */
    public override void Activate()
    {
#if POB_DEBUG
      Console.Error.WriteLine("In Activate: {0}", _node.Address);
#endif
      if( IsActive == false ) {
        return;
      }

      DateTime now = DateTime.UtcNow;
      lock( _sync ) {
        if( now - _last_retry_time < _current_retry_interval ) {
          //Not time yet...
          return;
        }
        _last_retry_time = now;
        //Double the length of time we wait (resets to default on connections)
        _current_retry_interval += _current_retry_interval;
        _current_retry_interval = (_MAX_RETRY_INTERVAL < _current_retry_interval) ?
            _MAX_RETRY_INTERVAL : _current_retry_interval;
      }

      ConnectionTable tab = _node.ConnectionTable;
      //If we are going to connect to someone, this is how we
      //know who to use
      Address target = null;
      string contype = String.Empty;
      ISender sender = null;
      int desired_ctms = 1;
      
      ConnectionList structs = tab.GetConnections(ConnectionType.Structured);
      if( structs.Count < 2 ) {
        ConnectionList leafs = tab.GetConnections(ConnectionType.Leaf);
        if( leafs.Count == 0 )
        {
          /*
           * We first need to get a Leaf connection
           */
          return;
        }
        //We don't have enough connections to guarantee a connected
        //graph.  Use a leaf connection to get another connection
        Connection leaf = null;
        //Make sure the following loop can't go on forever
        int attempts = 2 * leafs.Count;
        do {
          leaf = leafs[ _rand.Next( leafs.Count ) ];
          attempts--;
        }
        while( leafs.Count > 1 && structs.Contains( leaf.Address ) &&
               attempts > 0 );
        //Now we have a random leaf that is not a
        //structured neighbor to try to get a new neighbor with:
        if( leaf != null ) {
          target = GetSelfTarget();
          /*
           * This is the case of trying to find the nodes nearest
           * to ourselves, use the Annealing routing to get connected
           * more quickly
           */
          sender = new ForwardingSender(_node, leaf.Address, target);
          //We are trying to connect to the two nearest nodes in one
          //one attempt, so wait for two distinct responses:
          desired_ctms = 2;
          //This is a near neighbor connection
          contype = STRUC_NEAR;
        }
      }
      
      if( structs.Count > 0 && sender == null ) {
        /**
         * We need left or right neighbors we send
         * a ConnectToMessage in the directons we
         * need.
         */
        if( NeedLeftNeighbor ) {
#if POB_DEBUG
          Console.Error.WriteLine("NeedLeftNeighbor: {0}", _node.Address);
#endif
          target = new DirectionalAddress(DirectionalAddress.Direction.Left);
          short ttl = (short)DESIRED_NEIGHBORS;
          sender = new AHSender(_node, target, ttl, AHHeader.Options.Last);
          contype = STRUC_NEAR;
        } else if( NeedRightNeighbor ) {
#if POB_DEBUG
          Console.Error.WriteLine("NeedRightNeighbor: {0}", _node.Address);
#endif
          target = new DirectionalAddress(DirectionalAddress.Direction.Right);
          short ttl = (short)DESIRED_NEIGHBORS;
          sender = new AHSender(_node, target, ttl, AHHeader.Options.Last);
          contype = STRUC_NEAR;
        }
      }

      if( sender != null ) {
        ConnectTo(sender, target, contype, desired_ctms);
      }
    }

    /**
     * Every heartbeat we take a look to see if we should trim
     *
     * We only trim one at a time.
     */
    protected void CheckState(object node, EventArgs eargs)
    {
      lock( _sync ) {
        if( IsActive == false ) {
          //If we are not active, we do not care what
          //our state is.
          return;
        }
        TimeSpan elapsed = DateTime.UtcNow - _last_connection_time;
        if( elapsed.TotalSeconds < TRIM_DELAY ) {
          return;
        }
      }

      TrimConnections();

      if( NeedConnection ) {
        //Wake back up and try to get some
        Activate();
      }
    }

    /**
     * This method is called when a new Connection is added
     * to the ConnectionTable
     */
    protected void ConnectHandler(object contab, EventArgs eargs)
    {
      lock( _sync ) {
        _last_connection_time = DateTime.UtcNow;
        _current_retry_interval = _DEFAULT_RETRY_INTERVAL;
        _need_left = -1;
        _need_right = -1;
      }

      if( IsActive == false ) {
        return;
      }

      ConnectionEventArgs args = (ConnectionEventArgs)eargs;
      Connection new_con = args.Connection;
      ConnectionList structs = null;

      if( new_con.MainType == ConnectionType.Structured ) {
        structs = args.CList;
      } else {
        if( new_con.MainType == ConnectionType.Leaf ) {
          /*
           * We just got a leaf.  Try to use it to get a shortcut.near
           * This leaf could be connecting a new part of the network
           * to us.  We try to connect to ourselves to make sure
           * the network is connected:
           */
          Address target = GetSelfTarget();
          //This is a near neighbor connection
          ISender send = new ForwardingSender(_node, new_con.Address, target);
          //Try to connect to the two nearest to us:
          ConnectTo(send, target, STRUC_NEAR, 2);
        }
        structs = _node.ConnectionTable.GetConnections(ConnectionType.Structured);
      }
      ConnectToNearer(structs, new_con.Address, new_con.Status.Neighbors);
    }

    
    /*
     * Check to see if any of this node's neighbors
     * should be neighbors of us.  If they should, connect
     * to the closest such nodes on each side.
     * 
     * @param structs a ConnectionList of ConnectionType.Structured
     * @param neighbors an IEnumerable of NodeInfo objects
     * @param nltarget an address of a node that should be on our left
     * @param nrtarget an address of a node that should be on our right
     */
    protected void CheckForNearerNeighbors(ConnectionList structs,
        IEnumerable neighbors, out Address nltarget, out Address nrtarget)
    {

      BigInteger ldist = null;
      BigInteger rdist = null;
      nltarget = null;
      nrtarget = null;
      AHAddress local = (AHAddress)_node.Address;
      foreach(NodeInfo ni in neighbors) {
        if( !( _node.Address.Equals(ni.Address) ||
               structs.Contains(ni.Address) ) ) {
          AHAddress adr = (AHAddress)ni.Address;
          int n_left = structs.LeftInclusiveCount(_node.Address, adr);
          int n_right = structs.RightInclusiveCount(_node.Address, adr);
          if( n_left < DESIRED_NEIGHBORS || n_right < DESIRED_NEIGHBORS ) {
            //We should connect to this node! if we are not already:
            BigInteger adr_dist = local.LeftDistanceTo(adr);
            if( ( null == ldist ) || adr_dist < ldist ) {
              ldist = adr_dist;
              nltarget = adr;
            }
            adr_dist = local.RightDistanceTo(adr);
            if( ( null == rdist ) || adr_dist < rdist ) {
              rdist = adr_dist;
              nrtarget = adr;
            }
          }
        }
      }
    }
    
    /**
     * @param structs the ConnectionList to work with
     * @param target_to_for a mapping of Address -> Address, if we want to
     * connect to the key, the value should be the forwarder
     * @param neighs an IEnumerable of NodeInfo objects.
     */
    protected void ConnectToNearer(ConnectionList structs, 
                                   IDictionary target_to_for, IEnumerable neighs) {
      Address nltarget;
      Address nrtarget;
      CheckForNearerNeighbors(structs, neighs, out nltarget, out nrtarget);
      
      if( nrtarget != null ) {
        Address forwarder = (Address)target_to_for[nrtarget];
        ISender send = new ForwardingSender(_node, forwarder, nrtarget);
        ConnectTo(send, nrtarget, STRUC_NEAR, 1);
      }

      if( nltarget != null && !nltarget.Equals(nrtarget) ) {
        Address forwarder = (Address)target_to_for[nltarget];
        ISender send = new ForwardingSender(_node, forwarder, nltarget);
        ConnectTo(send, nltarget, STRUC_NEAR, 1);
      }
    }
                                   
    /**
     * Similar to the above except the forwarder is the same for all targets
     * @param cl ConnectionList of structs
     * @param forwarder the Node to forward through
     * @param ni an IEnumerable of NodeInfo objects representing neighbors
     * forwarder
     */
    protected void ConnectToNearer(ConnectionList cl, Address forwarder, IEnumerable ni)
    {
      Address nltarget;
      Address nrtarget;
      CheckForNearerNeighbors(cl, ni, out nltarget, out nrtarget);
      
      if( nrtarget != null ) {
        ISender send = new ForwardingSender(_node, forwarder, nrtarget);
        ConnectTo(send, nrtarget, STRUC_NEAR, 1);
      }

      if( nltarget != null && !nltarget.Equals(nrtarget) ) {
        ISender send = new ForwardingSender(_node, forwarder, nltarget);
        ConnectTo(send, nltarget, STRUC_NEAR, 1);
      }
    }

    /**
     * Initiates connection setup. 
     * @param sender the ISender for the Connector to use
     * @param contype the type of connection we want to make
     * @param token the token used for connection messages
     * @param responses the maximum number of ctm response messages to listen
     */
    protected void ConnectTo(ISender sender, Address target, string contype,
        int responses)
    {
      Connector con = GetConnector(sender, target, contype, _node.Address.ToString());
      if(con == null) {
        return;
      }

      lock( _sync ) {
        _connectors[con] = responses;
      }
      _node.TaskQueue.Enqueue(con);
    }

    /// We want to include our 4 nearest neighbors
    override protected ConnectToMessage GetConnectToMessage(string ConnectionType,
        string token)
    {
      ArrayList nearest = _node.ConnectionTable.GetNearestTo( (AHAddress)_node.Address, 4);
      NodeInfo[] near_ni = new NodeInfo[nearest.Count];
      int i = 0;
      foreach(Connection cons in nearest) {
        //We don't use the TAs, just the addresses
        near_ni[i] = NodeInfo.CreateInstance(cons.Address);
        i++;
      }
      return new ConnectToMessage(ConnectionType, _node.GetNodeInfo(12, TAAuth), near_ni, token);
    }

    /**
     * When a Connector finishes his job, this method is called to
     * clean up
     */
    override protected void ConnectorEndHandler(object connector, EventArgs args)
    {
      lock( _sync ) {
        _connectors.Remove(connector);
      }
      /**
       * Take a look at see if there is some node we should connect to.
       */
      Connector ctr = (Connector)connector;
      
      ArrayList neighs = new ArrayList();
      Hashtable t_to_f = new Hashtable();
      foreach(ConnectToMessage ctm in ctr.ReceivedCTMs) {
        if( ctm.Neighbors != null ) {
          foreach(NodeInfo n in ctm.Neighbors) {
            t_to_f[n.Address] = ctm.Target.Address;
          }
          neighs.AddRange( ctm.Neighbors );
        }
      }
      ConnectionList structs =
          _node.ConnectionTable.GetConnections(ConnectionType.Structured);
      ConnectToNearer(structs, t_to_f, neighs);
    }

    
    /**
     * This method is called when there is a Disconnection from
     * the ConnectionTable
     */
    protected void DisconnectHandler(object connectiontable, EventArgs args)
    { 
      ConnectionEventArgs ceargs = (ConnectionEventArgs)args;
      Connection c = ceargs.Connection;


      lock( _sync ) {
        _last_connection_time = DateTime.UtcNow;
        _need_left = -1;
        _need_right = -1;
        _current_retry_interval = _DEFAULT_RETRY_INTERVAL;
      }

      if( !IsActive ) {
        return;
      }


      if( c.MainType != ConnectionType.Structured ) {
        //Just activate and see what happens:
        Activate();
        return;
      }

      ConnectionList cl = ceargs.CList;
      int right_pos = cl.RightInclusiveCount(_node.Address, c.Address);
      if( right_pos < DESIRED_NEIGHBORS ) {
        //We lost a close friend.
        Address target = new DirectionalAddress(DirectionalAddress.Direction.Right);
        short ttl = (short)DESIRED_NEIGHBORS;
        string contype = STRUC_NEAR;
        ISender send = new AHSender(_node, target, ttl, AHHeader.Options.Last);
        ConnectTo(send, target, contype, 1);
      }

      int left_pos = cl.LeftInclusiveCount(_node.Address, c.Address);
      if( left_pos < DESIRED_NEIGHBORS ) {
        //We lost a close friend.
        Address target = new DirectionalAddress(DirectionalAddress.Direction.Left);
        short ttl = (short)DESIRED_NEIGHBORS;
        string contype = STRUC_NEAR;
        ISender send = new AHSender(_node, target, ttl, AHHeader.Options.Last);
        ConnectTo(send, target, contype, 1);
      }
    }
    
    /**
     * When we get ConnectToMessage responses the connector tells us.
     */
    override public bool HandleCtmResponse(Connector c, ISender ret_path,
                                           ConnectToMessage ctm_resp)
    {
      base.HandleCtmResponse(c, ret_path, ctm_resp);
      /**
       * Check this guys neighbors:
       */
      //See if we want more:
      bool got_enough = true;
      object des_o = _connectors[c];
      if( des_o != null ) {
        got_enough = (c.ReceivedCTMs.Count >= (int)des_o);
      }
      return got_enough;
    }

    /**
     * This method is called when there is a change in a Connection's status
     */
    protected void StatusChangedHandler(object connectiontable,EventArgs args)
    {
      //Console.Error.WriteLine("Status Changed:\n{0}\n{1}\n{2}\n{3}",c, c.Status, nltarget, nrtarget);
      ConnectionEventArgs ceargs = (ConnectionEventArgs)args;
      Connection c = ceargs.Connection; 
      ConnectionList structs;
      if( c.MainType == ConnectionType.Structured ) {
        structs = ceargs.CList; 
      }
      else {
        structs = _node.ConnectionTable.GetConnections(ConnectionType.Structured);
      }
      ConnectToNearer(structs, c.Address, c.Status.Neighbors);
    }
    
    /**
     * When we want to connect to the address closest
     * to us, we use this address.
     */
    protected Address GetSelfTarget()
    {
      /**
       * try to get at least one neighbor using forwarding through the 
       * leaf .  The forwarded address is 2 larger than the address of
       * the new node that is getting connected.
       */
      BigInteger local_int_add = _node.Address.ToBigInteger();
      //must have even addresses so increment twice
      local_int_add += 2;
      //Make sure we don't overflow:
      BigInteger tbi = new BigInteger(local_int_add % Address.Full);
      return new AHAddress(tbi);
    }

    /// Determine if there are any unuseful STRUC_NEAR that we can trim
    protected void TrimConnections() {
      ConnectionTable tab = _node.ConnectionTable;
      ConnectionList cons = tab.GetConnections(ConnectionType.Structured);
      ArrayList trim_candidates = new ArrayList();
      foreach(Connection c in cons) {
        if(!c.ConType.Equals(STRUC_NEAR)) {
          continue;
        }

        int left_pos = cons.LeftInclusiveCount(_node.Address, c.Address);
        int right_pos = cons.RightInclusiveCount(_node.Address, c.Address);

        if( right_pos >= 2 * DESIRED_NEIGHBORS && left_pos >= 2 * DESIRED_NEIGHBORS ) {
          //These are near neighbors that are not so near
          trim_candidates.Add(c);
        }
      }

      if(trim_candidates.Count == 0) {
        return;
      }

      //Delete a farthest trim candidate:
      BigInteger biggest_distance = new BigInteger(0);
      BigInteger tmp_distance = new BigInteger(0);
      Connection to_trim = null;
      foreach(Connection tc in trim_candidates ) 
      {
        AHAddress t_ah_add = (AHAddress)tc.Address;
        tmp_distance = t_ah_add.DistanceTo( (AHAddress)_node.Address).abs();
        if (tmp_distance > biggest_distance) {
          biggest_distance = tmp_distance;
          //Console.Error.WriteLine("...finding far distance for trim: {0}",biggest_distance.ToString() );
          to_trim = tc;
        }
      }

#if POB_DEBUG
        Console.Error.WriteLine("Attempt to trim Near: {0}", to_trim);
#endif
      _node.GracefullyClose( to_trim.Edge, "SCO, near connection trim" );
    }
  }
}
