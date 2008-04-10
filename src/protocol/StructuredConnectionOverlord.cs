/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

//#define POB_DEBUG
#define TRIM
//#define PERIODIC_NEIGHBOR_CHK

/*
 * When new near neighbors show up, should we ALWAYS send a directional
 * ConnectTo message to their neighbors.
 */
//#define SEND_DIRECTIONAL_TO_NEAR

using System;
using System.Threading;
using System.Collections;

namespace Brunet {

  /**
   * This is an attempt to write a simple version of
   * StructuredConnectionOverlord which is currently quite complex,
   * difficult to understand, and difficult to debug.
   */
  public class StructuredConnectionOverlord : ConnectionOverlord {
    
    public StructuredConnectionOverlord(Node n)
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
        
        _target_selector = new DefaultTargetSelector();
        _last_optimize_time = DateTime.UtcNow;
        _sum_con_lifetime = 0.0;
        _start_time = DateTime.UtcNow;
        _trim_count = 0;

        
        /*
         * Register event handlers after everything else is set
         */
        //Listen for connection events:
        _node.ConnectionTable.DisconnectionEvent +=
          new EventHandler(this.DisconnectHandler);
        _node.ConnectionTable.ConnectionEvent +=
          new EventHandler(this.ConnectHandler);     
        _node.ConnectionTable.StatusChangedEvent += 
          new EventHandler(this.StatusChangedHandler);
        
