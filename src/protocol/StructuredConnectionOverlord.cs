/*
 * Dependencies : 
 Brunet.Address;
 Brunet.AHAddress;
 Brunet.AHAddressComparer;
 Brunet.AHPacket;
 Brunet.BigInteger;
 Brunet.ConnectionType;
 Brunet.ConnectionTable;
 Brunet.ConnectionOverlord
 Brunet.Connector
 Brunet.ConnectToMessage
 Brunet.ConnectionMessage
 Brunet.CloseMessage
 Brunet.ConnectionPacket
 Brunet.TransportAddress;
 Brunet.DirectionalAddress;
 Brunet.Edge;
 Brunet.Node;
 Brunet.StructuredAddress;
 Brunet.PacketForwarder;
 Brunet.ConnectionEventArgs;
 */

// #define DEBUG

using System;
using System.Collections;
//using log4net;
namespace Brunet
{

  /**
   * Manages the ordered Address, Edge table which
   * is used to find the Edge which is closest
   * to the destination Address
   *
   * This is only for Structured Addresses, or subclasses
   * of AHAddress
   *
   */

  public class StructuredConnectionOverlord:ConnectionOverlord
  {
    /*private static readonly log4net.ILog log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/
    protected Node _local;
    ///The local TransportAddress objects we can use to get connections

    protected ConnectionTable _connection_table;
    /**
     * The number of desired neighbor connections.  Same value for left and 
     * right.
     */
    static protected readonly short _total_desired_neighbors = 2;

    /**
     * The number of desired shortcut connections.
     */
    static protected readonly short _total_desired_shortcuts = 0;

    /**
     * These are the edges which are shortcuts.  There should
     * be less shortcuts than neighbors, so any edge that is not
     * in the list, must be a neighbor.
     *
     * This is a first draft at trying to make sure neighbors
     * don't "become" shortcuts simply by virtue of more neighbors
     * joining.  This could confuse the statistics of the bona fide
     * shortcuts.
     *
     * Also, we need to close "old" neighbors that are displaced by
     * closer neighbors (say when we have twice as many as we need).
     * @todo deal with the situation where we get too many neighbors
     */
    protected ArrayList _shortcut_edges;

    /**
     * These are all the connectors that are currently working.
     * When they are done, their FinishEvent is fired, and we
     * will remove them from this list.  This makes sure they
     * are not garbage collected.
     */
    protected ArrayList _connectors;

    /**
     * When we trim an edge, we add it to this list.
     * When the disconnect event is fired, we remove
     * it from the list.
     */
    protected ArrayList _trimmed_edges;

    protected Random _rand;
    //This is the last leaf address we used to bootstrap
    protected Address _last_leaf;

    /**
     * An object we lock to get thread synchronization
     */
    protected object _sync;
    /**
     * @param a the address of the node in which the AHRoutingTable is located
     */
    public StructuredConnectionOverlord(Node local)
    {
      _local = local;
      _last_leaf = null;
      _compensate = true;
      _connection_table = _local.ConnectionTable;
      _shortcut_edges = new ArrayList();
      _trimmed_edges = new ArrayList();
      //Try to make sure each SCO has a different seed:
      int seed = GetHashCode() ^ local.GetHashCode()
                 ^ DateTime.Now.Millisecond;
      _rand = new Random(seed);

      _connectors = new ArrayList();

      _sync = new Object();
      lock( _sync ) {
        _local.ConnectionTable.DisconnectionEvent +=
          new EventHandler(this.CheckAndDisconnectHandler);
        _local.ConnectionTable.ConnectionEvent +=
          new EventHandler(this.CheckAndConnectHandler);

      }
    }

    protected bool _compensate;
    /**
     * If we start compensating, we check to see if we need to
     * make new neighbor or shortcut connections : 
     */
    override public bool IsActive
    {
      get
      {
        return _compensate;
      }
      set
      {
        _compensate = value;
      }
    }

