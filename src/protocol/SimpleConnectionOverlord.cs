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

#define POB_DEBUG

using System;
using System.Collections;

namespace Brunet {

  /**
   * This is an attempt to write a simple version of
   * StructuredConnectionOverlord which is currently quite complex,
   * difficult to understand, and difficult to debug.
   */
  public class SimpleConnectionOverlord : ConnectionOverlord {
    
    public SimpleConnectionOverlord(Node n)
    {
      _node = n;
      _rand = new Random();
      _sync = new Object();
      _connectors = new ArrayList();
      _last_connection_time = DateTime.Now;
      lock( _sync ) {
      //Listen for connection events:
        _node.ConnectionTable.DisconnectionEvent +=
          new EventHandler(this.DisconnectHandler);
        _node.ConnectionTable.ConnectionEvent +=
          new EventHandler(this.ConnectHandler);      
#if TRIM 
      /**
       * Every heartbeat we assess the trimming situation.
       * If we have excess edges and it has been more than
       * _trim_wait_time heartbeats then we trim.
       */
        _node.HeartBeatEvent +=
          new EventHandler(this.CheckForTrimConditions);
#endif
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
     * @returns true if we have too few left neighbor connections
     */
    protected bool NeedLeftNeighbor {
      get {
#if false
	int left = 0;
	lock( _node.ConnectionTable.SyncRoot ) {
          foreach(Connection c in _node.ConnectionTable.GetConnections("structured.near")) {
            if( LeftPosition((AHAddress) c.Address) < _desired_neighbors ) {
              //This is left neighbor:
	      left++; 
	    }
	  }
	}
        return (left < _desired_neighbors);
#else
	//There seems to be no good way to be sure that a connection is to the left
	//or right (especially in small networks).  As such, for now, we just
	//define the condition of needing neighbors as having less than the total you need:
        int nears = 0;	
	lock( _node.ConnectionTable.SyncRoot ) {
          foreach(Connection c in _node.ConnectionTable.GetConnections("structured.near")) {
	    nears++; 
	  }
	}
	return (nears < 2 * _desired_neighbors );
#endif
      }
    }

    /**
     * @returns true if we have too few right neighbor connections
     */
    protected bool NeedRightNeighbor {
      get {
#if false
	int right = 0;
	lock( _node.ConnectionTable.SyncRoot ) {
          foreach(Connection c in _node.ConnectionTable.GetConnections("structured.near")) {
            if( RightPosition((AHAddress) c.Address) < _desired_neighbors ) {
              //This is left neighbor:
	      right++; 
	    }
	  }
	}
        return (right < _desired_neighbors);
#else
	//There seems to be no good way to be sure that a connection is to the left
	//or right (especially in small networks).  As such, for now, we just
	//define the condition of needing neighbors as having less than the total you need:
        int nears = 0;	
	lock( _node.ConnectionTable.SyncRoot ) {
          foreach(Connection c in _node.ConnectionTable.GetConnections("structured.near")) {
	    nears++; 
	  }
	}
	return (nears < 2 * _desired_neighbors );
#endif
      }
    }
    
    /**
     * @returns true if we have too few right shortcut connections
     */
    protected bool NeedShortcut {
      get {
	int shortcuts = 0;
	lock( _node.ConnectionTable.SyncRoot ) {
          foreach(Connection c in _node.ConnectionTable.GetConnections("structured.shortcut")) {
            shortcuts++;
	  }
	}
        return (shortcuts < _desired_shortcuts);
      }
    }
    
    ///////////////// Methods //////////////////////
    
    /**
     * Starts the Overlord if we are active
     *
     * When the Connector finishes.
     */
    public override void Activate() {
      if( IsActive == false ) {
        return;
      }
      ConnectionTable tab = _node.ConnectionTable;
      //If we are going to connect to someone, this is how we
      //know who to use
      Address forwarder = null;
      Address target = null;
      short ttl = -1;
      string contype = "";
      
      lock( tab.SyncRoot ) {
	int structured_count = tab.Count(ConnectionType.Structured);

	if( structured_count < 2 ) {
          //We don't have enough connections to guarantee a connected
	  //graph.  Use a leaf connection to get another connection
	  Connection leaf = null;
	  do {
            leaf = tab.GetRandom(ConnectionType.Leaf);
	  }
	  while( leaf != null &&
	         tab.Count( ConnectionType.Leaf ) > 1 &&
		 tab.Contains( ConnectionType.Structured, leaf.Address ) );
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
	    contype = "structured.near";
	  }
	}
	else {
          //We have enough structured connections to ignore the leafs
#if false
          if( NeedLeftNeighbor ) {
            target = new DirectionalAddress(DirectionalAddress.Direction.Left);
	    ttl = (short)_desired_neighbors;
	    contype = "structured.near";
          }
	  else if( NeedRightNeighbor ) {
            target = new DirectionalAddress(DirectionalAddress.Direction.Right);
	    ttl = (short)_desired_neighbors;
	    contype = "structured.near";
          }
	  else 
#endif
	    if( NeedShortcut ) {
            target = GetShortcutTarget(); 
	    ttl = 1024;
	    contype = "structured.shortcut";
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
    }

    /**
     * Every heartbeat we take a look to see if we should trim
     *
     * We only trim one at a time.
     */
    protected void CheckForTrimConditions(object node, EventArgs eargs)
    {
#if POB_DEBUG
      Console.WriteLine("In Check for Trim");
#endif
      lock( _sync ) {
        TimeSpan elapsed = DateTime.Now - _last_connection_time;
	if( elapsed.TotalSeconds >= _trim_delay ) {
#if POB_DEBUG
           Console.WriteLine("Go for Trim");
#endif
          //We may be able to trim connections.
            //There is no reason to trim when we need connections.
            ConnectionTable tab = _node.ConnectionTable;
	    int not_near = 0;
	    int nears = 0;
	    ArrayList sc_trim_candidates = new ArrayList();
	    ArrayList near_trim_candidates = new ArrayList();
	    lock( tab.SyncRoot ) {
	      
	      foreach(Connection c in tab.GetConnections("structured.shortcut")) {
                sc_trim_candidates.Add(c);  
	      }
	      foreach(Connection c in tab.GetConnections("structured.near")) {
                int right_pos = RightPosition((AHAddress)c.Address);
                int left_pos = LeftPosition((AHAddress)c.Address);
	    
	        if( right_pos > 2 * _desired_neighbors &&
		    left_pos > 2 * _desired_neighbors ) {
	          //These are near neighbors that are not so near
	          near_trim_candidates.Add(c);
		}
                
	      }
	    }//End of ConnectionTable lock

	    bool sc_needs_trim = (sc_trim_candidates.Count > _desired_shortcuts);
	    bool near_needs_trim = (near_trim_candidates.Count > 0);
	    /*
	     * Prefer to trim near neighbors that are unneeded, since
	     * they are not as useful for routing
	     * If there are no unneeded near neighbors, then
	     * consider trimming the shortcuts
	     */
	    if( near_needs_trim ) {
	      //Delete a random trim candidate:
	      int idx = _rand.Next( near_trim_candidates.Count );
	      Connection to_trim = (Connection)near_trim_candidates[idx];
#if POB_DEBUG
              Console.WriteLine("Attempt to trim Near: {0}", to_trim);
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
              Console.WriteLine("Attempt to trim Shortcut: {0}", to_trim);
#endif
	      _node.GracefullyClose( to_trim.Edge );
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
	    
      lock( _sync ) { _last_connection_time = DateTime.Now; }
      if( IsActive == false ) {
        return;
      }
      ConnectionEventArgs args = (ConnectionEventArgs)eargs;
      Connection new_con = args.Connection;
      bool connect_left = false;
      bool connect_right = false;
      
      if( new_con.MainType == ConnectionType.Leaf ) {
	/*
	 * We just got a leaf.  Try to use it to get a shortcut.near
	 */
	Address target = GetSelfTarget();
	short ttl = 1024;
	//This is a near neighbor connection
	string contype = "structured.near";
	if( ! target.Equals( new_con.Address ) ) {
	  //As long as the forwarder is not the target, forward
          ForwardedConnectTo(new_con.Address, target, ttl, contype);
	}
	else {
          ConnectTo(target, ttl, contype);
	}
      }
      else if( new_con.MainType == ConnectionType.Structured ) {
       ConnectionTable tab = _node.ConnectionTable;
       lock( tab.SyncRoot ) {
        int left_pos = LeftPosition((AHAddress)new_con.Address);
        int right_pos = RightPosition((AHAddress)new_con.Address); 

	if( left_pos < _desired_neighbors ) {
        //This is a new left neighbor
          connect_right = true;
	  if( left_pos < _desired_neighbors - 1) { 
            connect_left = true;
	  }
	}
	if( right_pos < _desired_neighbors ) {
        //This is a new right neighbor
          connect_left = true;
	  if( right_pos < _desired_neighbors - 1) { 
            connect_right = true;
	  }
	}
	
	if( left_pos >= _desired_neighbors && right_pos >= _desired_neighbors ) {
        //This looks like a shortcut
	
	}

	//See if any of this guy's neighbors should be our neighbors:
	BigInteger ldist = 0;
	BigInteger rdist = 0;
	AHAddress local = (AHAddress)_node.Address;
	foreach(NodeInfo ni in new_con.Status.Neighbors) {
          int n_left = LeftPosition( (AHAddress)ni.Address);
          int n_right = RightPosition( (AHAddress)ni.Address);
	  if( left_pos < _desired_neighbors || right_pos < _desired_neighbors ) {
            //We should connect to this node! if we are not already:
	    if( !tab.Contains(ConnectionType.Structured, ni.Address) ) {
	      AHAddress adr = (AHAddress)ni.Address;
	      BigInteger adr_dist = local.LeftDistanceTo(adr);
	      if( adr_dist < ldist || ldist == 0 ) {
                ldist = adr_dist;
		ltarget = ni.Address;
	      }
	      adr_dist = local.RightDistanceTo(adr);
	      if( adr_dist < rdist || rdist == 0 ) {
                rdist = adr_dist;
		rtarget = ni.Address;
	      }
	    }
	  }
	}//We have looked at all his neighbors
       }
      }//Release the lock on the connection_table
      
      /* 
       * We want to make sure not to hold the lock on ConnectionTable
       * while we try to make new connections
       */
      if( rtarget != null ) {
        ForwardedConnectTo(new_con.Address, rtarget, 3, "structured.near");
      }
      if( ltarget != null ) {
        ForwardedConnectTo(new_con.Address, ltarget, 3, "structured.near");
      }
      //We also send directional messages.  In the future we may find this
      //to be unnecessary
      ///@todo evaluate the performance impact of this:
      if( connect_right ) {
        ConnectToOnEdge(new DirectionalAddress(DirectionalAddress.Direction.Right),
                        new_con.Edge, 1, "structured.near"); 
      }
      if( connect_left ) {
        ConnectToOnEdge(new DirectionalAddress(DirectionalAddress.Direction.Left),
                        new_con.Edge, 1, "structured.near"); 
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
      Activate();
    }

    /**
     * A helper function that handles the making Connectors
     * and setting up the ConnectToMessage
     */
    protected void ConnectTo(Address target, short t_ttl, string contype)
    {
      short t_hops = 0;
      ConnectToMessage ctm =
        new ConnectToMessage(contype, new NodeInfo( _node.Address, _node.LocalTAs )  );
      ctm.Id = _rand.Next(1, Int32.MaxValue);
      ctm.Dir = ConnectionMessage.Direction.Request;

      AHPacket ctm_pack =
        new AHPacket(t_hops, t_ttl, _node.Address, target,
                     AHPacket.Protocol.Connection, ctm.ToByteArray());

      #if DEBUG
      System.Console.WriteLine("In ConnectTo:");
      System.Console.WriteLine("Local:{0}", _node.Address);
      System.Console.WriteLine("Target:{0}", target);
      System.Console.WriteLine("Message ID:{0}", ctm.Id);
      #endif

      Connector con = new Connector(_node);
      //Keep a reference to it does not go out of scope
      lock( _sync ) {
        _connectors.Add(con);
      }
      con.FinishEvent += new EventHandler(this.ConnectorEndHandler);
      con.Connect(ctm_pack, ctm.Id);
#if PLAB_CTM_LOG
      this.Logger.LogCTMEvent(target);
#endif
    }

    /**
     * A helper function that handles the making Connectors
     * and setting up the ConnectToMessage
     */
    protected void ConnectToOnEdge(Address target, Edge edge, short t_ttl, string contype)
    {
      short t_hops = 1;
      ConnectToMessage ctm =
        new ConnectToMessage(contype, new NodeInfo(_node.Address, _node.LocalTAs) );
      ctm.Id = _rand.Next(1, Int32.MaxValue);
      ctm.Dir = ConnectionMessage.Direction.Request;

      AHPacket ctm_pack =
        new AHPacket(t_hops, t_ttl, _node.Address, target,
                     AHPacket.Protocol.Connection, ctm.ToByteArray());

      #if DEBUG
      System.Console.WriteLine("In ConnectToOnEdge:");
      System.Console.WriteLine("Local:{0}", _node.Address);
      System.Console.WriteLine("Target:{0}", target);
      System.Console.WriteLine("Message ID:{0}", ctm.Id);
      #endif

      Connector con = new Connector(_node);
      //Keep a reference to it does not go out of scope
      lock( _sync ) {
        _connectors.Add(con);
      }
      con.FinishEvent += new EventHandler(this.ConnectorEndHandler);
      con.Connect(edge, ctm_pack, ctm.Id);
#if PLAB_CTM_LOG
      this.Logger.LogCTMEvent(target);
#endif
    }
    
    /**
     * This method is called when there is a Disconnection from
     * the ConnectionTable
     */
    protected void DisconnectHandler(object connectiontable, EventArgs args)
    { 
      lock ( _sync ) { _last_connection_time = DateTime.Now; }
      Connection c = ((ConnectionEventArgs)args).Connection;

      if( IsActive ) {
        if( c.MainType == ConnectionType.Structured ) {
          int right_pos = RightPosition((AHAddress)c.Address);
          int left_pos = LeftPosition((AHAddress)c.Address);
	  if( right_pos < _desired_neighbors ) {
            //We lost a close friend.
            Address target = new DirectionalAddress(DirectionalAddress.Direction.Right);
	    short ttl = (short)_desired_neighbors;
	    string contype = "structured.near";
            ConnectTo(target, ttl, contype);
	  }
	  if( left_pos < _desired_neighbors ) {
            //We lost a close friend.
            Address target = new DirectionalAddress(DirectionalAddress.Direction.Left);
	    short ttl = (short)_desired_neighbors;
	    string contype = "structured.near";
            ConnectTo(target, ttl, contype);
	  }
          if( c.ConType == "structured.shortcut" ) {
            if( NeedShortcut ) {
              Address target = GetShortcutTarget(); 
	      short ttl = 1024;
	      string contype = "structured.shortcut";
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
     * This is a helper function.
     */
    protected void ForwardedConnectTo(Address forwarder,
                                      Address target,
                                      short t_ttl, string contype)
    {
      ConnectToMessage ctm = new ConnectToMessage(contype,
                              new NodeInfo( _node.Address, _node.LocalTAs) );
      ctm.Dir = ConnectionMessage.Direction.Request;
      ctm.Id = _rand.Next(1, Int32.MaxValue);
      short t_hops = 0;
      //This is the packet we wish we could send: local -> target
      AHPacket ctm_pack = new AHPacket(t_hops,
                                       t_ttl,
                                       _node.Address,
                                       target, AHPacket.Protocol.Connection,
                                       ctm.ToByteArray() );
      //We now have a packet that goes from local->forwarder, forwarder->target
      AHPacket forward_pack = PacketForwarder.WrapPacket(forwarder, 1, ctm_pack);

      #if DEBUG
      System.Console.WriteLine("In ForwardedConnectTo:");
      System.Console.WriteLine("Local:{0}", _node.Address);
      System.Console.WriteLine("Target:{0}", target);
      System.Console.WriteLine("Message ID:{0}", ctm.Id);
      #endif

      Connector con = new Connector(_node);
      //Keep a reference to it does not go out of scope
      lock( _sync ) {
        _connectors.Add(con);
      }
      con.FinishEvent += new EventHandler(this.ConnectorEndHandler);
      con.Connect(forward_pack, ctm.Id);
#if PLAB_CTM_LOG
      this.Logger.LogCTMEvent(target);
#endif

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
      // Random distance from 2^1 - 2^159 (1/d distributed)
      int rand_exponent = _rand.Next(1, 159);
      BigInteger rand_dist = new BigInteger(2);
      rand_dist <<= (rand_exponent - 1);

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
      BigInteger addr_dist = local.LeftDistanceTo(addr);
      //Don't let the Table change while we do this:
      ConnectionTable tab = _node.ConnectionTable;
      int closer_count = 0;
      lock( tab.SyncRoot ) {
        foreach(Connection c in tab.GetConnections(ConnectionType.Structured)) {
          AHAddress c_addr = (AHAddress)c.Address;
	  if( local.LeftDistanceTo( c_addr ) < addr_dist ) {
            closer_count++;
	  }
	}
      }
      return closer_count;
    }
    /**
     * Given an address, we see how many of our connections
     * are closer than this address to the right.
     */
    protected int RightPosition(AHAddress addr)
    {
      AHAddress local = (AHAddress)_node.Address;
      BigInteger addr_dist = local.RightDistanceTo(addr);
      //Don't let the Table change while we do this:
      ConnectionTable tab = _node.ConnectionTable;
      int closer_count = 0;
      lock( tab.SyncRoot ) {
        foreach(Connection c in tab.GetConnections(ConnectionType.Structured)) {
          AHAddress c_addr = (AHAddress)c.Address;
	  if( local.RightDistanceTo( c_addr ) < addr_dist ) {
            closer_count++;
	  }
	}
      }
      return closer_count;
    }
    
  }

}