        _node.HeartBeatEvent += new EventHandler(this.CheckState);
        _node.HeartBeatEvent += new EventHandler(this.CheckConnectionOptimality);
      }
    }

    ///////  Attributes /////////////////

    protected readonly Node _node;
    protected readonly Random _rand;

    protected int _compensate;
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
    
    //When we last tried to optimize shortcut.
    protected DateTime _last_optimize_time;
    public static readonly int OPTIMIZE_DELAY = 300;//300 seconds

    //keep some statistics, this will help understand the rate at which coordinates change
    protected double _sum_con_lifetime;
    public double MeanConLifetime {
      get {
        lock(_sync) {
          return _trim_count > 0 ? _sum_con_lifetime/_trim_count : 0.0;
        }
      }
    }

    protected readonly DateTime _start_time;
    protected int _trim_count;
    public double TrimRate {
      get {
        lock(_sync) {
          return ((double) _trim_count)/(DateTime.UtcNow - _start_time).TotalSeconds;
        }
      }      
    }

    //Keeps track of connections that have got a benefit of doubt
    protected Hashtable _doubts_table = new Hashtable();
    protected static readonly int MAX_DOUBT_BENEFITS = 2; 

    //optimizer class for shortcuts.
    protected TargetSelector _target_selector;
    public TargetSelector TargetSelector {
      set {
        lock(_sync) {
          _target_selector = value;
        }
      }
    }

    /*
     * In between connections or disconnections there is no
     * need to recompute whether we need connections.
     * So after each connection or disconnection, this becomes
     * false.
     *
     * These are -1 when we don't know 0 is false, 1 is true
     *
     * This is just an optimization, however, running many nodes
     * on one computer seems to benefit from this optimization
     * (reducing cpu usage and thus the likely of timeouts).
     */
    protected int _need_left;
    protected int _need_right;
    protected int _need_short;
    protected int _need_bypass = -1;

    /*
     * These are parameters of the Overlord.  These govern
     * the way it reacts and works.
     */
    
    ///How many neighbors do we want (same value for left and right)
    static protected readonly int DESIRED_NEIGHBORS = 2;
    ///How many seconds to wait between connections/disconnections to trim
    static protected readonly double TRIM_DELAY = 30.0;
    ///By default, we only wake up every 10 seconds, but we back off exponentially
    static protected readonly TimeSpan _DEFAULT_RETRY_INTERVAL = TimeSpan.FromSeconds(10);
    static protected readonly TimeSpan _MAX_RETRY_INTERVAL = TimeSpan.FromSeconds(60);
    /*
     * We don't want to risk mistyping these strings.
     */
    static protected readonly string STRUC_NEAR = "structured.near";
    static protected readonly string STRUC_SHORT = "structured.shortcut";

    /** this is a connection we keep to the physically closest of our logN ring neighbors. */
    static protected readonly string STRUC_BYPASS = "structured.bypass";
    
    /**
     * If we start compensating, we check to see if we need to
     * make new neighbor or shortcut connections : 
     */
    override public bool IsActive
    {
      get { return 1 ==_compensate; }
      set { Interlocked.Exchange(ref _compensate, value ? 1 : 0); }
    }    

    public int DesiredShortcuts {
      get {
        int desired_sc = 1;
        /** 
         * for networks smaller than 10 nodes, more than 1 shortcuts might be superfluous.
         */
        if( _node.NetworkSize > 10 ) {
          //0.5*logN
          desired_sc = (int) Math.Ceiling(0.5*Math.Log(_node.NetworkSize)/Math.Log(2.0));
        }
        return desired_sc;
      }
    }
    
    override public bool NeedConnection 
    {
      get {
        int structs = _node.ConnectionTable.Count(ConnectionType.Structured);
        if( structs < (2 * DESIRED_NEIGHBORS + DesiredShortcuts) ) {
          //We don't have enough connections for what we need:
          return true;
        }
        else {
          //The total is enough, but we may be missing some edges
          return NeedShortcut || NeedLeftNeighbor || NeedRightNeighbor;
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
    
    /**
     * @returns true if we have too few right shortcut connections
     */
    protected bool NeedShortcut {
      get {
        lock( _sync ) {
          if( _node.NetworkSize < 10 ) {
            //There is no need to bother with shortcuts on small networks
            return false;
          }
          if( _need_short != -1 ) {
            return (_need_short == 1);
          }
          int shortcuts = 0;
          ConnectionList cl =
            _node.ConnectionTable.GetConnections(ConnectionType.Structured);
          foreach(Connection c in cl) {
            if(c.ConType == STRUC_SHORT ) {
              int left_pos = cl.LeftInclusiveCount(_node.Address, c.Address);
              int right_pos = cl.RightInclusiveCount(_node.Address, c.Address);
              
              if( left_pos >= DESIRED_NEIGHBORS &&
                  right_pos >= DESIRED_NEIGHBORS ) {
              /*
               * No matter what they say, don't count them
               * as a shortcut if they are one a close neighbor
               */
                shortcuts++;
              }
            }
          }
          if( shortcuts < DesiredShortcuts ) {
            _need_short = 1;
            return true;
          } 
          else {
            _need_short = 0;
            return false;
          }
        }
      }
    }

    public bool NeedBypass {
      get {
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO, 
                            String.Format("Checking if need bypass"));
        }
        lock(_sync) {
          if (_need_bypass != -1) {
            if (LogEnabled) {
              ProtocolLog.Write(ProtocolLog.SCO, 
                                String.Format("Returning: {0}.", (_need_bypass == 1)));
            }
            return (_need_bypass == 1);
          }
          ConnectionList cl =
            _node.ConnectionTable.GetConnections(ConnectionType.Structured);
          bool found = false;
          foreach(Connection c in cl) {
            if (c.ConType == STRUC_BYPASS) {
              //make sure that we also initiated its creation
              //string initiator_address = c.PeerLinkMessage.Token; 
              //if (initiator_address.Equals(_node.Address)) {
              found = true;
              break;
              //}
            }
          }

          if (!found) {
            if (LogEnabled) {
              ProtocolLog.Write(ProtocolLog.SCO, String.Format("Returning: true."));
            }
            _need_bypass = 1;
            return true;
          } 
          else {
            if (LogEnabled) {
              ProtocolLog.Write(ProtocolLog.SCO, String.Format("Returning: false."));
            }
            _need_bypass = 0;
            return false;
          }
        }
      }
    }
    
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
        _current_retry_interval = _current_retry_interval + _current_retry_interval;
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
      else {
        //We have two or more structured connections
      }
      
      if( structs.Count > 0 && sender == null ) {
          //We have enough structured connections to ignore the leafs
          
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
            sender = new AHSender(_node, target, ttl, AHPacket.AHOptions.Last);
            contype = STRUC_NEAR;
          }
          else if( NeedRightNeighbor ) {
#if POB_DEBUG
      Console.Error.WriteLine("NeedRightNeighbor: {0}", _node.Address);
#endif
            target = new DirectionalAddress(DirectionalAddress.Direction.Right);
            short ttl = (short)DESIRED_NEIGHBORS;
            sender = new AHSender(_node, target, ttl, AHPacket.AHOptions.Last);
            contype = STRUC_NEAR;
          }
          else if( NeedShortcut ) {
          /*
           * If we are trying to get near connections it
           * is not smart to try to get a shortcut.  We
           * need to make sure we are on the proper place in
           * the ring before doing the below:
           */
#if POB_DEBUG
      Console.Error.WriteLine("NeedShortcut: {0}", _node.Address);
#endif
            CreateShortcut();
          }
          else if (NeedBypass) {
            CreateBypass();
          }
      }

      if( sender != null ) {
        ConnectTo(sender, contype, desired_ctms);
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
      /*
       * Check to see if we there are neighbors of
       * neighbors we are not connected to:
       */
      PeriodicNeighborCheck();
      /*
       * Trim excess connections
       */
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
        _need_short = -1;
        _need_bypass = -1;
      }
      if( IsActive == false ) {
        return;
      }
      ConnectionEventArgs args = (ConnectionEventArgs)eargs;
      Connection new_con = args.Connection;