    protected bool _initiated_shortcut = false;
    /**
     * Shows wether we have tried to establish a shortcut to a random address. 
     * Due to race conditions certain short-distance connections may be classified
     * as shortcuts. We want to make sure that every node has tried to get at least one
     * real shortcut in order to avoid the possibility of fake shortcuts preventing nodes
     * from getting at least one real shortcut. It is important to ensure that nodes try
     * to get at least one real shortcut in order to guarantee the desired performance 
     * of the network.
     */
    public bool TriedShortcut
    {
      get
      {
        return _initiated_shortcut;
      }
      set
      {
        _initiated_shortcut = value;
      }
    }

    /**
    * These are the structured types.  This provides convenient book-keeping
    * for the structuredconnectionoverlord.
    */
    public enum StructuredType
    {
      None,
      RightNeighbor,              //Neighbor to the right.
      LeftNeighbor,               //Neighbor to the left.
      Self,                       //Self.
      Shortcut                    //Shortcut.
    }

  public enum StructuredRingDirection:int
    {
      // Left is "clockwise", or increasing in numerical address
      Left = +1,
      // Right is "counterclockwise", or decreasing
      Right = -1
    }

    // This handles the Finish event from the connectors created in SCO.
    public void ConnectionEndHandler(object connector, EventArgs args)
    {
      lock( _sync ) {
        _connectors.Remove(connector);
      }
      //log.Info("ended connection attempt: node: " + _local.Address.ToString() );
      Activate();
    }

    /**
     * This method sends a forwarded ConnectTo message using leaf
     * as the forwarder.  We target our own address.  This helps us
     * bootstrap by finding the nodes closest to us on the other
     * side of each leaf
     * @param leaf The address of the leaf node to forward through
     */
    protected void ConnectToSelfUsing(Address leaf)
    {
      /**
       * try to get at least one neighbor using forwarding through the 
       * leaf .  The forwarded address is 2 larger than the address of
       * the new node that is getting connected.
       */
      BigInteger local_int_add = _local.Address.ToBigInteger();
      //must have even addresses so increment twice
      local_int_add += 2;
      //Make sure we don't overflow:
      BigInteger tbi = new BigInteger(local_int_add % Address.Full);
      AHAddress target = new AHAddress(tbi);

      short ttl = 1024; //160; //We use a large ttl to reach remote addresses
      if( leaf.Equals(target) ) {
        //This is dumb luck, we have no need to forward:
        ConnectTo(target, ttl);
      }
      else {
        //we try to get this leaf to forward to the correct node
        ForwardedConnectTo(leaf, target, ttl);
      }
    }

    /**
     * Get the index following the given one in the specified direction along
     * the structured ring.
     */
    static int GetNextIndex(int current_index, int total, StructuredRingDirection direction)
    {
      if (direction==StructuredRingDirection.Right) {
        if (current_index==0) {
          return total-1;
        }
        else {
          return --current_index;
        }
      }
      else if (direction==StructuredRingDirection.Left) {
        if (current_index==(total-1)) {
          return 0;
        }
        else {
          return ++current_index;
        }
      }

      //  should not be here
      return -1;
    }

    /**
     * finds the boundary index, *after* which going in the given direction until end, it is ok to close
     * neighbor(not shortcut) connections. this method should be called twice for finding the boundary
     * to the left and to the right. 
     */  
    public int OptimalNeighborsBoundary(int start, int end, int total,
                                        StructuredRingDirection direction)
    {
      int result = start;
      lock( _connection_table.SyncRoot ) {
        ArrayList structured_edges =_connection_table.GetEdgesOfType(ConnectionType.Structured);

        int next_index = start;
        int total_neighbors = 0;
        bool more = true;
        try {
          do {
            more = (next_index!=end);

            Edge next_edge = (Edge)structured_edges[next_index];
            // first check if this is a shortcut
            int sc_idx = _shortcut_edges.IndexOf( next_edge );
            if( sc_idx >= 0 ) {
              //This is a shortcut:
              if (more) {
                next_index = GetNextIndex(next_index, total, direction);
              }
            }
            else {
              total_neighbors++;
              result=next_index;
              next_index = GetNextIndex(next_index, total, direction);
              more = more && (total_neighbors<_total_desired_neighbors);
            }
          } while (more);
        } catch (Exception e) {
          System.Console.WriteLine("EXCEPTION IN OptimalNeighborsBoundary:{0}", e);
        }
      }
      return result;
    }

