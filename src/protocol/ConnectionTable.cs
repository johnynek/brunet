/*
This program is part of BruNet, a library for the creation of efficient overlay networks.
Copyright (C) 2005  University of California
Copyright (C) 2006-2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

#if BRUNET_NUNIT
#undef PRINT_CONNECTIONS
using NUnit.Framework;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Xml.Serialization;
using System.Xml;
using System.Text;


namespace Brunet
{

  /**
   * This is an immutable object which stores a sorted (by Address)
   * list of Connections.  Once created, it never changes
   */
  public class ConnectionList : IEnumerable {
    /*
     * Public Fields
     */
    public readonly ConnectionType MainType;
    public readonly int Count;
    
    /*
     * protected readonly variables
     */
    protected readonly ArrayList _addresses;
    protected readonly ArrayList _connections;

    /**
     * Make an Empty ConnectionList
     */
    public ConnectionList(ConnectionType ct) {
      MainType = ct;
      Count = 0;
      _addresses = new ArrayList(0);
      _connections = new ArrayList(0);
    }

    protected ConnectionList(ConnectionType ct, ArrayList adds, ArrayList cons) {
      MainType = ct;
      Count = adds.Count;
      _addresses = adds;
      _connections = cons;
    }
    /**
     * Get a particular connection out.
     * @return Connection at index = idx mod Count
     */
    public Connection this[int idx] {
      get {
        if( Count == 0 ) {
          throw new Exception("ConnectionList is empty");
        }
        int idx0 = idx % Count;
        if( idx0 < 0 ) { idx0 += Count; }
        return (Connection)_connections[idx0];
      }
    }
    /*
     * Methods
     */
    
    /**
     * @param a The address to check for the presence of.
     *
     * It is a synonym for IndexOf(a) >= 0.
     * This function is just to eliminate any chance of confusion arising
     * from using IndexOf to check for existence
     */
    public bool Contains(Address a) {
       return (IndexOf(a) >= 0);
    }

    /**
     * Required for IEnumerable Interface
     */
    IEnumerator IEnumerable.GetEnumerator()
    {
      return _connections.GetEnumerator();
    }
    /**
     * Note, left is direction of INCREASING address
     * @param the Address to return the left neighbor of
     * @return Connection to our neighbor which is just left of address
     */
    public Connection GetLeftNeighborOf(Address address) {
      int i = IndexOf(address);
      if (i<0) {
        i = ~i;
      }
      else {
        i++;
      }
      return this[i];
    }
    /**
     * Note, Right is direction of DECREASING address
     * @param the Address to return the right neighbor of
     * @return Connection to our neighbor which is just right of address
     */
    public Connection GetRightNeighborOf(Address address)
    {
      int i = IndexOf(address);
      if (i<0) {
        i = ~i;
      }
      i--;
      return this[i];
    }
    /**
     * Return a new connection list with at most max_count which are
     * the closest (by index distance) to dest (excluding dest)
     */
    public ConnectionList GetNearestTo(Address dest, int max_count) {
      Hashtable cons = new Hashtable();
      ArrayList adds = new ArrayList();
      int max = Count;
      if( max_count > max ) {
        max_count = max;
      }
      int idx = IndexOf(dest);
      bool skip_idx = false;
      if( idx < 0 ) {
        idx = ~idx;
      }
      else {
        //We need to skip the idx, because idx is present:
        skip_idx = true;
      }

      int start = idx - max_count/2;
      int end = start + max_count;
      if( skip_idx ) {
        end++;
      }
      for( int pos = start; pos < end; pos++) {
        if( skip_idx && ( pos == idx ) ) {
          pos++;
        }
        Connection c = this[pos];
        cons[c.Address] = c;
        adds.Add(c.Address);
      }
      adds.Sort();
      //Make a list in the same order:
      ArrayList c_list = new ArrayList();
      foreach(Address a in adds) {
        c_list.Add( cons[a] );
      }
      return new ConnectionList(this.MainType, adds, c_list);
    }

    /**
     * @param a the address you want the index of.
     * @return the index of a Connection containing this address, if it is
     * negative, then it is it bitwise compliment of where the Address would
     * be.
     *
     * @throw an ArgumentNullException if a is null.
     */
    public int IndexOf(Address a) {
      int index = 0;
      if( Count == 0 ) {
        //This item would be the first in the list
        index = ~index;
      }
      else {
        //Search for the item
        /**
        * the BinarySearch.
        */
        index = _addresses.BinarySearch(a);
      }
      return index;
    }
    
    /**
     * ConnectionList objects are immutable.  This method creates a new one
     * with the given Connection added
     * @param cl the old ConnectionList
     * @param c the Connection to insert
     * @param idx the index of the Connection in the returned ConnectionList
     * @return the new ConnectionList containing c
     */
    public static ConnectionList InsertInto(ConnectionList cl, Connection c, out int idx) {
      idx = cl.IndexOf(c.Address);
      if( idx > 0 ) {
        throw new ConnectionExistsException( cl[idx] );
      }
      idx = ~idx;
      ArrayList new_addresses = Functional.Insert(cl._addresses, idx, c.Address);
      ArrayList new_connections = Functional.Insert(cl._connections, idx, c);
      if(ProtocolLog.Connections.Enabled) {
        ProtocolLog.Write(ProtocolLog.Connections,
          String.Format("New Connection[{0}]: {1} instant: {2}", idx, c, c.CreationTime));
      }
      return new ConnectionList(cl.MainType, new_addresses, new_connections);
    }
    /**
     * @return the number of Structured Connections in the interval
     * (a1, a2) (not including the end points) when we move to the left.
     */
    public int LeftInclusiveCount(Address a1, Address a2) {
      if( a1.Equals(a2) ) { return 0; }
      int dist;
      //This list never changes:
      int a2_idx = IndexOf(a2);
      int a1_idx = IndexOf(a1);
        /*
         * There are four cases, we deal with each separately:
         * 0) neither a1 nor a2 are in the table
         * 1) a1 is not, but a2 is
         * 2) a1 is, but a2 is not
         * 3) a1 and a2 are.
         */

      bool a2_present = true;
      bool a1_present = true;
      if( a2_idx < 0 ) {
        a2_present = false;
        a2_idx = ~a2_idx;
      }
      if( a1_idx < 0 ) {
        a1_present = false;
        a1_idx = ~a1_idx;
      }
      if( a1_idx == a2_idx ) {
        //This is an easy case:
        int max_dist = Count;
        if( a2_present ) {
          max_dist--;
        }
        if( a1_present ) {
          max_dist--;
        }
        if( a2.CompareTo( a1 ) > 0 ) {
          dist = 0;  
        }
        else {
          dist = max_dist;
        }
      }
      else {
        //These two indices are different:
        dist = a2_idx - a1_idx;
        if( dist < 0 ) {
          //Wrap around.
          dist += Count;
        }
        if( a1_present ) {
          /*
           * In thie case, our calculations are too much by one, in both
           * cases (dist > 0, dist < 0), so we decrease by one:
           */
          dist = dist - 1;
        }
      }
      return dist;
    }

    /**
     * ConnectionList objects are immutable.  This method creates a new one
     * with the given index removed
     * @param i the index of the Connection to remove
     */
    public static ConnectionList RemoveAt(ConnectionList cl, int i) {
      ArrayList new_add = Functional.RemoveAt(cl._addresses, i);
      ArrayList new_cons = Functional.RemoveAt(cl._connections, i);
      return new ConnectionList(cl.MainType, new_add, new_cons); 
    }
    /**
     * ConnectionList objects are immutable.  This method replaces a
     * Connection with a given Connection, and returns the new
     * ConnectionList.  This is used for updating StatusMessage objects.
     * @param cl the ConnectionList to start with
     * @param old the Connection we are replacing
     * @param new_c the Connection we are replacing with.
     * @param idx the index of both old and new_c
     */
    public static ConnectionList Replace(ConnectionList cl, Connection old, Connection new_c,
                                         out int idx) {
      Address old_a = old.Address;
      if( !old_a.Equals( new_c.Address ) ) {
        throw new Exception(String.Format("Cannot replace: old Address: {0} != new Address {1}",
                            old.Address, new_c.Address));
      }
      idx = cl.IndexOf(old_a);
      if( idx < 0 ) {
        //This is a new address:
        throw new Exception(String.Format("Address: {0} not in ConnectionList.", old_a));
      }
      ArrayList adds = cl._addresses;
      ArrayList cons = Functional.SetElement(cl._connections, idx, new_c);
      return new ConnectionList(cl.MainType, adds, cons);
    }

    
    /**
     * @return the number of Structured Connections in the interval
     * (a1, a2) (not including the end points) when we move to the right.
     */
    public int RightInclusiveCount(Address a1, Address a2) {
      if( a1.Equals(a2) ) { return 0; }
      int dist;

      int a2_idx = IndexOf(a2);
      int a1_idx = IndexOf(a1);

        /*
         * There are four cases, we deal with each separately:
         * 0) neither a1 nor a2 are in the table
         * 1) a1 is not, but a2 is
         * 2) a1 is, but a2 is not
         * 3) a1 and a2 are.
         */

      bool a2_present = true;
      bool a1_present = true;
      if( a2_idx < 0 ) {
        a2_present = false;
        a2_idx = ~a2_idx;
      }
      if( a1_idx < 0 ) {
        a1_present = false;
        a1_idx = ~a1_idx;
      }
      if( a1_idx == a2_idx ) {
        //This is an easy case:
        int max_dist = Count;
        if( a2_present ) {
          max_dist--;
        }
        if( a1_present ) {
          max_dist--;
        }
        if( a2.CompareTo( a1 ) < 0 ) {
          dist = 0;  
        }
        else {
          dist = max_dist;
        }
      }
      else {
        //These two indices are different:
        dist = a1_idx - a2_idx;
        if( dist < 0 ) {
          //Wrap around.
          dist += Count;
        }
        if( a2_present ) {
          /*
           * In thie case, our calculations are too much by one, in both
           * cases (dist > 0, dist < 0), so we decrease by one:
           */
          dist = dist - 1;
        }
      }
      return dist;
    }
    
    /**
     * Returns the next closest connection using the greedy routing algorithm.
     * @param local address of the local node
     * @param dest address of the destination
     * @returns connection to the next hop node, null if current node is the best node
     *          or list is empty
     */ 
    public Connection GetNearestTo(AHAddress local, AHAddress dest) {
      if (Count == 0) {
        return null;
      }
      
      Connection next_closest = null;
      int idx = IndexOf(dest);
      if( idx < 0 ) {
        //a is not the table:
        Connection right = GetRightNeighborOf(dest);
        Connection left = GetLeftNeighborOf(dest);
        BigInteger my_dist = local.DistanceTo(dest).abs();
        BigInteger ld = ((AHAddress)left.Address).DistanceTo(dest).abs();
        BigInteger rd = ((AHAddress)right.Address).DistanceTo(dest).abs();
        if( (ld < rd) && (ld < my_dist) ) {
          next_closest = left;
        }
        if( (rd < ld) && (rd < my_dist) ) {
          next_closest = right;
        }
      }
      else {
        next_closest = this[idx];
      }    
      return next_closest;
    }
    
  }

  /**
   * Keeps track of all connections and all the
   * mappings of Address -> Connection,
   *             Edge -> Connection,
   *             ConnectionType -> Connection List
   *
   * All classes other than ConnectionOverlord should only use
   * the ReadOnly methods (not Add or Remove).
   * ConnectionOverlord objects can call Add and Remove
   * 
   */

  public sealed class ConnectionTable : IEnumerable //, ICollection
  {
    public int AverageEdgeTime {
      get {
        DateTime now = DateTime.UtcNow;
        int total_active_edges_time = 0, count = 0;
        foreach(DictionaryEntry de in _edge_start_time) {
          DateTime start = (DateTime) de.Value;
          total_active_edges_time += (int) (now - start).TotalSeconds;
          count++;
        }
        count += _closed_edges_time.Count;
        int average = 0;
        if(count != 0)
          average = (total_active_edges_time + _total_closed_edges_time) /
            (count + _closed_edges_time.Count);
        return average;
      }
    }

    protected int _total_closed_edges_time = 0;
    public const int MAX_CLOSED_EDGE_TIMES = 10;

    /* Only access this in locked methods */
    protected Queue _closed_edges_time;

    protected Hashtable _edge_start_time;

    protected readonly Random _rand;

    /*
     * These objects are often being reset (since we don't ever
     * modify the data structures once set).
     */
    protected Hashtable _type_to_conlist;
    protected Hashtable _edge_to_con;

    //We mostly deal with structured connections,
    //so we keep a ref to the address list for sructured
    //this is an optimization.
    protected ConnectionList _struct_conlist;

    protected ArrayList _unconnected;

    /**
     * These are the addresses we are trying to connect to.
     * The key is the address, the value is the object that
     * holds the lock.
     */
    protected readonly Hashtable _address_locks;

    /** an object to lock for thread sync */
    private readonly object _sync;
    /**
     * When there is a new connection, this event
     * is fired.
     */
    public event EventHandler ConnectionEvent;
    /**
     * When a connection is lost, this event is fired
     */
    public event EventHandler DisconnectionEvent;
    /**
     * When status changes, this event is fired
     */
    public event EventHandler StatusChangedEvent;

    protected readonly Address _local;

    protected bool _closed; 
    /**
     * Returns the total number of Connections.
     * This is for the ICollection interface
     */
    public int TotalCount {
      get {
        int count = 0;
        foreach(ConnectionType t in Enum.GetValues(typeof(ConnectionType)) ) {
          count += Count(t);
        }
      return count;
      }
    }

    /**
     * This is for the ICollection interface.
     * It returns true
     */
    public bool IsSynchronized {
      get { return true; }
    }

  /**
   * @param local the Address associated with the local node
   */

    public ConnectionTable(Address local)
    {
      _rand = new Random();

      _sync = new Object();
      lock( _sync ) {
        _local = local;
        _type_to_conlist = new Hashtable();
        _edge_to_con = new Hashtable();
        _closed = false;
        _unconnected = new ArrayList();
//        _closed_edges_time = new Queue(10);
//        _edge_start_time = new Hashtable();

        _address_locks = new Hashtable();
        // init all--it is safer to do it this way and avoid null pointer exceptions

        foreach(ConnectionType t in Enum.GetValues(typeof(ConnectionType)) ) {
          Init(t);
        }
      }
    }

    /**
     * Make a ConnectionTable with the default address comparer
     */
    public ConnectionTable() : this(null) { }

    /**
     * When an Edge is added, the ConnectionTable listens
     * for the Edges close event, and at that time, the
     * edge is removed.  Edges should not be removed
     * explicitly from the ConnectionTable, rather the
     * Edge method Edge.Close() should be called, and
     * the ConnectionTable will react properly
     * @throws ConnectionExistsException if there is already such a connection
     * @throws TableClosedException if the ConnectionTable is closed to new
     * connections.
     * @throws Exception if the Edge in this Connection is already closed.
     */
    public void Add(Connection c)
    {
      ConnectionType t = c.MainType;
      Edge e = c.Edge;
      ConnectionList new_cl;
      int index;

      lock(_sync) {
        if( _closed ) { throw new TableClosedException(); }
        Connection c_present = GetConnection(e);
        if( c_present != null ) {
          throw new ConnectionExistsException(c_present);
        }
        /*
         * Here we actually do the storing:
         */
        /*
         * Don't respond to the CloseEvent for now:
         */
        e.CloseEvent -= this.RemoveHandler;
        if( e.IsClosed ) {
          /*
           * RemoveHandler is idempotent, so make sure it is called
           * (since we removed the handler just before)
           * This is safe to do while holding the lock because
           * we don't have a connection on this edge, and thus
           * there can't be a DisconnectionEvent caused by the RemoveHandler
           */
          RemoveHandler(e, null);
          throw new Exception("Edge is already closed");
        }
        /*
         * Copy so we don't mess up an old list
         */
        ConnectionList oldlist = GetConnections(t);
        new_cl = ConnectionList.InsertInto(oldlist, c, out index);
        _type_to_conlist = Functional.SetElement(_type_to_conlist, t, new_cl);
        if( t == ConnectionType.Structured ) {
          //Optimize the most common case to avoid the hashtable
          _struct_conlist = new_cl;
        }
        _edge_to_con = Functional.Add(_edge_to_con, e, c);

        int ucidx = _unconnected.IndexOf(e);
        if( ucidx >= 0 ) {
          _unconnected = Functional.RemoveAt(_unconnected, ucidx);
        }
      } /* we release the lock */

      /*
       * If we get here we ALWAYS fire the ConnectionEvent even
       * if the Edge might have closed in the mean time.  After
       * the ConnectionEvent has fired, we'll start listening
       * to the CloseEvent which will trigger our DisconnectionEvent
       * upon Edge.Close
       */
      if(ConnectionEvent != null) {
        try {
          ConnectionEvent(this, new ConnectionEventArgs(c, index, new_cl) );
        }
        catch(Exception x) {
          if(ProtocolLog.Exceptions.Enabled)
            ProtocolLog.Write(ProtocolLog.Exceptions, String.Format(
              "ConnectionEvent triggered exception: {0}\n{1}", c, x));
        }
      }
      try {
        e.CloseEvent += this.RemoveHandler;
      }
      catch {
        /*
         * If the edge was closed before we got it added, it might be
         * added but never removed from the table.  Now that we have
         * completely added the Connection and registered the handler
         * for the CloseEvent, let's make sure it is still good.
         * If it closes after this, the CloseEvent will catch it.
         *
         * Since RemoveHandler is idempotent, this is safe to call
         * multiple times.
         */
        RemoveHandler(e, null);
        // rethrow the exception
        throw;
      }
      if(ProtocolLog.Stats.Enabled)
        ProtocolLog.Write(ProtocolLog.Stats, String.Format(
          "New Connection {0}|{1}", c, DateTime.UtcNow.Ticks));
    }

    /**
     * When the ConnectionTable is closed, Add will throw a
     * TableClosedException.
     * This is used at the time of Node.Disconnect to make sure
     * no new connections can be added
     */
    public void Close() {
      lock( _sync ) { _closed = true; }
    }

    /**
     * This function is to check if a given address of a given type
     * is already in the table.  It is a synonym for IndexOf(t,a) >= 0.
     * This function is just to eliminate any chance of confusion arising
     * from using IndexOf to check for existence
     */
    public bool Contains(ConnectionType t, Address a)
    {
       return (IndexOf(t,a) >= 0);
    }

    /**
     * @param t the ConnectionType we want to know the count of
     * @return the number of connections of this type
     */
    public int Count(ConnectionType t)
    {
      ConnectionList list = null;
      if( t == ConnectionType.Structured ) {
        //Optimize the most common case to avoid the hashtable
        list = _struct_conlist;
      }
      else {
        list = (ConnectionList)_type_to_conlist[t];
      }
      if( list == null ) { return 0; }
      return list.Count;
    }

    /**
     * This method removes the connection associated with an Edge,
     * then it adds this edge to the list of unconnected nodes.
     * 
     * @param e The edge to disconnect
     */
    public void Disconnect(Edge e)
    {
      //Remove the edge, but keep a reference to it.
      Remove(e, true);
    }

    public int UnconnectedCount
    {
      get
      {
        return _unconnected.Count;
      }
    }
    /**
     * Required for IEnumerable Interface
     */
    public IEnumerator GetEnumerator()
    {
      IDictionaryEnumerator en = _edge_to_con.GetEnumerator();
      while( en.MoveNext() ) {
        yield return en.Value;
      }
    }

    /**
     * Gets the Connection for the left structured neighbor of a given AHAddress
     */
    public Connection GetLeftStructuredNeighborOf(AHAddress address)
    {
      return _struct_conlist.GetLeftNeighborOf(address);
    }

    /**
     * Gets the Connection for the right structured neighbor of a given AHAddress
     */
    public Connection GetRightStructuredNeighborOf(AHAddress address)
    {
      return _struct_conlist.GetRightNeighborOf(address);
    }

    /**
     * @param t the ConnectionType of connection in question
     * @param index the index of the connection in question
     *
     * The index "wraps around", or equivalently, 
     * the result of getting (index + count) is the
     * same as (index)
     */
    public Connection GetConnection(ConnectionType t, int index)
    {
      ConnectionList list;
      if( t == ConnectionType.Structured ) {
        //Optimize the most common case to avoid the hashtable
        list = _struct_conlist;
      }
      else {
        list = (ConnectionList)_type_to_conlist[t];
      }
      return list[index];
    }
    /**
     * Convienience function.  Same as IndexOf followed by GetConnection
     * Get the Connection for a given address
     * @param t ConnectionType we want
     * @param a the address we are looking for
     * @return null if there is no such connection
     */
    public Connection GetConnection(ConnectionType t, Address a)
    {
      ConnectionList list = GetConnections(t);
      Connection c = null;
      int idx = list.IndexOf(a);
      if( idx >= 0 ) {
        c = list[idx];
      }
      return c;
    }
    /**
     * Returns a Connection for the given edge:
     * @return Connection
     */
    public Connection GetConnection(Edge e)
    {
      if( e == null ) { return null; } //Is this really a good idea?
      return (Connection)_edge_to_con[e];
    }

    /**
     * Return all the connections of type t.
     * This NEVER CHANGES onces created, so don't
     * worry about locking.
     * @param t the Type of Connections we want
     * @return an enumerable that we can foreach over
     */
    public ConnectionList GetConnections(ConnectionType t)
    {
      if( t == ConnectionType.Structured ) {
        return _struct_conlist;
      }
      else {
        return (ConnectionList)_type_to_conlist[t];
      }
    }
    /**
     * Return all the connections of type t.
     * This NEVER CHANGES onces created, so don't
     * worry about locking.
     * @param t the Type of Connections we want
     * @return an enumerable that we can foreach over
     */
    public IEnumerable GetConnections(string t)
    {
      return new ConnectionTypeEnumerable(this, t);
    }
    /**
     * Returns at most i structured connections which are nearest
     * to destination
     * @param dest the target we are asking for connections close to
     * @param max_count the maximum number of connections to return
     * @return a list of structured connections closest to the destination
     * EXCLUDING The destination itself.
     * @deprecated
     */
    public ArrayList GetNearestTo(AHAddress dest, int max_count)
    {
      ConnectionList near = _struct_conlist.GetNearestTo(dest, max_count);
      ArrayList ret_val = new ArrayList();
      foreach(Connection c in near) {
        ret_val.Add(c);
      }
      return ret_val;
    }
    /**
     * @return a random connection of type t
     * @deprecated
     */
    public Connection GetRandom(ConnectionType t)
    {
      ConnectionList list = GetConnections(t);
      int size = list.Count;
      if( size == 0 )
        return null;
      else {
        int pos = _rand.Next( size );
        return list[pos];
      }
    }

    /**
     * Returns an IEnumerable of the _unconnected edges
     */
    public IEnumerable GetUnconnectedEdges()
    {
      return _unconnected;
    }

    /**
     * Before we can use a ConnectionType, that type must
     * be initialized 
     */
    protected void Init(ConnectionType t)
    {
      ConnectionList new_list = new ConnectionList(t);
      lock( _sync ) {
        if( t == ConnectionType.Structured ) {
          _struct_conlist = new_list; 
        }
        _type_to_conlist[t] = new_list;
      }
    }

    /**
     * @param t the ConnectionType
     * @param a the Address you want to know the index of
     * @return the index.  If it is negative, the bitwise
     * compliment would indicate where it should be in the
     * list.
     * @deprecated
     */
    public int IndexOf(ConnectionType t, Address a)
    {
      ConnectionList list = GetConnections(t);
      return list.IndexOf(a);
    }

    /**
     * @return the number of Structured Connections in the interval
     * (a1, a2) (not including the end points) when we move to the left.
     * @deprecated
     */
    public int LeftInclusiveCount(AHAddress a1, AHAddress a2) {
      return _struct_conlist.LeftInclusiveCount(a1,a2);
    }
    /**
     * @return the number of Structured Connections in the interval
     * (a1, a2) (not including the end points) when we move to the right.
     */
    public int RightInclusiveCount(AHAddress a1, AHAddress a2) {
      return _struct_conlist.RightInclusiveCount(a1,a2);
    }

    /**
     * @param a the Address to lock
     * @param t the type of connection
     * @param locker the object wishing to hold the lock
     *
     * We use this to make sure that two linkers are not
     * working on the same address for the same connection type
     *
     * @throws ConnectionExistsException if there is already a connection to this address
     * @throws CTLockException if we cannot get the lock
     * @throws CTLockException if lockedvar is not null or a, when called. 
     */
    public void Lock(Address a, string t, ILinkLocker locker)
    {
      ConnectionType mt = Connection.StringToMainType(t);
      lock( _sync ) {
        if( null == a ) { return; }
        if( locker.TargetLock != null && (false == a.Equals(locker.TargetLock)) ) {
          //We only overwrite the locker.TargetLock() if it is null:
          throw new CTLockException(
                  String.Format("locker.TargetLock() not null, set to: {0}", locker.TargetLock));
        }
        Connection c_present = GetConnection(mt, a);
        if( c_present != null ) {
          /**
           * We already have a connection of this type to this node.
           */
          throw new ConnectionExistsException(c_present);
        }
        Hashtable locks = (Hashtable)_address_locks[mt];
        if( locks == null ) {
          locks = new Hashtable();
          _address_locks[mt] = locks;
        }
        ILinkLocker old_locker = (ILinkLocker)locks[a];
        if( null == old_locker ) {
          locks[a] = locker;
          locker.TargetLock = a;
          if(ProtocolLog.ConnectionTableLocks.Enabled) {
            ProtocolLog.Write(ProtocolLog.ConnectionTableLocks,
              String.Format("{0}, locker: {1} Unlocking: {2}",
                            _local, locker, a));
          }
        }
        else if (old_locker == locker) {
          //This guy already holds the lock
          locker.TargetLock = a;
        }
        else if ( old_locker.AllowLockTransfer(a,t,locker) ) {
        //See if we can transfer the lock:
          locks[a] = locker;
          locker.TargetLock = a;
          //Make sure the lock is null
          old_locker.TargetLock = null;
        }
        else {
          if(ProtocolLog.ConnectionTableLocks.Enabled) {
            ProtocolLog.Write(ProtocolLog.ConnectionTableLocks,
              String.Format("{0}, {1} tried to lock {2}, but {3} holds the lock",
              _local, locker, a, locks[a]));
          }
          throw new CTLockException(
                      String.Format(
                        "Lock on {0} cannot be transferred from {1} to {2}",
                        a, old_locker, locker));
        }
      }
    }

    /**
     * Remove the connection associated with an edge from the table
     * @param e Edge whose connection should be removed
     * @param add_unconnected if true, keep a reference to the edge in the
     * _unconnected list
     */
    protected void Remove(Edge e, bool add_unconnected)
    {
      int index = -1;
      bool have_con = false;
      Connection c = null;
      ConnectionList new_cl = null;
      lock(_sync) {
        c = GetConnection(e);	
        have_con = (c != null);
        if( have_con )  {
          ConnectionType t = c.MainType;
          ConnectionList old_cl = GetConnections(t);
          index = old_cl.IndexOf(c.Address);
          //Here we go:
          new_cl = ConnectionList.RemoveAt(old_cl, index);
          _type_to_conlist = Functional.SetElement(_type_to_conlist, t, new_cl);
          if( t == ConnectionType.Structured ) {
            //Optimize the most common case to avoid the hashtable
            _struct_conlist = new_cl;
          }
          //Remove the edge from the tables:
          _edge_to_con = Functional.Remove(_edge_to_con,e);
          
          if( add_unconnected ) {
            _unconnected = Functional.Add(_unconnected, e);
            if(ProtocolLog.Stats.Enabled) {
              ProtocolLog.Write(ProtocolLog.Stats, String.Format(
                "Intermediate add to unconnected {0}|{1}", e, DateTime.UtcNow.Ticks));
            }
          }
        }
        else if( !add_unconnected ) {
//We didn't have a connection, so, check to see if we have it in _unconnected:
//Don't keep this edge around at all:
          int idx = _unconnected.IndexOf(e);
          if( idx >= 0 ) {
            _unconnected = Functional.RemoveAt(_unconnected, idx);
          }
          if(ProtocolLog.Stats.Enabled) {
            ProtocolLog.Write(ProtocolLog.Stats, String.Format(
              "Edge removed from unconnected {0}|{1}", e, DateTime.UtcNow.Ticks));
          }
        }
      }
      if( have_con ) {
        if(ProtocolLog.Connections.Enabled) {
          DateTime now = DateTime.UtcNow;
          TimeSpan con_life = now - c.CreationTime;
          ProtocolLog.Write(ProtocolLog.Connections,
            String.Format("New Disconnection[{0}]: {1}, instant: {2}, con_life: {3} ", 
			                    index, c, now, con_life));
        }

        if(ProtocolLog.Stats.Enabled) {
          ProtocolLog.Write(ProtocolLog.Stats, String.Format(
            "Disconnection {0}|{1}", c, DateTime.UtcNow.Ticks));
        }

        //Announce the disconnection:
        if( DisconnectionEvent != null ) {
          try {
            DisconnectionEvent(this, new ConnectionEventArgs(c, index, new_cl));
          }
          catch(Exception x) {
            if(ProtocolLog.Exceptions.Enabled)
              ProtocolLog.Write(ProtocolLog.Exceptions, String.Format(
                "DisconnectionEvent triggered exception: {0}\n{1}", c, x));
          }
        }
      }
    }

    /**
     * When an Edge closes, this handler is called
     * This is just a wrapper for Remove
     */
    protected void RemoveHandler(object edge, EventArgs args)
    {
      Edge e = (Edge)edge;
      e.CloseEvent -= this.RemoveHandler;
      //Get rid of the edge and don't add it to our _unconnected list
      Remove(e, false);
    }

    /**
     * Print out all the tables (for debugging mostly)
     */
    public override string ToString()
    {
      System.Text.StringBuilder sb = new System.Text.StringBuilder();

      IDictionaryEnumerator myEnumerator;
      sb.Append("------Begin Table------\n");
      sb.Append("Type : Address Table\n");

      lock(_sync) {
        sb.Append("\nType : Edge Table\n");
        myEnumerator = _type_to_conlist.GetEnumerator();
        while (myEnumerator.MoveNext()) {
          sb.Append("Type: ");
          sb.Append(myEnumerator.Key.ToString() + "\n");
          sb.Append("Edge Table:\n");
          ConnectionList t = (ConnectionList) myEnumerator.Value;
          foreach(System.Object o in t) {
            sb.Append("\t" + o.ToString() + "\n");
          }
        }
        sb.Append("\nEdge : Type\n");
        myEnumerator = _edge_to_con.GetEnumerator();
        while (myEnumerator.MoveNext()) {
          sb.Append("Edge: ");
          sb.Append(myEnumerator.Key.ToString() + "\n");
          sb.Append("Connection: ");
          sb.Append(myEnumerator.Value.ToString() + "\n");
        }
      }
      sb.Append("\n------End of Table------\n\n");
      return sb.ToString();
    }

    /**
     * We use this to make sure that two linkers are not
     * working on the same address
     * @param t the type of connection.
     * @param locker the object which holds the lock.
     * @throw Exception if the lock is not held by locker
     */
    public void Unlock(string t, ILinkLocker locker)
    {
      ConnectionType mt = Connection.StringToMainType(t);
      lock( _sync ) {
        if( locker.TargetLock != null ) {
          Hashtable locks = (Hashtable)_address_locks[mt];
          if(ProtocolLog.ConnectionTableLocks.Enabled) {
            ProtocolLog.Write(ProtocolLog.ConnectionTableLocks,
              String.Format("{0} Unlocking {1}", _local, locker.TargetLock));
          }

          object real_locker = locks[locker.TargetLock];
          if(null == real_locker) {
            string err = String.Format("On node " + _local + ", " + locker +
              " tried to unlock " + locker.TargetLock + " but no such lock" );
            if(ProtocolLog.ConnectionTableLocks.Enabled) {
              ProtocolLog.Write(ProtocolLog.ConnectionTableLocks, err);
            }
            throw new Exception(err);
          }
          else if(real_locker != locker) {
            string err = String.Format("On node " + _local + ", " + locker +
                " tried to unlock " + locker.TargetLock + " but not the owner");
            if(ProtocolLog.ConnectionTableLocks.Enabled) {
              ProtocolLog.Write(ProtocolLog.ConnectionTableLocks, err);
            }
            throw new Exception(err);
          }

          locks.Remove(locker.TargetLock);
          locker.TargetLock = null;
        }
      }
    }

    /**
     * update the StatusMessage for a particular Connection.  Since 
     * Connections are immutable so we make a new Connection object 
     * with the new StatusMessage. All other constructor arguments
     * are taken from the old Connection instance.
     * This will fail if the con argument is not present.
     * @param con Connection to update.
     * @param sm StatusMessage to replace the old.
     * @return the new Connection
     * @throws Exception if con is not in the table
     */
    public Connection UpdateStatus(Connection con, StatusMessage sm)
    {
      ConnectionType t = con.MainType;
      Address a = con.Address;
      Edge e = con.Edge;
      string con_type = con.ConType;
      LinkMessage plm = con.PeerLinkMessage;
      ConnectionList cl;
        //Make the new connection and replace it in our data structures:
      Connection newcon = new Connection(e,a,con_type,sm,plm);
      int index;
      lock(_sync) {
        cl = GetConnections(t);
        cl = ConnectionList.Replace(cl, con, newcon, out index);
        //Update the Edge -> Connection mapping
        _edge_to_con = Functional.SetElement(_edge_to_con, e, newcon);
        //Update the ConnectionType -> ConnectionList mapping
        _type_to_conlist = Functional.SetElement(_type_to_conlist, t, cl);

        if( t == ConnectionType.Structured ) {
          //Optimize the most common case to avoid the hashtable
          _struct_conlist = cl;
        }
      } /* we release the lock */

      /* Send the event: */
      if( StatusChangedEvent != null ) {
        try {
          StatusChangedEvent(sm, new ConnectionEventArgs(newcon, index, cl) );
        }
        catch(Exception x) {
          if(ProtocolLog.Exceptions.Enabled)
            ProtocolLog.Write(ProtocolLog.Exceptions, String.Format(
              "StatusChangedEvent triggered exception: {0}\n{1}", newcon, x));
        }
      }
      return newcon;
    }

    /**
     * When a new Edge is created by the the Linker or received
     * by the ConnectionPacketHandler, they tell the ConnectionTable
     * about it.  It is sort of a null connection.  The ConnectionTable
     * should still know about all the Edge objects for the Node.
     *
     * When a connection is made, there is never a need to remove
     * _unconnected edges.  Either a connection is made (which will
     * remove the edge from this list) or the edge will be closed
     * (which will remove the edge from this list).
     *
     * @param e the new _unconnected Edge
     */
    public void AddUnconnected(Edge e)
    {
      if(ProtocolLog.Stats.Enabled) {
        ProtocolLog.Write(ProtocolLog.Stats, String.Format(
          "Initial add to unconnected {0}|{1}", e, DateTime.UtcNow.Ticks));
      }
      lock( _sync ) {
        if( _closed ) { throw new TableClosedException(); }
        Connection c = GetConnection(e);
        if( c != null ) {
          throw new Exception("We already have a Connection, can't add to Unconnected: " + c.ToString());
        }
        e.CloseEvent -= this.RemoveHandler;
        if( e.IsClosed ) {
          /*
           * RemoveHandler is idempotent, so make sure it is called
           * (since we removed the handler just before)
           * This is safe to do while holding the lock because
           * we don't have a connection on this edge, and thus
           * there can't be a DisconnectionEvent caused by the RemoveHandler
           */
          RemoveHandler(e, null);
          throw new Exception("Edge is already closed");
        }
        int idx = _unconnected.IndexOf(e);
        if( idx < 0 ) {
          _unconnected = Functional.Add(_unconnected, e);
        }
/*        if(!_edge_start_time.Contains(e))
          _edge_start_time = Functional.Add(_edge_start_time, e, DateTime.UtcNow);*/
        try {
          e.CloseEvent += this.RemoveHandler;
        }
        catch {
         /*
          * If the edge was closed before we got it added, it might be
          * added but never removed from the table.  Now that we have
          * completely added the Connection and registered the handler
          * for the CloseEvent, let's make sure it is still good.
          * If it closes after this, the CloseEvent will catch it.
          */
          RemoveHandler(e, null);
          throw;
        }
      }
    }
    /**
     * @param edge Edge to check to see if it is an Unconnected Edge
     * @return true if this edge is an _unconnected edge
     */
    public bool IsUnconnected(Edge e)
    {
      return _unconnected.Contains(e);
    }
    /**
     * Handles enumerating connection types, not just all
     * connections.
     *
     * This will never change once it is created.
     */
    private class ConnectionTypeEnumerable : IEnumerable<Connection> {

      private readonly string _contype;
      private readonly IEnumerable _cons;

      public ConnectionTypeEnumerable(ConnectionTable tab, string contype)
      {
        _contype = contype;
        ConnectionType ct = Connection.StringToMainType(contype);
        _cons = tab.GetConnections(ct);
      }

     /**
      * Required for IEnumerable Interface
      */
      IEnumerator<Connection> IEnumerable<Connection>.GetEnumerator()
      {
        foreach(Connection c in _cons) {
          if (c.ConType == _contype ) {
            yield return c;
          }
        }
      }
     /**
      * Required for IEnumerable Interface
      */
      IEnumerator IEnumerable.GetEnumerator()
      {
        foreach(Connection c in _cons) {
          if (c.ConType == _contype ) {
            yield return c;
          }
        }
      }
    }
  }

  /**
   * Thrown when someone tries to lock an address
   * but it is already locked
   */
  public class CTLockException : System.Exception {
    public CTLockException(string s) : base(s) { }
  }

  /**
   * Thrown when we try to Add a connection that is already
   * present, returns the existing connection
   */
  public class ConnectionExistsException : System.Exception {
    public readonly Connection Con;
    public ConnectionExistsException(Connection c) {
      Con = c;
    }
  }
  /**
   * If anyone calls Add once Close has been called, this
   * exception is thrown
   */
  public class TableClosedException : System.Exception {

  }