#if SEND_DIRECTIONAL_TO_NEAR
      bool connect_left = false;
      bool connect_right = false;
#endif
      
      ConnectionList structs = null;
      if( new_con.MainType == ConnectionType.Leaf ) {
        /*
         * We just got a leaf.  Try to use it to get a shortcut.near
         * This leaf could be connecting a new part of the network
         * to us.  We try to connect to ourselves to make sure
         * the network is connected:
         */
        Address target = GetSelfTarget();
        //This is a near neighbor connection
        string contype = STRUC_NEAR;
        ISender send = new ForwardingSender(_node, new_con.Address, target);
        //Try to connect to the two nearest to us:
        ConnectTo(send, contype, 2);
      }
      else if( new_con.MainType == ConnectionType.Structured ) {
#if SEND_DIRECTIONAL_TO_NEAR
        int left_pos = cl.LeftInclusiveCount(_node.Address, new_con.Address);
        int right_pos = cl.RightInclusiveCount(_node.Address, new_con.Address);

        if( left_pos < DESIRED_NEIGHBORS ) {
        /*
         * This is a new left neighbor.  Always
         * connect to the right of a left neighbor,
         * this will make sure we are connected to
         * our local neighborhood.
         */
          connect_right = true;
          if( left_pos < DESIRED_NEIGHBORS - 1) { 
            /*
             * Don't connect to the left of our most
             * left neighbor.  If this is not our
             * most left neighbor, make sure we are
             * connected to his left
             */
            connect_left = true;
          }
        }
        if( right_pos < DESIRED_NEIGHBORS ) {
        /*
         * This is a new right neighbor.  Always
         * connect to the left of a right neighbor,
         * this will make sure we are connected to
         * our local neighborhood.
         */
          connect_left = true;
          if( right_pos < DESIRED_NEIGHBORS - 1) { 
            /*
             * Don't connect to the right of our most
             * right neighbor.  If this is not our
             * most right neighbor, make sure we are
             * connected to his right
             */
            connect_right = true;
          }
        }
        
        if( left_pos >= DESIRED_NEIGHBORS && right_pos >= DESIRED_NEIGHBORS ) {
        //This looks like a shortcut
          
        }
#endif
        structs = args.CList;
      }//Done handling the structured connection case
      /*
       * Now see if we need to connect to any of the neighbors of this guy
       */
      if( structs == null ) {
        structs = _node.ConnectionTable.GetConnections(ConnectionType.Structured);
      }
      ConnectToNearer(structs, new_con.Address, new_con.Status.Neighbors);
      
      //We also send directional messages.  In the future we may find this
      //to be unnecessary
      ///@todo evaluate the performance impact of this:
#if SEND_DIRECTIONAL_TO_NEAR
      if( nrtarget == null || nltarget == null ) {
      /**
       * Once we find nodes for which we can't get any closer, we
       * make sure we are connected to the right and left of that node.
       *
       * When we connect to a neighbor's neighbor with directional addresses
       * we need TTL = 2.  1 to get to the neighbor, 2 to get to the neighbor's
       * neighbor.
       */
        short nn_ttl = 2;
        if( connect_right ) {
          ConnectToOnEdge(new DirectionalAddress(DirectionalAddress.Direction.Right),
                        new_con.Edge, nn_ttl, STRUC_NEAR); 
        }
        if( connect_left ) {
          ConnectToOnEdge(new DirectionalAddress(DirectionalAddress.Direction.Left),
                        new_con.Edge, nn_ttl, STRUC_NEAR); 
        }
      }