    /**
     * Get all the neighbors(not shortcuts) between start and end inclusive and put them in an array list
     */
    public ArrayList EdgesToRemove(int start, int end, int total,
                                   StructuredRingDirection direction)
    {
      ArrayList result = new ArrayList();
      lock( _connection_table.SyncRoot ) {
        ArrayList structured_edges =_connection_table.GetEdgesOfType(ConnectionType.Structured);
        int total_neighbors = 0;
        int next_index = start;
        bool more = true;

        try {
          do {
            more = (next_index!=end);

            Edge next_edge = (Edge)structured_edges[next_index];
            // first check if this is a shortcut
            int sc_idx = _shortcut_edges.IndexOf( next_edge );

            if( sc_idx >= 0 ) {
              //This is a shortcut:
              if (more) {
                next_index = GetNextIndex(next_index, total, direction);
              }
            } else {
              result.Add(next_edge);
              next_index = GetNextIndex(next_index, total, direction);
            }
          } while (more);
        } catch (Exception e) {
          System.Console.WriteLine("EXCEPTION IN EdgesToRemove: {0}", e);
        }

      }
      return result;
    }

    /**
     * Check the list of structured connections and remove and
     * if necessary close the ones that are not among the desired
     * immediate left or right neighbors.
     */
    public void TrimStructuredConnections()
    {
      ArrayList edges_to_remove;
      lock( _connection_table.SyncRoot ) {
        int total_structured = _connection_table.Count(ConnectionType.Structured);
        int total_shortcuts = _shortcut_edges.Count;
        int total_neighbors = total_structured - total_shortcuts;

        if (total_neighbors<=(2*_total_desired_neighbors)) {
          return;
        }

        int self_idx, left_start, left_end, right_start, right_end;
        int left_boundary, right_boundary;
        self_idx = _connection_table.IndexOf(ConnectionType.Structured,_local.Address);
        if (self_idx<0) {
          self_idx=~self_idx;
        }

        if (self_idx<total_structured) {
          right_start = GetNextIndex(self_idx, total_structured, StructuredRingDirection.Right);
          right_end   = self_idx;
          left_start  = self_idx;
          left_end    = GetNextIndex(self_idx, total_structured, StructuredRingDirection.Right);//yes,both are right
        }
        else {
          right_start = total_structured-1;
          right_end   = 0;
          left_start  = 0;
          left_end    = total_structured-1;
        }

        left_boundary = OptimalNeighborsBoundary(left_start,
                        left_end,
                        total_structured,
                        StructuredRingDirection.Left);
        right_boundary = OptimalNeighborsBoundary(right_start,
                         right_end,
                         total_structured,
                         StructuredRingDirection.Right);

        int remove_start = GetNextIndex(left_boundary,
                                        total_structured,
                                        StructuredRingDirection.Left);
        int remove_end = GetNextIndex(right_boundary,
                                      total_structured,
                                      StructuredRingDirection.Right);

        if ( remove_start>left_end ) {
          if ( (remove_end > left_end) && (remove_end < remove_start) ) return;
          edges_to_remove = EdgesToRemove(remove_start,
                                          remove_end,
                                          total_structured,
                                          StructuredRingDirection.Left);
        }
        else {
          if (remove_start > remove_end) return;
          edges_to_remove = EdgesToRemove(remove_start,
                                          remove_end,
                                          total_structured,
                                          StructuredRingDirection.Left);
        }
      }
      //Release the lock on the connection table before calling external functions
      foreach(Edge e in edges_to_remove) {
        /**
        * We note all the edges which we are trimming.
        * This prevents us from reacting to its closure
        */
        lock( _sync ) {
          _trimmed_edges.Add(e);
        }
        _local.GracefullyClose(e);
        Console.WriteLine("{0} Trimming: {1}", _local.Address, e);
      }
    }

