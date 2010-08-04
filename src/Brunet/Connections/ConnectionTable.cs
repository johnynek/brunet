/*
This program is part of BruNet, a library for the creation of efficient overlay networks.
Copyright (C) 2005  University of California
Copyright (C) 2006-2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System.Threading;
using Brunet.Util;
using Brunet.Concurrent;
using Brunet.Collections;
using Brunet.Messaging;
using Brunet.Transport;
//This is violation of the modularization hierarchy and needs to be fixed:
///@todo Remove the Brunet.Symphony dependence
using Brunet.Symphony;

namespace Brunet.Connections
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

    protected readonly WriteOnce<List<object>> _as_list;

    /**
     * Make an Empty ConnectionList
     */
    public ConnectionList(ConnectionType ct) {
      MainType = ct;
      Count = 0;
      _addresses = new ArrayList(0);
      _connections = new ArrayList(0);
      _as_list = new WriteOnce<List<object>>();
    }

    protected ConnectionList(ConnectionType ct, ArrayList adds, ArrayList cons) {
      MainType = ct;
      Count = adds.Count;
      _addresses = adds;
      _connections = cons;
      _as_list = new WriteOnce<List<object>>();
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

    public Connection this[Address a] {
      get {
        int idx = IndexOf(a);
        if( idx < 0 ) {
          throw new Exception( String.Format("Address {0} not in ConnectionList", a));
        }
        return (Connection)_connections[idx];
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

    /** Convert to an ADR-compatible list
     * returns an IList of ILists
     * which comes from Connection.ToDictionary()
     * the first element is the list of keys in Connection.ToDictionary,
     * then all subsequent items are the values in the order of the keys.
     * This is because this list could easily exceed the size of UDP packets
     * if we are not careful about how we write it, so saving space is worth
     * it
     */
    public IList ToList() {
      
      List<object> result;
      if( false == _as_list.TryGet(out result) ) {
        result = new List<object>(Count);
        result.Add(Connection.ConnectionTypeToString(MainType));
        result.Add(new string[]{"address", "sender", "subtype"});
        if( Count > 0 ) {
          foreach(Connection c in _connections) {
            ArrayList c_vals = new ArrayList();
            c_vals.Add(c.Address.ToString());
            c_vals.Add(c.Edge.ToUri());
            c_vals.Add(c.SubType);
            result.Add(c_vals);
          }
        }
        else {
          //Add an empty list
          result.Add(new object[0]);
        }
        _as_list.TrySet(result);
      }
      return result;
    }
    
  }
  /**
   * This is an immutable class which holds a complete
   * snapshot of the ConnectionTable state
   */
  public class ConnectionTableState : IEnumerable<Connection> {
    /*** If Closed new Connections cannot be added
     */
    public readonly bool Closed;
    public readonly int View;
    public readonly ImmutableList<Edge> Unconnected; 

    /////
    // Protected
    /////

    protected readonly ConnectionList[] _type_to_conlist;
    protected readonly Dictionary<Edge, Connection> _edge_to_con;

    ////
    // Constructors
    ////

    public ConnectionTableState() {
      Closed = false;
      View = 0;
      Array enum_vals = System.Enum.GetValues(typeof(ConnectionType));
      _type_to_conlist = new ConnectionList[ enum_vals.Length ];
      foreach(ConnectionType ct in enum_vals) {
        _type_to_conlist[(int)ct] = new ConnectionList(ct);
      }
      Unconnected = ImmutableList<Edge>.Empty; 
      _edge_to_con = new Dictionary<Edge,Connection>();
    }

    protected ConnectionTableState(bool closed, int view, ConnectionList[] cons,
                              Dictionary<Edge, Connection> e_to_c,
                              ImmutableList<Edge> uncon) {
      Closed = closed;
      View = view;
      _type_to_conlist = cons;
      _edge_to_con = e_to_c;
      Unconnected = uncon;
    }

    ////
    // Properties
    ////
    public int Count {
      get {
        return _edge_to_con.Count;
      }
    }

    ////
    // Methods
    ////

    public ConnectionTableState AddUnconnected(Edge e) {
      if( Unconnected.Contains(e) ) { return this; }
      return new ConnectionTableState(Closed, View + 1,
                                      _type_to_conlist,
                                      _edge_to_con,
                                      Unconnected.PushIntoNew(e));
    }

    public Pair<ConnectionTableState,int> AddConnection(Connection c) {
      if( Closed ) { throw new TableClosedException(); }
      Connection c_present = GetConnection(c.Edge);
      if( c_present != null ) {
        throw new ConnectionExistsException(c_present);
      }
      var newd = new Dictionary<Edge, Connection>(_edge_to_con);
      newd[c.Edge] = c;
      int mt_idx = (int)c.MainType;
      var old_clist = _type_to_conlist[ mt_idx ];
      var newt_to_c = (ConnectionList[])_type_to_conlist.Clone();
      int c_idx;
      newt_to_c[ mt_idx ] = ConnectionList.InsertInto(old_clist, c, out c_idx);
      var unc = Unconnected.RemoveFromNew(c.Edge);
      var cts = new ConnectionTableState(false, View + 1,
                                      newt_to_c,
                                      newd,
                                      unc);
      return new Pair<ConnectionTableState, int>(cts, c_idx);
    }

    public ConnectionTableState Close() {
      if( Closed ) { return this; }
      return new ConnectionTableState(true, View + 1,
                                      _type_to_conlist,
                                      _edge_to_con,
                                      Unconnected);
    }

    public Connection GetConnection(Edge e) {
      Connection res;
      if( _edge_to_con.TryGetValue(e, out res) ) {
        return res;
      }
      return null;
    }

    public ConnectionList GetConnections(ConnectionType ct) {
      return _type_to_conlist[(int)ct];
    }

    /** return just the connections such that ConType == ct 
     */
    public IEnumerable<Connection> GetConnections(string ct) {
      return new ConnectionTypeEnumerable(this, ct);
    }

    public IEnumerator<Connection> GetEnumerator()
    {
      foreach(var kv in _edge_to_con) {
        yield return kv.Value;
      }
    }

    IEnumerator IEnumerable.GetEnumerator() {
      //Use the above
      return this.GetEnumerator();
    }

    /** Removes a connection, if present
     * idx is -1 if the connection was not present
     */
    public Pair<ConnectionTableState,Pair<Connection,int>> RemoveConnection(Edge e) {
      Connection c;
      int idx;
      ConnectionTableState newcts;
      if(_edge_to_con.TryGetValue(e, out c)) { 
        var newd = new Dictionary<Edge, Connection>(_edge_to_con);
        newd.Remove(c.Edge);
        int mt_idx = (int)c.MainType;
        var old_clist = _type_to_conlist[ mt_idx ];
        var newt_to_c = (ConnectionList[])_type_to_conlist.Clone();
        idx = old_clist.IndexOf(c.Address);
        newt_to_c[ mt_idx ] = ConnectionList.RemoveAt(old_clist, idx);
        newcts = new ConnectionTableState(Closed, View + 1,
                                      newt_to_c,
                                      newd,
                                      Unconnected);
      }
      else {
        c = null;
        idx = -1;
        newcts = this;
      }
      var sideinfo = new Pair<Connection,int>(c, idx);
      return new Pair<ConnectionTableState, Pair<Connection,int>>(newcts, sideinfo);
    }

    public ConnectionTableState RemoveUnconnected(Edge e) {
      var unc = Unconnected.RemoveFromNew(e);
      if( unc == Unconnected ) {
        //No change
        return this;
      }
      return new ConnectionTableState(Closed, View + 1,
                                      _type_to_conlist,
                                      _edge_to_con,
                                      unc);

    }
    /*
     * @throws Exception if con is not in the table
     */
    public Pair<ConnectionTableState, Pair<Connection,int>> UpdateStatus(Connection con, StatusMessage sm) {
      if( Closed ) { throw new TableClosedException(); }
      Address a = con.Address;
      Edge e = con.Edge;
      string con_type = con.ConType;
      LinkMessage plm = con.PeerLinkMessage;
        //Make the new connection and replace it in our data structures:
      Connection newcon = new Connection(e,a,con_type,sm,plm);
      var new_d = new Dictionary<Edge, Connection>(_edge_to_con);
      new_d[con.Edge] = newcon;
      int mt_idx = (int)con.MainType;
      var old_cl = _type_to_conlist[mt_idx];
      var newt_to_c = (ConnectionList[])_type_to_conlist.Clone();
      int index;
      newt_to_c[mt_idx] = ConnectionList.Replace(old_cl, con, newcon, out index);
      var newcts = new ConnectionTableState(false, View + 1,
                                      newt_to_c,
                                      new_d,
                                      Unconnected);
      var side = new Pair<Connection, int>(newcon, index);
      return new Pair<ConnectionTableState,Pair<Connection,int>>(newcts, side);
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

      public ConnectionTypeEnumerable(ConnectionTableState tab, string contype)
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

  public sealed class ConnectionTable : IEnumerable
  {
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
    /**
     * ANY time the ConnectionTableState State changes, this is fired
     * the EventArg is a ConnectionEventArgs with potentially null Connection, and idx = -1,
     * in the case of table closing, and AddUnconnected
     */
    public event EventHandler StateEvent;

    private readonly Mutable<ConnectionTableState> _cts;

    public ConnectionTableState State {
      get { 
        return _cts.State;
      }
    }
  
  /**
   * @param local the Address associated with the local node
   */

    public ConnectionTable()
    {
      _cts = new Mutable<ConnectionTableState>(new ConnectionTableState());
    }

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
    public ConnectionEventArgs Add(Connection c)
    {
      Converter<ConnectionTableState, Pair<ConnectionTableState,int>> add_up =
        delegate(ConnectionTableState cts) {
          var ncts = cts.RemoveUnconnected(c.Edge);
          return ncts.AddConnection(c);
        };
      Triple<ConnectionTableState,ConnectionTableState,int> res
        = _cts.Update<int>(add_up);
      
      var cea = new ConnectionEventArgs(c, res.Third, res.First, res.Second);
      SendEvent(ConnectionEvent, cea);
      SendEvent(StateEvent, cea);
      try {
        //If this edge is already closed, the below throws
        c.Edge.CloseEvent += RemoveHandler;
      }
      catch {
        RemoveHandler(c.Edge, null);
        throw;
      }
      
      if(ProtocolLog.Stats.Enabled) {
        ProtocolLog.Write(ProtocolLog.Stats, String.Format(
          "New Connection {0}|{1}", c, DateTime.UtcNow.Ticks));
      }
      return cea;
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
    public Pair<ConnectionTableState, ConnectionTableState> AddUnconnected(Edge e)
    {
      if(ProtocolLog.Stats.Enabled) {
        ProtocolLog.Write(ProtocolLog.Stats, String.Format(
          "Initial add to unconnected {0}|{1}", e, DateTime.UtcNow.Ticks));
      }
      Converter<ConnectionTableState, ConnectionTableState> addunc =
        delegate(ConnectionTableState cts) {
          Connection c = cts.GetConnection(e);
          if( c != null ) {
            throw new Exception("We already have a Connection, can't add to Unconnected: " + c.ToString());
          }
          return cts.AddUnconnected(e);
        };
      var res = _cts.Update(addunc);
      if( res.First != res.Second ) {
        //There was actually a state change:
        var cea = new ConnectionEventArgs(null, -1, res.First, res.Second);
        SendEvent(StateEvent, cea);
        try {
          e.CloseEvent += RemoveHandler;
        }
        catch {
          RemoveHandler(e, null);
          throw;
        }
      }
      return res;
    }

    /**
     * When the ConnectionTable is closed, Add will throw a
     * TableClosedException.
     * This is used at the time of Node.Disconnect to make sure
     * no new connections can be added
     */
    public Pair<ConnectionTableState, ConnectionTableState> Close() {
      Converter<ConnectionTableState, ConnectionTableState> close =
        delegate(ConnectionTableState cts) {
          return cts.Close();
        };
      var res = _cts.Update(close);
      if( res.First != res.Second ) {
        var cea = new ConnectionEventArgs(null, -1, res.First, res.Second);
        SendEvent(StateEvent, cea);
      }
      return res;
    }

    /**
     * This method removes the connection associated with an Edge,
     * then it adds this edge to the list of unconnected nodes.
     * 
     * @param e The edge to disconnect
     */
    public ConnectionEventArgs Disconnect(Edge e)
    {
      //Remove the edge, but keep a reference to it.
      return Remove(e, true);
    }

    /**
     * Remove the connection associated with an edge from the table
     * @param e Edge whose connection should be removed
     * @param add_unconnected if true, keep a reference to the edge in the
     * _unconnected list
     */
    private ConnectionEventArgs Remove(Edge e, bool add_unconnected)
    {
      Converter<ConnectionTableState,Pair<ConnectionTableState,
                                          Pair<Connection,int>>> remcon =
        delegate(ConnectionTableState cts) {
          //Remove the connection:
          Pair<ConnectionTableState, Pair<Connection,int>>
            res = cts.RemoveConnection(e);
          if(add_unconnected) {
            var ncts = res.First.AddUnconnected(e);
            return new Pair<ConnectionTableState, Pair<Connection,int>>(ncts, res.Second);
          }
          else { 
            if( res.Second.First == null ) {
              //There was no connection, in this case, we need to remove from
              //unconnected:
              var ncts = res.First.RemoveUnconnected(e);
              return new Pair<ConnectionTableState,Pair<Connection,int>>(ncts,res.Second);
            }
            else {
              //There was a connection, but now it has been removed:
              return res;
            }
          }
        };
      Triple<ConnectionTableState,ConnectionTableState,Pair<Connection,int>> ures
        = _cts.Update<Pair<Connection,int>>(remcon);
      Connection c = ures.Third.First;
      int idx = ures.Third.Second;
      var cea = new ConnectionEventArgs(c, idx, ures.First, ures.Second);
      if( idx != -1 ) {
        if(ProtocolLog.Connections.Enabled) {
          DateTime now = DateTime.UtcNow;
          TimeSpan con_life = now - c.CreationTime;
          ProtocolLog.Write(ProtocolLog.Connections,
            String.Format("New Disconnection[{0}]: {1}, instant: {2}, con_life: {3} ", 
			                    idx, c, now, con_life));
        }

        if(ProtocolLog.Stats.Enabled) {
          ProtocolLog.Write(ProtocolLog.Stats, String.Format(
            "Disconnection {0}|{1}", c, DateTime.UtcNow.Ticks));
        }
        SendEvent(DisconnectionEvent, cea);
        SendEvent(StateEvent, cea);
      }
      return cea;
    }

    /**
     * When an Edge closes, this handler is called
     * This is just a wrapper for Remove
     */
    private void RemoveHandler(object edge, EventArgs args)
    {
      Edge e = (Edge)edge;
      e.CloseEvent -= this.RemoveHandler;
      //Get rid of the edge and don't add it to our _unconnected list
      Remove(e, false);
    }
    private void SendEvent(EventHandler eh, ConnectionEventArgs cea) {
      if( eh != null ) {
        try { eh(this, cea); }
        catch(Exception x) {
          if(ProtocolLog.Exceptions.Enabled)
            ProtocolLog.Write(ProtocolLog.Exceptions, String.Format(
              "ConnectionEvent triggered exception: {0}\n{1}", cea, x));
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
    public ConnectionEventArgs UpdateStatus(Connection con, StatusMessage sm)
    {
      Converter<ConnectionTableState,Pair<ConnectionTableState,
                                          Pair<Connection,int>>> upcon =
        delegate(ConnectionTableState ct) {
          return ct.UpdateStatus(con, sm);
        };
      var res = _cts.Update<Pair<Connection,int>>(upcon);
      Pair<Connection,int> side = res.Third;
      ConnectionEventArgs cea = new ConnectionEventArgs(side.First, side.Second, res.First, res.Second);
      SendEvent(StatusChangedEvent, cea);
      SendEvent(StateEvent, cea);
      return cea;
    }
 // /////////////
 // Deprecated Methods.  These are not safe for concurrent programing because
 // The state can change between calls.  Prefer to read the State, and use the methods
 // there.
 // @todo find code that calls these methods, and replace it with reading the
 // State first, and working from that.
 // /////////////
    ///@deprecated
    public int TotalCount { 
      get {
        var cts = State;
        int count = 0;
        foreach(ConnectionType t in Enum.GetValues(typeof(ConnectionType)) ) {
          count += cts.GetConnections(t).Count;
        }
        return count;
      }
    }
    public int UnconnectedCount { get { return State.Unconnected.Count; } }
    
    ///@deprecated
    public bool Contains(ConnectionType t, Address a) {
      return IndexOf(t,a) >= 0;
    } 
    ///@deprecated
    public int Count(ConnectionType t) {
      return GetConnections(t).Count;
    }
    public ConnectionList GetConnections(ConnectionType t) {
      return State.GetConnections(t);
    } 
    public Connection GetConnection(ConnectionType t, int idx) {
      return GetConnections(t)[idx];
    }
    public Connection GetConnection(ConnectionType t, Address a) {
      try {
        return GetConnections(t)[a];
      }
      catch { return null; }
    }
    public Connection GetConnection(Edge e) {
      return State.GetConnection(e);
    }
    public IEnumerable GetConnections(string t) {
      return State.GetConnections(t);
    }
    public Connection GetLeftStructuredNeighborOf(Address ad) {
      return GetConnections(ConnectionType.Structured).GetLeftNeighborOf(ad);
    }
    public Connection GetRightStructuredNeighborOf(Address ad) {
      return GetConnections(ConnectionType.Structured).GetRightNeighborOf(ad);
    }
    public IEnumerable GetUnconnectedEdges() {
      return State.Unconnected;
    }
    public IEnumerator GetEnumerator() {
      return State.GetEnumerator();
    }
    public ArrayList GetNearestTo(Address dest, int max_count) {
      var cons = State.GetConnections(ConnectionType.Structured).GetNearestTo(dest, max_count);
      var res = new ArrayList();
      foreach(Connection c in cons) {
        res.Add(c);
      }
      return res;
    }
    public int IndexOf(ConnectionType t, Address a) {
      return State.GetConnections(t).IndexOf(a);
    }

  }

  /** A class to do some reading of the ConnectionTable
   */
  public class ConnectionTableRpc : IRpcHandler {
    protected readonly ConnectionTable _tab;
    protected readonly RpcManager _rpc;
    protected readonly Dictionary<Triple<ISender, string, object>, CallBackHandler> _hands;

    public ConnectionTableRpc(ConnectionTable tab, RpcManager rpc) {
      _tab = tab;
      _rpc = rpc;
      _hands = new Dictionary<Triple<ISender, string, object>, CallBackHandler>();
    }

    protected class CallBackHandler {
      protected readonly ISender _dest;
      protected readonly string _method;
      protected readonly object _state;
      protected readonly ConnectionTable _tab;
      protected readonly RpcManager _rpc;
      protected int _fails;
      protected int _send;

      /** After this many failures we stop trying
       */
      protected const int MAX_FAILS = 3;
      protected readonly Action<CallBackHandler> _on_fail;

      public Triple<ISender, string, object> Key {
        get {
          return new Triple<ISender, string, object>(_dest, _method, _state);
        }
      }

      public CallBackHandler(ConnectionTable tab, RpcManager rpc,
                             ISender d, string m, object s, Action<CallBackHandler> on_fail) {
        _tab = tab;
        _rpc = rpc;
        _dest = d;
        _method = m;
        _state = s;
        _fails = 0; 
        _send = 0;
        _on_fail = on_fail;
      }

      public void AddHandler(object o, EventArgs arg) {
        Channel c = new Channel(1);
        c.CloseEvent += this.FinishRpc;
        ConnectionEventArgs cargs = (ConnectionEventArgs)arg;
        try {
          if( 1 == _send ) {
            _rpc.Invoke(_dest, c, _method, "add", cargs.ToDictionary(), _state);
          }
        }
        catch(Exception) {
          Fail(); 
        }
      }
      
      protected void Fail() {
        int failcount = Interlocked.Increment(ref _fails);
        if( failcount >= MAX_FAILS ) {
          Stop();
        }
        _on_fail(this);
      }

      public void FinishRpc(object q, EventArgs arg) {
        try {
          Channel c = (Channel)q;
          RpcResult r = (RpcResult)c.Dequeue();
          r.AssertNotException();
        }
        catch(Exception) {
          Fail(); 
        }
      }

      public void RemHandler(object o, EventArgs arg) {
        Channel c = new Channel(1);
        c.CloseEvent += this.FinishRpc;
        ConnectionEventArgs cargs = (ConnectionEventArgs)arg;
        try {
          if( 1 == _send ) {
            _rpc.Invoke(_dest, c, _method, "rem", cargs.ToDictionary(), _state);
          }
        }
        catch(Exception) {
          Fail(); 
        }
      }

      public bool Start() {
        if( 0 == Interlocked.Exchange(ref _send, 1) ) {
          _tab.ConnectionEvent += this.AddHandler;
          _tab.DisconnectionEvent += this.RemHandler;
          return true;
        }
        return false;
      }
      public bool Stop() {
        if( 1 == Interlocked.Exchange(ref _send, 0) ) {
          _tab.ConnectionEvent -= this.AddHandler; 
          _tab.DisconnectionEvent -= this.RemHandler;
          return true;
        }
        return false;
      }
    }

    /** This is the public API we share with nodes:
     */
    public void HandleRpc(ISender caller, string method, IList arguments, object request_state) {
      if( method == "GetConnections" ) {
        ConnectionType ct = Connection.StringToMainType((string)arguments[0]);
        ConnectionList cl = _tab.State.GetConnections(ct);
        if( cl != null ) {
          _rpc.SendResult(request_state, cl.ToList());   
        }
        else {
          _rpc.SendResult(request_state, new object[0]);
        }
      }
      else if( method == "addConnectionHandler" ) {
        ISender cb_dest = ((ReqrepManager.ReplyState)caller).ReturnPath;
        string cb_method = (string)arguments[0];
        object cb_state = arguments[1];
        
        Triple<ISender, string, object> cb_key
          = new Triple<ISender, string, object>(cb_dest, cb_method, cb_state);
        CallBackHandler cbh =
             new CallBackHandler(_tab, _rpc, cb_dest, cb_method, cb_state, HandleFail);
        lock( _hands ) {
          if( _hands.ContainsKey( cb_key ) ) {
            throw new Exception("already have a matching callback method and state");
          }
          _hands.Add(cb_key, cbh);
        }
        cbh.Start();
        _rpc.SendResult(request_state, true);
      }
      else if( method == "removeConnectionHandler" ) {
        ISender cb_dest = ((ReqrepManager.ReplyState)caller).ReturnPath;
        string cb_method = (string)arguments[0];
        object cb_state = arguments[1];
        
        Triple<ISender, string, object> cb_key
          = new Triple<ISender, string, object>(cb_dest, cb_method, cb_state);
        CallBackHandler cbh;
        lock( _hands ) {
          //This throws an exception if there is no key
          cbh = _hands[cb_key];
          _hands.Remove(cb_key);
        }
        cbh.Stop();
        _rpc.SendResult(request_state, true);
      }
      else {
        throw new Exception("Unrecognized method: " + method);
      }
    }

    protected void HandleFail(CallBackHandler cbh) {
      lock( _hands ) {
        _hands.Remove(cbh.Key);
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

      Assert.AreEqual(tab.State.Count, 2, "total count");
      Assert.AreEqual(tab.State.GetConnections(ConnectionType.Structured).Count, 2, "structured count");
      
      int total = 0;
      foreach(Connection c in tab.State) {
	total++;
	//Mostly a hack to make sure the compiler doesn't complain about an
	//unused variable
	Assert.IsNotNull(c);
       
       	//Console.Error.WriteLine("{0}\n",c);
      }
      Assert.AreEqual(total,2,"all connections");
     
      int struct_tot = 0;
      foreach(Connection c in tab.State.GetConnections(ConnectionType.Structured)) {
        struct_tot++;
	//Mostly a hack to make sure the compiler doesn't complain about an
	//unused variable
	Assert.IsNotNull(c);
        //Console.Error.WriteLine("{0}\n",c);
      }
      Assert.AreEqual(struct_tot, 2, "structured connections");
      int near_tot = 0;
      foreach(Connection c in tab.State.GetConnections("structured.near")) {
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
          Assert.AreEqual( tab.State.GetConnection(e),
                           tab.State.GetConnections(ConnectionType.Structured)[a],
                           "Edge equals Address lookup");
                                                
        }
        //Now do some tests:
        for(int k = 0; k < 100; k++) {
          byte[] buf = new byte[ Address.MemSize ];
          r.NextBytes(buf);
          Address.SetClass(buf, 0);
          a1 = new AHAddress( MemBlock.Copy(buf, 0, buf.Length) );
        //Do the same for a2:
          r.NextBytes(buf);
          Address.SetClass(buf, 0);
          a2 = new AHAddress( MemBlock.Reference(buf, 0, buf.Length) );
        //Now do some checks:
        var structs = tab.State.GetConnections(ConnectionType.Structured);
        int r_c = structs.RightInclusiveCount(a1, a2);
        int l_c = structs.LeftInclusiveCount(a1, a2);
        //Now manually count them:
        int r_c_manual = 0;
        int l_c_manual = 0;
        int iterated = 0;
        foreach(Connection c in tab.State) {
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
        int r_c2 = structs.RightInclusiveCount(a2, a1);
        int l_c2 = structs.LeftInclusiveCount(a2, a1);
        //Console.Error.WriteLine("LIC(a,b): {0}, RIC(b,a): {1}", l_c2, r_c);
        Assert.AreEqual(l_c, r_c2, "RIC(a2, a1) == LIC(a1, a2)");
        Assert.AreEqual(r_c, l_c2, "LIC(a2, a1) == RIC(a1, a2)");
        }
        //Do some removals:
        while(tab.State.GetConnections(ConnectionType.Structured).Count > 0) {
          var cts = tab.State;
          //Check that the table is sorted:
          Address last_a = null;
          foreach(Connection cn in cts.GetConnections(ConnectionType.Structured)) {
            if( last_a != null ) {
              Assert.IsTrue( last_a.CompareTo( cn.Address ) < 0, "Sorted table");
            }
            last_a = cn.Address;
          }
          //Look at the first connection:
          Connection c = cts.GetConnections(ConnectionType.Structured)[0];
          Assert.AreEqual( c, cts.GetConnection(c.Edge), "Edge lookup");
          Assert.AreEqual( cts.GetConnection(c.Edge),
                           cts.GetConnections(ConnectionType.Structured)[c.Address],
                           "Edge equals Address lookup");
          //Check to see that UpdateStatus basically works
          ConnectionEventArgs cea = tab.UpdateStatus(c, null);
          Connection c2 = cea.Connection;
          Assert.AreEqual(cts, cea.OldState, "old state check");
          cts = cea.NewState;
          Assert.AreEqual( c2, cts.GetConnection(c.Edge), "Edge lookup 2");
          Assert.AreEqual( c2, cts.GetConnections(ConnectionType.Structured)[c.Address],
                           "Edge equals Address lookup");

          int before = cts.GetConnections(ConnectionType.Structured).Count;
          int uc_count = cts.Unconnected.Count;
          cea = tab.Disconnect(c.Edge);
          Assert.AreEqual(cts, cea.OldState, "old state check");
          cts = cea.NewState;
          int after = cts.GetConnections(ConnectionType.Structured).Count;
          int uc_count_a = cts.Unconnected.Count;
          Assert.AreEqual( before, (after + 1), "Disconnect subtracted one");
          Assert.AreEqual( uc_count, (uc_count_a - 1), "Disconnect added one _unconnected");
          Assert.IsTrue( cts.GetConnections(ConnectionType.Structured).IndexOf(c.Address) < 0, "Removal worked");
          Assert.IsNull( cts.GetConnection(c.Edge), "Connection is gone");
          Assert.IsTrue( cts.Unconnected.Contains(c.Edge), "Edge is _unconnected" );
          c.Edge.Close(); //Should trigger removal completely:
          cts = tab.State;
          Assert.IsFalse( cts.Unconnected.Contains(c.Edge), "Edge is completely gone");
          Assert.IsNull( cts.GetConnection( c.Edge ), "Connection is still gone");
        }
      }
    }
  }

#endif

}
