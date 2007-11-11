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
        //Listen for connection events:
        _node.ConnectionTable.DisconnectionEvent +=
          new EventHandler(this.DisconnectHandler);
        _node.ConnectionTable.ConnectionEvent +=
          new EventHandler(this.ConnectHandler);     
        _node.ConnectionTable.StatusChangedEvent += 
          new EventHandler(this.StatusChangedHandler);
      /**
       * Every heartbeat we assess the trimming situation.
       * If we have excess edges and it has been more than
       * _trim_wait_time heartbeats then we trim.
       */
        _last_retry_time = DateTime.UtcNow;
        _current_retry_interval = _DEFAULT_RETRY_INTERVAL;
        _node.HeartBeatEvent += new EventHandler(this.CheckState);
      }
    }

    ///////  Attributes /////////////////

    protected Node _node;
    protected Random _rand;

    volatile protected bool _compensate;
    //We use this to make sure we don't trim connections
    //too fast.  We want to only trim in the "steady state"
    protected DateTime _last_connection_time;
    protected object _sync;

    protected Hashtable _connectors;
    
    protected TimeSpan _current_retry_interval;
    protected DateTime _last_retry_time;
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

    /*
     * These are parameters of the Overlord.  These govern
     * the way it reacts and works.
     */
    
    ///How many neighbors do we want (same value for left and right)
    static protected readonly int DESIRED_NEIGHBORS = 2;
    static protected readonly int DESIRED_SHORTCUTS = 1;
    ///How many seconds to wait between connections/disconnections to trim
    static protected readonly double TRIM_DELAY = 30.0;
    ///By default, we only wake up every 10 seconds, but we back off exponentially
    static protected readonly TimeSpan _DEFAULT_RETRY_INTERVAL = TimeSpan.FromSeconds(1);
    static protected readonly TimeSpan _MAX_RETRY_INTERVAL = TimeSpan.FromSeconds(60);
    /*
     * We don't want to risk mistyping these strings.
     */
    static protected readonly string STRUC_NEAR = "structured.near";
    static protected readonly string STRUC_SHORT = "structured.shortcut";
    
    /**
     * If we start compensating, we check to see if we need to
     * make new neighbor or shortcut connections : 
     */
    override public bool IsActive
    {
      get { return _compensate; }
      set { _compensate = value; }
    }    

    override public bool NeedConnection 
    {
      get {
        int structs = _node.ConnectionTable.Count(ConnectionType.Structured);
        if( structs < (2 * DESIRED_NEIGHBORS + DESIRED_SHORTCUTS) ) {
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
          
        ConnectionTable tab = _node.ConnectionTable;
        lock( tab.SyncRoot ) {
          try {
            lc = tab.GetLeftStructuredNeighborOf(our_addr);
          } catch(Exception) { }
          try {
            rc = tab.GetRightStructuredNeighborOf(our_addr);
          } catch (Exception) { }
        }
          if (rc == null || lc == null) {
            if(ProtocolLog.SCO.Enabled)
              ProtocolLog.Write(ProtocolLog.SCO, String.Format(
                "{0}: No left or right neighbor (false)", our_addr));
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
          //now make sure things are good about Status Messages
          AHAddress left_addr = lc.Address as AHAddress;
          AHAddress right_addr = rc.Address as AHAddress;
          
          //we have to make sure than nothing is between us and left
          foreach (NodeInfo n_info in lc.Status.Neighbors) {
            AHAddress stat_addr = n_info.Address as AHAddress;
            if (stat_addr.IsBetweenFromLeft(our_addr, left_addr)) {
              //we are expecting a better candidate for left neighbor!
              if(ProtocolLog.SCO.Enabled)
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
              if(ProtocolLog.SCO.Enabled)
                ProtocolLog.Write(ProtocolLog.SCO, String.Format(
                  "{0}: Better right: {1} (false)", our_addr, stat_addr));
              return false;
            }
          }
          if(ProtocolLog.SCO.Enabled)
            ProtocolLog.Write(ProtocolLog.SCO, String.Format(
              "{0}:  Returning (true)", our_addr));
          return true;
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
          //foreach(Connection c in _node.ConnectionTable.GetConnections(STRUC_NEAR)) {
            foreach(Connection c in tab.GetConnections(ConnectionType.Structured)) {
              AHAddress adr = (AHAddress)c.Address;
#if POB_DEBUG
          AHAddress local = (AHAddress)_node.Address;
         Console.Error.WriteLine(
           "{0} -> {1}, lidx: {2}, is_left: {3}",
                            _node.Address, adr, LeftPosition(adr), adr.IsLeftOf( local ) );
#endif
              if( 
                //adr.IsLeftOf( local ) &&
                (LeftPosition( adr ) < DESIRED_NEIGHBORS) ) {
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
            //foreach(Connection c in _node.ConnectionTable.GetConnections(STRUC_NEAR)) {
            foreach(Connection c in tab.GetConnections(ConnectionType.Structured)) {
              AHAddress adr = (AHAddress)c.Address;
#if POB_DEBUG
          AHAddress local = (AHAddress)_node.Address;
              Console.Error.WriteLine("{0} -> {1}, ridx: {2}, is_right: {3}",
                            _node.Address, adr, RightPosition(adr), adr.IsRightOf( local) );
#endif
              if(
                //adr.IsRightOf( local ) &&
                (RightPosition( adr ) < DESIRED_NEIGHBORS) ) {
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
            foreach(Connection c in _node.ConnectionTable.GetConnections(STRUC_SHORT)) {
              int left_pos = LeftPosition((AHAddress)c.Address);
              int right_pos = RightPosition((AHAddress)c.Address); 
              if( left_pos >= DESIRED_NEIGHBORS &&
                  right_pos >= DESIRED_NEIGHBORS ) {
              /*
               * No matter what they say, don't count them
               * as a shortcut if they are one a close neighbor
               */
                shortcuts++;
              }
            }
          if( shortcuts < DESIRED_SHORTCUTS ) {
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
      int structured_count = 0;
      int desired_ctms = 1;
      
      lock( tab.SyncRoot ) {
        structured_count = tab.Count(ConnectionType.Structured);
        if( structured_count < 2 ) {
          int leaf_count = tab.Count(ConnectionType.Leaf);
          if( leaf_count == 0 )
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
          int attempts = 2 * leaf_count;
          do {
            leaf = tab.GetRandom(ConnectionType.Leaf);
            attempts--;
          }
          while( leaf != null &&
                 tab.Count( ConnectionType.Leaf ) > 1 &&
                 tab.Contains( ConnectionType.Structured, leaf.Address ) &&
                 (attempts > 0) );
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
      }//End of ConnectionTable lock
      
      if( structured_count > 0 && sender == null ) {
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
            target = GetShortcutTarget(); 
            contype = STRUC_SHORT;
            //Console.Error.WriteLine("Making Connector for shortcut to: {0}", target);
            /*
             * Use greedy routing to make shortcuts, because we only want to
             * find the node closest to the target address.
             */
            sender = new AHGreedySender(_node, target);
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
      
      //These are the two closest target addresses
      Address ltarget = null;
      Address rtarget = null;
      Address nltarget = null;
      Address nrtarget = null;
      
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
       ConnectionTable tab = _node.ConnectionTable;
       lock( tab.SyncRoot ) {

#if SEND_DIRECTIONAL_TO_NEAR
        int left_pos = LeftPosition((AHAddress)new_con.Address);
        int right_pos = RightPosition((AHAddress)new_con.Address); 

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
        /*
         * Check to see if any of this node's neighbors
         * should be neighbors of us. It provides modified
         * Address targets ltarget and rtarget.
         */
  
        CheckForNearerNeighbors(new_con.Status.Neighbors,
                                ltarget,out nltarget,
                                rtarget,out nrtarget);
       
       } //Release the lock on the connection_table
      }
      
      /* 
       * We want to make sure not to hold the lock on ConnectionTable
       * while we try to make new connections
       */
      if( nrtarget != null ) {
        /*
         * nrtarget should exist in the network and should be a neighbor
         * of new_con, so the default routing options are good.
         */
        ISender send = new ForwardingSender(_node, new_con.Address, nrtarget);
        ConnectTo(send, STRUC_NEAR);
      }
      if( nltarget != null && !nltarget.Equals(nrtarget) ) {
        /*
         * nltarget should exist in the network and should be a neighbor
         * of new_con, so the default routing options are good.
         */
        ISender send = new ForwardingSender(_node, new_con.Address, nltarget);
        ConnectTo(send, STRUC_NEAR);
      }
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
      ConnectToNearer(new_con.Address, new_con.Status.Neighbors);
    }

    
    /*
     * Check to see if any of this node's neighbors
     * should be neighbors of us.  If they should, connect
     * to the closest such nodes on each side.
     *
     * This function accepts several ref params in order to provide a
     * "pass-through" type function for the examining of neighbor lists in
     * several different functions. This function does not provide locking.
     * Please lock and unlock as needed.
     */
    protected void CheckForNearerNeighbors(IEnumerable neighbors, 
        Address ltarget, out Address nltarget,
        Address rtarget, out Address nrtarget)
    {

      ConnectionTable tab = _node.ConnectionTable;
      BigInteger ldist = null;
      BigInteger rdist = null;
      AHAddress local = (AHAddress)_node.Address;
      foreach(NodeInfo ni in neighbors) {
        if( !( _node.Address.Equals(ni.Address) ||
               tab.Contains(ConnectionType.Structured, ni.Address) ) ) {
          AHAddress adr = (AHAddress)ni.Address;
          int n_left = LeftPosition( adr );
          int n_right = RightPosition( adr );
          if( n_left < DESIRED_NEIGHBORS || n_right < DESIRED_NEIGHBORS ) {
            //We should connect to this node! if we are not already:
            BigInteger adr_dist = local.LeftDistanceTo(adr);
            if( ( null == ldist ) || adr_dist < ldist ) {
              ldist = adr_dist;
              ltarget = adr;
            }
            adr_dist = local.RightDistanceTo(adr);
            if( ( null == rdist ) || adr_dist < rdist ) {
              rdist = adr_dist;
              rtarget = adr;
            }
          }
        }
      }
      nltarget = ltarget;
      nrtarget = rtarget;
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
      ConnectToNearer(ctr.ReceivedCTMs);
    }
    /**
     * Given an array of CTM messages, look for any neighbor nodes
     * that we should be connected to, and start a new Connector
     */
    protected void ConnectToNearer(IEnumerable ctms) {
      ArrayList neighs = new ArrayList();
      Hashtable neighbors_to_ctm = new Hashtable();
      Hashtable add_to_neighbor = new Hashtable();
      foreach(ConnectToMessage ctm in ctms) {
        if( ctm.Neighbors != null ) {
          foreach(NodeInfo n in ctm.Neighbors) {
            neighbors_to_ctm[n] = ctm;
            add_to_neighbor[n.Address] = n;
          }
          neighs.AddRange( ctm.Neighbors );
        }
      }
      Address ltarget = null;
      Address nltarget = null;
      Address rtarget = null;
      Address nrtarget = null;
      lock( _node.ConnectionTable.SyncRoot ) {
        CheckForNearerNeighbors(neighs, ltarget, out nltarget,
                              rtarget, out nrtarget);
      }
      if( nrtarget != null ) {
        NodeInfo target_info = (NodeInfo)add_to_neighbor[nrtarget];
        ConnectToMessage ctm = (ConnectToMessage)neighbors_to_ctm[target_info];
        Address forwarder = ctm.Target.Address;
        ISender send = new ForwardingSender(_node, forwarder, nrtarget);
        ConnectTo(send, STRUC_NEAR);
      }
      if( nltarget != null && !nltarget.Equals(nrtarget) ) {
        NodeInfo target_info = (NodeInfo)add_to_neighbor[nltarget];
        ConnectToMessage ctm = (ConnectToMessage)neighbors_to_ctm[target_info];
        Address forwarder = ctm.Target.Address;
        ISender send = new ForwardingSender(_node, forwarder, nrtarget);
        ConnectTo(send, STRUC_NEAR);
      }
    }
    /**
     * Similar to the above
     */
    protected void ConnectToNearer(Address forwarder, IEnumerable ni)
    {
      Address ltarget = null;
      Address rtarget = null;
      Address nltarget = null;
      Address nrtarget = null;
    
      lock( _node.ConnectionTable.SyncRoot ) {
        CheckForNearerNeighbors(ni, ltarget, out nltarget, rtarget, out nrtarget);
      }
       /* 
       * We want to make sure not to hold the lock on ConnectionTable
       * while we try to make new connections
       */
      if( nrtarget != null ) {
        ISender send = new ForwardingSender(_node, forwarder, nrtarget);
        ConnectTo(send, STRUC_NEAR);
      }
      if( nltarget != null && !nltarget.Equals(nrtarget) ) {
        ISender send = new ForwardingSender(_node, forwarder, nltarget);
        ConnectTo(send, STRUC_NEAR);
      }
    }

    protected void ConnectTo(ISender sender, string contype) {
      ConnectTo(sender, contype, 1);
    }
    /**
     * @param sender the ISender for the Connector to use
     * @param contype the type of connection we want to make
     * @param responses the maximum number of ctm response messages to listen
     */
    protected void ConnectTo(ISender sender, string contype, int responses)
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
        Linker l = new Linker(_node, target, null, contype);
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
      ConnectToMessage ctm = new ConnectToMessage(contype, _node.GetNodeInfo(8), near_ni);

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
      lock( _sync ) {
        _last_connection_time = DateTime.UtcNow;
        _need_left = -1;
        _need_right = -1;
        _need_short = -1;
        _current_retry_interval = _DEFAULT_RETRY_INTERVAL;
      }
      Connection c = ((ConnectionEventArgs)args).Connection;

      if( IsActive ) {
        if( c.MainType == ConnectionType.Structured ) {
          int right_pos = RightPosition((AHAddress)c.Address);
          int left_pos = LeftPosition((AHAddress)c.Address);
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
              Address target = GetShortcutTarget(); 
              ISender send = new AHSender(_node, target,
                                          _node.DefaultTTLFor(target),
                                          AHPacket.AHOptions.Greedy);
              ConnectTo(send, STRUC_SHORT);
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
                            ctm_resp.ConnectionType);
      _node.TaskQueue.Enqueue( l );
      /**
       * Check this guys neighbors:
       */
      ConnectToNearer(ctm_resp.Target.Address, ctm_resp.Neighbors);
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
      Connection c = ((ConnectionEventArgs)args).Connection; 
      ConnectToNearer(c.Address, c.Status.Neighbors);
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
     * Return a random shortcut target with the
     * correct distance distribution
     */
    protected Address GetShortcutTarget()
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
      return new AHAddress(target_int); 
    }
    
    /**
     * Given an address, we see how many of our connections
     * are closer than this address to the left
     */
    protected int LeftPosition(AHAddress addr)
    {
      AHAddress local = (AHAddress)_node.Address;
      //Don't let the Table change while we do this:
      ConnectionTable tab = _node.ConnectionTable;
      return tab.LeftInclusiveCount(local, addr);
    }
    
    protected void PeriodicNeighborCheck() {
#if PERIODIC_NEIGHBOR_CHK
        /*
         * If we haven't had any connections in a while, we check, our
         * neighbors to see if there are node we should try to 
         * connect to:
         * This is basically the same code as in ConnectorEndHandler
         */
          ConnectionTable tab = _node.ConnectionTable;
          Address ltarget = null;
          Address nltarget = null;
          Address rtarget = null;
          Address nrtarget = null;
          Hashtable neighbors_to_con = new Hashtable();
          Hashtable add_to_neighbor = new Hashtable();
          lock( tab.SyncRoot ) {
            ArrayList neighs = new ArrayList();
            foreach(Connection c in tab.GetConnections(ConnectionType.Structured)) {
              foreach(NodeInfo n in c.Status.Neighbors) {
                neighbors_to_con[n] = c;
                add_to_neighbor[n.Address] = n;
              }
              neighs.AddRange( c.Status.Neighbors );
            }
            CheckForNearerNeighbors(neighs, ltarget, out nltarget,
                                rtarget, out nrtarget);
          }
          if( nrtarget != null ) {
            NodeInfo target_info = (NodeInfo)add_to_neighbor[nrtarget];
            Connection con = (Connection)neighbors_to_con[target_info];
            Address forwarder = con.Address;
            ISender sender = new ForwardingSender(_node, forwarder, nrtarget);
            ConnectTo( sender, STRUC_NEAR );
          }
          if( nltarget != null && !nltarget.Equals(nrtarget) ) {
            NodeInfo target_info = (NodeInfo)add_to_neighbor[nltarget];
            Connection con = (Connection)neighbors_to_con[target_info];
            Address forwarder = con.Address;
            ISender sender = new ForwardingSender(_node, forwarder, nltarget);
            ConnectTo( sender, STRUC_NEAR );
          }
#endif
    }

    protected void TrimConnections() {
#if TRIM
            //We may be able to trim connections.
            ArrayList sc_trim_candidates = new ArrayList();
            ArrayList near_trim_candidates = new ArrayList();
            ConnectionTable tab = _node.ConnectionTable;
              foreach(Connection c in tab.GetConnections(STRUC_SHORT)) {
                int left_pos = LeftPosition((AHAddress)c.Address);
                int right_pos = RightPosition((AHAddress)c.Address); 
                if( left_pos >= DESIRED_NEIGHBORS &&
                  right_pos >= DESIRED_NEIGHBORS ) {
                 /*
                  * Verify that this shortcut is not close
                  */
                sc_trim_candidates.Add(c);
                }
              }
              foreach(Connection c in tab.GetConnections(STRUC_NEAR)) {
                int right_pos = RightPosition((AHAddress)c.Address);
                int left_pos = LeftPosition((AHAddress)c.Address);
                if( right_pos > 2 * DESIRED_NEIGHBORS &&
                    left_pos > 2 * DESIRED_NEIGHBORS ) {
                  //These are near neighbors that are not so near
                  near_trim_candidates.Add(c);
                }
            }
            /*
             * The maximum number of shortcuts we allow is log N,
             * but we only want 1.  This gives some flexibility to
             * prevent too much edge churning
             */
            int max_sc = 2 * DESIRED_SHORTCUTS;
            if( _node.NetworkSize > 2 ) {
              max_sc = (int)(Math.Log(_node.NetworkSize)/Math.Log(2.0)) + 1;
            }
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

    /**
     * Given an address, we see how many of our connections
     * are closer than this address to the right.
     */
    protected int RightPosition(AHAddress addr)
    {
      AHAddress local = (AHAddress)_node.Address;
      //Don't let the Table change while we do this:
      ConnectionTable tab = _node.ConnectionTable;
      return tab.RightInclusiveCount(local, addr);
    }
    
  }

}