    /**
     * When new ConnectionEvents occur, this method is called.
     * Usually, some action will need to be taken.
     * @see ConnectionTable
     */
    public void CheckAndConnectHandler(object connectiontable,
                                       EventArgs args)
    {
      ConnectionEventArgs conargs = (ConnectionEventArgs)args;

      if (conargs.ConnectionType == ConnectionType.Leaf ) {
        /**
        * When there is a new leaf connection it may be connecting
        * a previously disconnected part of the network.  To make
        * sure the ring has the proper structure, we try a forwarded
        * connectTo our own address:
        */
        if( _compensate ) {
          ConnectToSelfUsing( conargs.RemoteAddress );
        }
      }
      else if (conargs.ConnectionType == ConnectionType.Structured ) {
        /**
         * If this is a new structured connection, see if it
         * was a neighbor, or shortcut, and increase the count
         */
        // do book-keeping for added edge
        int left_distance, right_distance, shortest_dist;
        GetIdxDistancesTo(conargs.Index, out left_distance, out right_distance);
        shortest_dist = System.Math.Min(left_distance, right_distance);
        bool is_boundary = (shortest_dist == _total_desired_neighbors);
        StructuredType st;

        if( shortest_dist > _total_desired_neighbors ) {
          /**
          * This is not close enough to be a neighbor, so we
          * consider it a shortcut.  All we need to do is add
          * it to our list of shortcuts:
          */
          lock( _sync ) {
            _shortcut_edges.Add( conargs.Edge );
          }
          st = StructuredType.Shortcut;
        }
        else if( left_distance == shortest_dist ) {
          st = StructuredType.LeftNeighbor;
        }
        else {
          st = StructuredType.RightNeighbor;
        }

        if( _compensate ) {
          short ttl = 2; //We look for neighbors of neighbors
          if( st == StructuredType.RightNeighbor ) {
            ConnectToOnEdge(new DirectionalAddress(DirectionalAddress.Direction.Left),
                            conargs.Edge,
                            ttl);
            if( ! is_boundary ) {
              ConnectToOnEdge(new DirectionalAddress(DirectionalAddress.Direction.Right),
                              conargs.Edge,
                              ttl);
            }
          }
          else if( st == StructuredType.LeftNeighbor ) {
            ConnectToOnEdge(new DirectionalAddress(DirectionalAddress.Direction.Right),
                            conargs.Edge,
                            ttl);
            if( ! is_boundary ) {
              ConnectToOnEdge(new DirectionalAddress(DirectionalAddress.Direction.Left),
                              conargs.Edge,
                              ttl);
            }

            int total_structured = _connection_table.Count(ConnectionType.Structured);
            int total_shortcuts  = _shortcut_edges.Count;
            int total_neighbors  = total_structured - total_shortcuts;

            if ( (total_neighbors >= 2*_total_desired_neighbors) &&
                 ( (total_shortcuts < _total_desired_shortcuts) || (!TriedShortcut) ) )  {
              GetShortcut();
            }
          }
          else if (st == StructuredType.Shortcut) {
            /*
            * When we get a new shortcut, we don't react.
            * Above we added this shortcut to our list, but that's it.
            */
          }

          if (st!=StructuredType.Shortcut) {
            /**
             * When we get a neighbor connection we check to see if we have
             * too many neighbor connections and if so, we trim some of them
             */
            TrimStructuredConnections();
          }
          else {
            // no trimming of shortcuts for now
          }
          //System.Console.ReadLine();
        }
      }

    }

