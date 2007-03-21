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

#define POB_DEBUG
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
      _node = n;
      _rand = new Random();
      _sync = new Object();
      _connectors = new ArrayList();
      _last_connection_time = DateTime.UtcNow;
      lock( _sync ) {
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

    protected bool _compensate;
    //We use this to make sure we don't trim connections
    //too fast.  We want to only trim in the "steady state"
    protected DateTime _last_connection_time;
    protected object _sync;

    protected ArrayList _connectors;

    /*
     * These are parameters of the Overlord.  These govern
     * the way it reacts and works.
     */
    
    ///How many neighbors do we want (same value for left and right)
    static protected readonly int _desired_neighbors = 2;
    static protected readonly int _desired_shortcuts = 1;
    ///How many seconds to wait between connections/disconnections to trim
    static protected readonly double _trim_delay = 30.0;
    ///By default, we only wake up every 10 seconds, but we back off exponentially
    static protected readonly TimeSpan _DEFAULT_RETRY_INTERVAL = new TimeSpan(0,0,0,0,10000); 
    protected TimeSpan _current_retry_interval;
    protected DateTime _last_retry_time;
    /*
     * We don't want to risk mistyping these strings.
     */
    static protected readonly string struc_near = "structured.near";
    static protected readonly string struc_short = "structured.shortcut";
    
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
	if( structs < (2 * _desired_neighbors + _desired_shortcuts) ) {
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
	ConnectionTable tab = _node.ConnectionTable;
	AHAddress our_addr = _node.Address as AHAddress;
	  
	lock( tab.SyncRoot ) {
	  Connection lc = null; 
	  try {
	    lc = tab.GetLeftStructuredNeighborOf(our_addr);
	  } catch(Exception) {
	  }
	  
	  Connection rc = null;
	  try {
	    rc = tab.GetRightStructuredNeighborOf(our_addr);
	  } catch (Exception) {
	  }
	  if (rc == null || lc == null) {
	    Console.WriteLine("{0}: No left or right neighbor (false)", our_addr);
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
	      Console.WriteLine("{0}: Better left: {1} (false)", our_addr, stat_addr);
	      return false;
	    }
	  }
	  
	  //we have to make sure than nothing is between us and left
	  foreach (NodeInfo n_info in rc.Status.Neighbors) {
	    AHAddress stat_addr = n_info.Address as AHAddress;
	    if (stat_addr.IsBetweenFromRight(our_addr, right_addr)) {
	      //we are expecting a better candidate for left neighbor!
	      Console.WriteLine("{0}: Better right: {1} (false)", our_addr, stat_addr);
	      return false;
	    }
	  }
	  Console.WriteLine("{0}:  Returning (true)", our_addr);	  
	  return true;
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
	lock( _sync ) {
          if( _need_left != -1 ) {
            return (_need_left == 1);
	  }
	  int left = 0;
	  ConnectionTable tab = _node.ConnectionTable;
	  lock( tab.SyncRoot ) {
          //foreach(Connection c in _node.ConnectionTable.GetConnections(struc_near)) {
            foreach(Connection c in tab.GetConnections(ConnectionType.Structured)) {
              AHAddress adr = (AHAddress)c.Address;
//#if POB_DEBUG
#if false
	  AHAddress local = (AHAddress)_node.Address;
              Console.WriteLine("{0} -> {1}, lidx: {2}, is_left: {3}" ,
			    _node.Address, adr, LeftPosition(adr), adr.IsLeftOf( local ) );
#endif
	      if( 
	        //adr.IsLeftOf( local ) &&
                (LeftPosition( adr ) < _desired_neighbors) ) {
                //This is left neighbor:
	        left++; 
	      }
	    }
	  }
//#if POB_DEBUG
#if false
          Console.WriteLine("{0} left: {1}" , _node.Address, left);
#endif
	  if( left < _desired_neighbors ) {
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
	lock( _sync ) {
          if( _need_right != -1 ) {
            return (_need_right == 1);
	  }
	  int right = 0;
	  ConnectionTable tab = _node.ConnectionTable;
	  lock( tab.SyncRoot ) {
            //foreach(Connection c in _node.ConnectionTable.GetConnections(struc_near)) {
            foreach(Connection c in tab.GetConnections(ConnectionType.Structured)) {
              AHAddress adr = (AHAddress)c.Address;
//#if POB_DEBUG
#if false
	  AHAddress local = (AHAddress)_node.Address;
              Console.WriteLine("{0} -> {1}, ridx: {2}, is_right: {3}",
			    _node.Address, adr, RightPosition(adr), adr.IsRightOf( local) );
#endif
	      if(
	        //adr.IsRightOf( local ) &&
                (RightPosition( adr ) < _desired_neighbors) ) {
                //This is right neighbor:
	        right++; 
	      }
	    }
	  }
	  if( right < _desired_neighbors ) {
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
	  lock( _node.ConnectionTable.SyncRoot ) {
            foreach(Connection c in _node.ConnectionTable.GetConnections(struc_short)) {
              int left_pos = LeftPosition((AHAddress)c.Address);
              int right_pos = RightPosition((AHAddress)c.Address); 
	      if( left_pos >= _desired_neighbors &&
	          right_pos >= _desired_neighbors ) {
              /*
	       * No matter what they say, don't count them
	       * as a shortcut if they are one a close neighbor
	       */
                shortcuts++;
	      }
	    }
	  }
	  if( shortcuts < _desired_shortcuts ) {
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
      //Console.WriteLine("In Activate: {0}", _node.Address);
#endif
      if( IsActive == false ) {
        return;
      }
      DateTime now = DateTime.UtcNow;
      if( now - _last_retry_time < _current_retry_interval ) {
        //Not time yet...
	return;
      }
      _last_retry_time = now;
      //Double the length of time we wait (resets to default on connections)
      _current_retry_interval = _current_retry_interval + _current_retry_interval;

      ConnectionTable tab = _node.ConnectionTable;
      //If we are going to connect to someone, this is how we
      //know who to use
      Address forwarder = null;
      Address target = null;
      short ttl = -1;
      string contype = String.Empty;
      int structured_count = 0;
      
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
	    if( ! target.Equals( leaf.Address ) ) {
	      //As long as the forwarder is not the target, forward
              forwarder = leaf.Address;
	    }
	    ttl = 1024;
	    //This is a near neighbor connection
	    contype = struc_near;
	  }
	}
      }//End of ConnectionTable lock
      if( target != null ) {
        if( forwarder != null ) {
          ForwardedConnectTo(forwarder, target, ttl, contype);
	}
	else {
          ConnectTo(target, ttl, contype);
	}
      }
      if( structured_count > 0 && target == null ) {
          //We have enough structured connections to ignore the leafs
          
	  /**
	   * We need left or right neighbors we send
	   * a ConnectToMessage in the directons we
	   * need.
	   */

	  bool trying_near = false;
          if( NeedLeftNeighbor ) {
#if POB_DEBUG
      //Console.WriteLine("NeedLeftNeighbor: {0}", _node.Address);
#endif
            target = new DirectionalAddress(DirectionalAddress.Direction.Left);
	    ttl = (short)_desired_neighbors;
	    contype = struc_near;
	    trying_near = true;
            ConnectTo(target, ttl, contype);
          }
	  if( NeedRightNeighbor ) {
#if POB_DEBUG
      //Console.WriteLine("NeedRightNeighbor: {0}", _node.Address);
#endif
            target = new DirectionalAddress(DirectionalAddress.Direction.Right);
	    ttl = (short)_desired_neighbors;
	    contype = struc_near;
	    trying_near = true;
            ConnectTo(target, ttl, contype);
          }
	  /*
	   * If we are trying to get near connections it
	   * is not smart to try to get a shortcut.  We
	   * need to make sure we are on the proper place in
	   * the ring before doing the below:
	   */
	  if( !trying_near && NeedShortcut ) {
#if POB_DEBUG
      //Console.WriteLine("NeedShortcut: {0}", _node.Address);
#endif
            target = GetShortcutTarget(); 
	    ttl = 1024;
	    contype = struc_short;
            //Console.WriteLine("Making Connector for shortcut to: {0}", target);
            ConnectTo(target, ttl, contype);
          }
      }
    }

    /**
     * Every heartbeat we take a look to see if we should trim
     *
     * We only trim one at a time.
     */
    protected void CheckState(object node, EventArgs eargs)
    {
#if POB_DEBUG
      //Console.WriteLine("In Check for State");
#endif
      lock( _sync ) {
        if( IsActive == false ) {
          //If we are not active, we do not care what
	  //our state is.
          return;
        }
        TimeSpan elapsed = DateTime.UtcNow - _last_connection_time;
	if( elapsed.TotalSeconds >= _trim_delay ) {
          ConnectionTable tab = _node.ConnectionTable;
#if POB_DEBUG
    //Console.WriteLine("Go for State check");
#endif

#if PERIODIC_NEIGHBOR_CHK
        /*
         * If we haven't had any connections in a while, we check, our
         * neighbors to see if there are node we should try to 
         * connect to:
         * This is basically the same code as in ConnectorEndHandler
         */
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
            short ttl = _node.DefaultTTLFor( forwarder );
            ForwardedConnectTo(forwarder, nrtarget, ttl, struc_near);
          }
          if( nltarget != null && !nltarget.Equals(nrtarget) ) {
            NodeInfo target_info = (NodeInfo)add_to_neighbor[nltarget];
            Connection con = (Connection)neighbors_to_con[target_info];
            Address forwarder = con.Address;
            short ttl = _node.DefaultTTLFor( forwarder );
            ForwardedConnectTo(forwarder, nltarget, ttl, struc_near);
          }
#endif

#if TRIM
            //We may be able to trim connections.
	    ArrayList sc_trim_candidates = new ArrayList();
	    ArrayList near_trim_candidates = new ArrayList();
	    lock( tab.SyncRoot ) {
	      
	      foreach(Connection c in tab.GetConnections(struc_short)) {
                int left_pos = LeftPosition((AHAddress)c.Address);
                int right_pos = RightPosition((AHAddress)c.Address); 
	        if( left_pos >= _desired_neighbors &&
	          right_pos >= _desired_neighbors ) {
	         /*
		  * Verify that this shortcut is not close
		  */
                  sc_trim_candidates.Add(c);
		}
	      }
	      foreach(Connection c in tab.GetConnections(struc_near)) {
                int right_pos = RightPosition((AHAddress)c.Address);
                int left_pos = LeftPosition((AHAddress)c.Address);
	    
	        if( right_pos > 2 * _desired_neighbors &&
		    left_pos > 2 * _desired_neighbors ) {
	          //These are near neighbors that are not so near
	          near_trim_candidates.Add(c);
		}
                
	      }
	    }//End of ConnectionTable lock
            /*
             * The maximum number of shortcuts we allow is log N,
             * but we only want 1.  This gives some flexibility to
             * prevent too much edge churning
             */
            int max_sc = 2 * _desired_shortcuts;
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
            //Console.WriteLine("...finding far distance for trim: {0}",biggest_distance.ToString() );
            to_trim = tc;
          }
        }
        //Console.WriteLine("Final distance for trim{0}: ",biggest_distance.ToString() );
	      //Delete a random trim candidate:
	      //int idx = _rand.Next( near_trim_candidates.Count );
	      //Connection to_trim = (Connection)near_trim_candidates[idx];
#if POB_DEBUG
        //Console.WriteLine("Attempt to trim Near: {0}", to_trim);
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
              //Console.WriteLine("Attempt to trim Shortcut: {0}", to_trim);
#endif
	      _node.GracefullyClose( to_trim.Edge );
	    }
#endif
	  //}
	  if( NeedConnection ) {
            //Wake back up and try to get some
            Activate();
	  }
	}
      }
    }
    /**
     * This method is called when a new Connection is added
     * to the ConnectionTable
     */
    protected void ConnectHandler(object contab, EventArgs eargs)
    {
      //These are the two closest target addresses
      Address ltarget = null;
      Address rtarget = null;
      Address nltarget = null;
      Address nrtarget = null;
	    
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
      
      if( new_con.MainType == ConnectionType.Leaf ) {
	/*
	 * We just got a leaf.  Try to use it to get a shortcut.near
	 * This leaf could be connecting a new part of the network
	 * to us.  We try to connect to ourselves to make sure
	 * the network is connected:
	 */
	Address target = GetSelfTarget();
	short ttl = 1024;
	//This is a near neighbor connection
	string contype = struc_near;
        ForwardedConnectTo(new_con.Address, target, ttl, contype);
      }
      else if( new_con.MainType == ConnectionType.Structured ) {
       ConnectionTable tab = _node.ConnectionTable;
       lock( tab.SyncRoot ) {

#if SEND_DIRECTIONAL_TO_NEAR
        int left_pos = LeftPosition((AHAddress)new_con.Address);
        int right_pos = RightPosition((AHAddress)new_con.Address); 

	if( left_pos < _desired_neighbors ) {
        /*
	 * This is a new left neighbor.  Always
	 * connect to the right of a left neighbor,
	 * this will make sure we are connected to
	 * our local neighborhood.
	 */
          connect_right = true;
	  if( left_pos < _desired_neighbors - 1) { 
            /*
	     * Don't connect to the left of our most
	     * left neighbor.  If this is not our
	     * most left neighbor, make sure we are
	     * connected to his left
	     */
            connect_left = true;
	  }
	}
	if( right_pos < _desired_neighbors ) {
        /*
	 * This is a new right neighbor.  Always
	 * connect to the left of a right neighbor,
	 * this will make sure we are connected to
	 * our local neighborhood.
	 */
          connect_left = true;
	  if( right_pos < _desired_neighbors - 1) { 
            /*
	     * Don't connect to the right of our most
	     * right neighbor.  If this is not our
	     * most right neighbor, make sure we are
	     * connected to his right
	     */
            connect_right = true;
	  }
	}
	
	if( left_pos >= _desired_neighbors && right_pos >= _desired_neighbors ) {
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
      short f_ttl = 3; //2 would be enough, but 1 extra...
      if( nrtarget != null ) {
        ForwardedConnectTo(new_con.Address, nrtarget, f_ttl, struc_near);
#if PLAB_LOG
        BrunetEventDescriptor bed1 = new BrunetEventDescriptor();      
        bed1.RemoteAHAddress = new_con.Address.ToBigInteger().ToString();
        bed1.EventDescription = "SCO.ConnectHandler.rforwarder";
        Logger.LogAttemptEvent( bed1 );

        BrunetEventDescriptor bed2 = new BrunetEventDescriptor();      
        bed2.RemoteAHAddress = nrtarget.ToBigInteger().ToString();
        bed2.EventDescription = "SCO.ConnectHandler.rtarget";
        Logger.LogAttemptEvent( bed2 );                    
#endif
      }
      if( nltarget != null && !nltarget.Equals(nrtarget) ) {
        ForwardedConnectTo(new_con.Address, nltarget, f_ttl, struc_near);
#if PLAB_LOG
        BrunetEventDescriptor bed1 = new BrunetEventDescriptor();      
        bed1.RemoteAHAddress = new_con.Address.ToBigInteger().ToString();
        bed1.EventDescription = "SCO.ConnectHandler.lforwarder";
        Logger.LogAttemptEvent( bed1 );

        BrunetEventDescriptor bed2 = new BrunetEventDescriptor();      
        bed2.RemoteAHAddress = nltarget.ToBigInteger().ToString();
        bed2.EventDescription = "SCO.ConnectHandler.ltarget";
        Logger.LogAttemptEvent( bed2 );                   
#endif
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
                        new_con.Edge, nn_ttl, struc_near); 
        }
        if( connect_left ) {
          ConnectToOnEdge(new DirectionalAddress(DirectionalAddress.Direction.Left),
                        new_con.Edge, nn_ttl, struc_near); 
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
      BigInteger ldist = -1;
      BigInteger rdist = -1;
      AHAddress local = (AHAddress)_node.Address;
      foreach(NodeInfo ni in neighbors) {
        if( !( _node.Address.Equals(ni.Address) ||
	       tab.Contains(ConnectionType.Structured, ni.Address) ) ) {
          AHAddress adr = (AHAddress)ni.Address;
          int n_left = LeftPosition( adr );
          int n_right = RightPosition( adr );
          if( n_left < _desired_neighbors || n_right < _desired_neighbors ) {
            //We should connect to this node! if we are not already:
            BigInteger adr_dist = local.LeftDistanceTo(adr);
            if( adr_dist < ldist || ldist == -1 ) {
              ldist = adr_dist;
              ltarget = adr;
            }
            adr_dist = local.RightDistanceTo(adr);
            if( adr_dist < rdist || rdist == -1 ) {
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
        short ttl = _node.DefaultTTLFor( forwarder );
        ForwardedConnectTo(forwarder, nrtarget, ttl, struc_near);
      }
      if( nltarget != null && !nltarget.Equals(nrtarget) ) {
        NodeInfo target_info = (NodeInfo)add_to_neighbor[nltarget];
        ConnectToMessage ctm = (ConnectToMessage)neighbors_to_ctm[target_info];
        Address forwarder = ctm.Target.Address;
        short ttl = _node.DefaultTTLFor( forwarder );
        ForwardedConnectTo(forwarder, nltarget, ttl, struc_near);
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
        CheckForNearerNeighbors(ni, ltarget, out nltarget,
                                rtarget, out nrtarget);
      }
       /* 
       * We want to make sure not to hold the lock on ConnectionTable
       * while we try to make new connections
       */
      short f_ttl = 3; //2 would be enough, but 1 extra...
      if( nrtarget != null ) {
        ForwardedConnectTo(forwarder, nrtarget, f_ttl, struc_near);
      }
      if( nltarget != null && !nltarget.Equals(nrtarget) ) {
        ForwardedConnectTo(forwarder, nltarget, f_ttl , struc_near);
      }
    }

    /**
     * A helper function that handles the making Connectors
     * and setting up the ConnectToMessage
     */
    protected void ConnectTo(Address target, short t_ttl, string contype)
    {
      ConnectToOnEdge(target, _node, t_ttl, contype);
    }

    /**
     * A helper function that handles the making Connectors
     * and setting up the ConnectToMessage
     *
     * This returns immediately if we are already connected
     * to this node.
     */
    protected void ConnectToOnEdge(Address target, IPacketSender edge,
		                   short t_ttl, string contype)
    {
      ConnectionType mt = Connection.StringToMainType(contype);
      /*
       * This is an anonymous delegate which is called before
       * the Connector starts.  If it returns true, the Connector
       * will finish immediately without sending an ConnectToMessage
       */
      Linker l = new Linker(_node, target, null, contype);
      object linker_task = l.Task;  //This is what we check for
      Connector.AbortCheck abort = delegate(Connector c) {
        bool stop = false;
        lock( _node.ConnectionTable.SyncRoot ) {
          stop = _node.ConnectionTable.Contains( mt, target );
          if (!stop ) {
            /*
             * Make a linker to get the task.  We won't use
             * this linker.
             * No need in sending a ConnectToMessage if we
             * already have a linker going.
             */
            stop = _node.TaskQueue.HasTask( linker_task );
          }
        }
        return stop;
      };
      if ( abort(null) ) {
        return;
      }
      short t_hops = 0;
      if( edge is Edge ) {
	/*
	 * In this case we are bypassing the router and sending
	 * having the Connector talk directly to the neighbor
	 * using the edge.  We need to go ahead an increment
	 * the hops of the packet in order to make it the case
	 * the the packet has taken 1 hop by the time it arrives
	 */
        t_hops = 1;
      }
      //Send the 4 neighbors closest to this node:
      ArrayList nearest = _node.ConnectionTable.GetNearestTo(
							 (AHAddress)_node.Address, 4);
      NodeInfo[] near_ni = new NodeInfo[nearest.Count];
      int i = 0;
      foreach(Connection cons in nearest) {
	near_ni[i] = new NodeInfo(cons.Address, cons.Edge.RemoteTA);
	i++;
      }

      ConnectToMessage ctm =
        new ConnectToMessage(contype, _node.GetNodeInfo(8), near_ni);
      ctm.Id = _rand.Next(1, Int32.MaxValue);
      ctm.Dir = ConnectionMessage.Direction.Request;

      ushort options = AHPacket.AHOptions.AddClassDefault;
      if( contype == struc_short ) {
	/*
	 * We only want one node to get this packet, so
	 * we send it only the last node in the path.
	 */
        options = AHPacket.AHOptions.Last;
      }
      else {
        options = AHPacket.AHOptions.Annealing;
      }
      AHPacket ctm_pack =
        new AHPacket(t_hops, t_ttl, _node.Address, target, options,
                     AHPacket.Protocol.Connection, ctm.ToByteArray());
      Console.WriteLine("Size of CTM packet: {0}", ctm_pack.Length);

      #if DEBUG
      System.Console.WriteLine("In ConnectToOnEdge:");
      System.Console.WriteLine("Local:{0}", _node.Address);
      System.Console.WriteLine("Target:{0}", target);
      System.Console.WriteLine("Message ID:{0}", ctm.Id);
      #endif

      Connector con = new Connector(_node, edge, ctm_pack, ctm, this);
      con.AbortIf = abort;
      //Keep a reference to it does not go out of scope
      lock( _sync ) {
        _connectors.Add(con);
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
	  if( right_pos < _desired_neighbors ) {
            //We lost a close friend.
            Address target = new DirectionalAddress(DirectionalAddress.Direction.Right);
	    short ttl = (short)_desired_neighbors;
	    string contype = struc_near;
            ConnectTo(target, ttl, contype);
	  }
	  if( left_pos < _desired_neighbors ) {
            //We lost a close friend.
            Address target = new DirectionalAddress(DirectionalAddress.Direction.Left);
	    short ttl = (short)_desired_neighbors;
	    string contype = struc_near;
            ConnectTo(target, ttl, contype);
	  }
          if( c.ConType == struc_short ) {
            if( NeedShortcut ) {
              Address target = GetShortcutTarget(); 
	      short ttl = 1024;
	      string contype = struc_short;
              ConnectTo(target, ttl, contype);
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
    override public void HandleCtmResponse(Connector c, AHPacket resp_p,
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
    }
    /**
     * This method is called when there is a change in a Connection's status
     */
    protected void StatusChangedHandler(object connectiontable,EventArgs args)
    {
      //Console.WriteLine("Status Changed:\n{0}\n{1}\n{2}\n{3}",c, c.Status, nltarget, nrtarget);
      Connection c = ((ConnectionEventArgs)args).Connection; 
      ConnectToNearer(c.Address, c.Status.Neighbors);
    }
    
    /**
     * This is a helper function.
     */
    protected void ForwardedConnectTo(Address forwarder,
                                      Address target,
                                      short t_ttl, string contype)
    {
      IPacketSender f_sender = new ForwardingSender(_node, forwarder, 1);
      ConnectToOnEdge(target, f_sender, t_ttl, contype);
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