#endif
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
     * When a Connector finishes his job, this method is called to
     * clean up
     */
    protected void ConnectorEndHandler(object connector, EventArgs args)
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
        ConnectTo(send, STRUC_NEAR);
      }
      if( nltarget != null && !nltarget.Equals(nrtarget) ) {
        Address forwarder = (Address)target_to_for[nltarget];
        ISender send = new ForwardingSender(_node, forwarder, nrtarget);
        ConnectTo(send, STRUC_NEAR);
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
        ConnectTo(send, STRUC_NEAR);
      }
      if( nltarget != null && !nltarget.Equals(nrtarget) ) {
        ISender send = new ForwardingSender(_node, forwarder, nltarget);
        ConnectTo(send, STRUC_NEAR);
      }
    }


    /**
     * Initiates connection setup.
     * @param sender the ISender for the Connector to use
     * @param contype the type of connection we want to make
     */    
    protected void ConnectTo(ISender sender, string contype) {
      ConnectTo(sender, contype, 1);
    }

    /**
     * Initiates connection setup. The default token is the current node address. 
     * @param sender the ISender for the Connector to use
     * @param contype the type of connection we want to make
     * @param responses the maximum number of ctm response messages to listen
     */    
    protected void ConnectTo(ISender sender, string contype, int responses) {
      ConnectTo(sender, contype, _node.Address.ToString(), responses);
    }

    /**
     * Initiates connection setup. 
     * @param sender the ISender for the Connector to use
     * @param contype the type of connection we want to make
     * @param token the token used for connection messages
     * @param responses the maximum number of ctm response messages to listen
     */
    protected void ConnectTo(ISender sender, string contype, string token, int responses)
    {
      ConnectionType mt = Connection.StringToMainType(contype);
      /*
       * This is an anonymous delegate which is called before
       * the Connector starts.  If it returns true, the Connector
       * will finish immediately without sending an ConnectToMessage
       */
      Connector.AbortCheck abort = null;
      ForwardingSender fs = sender as ForwardingSender;
      if( fs != null ) {
        //In general, we only know the exact node we are trying
        //to reach when we are using a ForwardingSender
        Address target = fs.Destination;
        Linker l = new Linker(_node, target, null, contype, token);
        object linker_task = l.Task;  //This is what we check for
        abort = delegate(Connector c) {
          bool stop = _node.ConnectionTable.Contains( mt, target );
          if (!stop ) {
              /*
               * Make a linker to get the task.  We won't use
               * this linker.
               * No need in sending a ConnectToMessage if we
               * already have a linker going.
               */
            stop = _node.TaskQueue.HasTask( linker_task );
          }
          return stop;
        };
        if ( abort(null) ) {
          return;
        }
      }
      //Send the 4 neighbors closest to this node:
      ArrayList nearest = _node.ConnectionTable.GetNearestTo( (AHAddress)_node.Address, 4);
      NodeInfo[] near_ni = new NodeInfo[nearest.Count];
      int i = 0;
      foreach(Connection cons in nearest) {
        //We don't use the TAs, just the addresses
        near_ni[i] = NodeInfo.CreateInstance(cons.Address);
        i++;
      }
      ConnectToMessage ctm = new ConnectToMessage(contype, _node.GetNodeInfo(8), near_ni, token);

      Connector con = new Connector(_node, sender, ctm, this);
      con.AbortIf = abort;
      //Keep a reference to it does not go out of scope
      lock( _sync ) {
        _connectors[con] = responses;
      }
      con.FinishEvent += new EventHandler(this.ConnectorEndHandler);
      //Start up this Task:
      _node.TaskQueue.Enqueue(con);
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
        _need_short = -1;
        _need_bypass = -1;
        _current_retry_interval = _DEFAULT_RETRY_INTERVAL;
        _doubts_table.Remove(c.Address);
      }

      if( IsActive ) {
        if( c.MainType == ConnectionType.Structured ) {
          ConnectionList cl = ceargs.CList;
          int left_pos = cl.LeftInclusiveCount(_node.Address, c.Address);
          int right_pos = cl.RightInclusiveCount(_node.Address, c.Address);
          if( right_pos < DESIRED_NEIGHBORS ) {
            //We lost a close friend.
            Address target = new DirectionalAddress(DirectionalAddress.Direction.Right);
            short ttl = (short)DESIRED_NEIGHBORS;
            string contype = STRUC_NEAR;
            ISender send = new AHSender(_node, target, ttl, AHPacket.AHOptions.Last);
            ConnectTo(send, contype);
          }
          if( left_pos < DESIRED_NEIGHBORS ) {
            //We lost a close friend.
            Address target = new DirectionalAddress(DirectionalAddress.Direction.Left);
            short ttl = (short)DESIRED_NEIGHBORS;
            string contype = STRUC_NEAR;
            ISender send = new AHSender(_node, target, ttl, AHPacket.AHOptions.Last);
            ConnectTo(send, contype);
          }
          if( c.ConType == STRUC_SHORT ) {
            if( NeedShortcut ) {
              CreateShortcut();
            }
          }
          if (c.ConType == STRUC_BYPASS) {
            if (NeedBypass) {
              CreateBypass();
            }
          }
        }
        else {
          //Just activate and see what happens:
          Activate();
        }
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
      /**
       * Check this guys neighbors:
       */
      /* POB: I don't think this is needed because we also do the 
       * same thing in the ConnectorEndHandler, so why do this twice?
       * In fact, it seems better to wait for all the responses before
       * looking for the closest one
       *
       * commenting this out:
       *
      ConnectionList structs =
          _node.ConnectionTable.GetConnections(ConnectionType.Structured);
      ConnectToNearer(structs, ctm_resp.Target.Address, ctm_resp.Neighbors);
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

    /**
     * Initiates shortcut connection creation to a random shortcut target with the
     * correct distance distribution.
     */
    protected void CreateShortcut()
    {
#if false
      //The old code:
      // Random distance from 2^1 - 2^159 (1/d distributed)
      int rand_exponent = _rand.Next(1, 159);
      BigInteger rand_dist = new BigInteger(2);
      rand_dist <<= (rand_exponent - 1);
#else
      /*
       * If there are k nodes out of a total possible
       * number of N ( =2^(160) ), the average distance
       * between them is d_ave = N/k.  So we want to select a distance
       * that is at least N/k from us.  We want to do this
       * with prob(dist = d) ~ 1/d.  We can do this by selecting
       * a uniformly distributed p, and sample:
       * 
       * d = d_ave(d_max/d_ave)^p
       *   = d_ave( 2^(p log d_max - p log d_ave) )
       *   = 2^( p log d_max + (1 - p) log d_ave )
       *  
       * since we can go all the way around the ring d_max = N
       * and: log d_ave = log N - log k, but k is the size of the network:
       * 
       * d = 2^( p log N + (1 - p) log N - (1-p) log k)
       *   = 2^( log N - (1-p)log k)
       * 
       */
      double logN = (double)(Address.MemSize * 8);
      double logk = Math.Log( (double)_node.NetworkSize, 2.0 );
      double p = _rand.NextDouble();
      double ex = logN - (1.0 - p)*logk;
      int ex_i = (int)Math.Floor(ex);
      double ex_f = ex - Math.Floor(ex);
      //Make sure 2^(ex_long+1)  will fit in a long:
      int ex_long = ex_i % 63;
      int ex_big = ex_i - ex_long;
      ulong dist_long = (ulong)Math.Pow(2.0, ex_long + ex_f);
      //This is 2^(ex_big):
      BigInteger big_one = 1;
      BigInteger dist_big = big_one << ex_big;
      BigInteger rand_dist = dist_big * dist_long;
#endif

      // Add or subtract random distance to the current address
      BigInteger t_add = _node.Address.ToBigInteger();

      // Random number that is 0 or 1
      if( _rand.Next(2) == 0 ) {
        t_add += rand_dist;
      }
      else {
        t_add -= rand_dist;
      }

      BigInteger target_int = new BigInteger(t_add % Address.Full);
      AHAddress start = new AHAddress(target_int);
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.SCO, 
                          String.Format("SCO local: {0}, Selecting shortcut to create close to start: {1}.", 
                                        _node.Address, start));
      }
      //make a call to the target selector to find the optimal
      _target_selector.ComputeCandidates(start, (int) Math.Ceiling(logk), CreateShortcutCallback, null);
    }
    
    /**
     * Callback function that is invoked when TargetSelector fetches candidate scores in a range.
     * Initiates connection setup. 
     * Node: All connection messages can be tagged with a token string. This token string is currenly being
     * used to keep the following information about a shortcut:
     * 1. The node who initiated the shortcut setup.
     * 2. The random target near which shortcut was chosen.
     * @param start address pointing to the start of range to query.
     * @param score_table list of candidate addresses sorted by score.
     * @param current currently selected optimal (nullable) 
     */
    protected void CreateShortcutCallback(Address start, SortedList score_table, Address current) {
      if (score_table.Count > 0) {
        /**
         * we remember our address and the start of range inside the token.
         * token is the concatenation of 
         * (a) local node address
         * (b) random target for the range queried by target selector
         */
        string token = _node.Address + start.ToString();
        //connect to the min_target
        Address min_target = (Address) score_table.GetByIndex(0);
        ISender send = null;
        if (start.Equals(min_target)) {
          //looks like the target selector simply returned our random address
          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.SCO, 
                              String.Format("SCO local: {0}, Connecting (shortcut) to min_target: {1} (greedy), random_target: {2}.", 
                                            _node.Address, min_target, start));
          }
          //use a greedy sender
          send = new AHGreedySender(_node, min_target);
        } 
        else {
          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.SCO, 
                              String.Format("SCO local: {0}, Connecting (shortcut) to min_target: {1} (exact), random_target: {2}.", 
                                  _node.Address, min_target, start));
          }
          //use exact sender
          send = new AHExactSender(_node, min_target);
        }
        ConnectTo(send, STRUC_SHORT, token, 1);
      }
    }

    /**
     * Initiates creation of a bypass connection.
     * 
     */
    protected void CreateBypass() {
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.SCO, 
                          String.Format("SCO local: {0}, Selecting bypass to create.", 
                                        _node.Address));
      }
      double logk = Math.Log( (double)_node.NetworkSize, 2.0 );
      _target_selector.ComputeCandidates(_node.Address, (int) Math.Ceiling(logk), CreateBypassCallback, null);
    }
    
    protected void CreateBypassCallback(Address start, SortedList score_table, Address current) {
      if (score_table.Count > 0) {
        Address min_target = (Address) score_table.GetByIndex(0);
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO, 
                            String.Format("SCO local: {0}, Connecting (bypass) to min_target: {1}", 
                                          _node.Address, min_target));
        }
        ISender send = new AHExactSender(_node, min_target);
        ConnectTo(send, STRUC_BYPASS);
      }
    }


    /** 
     * Periodically check if our connections are still optimal. 
     */
    protected void CheckConnectionOptimality(object node, EventArgs eargs) {
      DateTime now = DateTime.UtcNow;
      lock(_sync) {
        if ((now - _last_optimize_time).TotalSeconds < OPTIMIZE_DELAY) {
          return;
        }
        _last_optimize_time = now;
      }

      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.SCO, 
                          String.Format("SCO local: {0}, Selcting a random shortcut to optimize.", 
                                        _node.Address));
      }
      double logk = Math.Log( (double)_node.NetworkSize, 2.0 );  
      
      //Get a random shortcut:
      ArrayList shortcuts = new ArrayList();
      foreach(Connection sc in _node.ConnectionTable.GetConnections(STRUC_SHORT) ) {
        /** 
         * Only if we initiated it, we check if the connection is optimal.
         * First half of the token is initiator address, while the other half 
         * is the start of the range.
         */
        string token = sc.PeerLinkMessage.Token;
        if (token != null && token != String.Empty) {
          string initiator_addr = token.Substring(0, token.Length/2);
          if (initiator_addr == _node.Address.ToString()) {
            shortcuts.Add(sc);
          }
        }
      }
        

      if (shortcuts.Count > 0) {
        // Pick a random shortcut and check for optimality.
        Connection sc = (Connection)shortcuts[ _rand.Next(shortcuts.Count) ];
        string token = sc.PeerLinkMessage.Token;
        // Second half of the token is the random target for the shortcut.
        Address random_target = AddressParser.Parse(token.Substring(token.Length/2));
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO, 
                            String.Format("SCO local: {0}, Optimizing shortcut connection: {1}, random_target: {2}.",
                                          _node.Address, sc.Address, random_target));
        }

        _target_selector.ComputeCandidates(random_target, (int) Math.Ceiling(logk), 
                                           CheckShortcutCallback, sc.Address);
      } else {
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO,
                            String.Format("SCO local: {0}, Cannot find a shortcut to optimize.", 
                                          _node.Address));
        }
      }

      //also optimize the bypass connections.
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.SCO, 
                          String.Format("SCO local: {0}, Selecting a bypass to optimize.", 
                                        _node.Address));
      }
      _target_selector.ComputeCandidates(_node.Address, (int) Math.Ceiling(logk), CheckBypassCallback, null);
    }
    
    /**
     * Checks if the shortcut connection is still optimal, and trims it if not optimal.
     * @param random_target random target pointing to the start of the range for connection candidates.
     * @param score_table candidate addresses sorted by scores.
     * @param sc_address address of the current connection.
     */
    protected void CheckShortcutCallback(Address random_target, SortedList score_table, Address sc_address) {
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.SCO, 
                          String.Format("SCO local: {0}, Checking shortcut optimality: {1}.", 
                                _node.Address, sc_address));
      }
      
      int max_rank = (int) Math.Ceiling(0.2*score_table.Count);
      bool optimal = IsConnectionOptimal(sc_address, score_table, max_rank);
      if (!optimal) {
        Address min_target = (Address) score_table.GetByIndex(0);
        //find the connection and trim it.
        Connection to_trim = null;
        foreach(Connection c in _node.ConnectionTable.GetConnections(STRUC_SHORT) ) {
          string token = c.PeerLinkMessage.Token;
          if (token != null && token != String.Empty) {
            // First half of the token should be the connection initiator
            string initiator_address = token.Substring(0, token.Length/2);
            if (initiator_address == _node.Address.ToString() && c.Address.Equals(sc_address)) {
              to_trim = c;
              break;
            }
          }
        }
        
        if (to_trim != null) {
          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.SCO, 
                              String.Format("SCO local: {0}, Trimming shortcut : {1}, min_target: {2}.",
                                            _node.Address, to_trim.Address, min_target));
          }
          lock(_sync) {
            double total_secs = (DateTime.UtcNow - to_trim.CreationTime).TotalSeconds;
            _sum_con_lifetime += total_secs;
            _trim_count++;
          }
          _node.GracefullyClose(to_trim.Edge);
        }
      } else {
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO,
                            String.Format("SCO local: {0}, Shortcut is optimal: {1}.", 
                                          _node.Address, sc_address));
        }
      }
    }
    
    /**
     * Checks if we have the optimal bypass connection, and trims the ones that are unnecessary.
     * @param start random target pointing to the start of the range for connection candidates.
     * @param score_table candidate addresses sorted by scores.
     * @param bp_address address of the current connection (nullable).
     */
    protected void CheckBypassCallback(Address start, SortedList score_table, Address bp_address) {
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.SCO, 
                          String.Format("SCO local: {0}, Checking bypass optimality.", 
                                        _node.Address));
      }
      
      ArrayList bypass_cons = new ArrayList();
      foreach(Connection c in _node.ConnectionTable.GetConnections(STRUC_BYPASS) ) {
        string token = c.PeerLinkMessage.Token;
        if (token != null) {
          if (token == _node.Address.ToString()) {
            bypass_cons.Add(c);
          }
        }
      }
      
      int max_rank = bypass_cons.Count > 1 ? 0: (int) Math.Ceiling(0.2*score_table.Count);
      foreach (Connection bp in bypass_cons) {
        bool optimal = IsConnectionOptimal(bp.Address, score_table, max_rank);
        if (!optimal) {
          Address min_target = (Address) score_table.GetByIndex(0);
          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.SCO, 
                              String.Format("SCO local: {0}, Trimming bypass : {1}, min_target: {2}.", 
                                            _node.Address, bp.Address, min_target));
          }
          lock(_sync) {
            double total_secs = (DateTime.UtcNow - bp.CreationTime).TotalSeconds;
            _sum_con_lifetime += total_secs;
            _trim_count++;
          }
          _node.GracefullyClose(bp.Edge);
        }
        else {
          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.SCO, 
                              String.Format("SCO local: {0}, Bypass is optimal: {1}.", 
                                            _node.Address, bp));
          }
        }
      }
    }

    /**
     * Checks if connection to the current address is optimal. 
     * Scores can vary over time, and there might be "tight" race for the optimal.
     * We may end up in a situation that we are trimming a connection that is not optimal, even 
     * though the penalty for not using the optimal is marginal. The following algorithm
     * checks that the current selection is in the top-percentile and also the penalty for not
     * using the current optimal is marginal. 
     * @param curr_address address of the current connection target. 
     * @param score_table candidate addresses sorted by scores.
     * @param max_rank maximum rank within the score table, beyond which connection 
     *                 is treated suboptimal.
     */
    protected bool IsConnectionOptimal(Address curr_address, SortedList score_table, int max_rank) {
      if (score_table.Count == 0) {
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO, 
                            String.Format("SCO local: {0}, Not sufficient scores available to determine optimality: {1}.", 
                                          _node.Address, curr_address));
        }
        return true;
      }
            
      bool optimal = false; //if shortcut is optimal.
      bool doubtful = false; //if there is doubt on optimality of this connection.
      int curr_rank = score_table.IndexOfValue(curr_address);
      if (curr_rank == -1) {
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO, 
                            String.Format("SCO local: {0}, No score available for current: {1}.", 
                                          _node.Address, curr_address));
        }
        
        //doubtful case
        doubtful = true;
      } else if (curr_rank == 0) {
        //definitely optimal
        optimal = true;
      } else if (curr_rank <= max_rank) {
        //not the minimum, but still in top %ile.
        double penalty = (double) score_table.GetKey(curr_rank)/(double) score_table.GetKey(0);
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO, 
                            String.Format("SCO local: {0}, Penalty for using current: {1} penalty: {2}).", 
                                          _node.Address, curr_address, penalty));
        }

        //we allow for 10 percent penalty for not using the optimal
        if (penalty < 1.1 ) {
          optimal = true;
        } 
      } else {
        if (LogEnabled) {        
          ProtocolLog.Write(ProtocolLog.SCO, 
                            String.Format("SCO local: {0}, Current: {1} too poorly ranked: {2}.", 
                                  _node.Address, curr_address, curr_rank));
        }
      }

      /** 
       * If we are doubtful about the current selection, we will continue to treat it
       * optimal for sometime.
       */
      string log = null;
      lock(_sync) {
        if (optimal) {
          //clear the entry
          _doubts_table.Remove(curr_address);
        } 
        else if (doubtful) { //if we have doubt about the selection
          //make sure that we are not being to generous
          if (!_doubts_table.ContainsKey(curr_address)) {
            _doubts_table[curr_address] = 1;
          } 
          int idx = (int) _doubts_table[curr_address];
          if (idx < MAX_DOUBT_BENEFITS) {
            _doubts_table[curr_address] = idx + 1;
            log = String.Format("SCO local: {0}, Giving benfit: {1} of doubt for current: {2}.", 
                                       _node.Address, idx, curr_address);
            optimal = true;
          } else {
            log = String.Format("SCO local: {0}, Reached quota: {1} on doubts for current: {2}.", 
                                _node.Address, idx, curr_address);
          }
        }
        
        //all efforts to make the connection look optimal have failed
        if (!optimal) {
          //clear the entry
          _doubts_table.Remove(curr_address);          
        }
      } //end of lock
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.SCO, log);
      }
      return optimal;
    }

    protected void PeriodicNeighborCheck() {
#if PERIODIC_NEIGHBOR_CHK
    /*
     * If we haven't had any connections in a while, we check, our
     * neighbors to see if there are node we should try to 
     * connect to:
     * This is basically the same code as in ConnectorEndHandler
     */

      ArrayList neighs = new ArrayList();
      Hashtable t_to_f = new Hashtable();
      ConnectionList structs = tab.GetConnections(ConnectionType.Structured);
      foreach(Connection c in structs) {
        Address f = c.Address;
        foreach(NodeInfo n in c.Status.Neighbors) {
          t_to_f[n.Address] = f;
        }
        neighs.AddRange( c.Status.Neighbors );
      }
      ConnectToNearer(structs,t_to_f,neighs);
#endif
    }

    protected void TrimConnections() {
#if TRIM
            //We may be able to trim connections.
            ArrayList sc_trim_candidates = new ArrayList();
            ArrayList near_trim_candidates = new ArrayList();
            ConnectionTable tab = _node.ConnectionTable;
            ConnectionList structs = tab.GetConnections(ConnectionType.Structured);
            foreach(Connection c in structs) {
              int left_pos = structs.LeftInclusiveCount(_node.Address, c.Address);
              int right_pos = structs.RightInclusiveCount(_node.Address, c.Address);
              if( c.ConType == STRUC_SHORT ) {
                if( left_pos >= DESIRED_NEIGHBORS &&
                  right_pos >= DESIRED_NEIGHBORS ) {
                 /*
                  * Verify that this shortcut is not close
                  */
                  sc_trim_candidates.Add(c);
                }
              }
              else if( c.ConType == STRUC_NEAR ) {
                if( right_pos > 2 * DESIRED_NEIGHBORS &&
                    left_pos > 2 * DESIRED_NEIGHBORS ) {
                  //These are near neighbors that are not so near
                  near_trim_candidates.Add(c);
                }
              }
            }
            /*
             * The maximum number of shortcuts we allow is log N,
             * but we only want 1.  This gives some flexibility to
             * prevent too much edge churning
             */
            int max_sc = 2 * DesiredShortcuts;
            bool sc_needs_trim = (sc_trim_candidates.Count > max_sc);
            bool near_needs_trim = (near_trim_candidates.Count > 0);
            /*
             * Prefer to trim near neighbors that are unneeded, since
             * they are not as useful for routing
             * If there are no unneeded near neighbors, then
             * consider trimming the shortcuts
             */
            if( near_needs_trim ) {
              //Delete a farthest trim candidate:
        BigInteger biggest_distance = new BigInteger(0);
        BigInteger tmp_distance = new BigInteger(0);
        Connection to_trim = null;
        foreach(Connection tc in near_trim_candidates ) 
        {
          AHAddress t_ah_add = (AHAddress)tc.Address;
          tmp_distance = t_ah_add.DistanceTo( (AHAddress)_node.Address).abs();
          if (tmp_distance > biggest_distance) {
            biggest_distance = tmp_distance;
            //Console.Error.WriteLine("...finding far distance for trim: {0}",biggest_distance.ToString() );
            to_trim = tc;
          }
        }
        //Console.Error.WriteLine("Final distance for trim{0}: ",biggest_distance.ToString() );
              //Delete a random trim candidate:
              //int idx = _rand.Next( near_trim_candidates.Count );
              //Connection to_trim = (Connection)near_trim_candidates[idx];
#if POB_DEBUG
        Console.Error.WriteLine("Attempt to trim Near: {0}", to_trim);
#endif
              _node.GracefullyClose( to_trim.Edge );
            }
            else if( sc_needs_trim ) {
              /**
               * @todo use a better algorithm here, such as Nima's
               * algorithm for biasing towards more distant nodes:
               */
              //Delete a random trim candidate:
              int idx = _rand.Next( sc_trim_candidates.Count );
              Connection to_trim = (Connection)sc_trim_candidates[idx];
#if POB_DEBUG
             Console.Error.WriteLine("Attempt to trim Shortcut: {0}", to_trim);
#endif
              _node.GracefullyClose( to_trim.Edge );
            }
#endif
    }
  }

}