    /**
     * This method is called on the DisconnectionEvent.  @see ConnectionTable.
     * 
     * If we lost a connection, we may need to replace it.  This
     * code does so.
     */
    public void CheckAndDisconnectHandler(object connectiontable,
                                          EventArgs args)
    {
      ConnectionEventArgs conargs = (ConnectionEventArgs)args;
      if ( conargs.ConnectionType == ConnectionType.Structured )
      {
        // do book-keeping for added edge
        bool is_shortcut = false;
        bool need_shortcut = false;
        lock( _sync ) {
          int sc_idx = _shortcut_edges.IndexOf( conargs.Edge );
          if( sc_idx >= 0 ) {
            /**
            * When we loose a Shortcut, we remove it from our list
            * and if we no longer have enough, we will attempt to
            * get a new one
            */
            _shortcut_edges.RemoveAt(sc_idx);
            is_shortcut = true;
            if( _shortcut_edges.Count < _total_desired_shortcuts ) {
              need_shortcut = true;
            }
          }
        }
        if ( is_shortcut ) {
          if( need_shortcut ) {
            GetShortcut();
          }
        }
        else {
          //Else it was a neighbor.
          /**
           * Anytime we loose a neighbor (which we did not Close)
           * we connect to the node (_total_desired_neighbors) hops
           * in the direction of the lost node
           */
          bool was_not_trimmed = true;
          lock( _sync ) {
            int idx =_trimmed_edges.IndexOf( conargs.Edge );
            if( idx >= 0 ) {
              /*
               * This edge was trimmed.  No need to compensate for it
               */
              was_not_trimmed = false;
              _trimmed_edges.RemoveAt(idx);
            }
            else {
              was_not_trimmed = true;
            }
          }
          //If the edge was not trimmed, and we are compensating:
          if( was_not_trimmed && _compensate ) {
            int left_distance, right_distance, shortest_dist;
            GetIdxDistancesTo(conargs.Index, out left_distance, out right_distance);
            shortest_dist = System.Math.Min(left_distance, right_distance);

            // review again
            if (shortest_dist > _total_desired_neighbors) return;

            if( shortest_dist == left_distance ) {
              ConnectTo(new DirectionalAddress(DirectionalAddress.Direction.Left),
                        _total_desired_neighbors);
            }
            else {
              ConnectTo(new DirectionalAddress(DirectionalAddress.Direction.Right),
                        _total_desired_neighbors);
            }
          }
        }
      }
    }

    protected void ForwardedConnectTo(Address forwarder,
                                      Address target,
                                      short t_ttl)
    {
      ConnectToMessage ctm = new ConnectToMessage(ConnectionType.Structured,
                             _local.Address, _local.LocalTAs);
      ctm.Dir = ConnectionMessage.Direction.Request;
      ctm.Id = _rand.Next(1, Int32.MaxValue);
      short t_hops = 0;
      //This is the packet we wish we could send: local -> target
      AHPacket ctm_pack = new AHPacket(t_hops,
                                       t_ttl,
                                       _local.Address,
                                       target, AHPacket.Protocol.Connection,
                                       ctm.ToByteArray() );
      //We now have a packet that goes from local->forwarder, forwarder->target
      AHPacket forward_pack = PacketForwarder.WrapPacket(forwarder, 1, ctm_pack);

      #if DEBUG
      System.Console.WriteLine("In ForwardedConnectTo:");
      System.Console.WriteLine("Local:{0}", _local.Address);
      System.Console.WriteLine("Target:{0}", target);
      System.Console.WriteLine("Message ID:{0}", ctm.Id);
      #endif

      Connector con = new Connector(_local);
      //Keep a reference to it does not go out of scope
      lock( _sync ) {
        _connectors.Add(con);
      }
      con.FinishEvent += new EventHandler(this.ConnectionEndHandler);
      con.Connect(forward_pack, ctm.Id);
    }