#if BRUNET_NUNIT
  [TestFixture]
  public class ConnectionTableTest
  {
    public ConnectionTableTest() { }
    public class TestLinkLocker : ILinkLocker {
      protected readonly bool _allow;
      protected Address _target_lock;
      public Object TargetLock {
        get { return _target_lock; }
        set { _target_lock = (Address) value; }
      }
      public TestLinkLocker(bool allow) { _allow = allow; }
      public bool AllowLockTransfer(Address a, string t, ILinkLocker l) {
        if( _allow ) { TargetLock = null; }
        return _allow;
      }
    }

    [Test]
    public void LockTest() {
      byte[]  abuf = new byte[20];
      Address a = new AHAddress(abuf);

      ConnectionTable tab = new ConnectionTable();
      TestLinkLocker lt = new TestLinkLocker(true);
      TestLinkLocker lf = new TestLinkLocker(false);

      //Get a lock on a.
      tab.Lock(a, "structured.near", lt);
      Assert.AreEqual(a, lt.TargetLock, "lt has lock");
      tab.Unlock("structured.near", lt);
      Assert.IsNull(lt.TargetLock, "Unlock nulling test");
      //Unlock null should be fine:
      tab.Unlock("structured.near", lt); 
      Assert.IsNull(lt.TargetLock, "Unlock nulling test");
      //We can't unlock if we don't have the lock:
      lt.TargetLock = a;

      try {
        tab.Unlock("structured.near", lt);
        Assert.IsFalse(true, "We were able to unlock an address incorrectly");
      } catch { }
      //Get a lock and transfer:
      tab.Lock(a, "structured.near", lt);
      Assert.AreEqual(a, lt.TargetLock, "lt has lock");
      tab.Lock(a, "structured.near", lf);
      Assert.IsTrue(lf.TargetLock == a, "Lock was transferred to lf");
      //lt.TargetLock should be null;
      Assert.IsNull(lt.TargetLock, "lock was transfered and we are null");
      
      //Now, lt should not be able to get the lock:
      try {
        tab.Lock(a, "structured.near", lt);
        Assert.IsFalse(true, "We somehow got the lock");
      }
      catch { }
      Assert.IsNull(lt.TargetLock, "lt shouldn't hold the lock");
      Assert.AreEqual(lf.TargetLock, a, "lf still holds the lock");
      //Now let's unlock:
      tab.Unlock("structured.near", lf);
      //lf.TargetLock should be null;
      Assert.IsNull(lf.TargetLock, "lock was transfered and we are null");

    }
    [Test]
    public void LoopTest() {
      //Make some fake edges: 
      TransportAddress home_ta =
        TransportAddressFactory.CreateInstance("brunet.tcp://127.0.27.1:5000");
      TransportAddress ta1 =
        TransportAddressFactory.CreateInstance("brunet.tcp://158.7.0.1:5000");
      TransportAddress ta2 =
        TransportAddressFactory.CreateInstance("brunet.tcp://169.0.5.1:5000");
      FakeEdge e1 = new FakeEdge(home_ta, ta1);
      FakeEdge e2 = new FakeEdge(home_ta, ta2);
      //Make some addresses:
      byte[]  buf1 = new byte[20];
      for (int i = 0; i <= 17; i++)
      {
        buf1[i] = 0xFF;
      }
      buf1[18] = 0xFF;
      buf1[19] = 0xFE;
      AHAddress a1 = new AHAddress(buf1);

      byte[] buf2 = new byte[20];
      for (int i = 0; i <= 17; i++) {
        buf2[i] = 0x00;
      }
      buf2[18] = 0x00;
      buf2[19] = 0x04;
      AHAddress a2 = new AHAddress(buf2); 
      ConnectionTable tab = new ConnectionTable();
      
      tab.Add(new Connection(e1, a1, "structured", null, null));
      tab.Add(new Connection(e2, a2, "structured.near", null, null));

      Assert.AreEqual(tab.TotalCount, 2, "total count");
      Assert.AreEqual(tab.Count(ConnectionType.Structured) , 2, "structured count");
      
      int total = 0;
      foreach(Connection c in tab) {
	total++;
	//Mostly a hack to make sure the compiler doesn't complain about an
	//unused variable
	Assert.IsNotNull(c);
       
       	//Console.Error.WriteLine("{0}\n",c);
      }
      Assert.AreEqual(total,2,"all connections");
     
      int struct_tot = 0;
      foreach(Connection c in tab.GetConnections(ConnectionType.Structured)) {
        struct_tot++;
	//Mostly a hack to make sure the compiler doesn't complain about an
	//unused variable
	Assert.IsNotNull(c);
        //Console.Error.WriteLine("{0}\n",c);
      }
      Assert.AreEqual(struct_tot, 2, "structured connections");
      int near_tot = 0;
      foreach(Connection c in tab.GetConnections("structured.near")) {
        near_tot++;
	//Mostly a hack to make sure the compiler doesn't complain about an
	//unused variable
	Assert.IsNotNull(c);
        //Console.Error.WriteLine("{0}\n",c);
      }
      Assert.AreEqual(near_tot, 1, "structured near");

      /*
       * Here are some randomized tests:
       */
      Random r = new Random();
      for(int i = 0; i < 100; i++) {
        //Make a random connection table:
        int count = r.Next(1,100); //At most 99 connections:
        tab = new ConnectionTable();
        for(int j = 0; j < count; j++) {
          //Add the connections:
          byte[] buf = new byte[ Address.MemSize ];
          r.NextBytes(buf);
          Address.SetClass(buf, 0);
          AHAddress a = new AHAddress( MemBlock.Reference(buf, 0, buf.Length) );
          FakeEdge e = new FakeEdge(home_ta, ta2);
          //Must put different edges in each time.
          Connection c = new Connection(e, a, "structured", null, null);
          tab.Add( c );
          Assert.AreEqual( tab.GetConnection(e),
                           tab.GetConnection(ConnectionType.Structured, a),
                           "Edge equals Address lookup");
                                                
        }
        //Now do some tests:
        for(int k = 0; k < 100; k++) {
        if( r.Next(2) == 0 ) {
          byte[] buf = new byte[ Address.MemSize ];
          r.NextBytes(buf);
          Address.SetClass(buf, 0);
          a1 = new AHAddress( MemBlock.Reference(buf, 0, buf.Length) );
        }
        else {
          //Get a random connection:
          Connection c_r = tab.GetRandom(ConnectionType.Structured);
          a1 = (AHAddress)c_r.Address;
        }
        //Do the same for a2:
        if( r.Next(2) == 0 ) {
          byte[] buf = new byte[ Address.MemSize ];
          r.NextBytes(buf);
          Address.SetClass(buf, 0);
          a2 = new AHAddress( MemBlock.Reference(buf, 0, buf.Length) );
        }
        else {
          //Get a random connection:
          Connection c_r = tab.GetRandom(ConnectionType.Structured);
          a2 = (AHAddress)c_r.Address;
        }
        //Now do some checks:
        int r_c = tab.RightInclusiveCount(a1, a2);
        int l_c = tab.LeftInclusiveCount(a1, a2);
        //Now manually count them:
        int r_c_manual = 0;
        int l_c_manual = 0;
        int iterated = 0;
        foreach(Connection c in tab) {
          AHAddress a3 = (AHAddress)c.Address;
          if( a3.IsBetweenFromLeft(a1, a2) ) {
            l_c_manual++;
          }
          if( a3.IsBetweenFromRight(a1, a2) ) {
            r_c_manual++;
          }
          iterated++;
        }
        Assert.AreEqual(iterated, count, "Enumeration of structured");
        Assert.AreEqual(r_c, r_c_manual, "RightInclusive test");
        Assert.AreEqual(l_c, l_c_manual, "LeftInclusive test");
        //Check symmetry:
        int r_c2 = tab.RightInclusiveCount(a2, a1);
        int l_c2 = tab.LeftInclusiveCount(a2, a1);
        //Console.Error.WriteLine("LIC(a,b): {0}, RIC(b,a): {1}", l_c2, r_c);
        Assert.AreEqual(l_c, r_c2, "RIC(a2, a1) == LIC(a1, a2)");
        Assert.AreEqual(r_c, l_c2, "LIC(a2, a1) == RIC(a1, a2)");
        }
        //Do some removals:
        while(tab.Count(ConnectionType.Structured) > 0) {
          //Check that the table is sorted:
          Address last_a = null;
          foreach(Connection cn in tab.GetConnections(ConnectionType.Structured)) {
            if( last_a != null ) {
              Assert.IsTrue( last_a.CompareTo( cn.Address ) < 0, "Sorted table");
            }
            last_a = cn.Address;
          }
          Connection c = tab.GetRandom(ConnectionType.Structured);
          Assert.AreEqual( c, tab.GetConnection(c.Edge), "Edge lookup");
          Assert.AreEqual( tab.GetConnection(c.Edge),
                           tab.GetConnection(ConnectionType.Structured, c.Address),
                           "Edge equals Address lookup");
          //Check to see that UpdateStatus basically works
          Connection c2 = tab.UpdateStatus(c, null);
          Assert.AreEqual( c2, tab.GetConnection(c.Edge), "Edge lookup");
          Assert.AreEqual( tab.GetConnection(c.Edge),
                           tab.GetConnection(ConnectionType.Structured, c.Address),
                           "Edge equals Address lookup");

          int before = tab.Count(ConnectionType.Structured);
          int uc_count = tab.UnconnectedCount;
          tab.Disconnect(c.Edge);
          int after = tab.Count(ConnectionType.Structured);
          int uc_count_a = tab.UnconnectedCount;
          Assert.IsTrue( before == (after + 1), "Disconnect subtracted one");
          Assert.IsTrue( uc_count == (uc_count_a - 1), "Disconnect added one _unconnected");
          Assert.IsTrue( tab.IndexOf(ConnectionType.Structured, c.Address) < 0, "Removal worked");
          Assert.IsNull( tab.GetConnection(c.Edge), "Connection is gone");
          Assert.IsTrue( tab.IsUnconnected( c.Edge ), "Edge is _unconnected" );
          c.Edge.Close(); //Should trigger removal completely:
          Assert.IsFalse( tab.IsUnconnected( c.Edge ), "Edge is completely gone");
          Assert.IsNull( tab.GetConnection( c.Edge ), "Connection is still gone");
        }
      }
    }
  }

#endif

}