    protected void ConnectTo(Address target, short t_ttl)
    {
      short t_hops = 0;
      ConnectToMessage ctm =
        new ConnectToMessage(ConnectionType.Structured, _local.Address,
                             _local.LocalTAs);
      ctm.Id = _rand.Next(1, Int32.MaxValue);
      ctm.Dir = ConnectionMessage.Direction.Request;

      AHPacket ctm_pack =
        new AHPacket(t_hops, t_ttl, _local.Address, target,
                     AHPacket.Protocol.Connection, ctm.ToByteArray());

      #if DEBUG
      System.Console.WriteLine("In ConnectTo:");
      System.Console.WriteLine("Local:{0}", _local.Address);
      System.Console.WriteLine("Target:{0}", target);
      System.Console.WriteLine("Message ID:{0}", ctm.Id);
      #endif

      Connector con = new Connector(_local);
      //Keep a reference to it does not go out of scope
      lock( _sync ) {
        _connectors.Add(con);
      }
      con.FinishEvent += new EventHandler(this.ConnectionEndHandler);
      con.Connect(ctm_pack, ctm.Id);
    }

    protected void ConnectToOnEdge(Address target, Edge edge, short t_ttl)
    {
      short t_hops = 1;
      ConnectToMessage ctm =
        new ConnectToMessage(ConnectionType.Structured, _local.Address,
                             _local.LocalTAs);
      ctm.Id = _rand.Next(1, Int32.MaxValue);
      ctm.Dir = ConnectionMessage.Direction.Request;

      AHPacket ctm_pack =
        new AHPacket(t_hops, t_ttl, _local.Address, target,
                     AHPacket.Protocol.Connection, ctm.ToByteArray());

      #if DEBUG
      System.Console.WriteLine("In ConnectToOnEdge:");
      System.Console.WriteLine("Local:{0}", _local.Address);
      System.Console.WriteLine("Target:{0}", target);
      System.Console.WriteLine("Message ID:{0}", ctm.Id);
      #endif

      Connector con = new Connector(_local);
      //Keep a reference to it does not go out of scope
      lock( _sync ) {
        _connectors.Add(con);
      }
      con.FinishEvent += new EventHandler(this.ConnectionEndHandler);
      con.Connect(edge, ctm_pack, ctm.Id);
    }

    /**
     * add a new shortcut to the left or right with equal probability 
     */
    public void GetShortcut()
    {
      /**
       * add a new shortcut to the left or right with equal probability 
       */

      // Random distance from 2^1 - 2^159 (1/d distributed)
      int rand_exponent = _rand.Next(1, 159);
      BigInteger rand_dist = new BigInteger(2);
      rand_dist <<= (rand_exponent - 1);

      // Add or subtract random distance to the current address
      BigInteger t_add = _local.Address.ToBigInteger();

      // Random number that is 0 or 1
      if( _rand.Next(2) == 0 ) {
        t_add += rand_dist;
      }
      else {
        t_add -= rand_dist;
      }

      BigInteger target_int = new BigInteger(t_add % Address.Full);

      AHAddress target = new AHAddress(target_int);
      short t_ttl = 1024; //160;

      #if DEBUG
      System.Console.WriteLine("---");
      System.Console.WriteLine("INITIATING A NEW SHORTCUT FROM {0} TO {1}", _local.Address, target);
      System.Console.WriteLine("---");
      #endif

      ConnectTo(target, t_ttl);
      TriedShortcut = true;
    }

    /**
     * Activate looks at all the connections we have, sees which we need most
     * and tries to get one of that type.  Once we get a connection, other
     * methods will take over.
     * 
     * This method is a general: get-started-again method.  If connections never
     * failed, this would never need to be called.  But since there can be failed
     * connections, we may sometimes need to trigger this method.
     *
     */
    override public void Activate()
    {
      int total_structured = _connection_table.Count(ConnectionType.Structured);
      int total_shortcuts = _shortcut_edges.Count;
      int total_neighbors = total_structured - total_shortcuts;
      //log.Info("Trying: " + _local.Address.ToString());
      //no book-keeping needed.  Enter connect loop.
      if (IsActive && NeedConnection) {
        if ( total_neighbors < 2 ) {
          /**
          * We have less than two neighbors
          * This is bad because a new network can have a bunch of
          * dimers, each with one neighbor, but no way to connect
          * to other nodes.  We fix this by continuing to use
          * leafs until we get at least 2 connections, and then
          * it is almost certain the graph is connected
          */
          if ( _connection_table.Count(ConnectionType.Leaf) < 1 ) {
            //log.Warn("We need connections, but have no leaves");
            ///do nothing. we must wait for a leaf node
          }
          else {
            /**
             * If we don't have 2 neighbors, try a random leaf connection.
             */
            Address leaf;
            lock( _connection_table.SyncRoot ) {
              /**
               * We start at a random leaf.  We then go through the ConnectionTable
               * until we find a leaf we are not connected to.
               */
              int size = _connection_table.Count(ConnectionType.Leaf);
              int lidx = _rand.Next( size );
              do {
                Edge edge;
                _connection_table.GetConnection(ConnectionType.Leaf,
                                                lidx,
                                                out leaf,
                                                out edge);
                lidx++;
                size--;
              }
              while( _connection_table.Contains(ConnectionType.Structured,leaf)
                     && (size > 0) );
            }
            ConnectToSelfUsing(leaf);
          }
        }
        else if( total_neighbors < 2 * _total_desired_neighbors )
        {
          /**
           * When we don't have enough neighbors we try to connect to
           * the right and to the left.
           * @todo find which we have more of, left or right neighbors.
           */
          DirectionalAddress target;
          /// send a left CTM packet
          target = new DirectionalAddress(DirectionalAddress.Direction.Left);
          ConnectTo(target, _total_desired_neighbors);
          /// send a right CTM packet
          target = new DirectionalAddress(DirectionalAddress.Direction.Right);
          ConnectTo(target, _total_desired_neighbors);
        }
        else if ( (total_shortcuts < _total_desired_shortcuts)
                  || (!TriedShortcut) )
        {
          GetShortcut();
        }
        else {
          //This should never happen
          ///@todo make this smarter
        }
      }
      else {
        if ( !IsActive ) {
          //log.Info("We are not Active!");
        }
        else if( !NeedConnection ) {
          //log.Info("Don't NeedConnection");
        }
      }
    }

    override public ConnectionType ConnectionType
    {
      get
      {
        return ConnectionType.Structured;
      }
    }

    override public bool NeedConnection
    {
      get {
        int total_structured = _connection_table.Count(ConnectionType.Structured);
        int total_shortcuts = _shortcut_edges.Count;
        int total_neighbors = total_structured - total_shortcuts;

        return ( total_neighbors < 2 * _total_desired_neighbors ||
                 total_shortcuts < _total_desired_shortcuts );
      }
    }

    /**
     * Since all structured addresses are on a ring,
     * in our ConnectionTable, each index is a certain distance
     * to the left, and to the right.  This function tells us
     * how far in each direction to the given index.
     *
     * @param index the index of the new connection
     * @param left_distance the number of positions to the left to the index
     * @param right_distance the number of positions to the right to the index
     */
    protected void GetIdxDistancesTo(int index,
                                     out int left_distance,
                                     out int right_distance)
    {
      int self_idx, count;
      lock( _connection_table.SyncRoot ) {
        self_idx = _connection_table.IndexOf(ConnectionType.Structured,
                                             _local.Address);
        count = _connection_table.Count(ConnectionType.Structured);
      }

      if( self_idx == index ) {
        left_distance = 0;
        right_distance = 0;
        return;
      }

      /*
       * We count the distance in the ConnectionTable (both
       * from the left and the right) to the given index
       */
      if( self_idx < 0 ) {
        //We are not in the table:
        self_idx = ~self_idx;
        if( index < self_idx ) {
          //It is right of us in the table:
          right_distance = self_idx - index;
          left_distance = count - right_distance + 1;
        }
        else {
          //It is left of us in the table:
          //And we need to add one to the distance, since we are
          //not actually in the table and self_idx is where we *WOULD* be
          left_distance = index - self_idx + 1;
          right_distance = count - left_distance + 1;
        }
      }
      else {
        ///We are in the table, and we should not be.
        //AHHHHHH
        throw new Exception();
      }
    }
  }

}
