/**
 * Dependencies
 * Brunet.Address
 * Brunet.AHAddress
 * Brunet.AHAddressComparer
 * Brunet.ConnectionType
 * Brunet.ConnectionEventArgs
 * Brunet.Edge
 * Brunet.TransportAddress
 */

#define KML_DEBUG
//#define LOCK_DEBUG

using System;
using System.Collections;

namespace Brunet
{

 /**
  * Keeps track of all connections and all the
  * mappings of Address -> Edge,
  *             Edge -> Address,
  *             Edge -> ConnectionType
  *             ConnectionType -> Address List
  *             ConnectionType -> Edge List
  *
  * All classes other than ConnectionOverlord should only use
  * the ReadOnly methods (not Add or Remove).
  * ConnectionOverlord objects can call Add and Remove
  * 
  */

  public class ConnectionTable
  {

  /*private static readonly log4net.ILog _log =
      log4net.LogManager.GetLogger(System.Reflection.MethodBase.
      GetCurrentMethod().DeclaringType);*/
  
    protected Random _rand;

    protected Hashtable type_to_addlist;
    protected Hashtable type_to_edgelist;
    protected Hashtable edge_to_add;
    protected Hashtable edge_to_type;

    protected ArrayList unconnected;

    protected Hashtable ta_to_edge;

    /**
     * These are the addresses we are trying to connect to.
     * The key is the address, the value is the object that
     * holds the lock.
     */
    protected Hashtable _address_locks;

    /** an object to lock for thread sync */
    private object _sync;
    /** Allows external objects to make sure the ConnectionTable
     * does not change as they are working with it
     */
    public object SyncRoot {
      get { return _sync; }
    }
    /**
     * When there is a new connection, this event
     * is fired.
     */
    public event EventHandler ConnectionEvent;
    /**
     * When a connection is lost, this event is fired
     */
    public event EventHandler DisconnectionEvent;
    
    protected AHAddressComparer _cmp;
    public AHAddressComparer AHAddressComparer
    {
      get
      {
        return _cmp;
      }
    }

  /**
   * Since address lists in the ConnectionTable are sorted,
   * we need to assign the AHAddressComparer which does
   * those comparisons.
   */
    public ConnectionTable(AHAddressComparer cmp)
    {
      _rand = new Random(DateTime.Now.Millisecond);

      _sync = new Object();
      lock( _sync ) {
        _cmp = cmp;
        type_to_addlist = new Hashtable();
        type_to_edgelist = new Hashtable();
        edge_to_add = new Hashtable();
        edge_to_type = new Hashtable();

        //unconnected = new Hashtable();
        unconnected = ArrayList.Synchronized(new ArrayList());

        _address_locks = new Hashtable();
	foreach(ConnectionType t in Enum.GetValues(typeof(ConnectionType)) ) {
	  /**
	   * We have a lock table for each type
	   */
          _address_locks[t] = new Hashtable();
	}
        
        // retrieve an edge by its remote ta. the local address is the same for all edges in this table
        ta_to_edge = new Hashtable();

        // init all--it is safer to do it this way and avoid null pointer exceptions

        Init(ConnectionType.Leaf);
        Init(ConnectionType.Structured);
        Init(ConnectionType.Unstructured);        
        Init(ConnectionType.Unknown);
      }
    }

    /**
     * Add the triplet, (t,a,e) at the index into the
     * table.  Only ConnectionOverlord objects should
     * call this method.
     *
     * The Connection index is selected by inserting
     * the Address such that the address list for each
     * ConnectionType is sorted.
     * 
     * When an Edge is added, the ConnectionTable listens
     * for the Edges close event, and at that time, the
     * edge is removed.  Edges should not be removed
     * explicitly from the ConnectionTable, rather the
     * Edge method Edge.Close() should be called, and
     * the ConnectionTable will react properly
     * 
     * @return the index we insert to
     */
    public int Add(ConnectionType t,
		    Address a,
		    Edge e)
    {
     int index;

     lock(_sync) {

      ArrayList adds = (ArrayList)type_to_addlist[t];
      index = adds.BinarySearch(a, _cmp);
      if (index < 0)
      {
        //This is a new address:
	index = ~index;
      }
      else {
        //This is an old address, no good
	throw new Exception("Address: " + a.ToString() + " already in ConnectionTable");
      }
      adds.Insert(index, a);
      ((ArrayList)type_to_edgelist[t]).Insert(index, e);
      edge_to_add[e] = a;
      edge_to_type[e] = t;
      
      ((Hashtable)ta_to_edge[t]).Add(e.RemoteTA.ToString(),e);
     } /* we release the lock */
      
      //Now that we have registered the new CloseEvent handler,
      //we can remove the old one
      int ucidx = unconnected.IndexOf(e);
      if( ucidx >= 0 ) {
	//Remove the edge from the unconnected table
        unconnected.RemoveAt(ucidx);
      }
      else {
        //This is a new connection, so we need to add the CloseEvent
        /* Tell the edge to let you know when it dies: */
        e.CloseEvent += new EventHandler(this.RemoveHandler);
      }
      
      /*_log.Info("ConnectionEvent: address: " + a.ToString() + 
		                ", edge: " + e.ToString() +
				", type: " + t.ToString() +
				", index: " + index);*/


      #if KML_DEBUG
      System.Console.WriteLine("ConnectionEvent: address: " + a.ToString() + 
		                ", edge: " + e.ToString() +
				", type: " + t.ToString() +
				", index: " + index);    
      //System.Console.ReadLine();
      #endif

      /* Send the event: */
      if( ConnectionEvent != null )
        ConnectionEvent(this, new ConnectionEventArgs(a, e, t, index) );
      return index;
    }
  
    /**
     * This function is to check if a given address of a given type
     * is already in the table.  It is a synonym for IndexOf(t,a) >= 0.
     * This function is just to eliminate any chance of confusion arising
     * from using IndexOf to check for existence
     */
    public bool Contains(ConnectionType t, Address a)
    {
      ArrayList al = (ArrayList)type_to_addlist[t];
      bool result = (IndexOf(t,a) >= 0);
      return result;
    }
    /**
     * @param t the ConnectionType we want to know the count of
     * @return the number of connections of this type
     */
    public int Count(ConnectionType t)
    {
     lock(_sync) {
      object val = type_to_edgelist[t];
      if( val == null ) {
        return 0;
      }
      else {
        return ((ArrayList)val).Count;
      }
     }
    }
   
    /**
     * This method removes the connection associated with an Edge,
     * then it adds this edge to the list of unconnected nodes.
     * This would be almost the same as Remove(e); AddUnconnected(e);
     * but Remove would fire an event which should not be fired
     * until after the Edge is added to the Unconnected list
     * 
     * @param e The edge to disconnect
     */
    public void Disconnect(Edge e)
    {
      ConnectionType t;
      int index;
      Address remote;
      bool have_con = false;
      lock(_sync) {
        have_con = GetConnection(e, out t, out index, out remote);
	if( have_con )  {
          Remove(t, index);
	  unconnected.Add(e);
	}
      }
      if( have_con ) {
      /*_log.Info("DisconnectionEvent: address: " + remote.ToString() + 
		                ", edge: " + e.ToString() +
				", type: " + t.ToString() +
				", index: " + index);*/
      #if DEBUG
      System.Console.WriteLine("Disconnect: DisconnectionEvent: address: " + remote.ToString() + 
		                ", edge: " + e.ToString() +
				", type: " + t.ToString() +
				", index: " + index);    
      #endif
        //Announce the disconnection:
        if( DisconnectionEvent != null )
          DisconnectionEvent(this, new ConnectionEventArgs(remote, e, t, index));
      }
    }
    
  public int UnconnectedCount
  {
    get
    {
      return unconnected.Count;
    }
  }

    /**
     * Returns the Address associated with this Edge
     */
  public Address GetAddressFor(Edge e)
  {
    lock( _sync ) {
      return (Address) edge_to_add[e];
    }
  }

  /**
   * Gets the edge for the left structured neighbor of a given AHAddress
   */
  public Edge GetLeftStructuredNeighborOf(AHAddress address)
  {
    lock( _sync ) {
      int i = IndexOf(ConnectionType.Structured, address);
      if (i<0) {
        i = ~i;
      }
      else {
        i++;
      }

      Address neighbor_add=null;
      Edge    neighbor_edge=null;
      GetConnection(ConnectionType.Structured, i, out neighbor_add, out neighbor_edge);

      return neighbor_edge;
    }
  }

  /**
   * Gets the edge for the right structured neighbor of a given AHAddress
   */
  public Edge GetRightStructuredNeighborOf(AHAddress address)
  {
    lock( _sync ) {
      int i = IndexOf(ConnectionType.Structured, address);
      if (i<0) {
        i = ~i;
      }

      i--;
      Address neighbor_add=null;
      Edge    neighbor_edge=null;
      GetConnection(ConnectionType.Structured, i, out neighbor_add, out neighbor_edge);

      return neighbor_edge;
    }
  }

    /**
     * @param t the ConnectionType of connection in question
     * @param index the index of the connection in question
     * @param add the Address of the node in the connection
     * @param e the edge to the node in the connection
     *
     * The index "wraps around", or equivalently, 
     * the result of getting (index + count) is the
     * same as (index)
     */
    public void GetConnection(ConnectionType t, int index,
		              out Address add, out Edge e)
    {
     lock(_sync ) {
      int count = ((ArrayList)type_to_addlist[t]).Count;
      if( count == 0 ) {
        throw new System.ArgumentOutOfRangeException("index", index,
			    "Trying to get and index from an empty Array");
      }
      index %= count;
      if( index < 0 ) {
        index += count;
      }
      add = (Address)((ArrayList)type_to_addlist[t])[index];
      e = (Edge)((ArrayList)type_to_edgelist[t])[index];
     }
    }
   
    /**
     * @param e Edge to check for a connection
     * @param t the ConnectionType of the Edge if there is a connection
     * @param index the index in the ConnectionTable of this connection
     * @param add the address for this Connection
     */
    public bool GetConnection(Edge e, out ConnectionType t, out int index,
		              out Address add)
    {
      bool have_con = false;
      lock( _sync ) {
	t = GetConnectionType(e);
	if( t != ConnectionType.Unknown ) {
          index = ((ArrayList)type_to_edgelist[t]).IndexOf(e);
          add = (Address)((ArrayList)type_to_addlist[t])[index];
	  have_con = true;
	}
	else {
          index = -1;
	  add = null;
	  have_con = false;
	}
      }
      return have_con;
    }
    
    /**
     * Returns the ConnectionType for the given edge
     * @return ConnectionType.Unknown if the edge is not connected
     */
    public ConnectionType GetConnectionType(Edge e)
    {
      ConnectionType ret_val = ConnectionType.Unknown;
      if( e != null ) {
        lock( _sync ) {
          if (edge_to_type.Contains(e))
            ret_val = (ConnectionType) edge_to_type[e];
	}
      }
      return ret_val;
    }

    /**
     * Returns a ReadOnly ArrayList of addresses of a given
     * type
     */
    public ArrayList GetAddressesOfType(ConnectionType t)
    {
     lock( _sync ) {
      object val = type_to_addlist[t];
      if( val != null ) {
        return ArrayList.ReadOnly( (ArrayList) val );
      }
      else {
        return null;
      }
     }
    }

    /**
     * Returns a ReadOnly ArrayList of the edges of a given
     * type
     */
    public ArrayList GetEdgesOfType(ConnectionType t)
    {
     lock(_sync) {
      object val = type_to_edgelist[t];
      if( val != null ) {
        return ArrayList.ReadOnly( (ArrayList) val );
      }
      else {
        return null;
      }
     }
    }
 
    /**
     * Returns a ReadOnly ArrayList of the unconnected edges
     */
    public ArrayList GetUnconnectedEdges()
    {
      return ArrayList.ReadOnly( unconnected );
    }
    
    /**
     * Returns an edge of a given type at a given index.
     */
    public Edge GetEdgeTypeAt(ConnectionType t, int position)
    {
      // do not check for null because type_to_edgelist should
      // be initialized for all edgetypes. this should improve
      // the performance of the router code.
      lock(_sync) {
        return (Edge)((ArrayList)type_to_edgelist[t])[position];
      }
    }

    /**
     * Returns an unstructured edge at a given index.
     */
    public Edge GetUnstructuredEdgeAt(int position)
    {
      // do not check for null because type_to_edgelist should
      // be initialized for all edgetypes. this should improve
      // the performance of the router code.
      lock(_sync) {
        return (Edge)((ArrayList)type_to_edgelist[ConnectionType.Unstructured])[position];
      }
    }

    /**
     * Returns a random unstructured edge which is different from the from Edge.
     * @param from The edge to avoid.
     */
    public Edge GetRandomUnstructuredEdge(Edge from)
    {
      lock(_sync) {
        object val = type_to_edgelist[ConnectionType.Unstructured];
        // do not check for null because type_to_edgelist should
        // be initialized for all edgetypes. this should improve
        // the performance of the router code.
        ArrayList unstructured_edges = (ArrayList)val;
        int size = unstructured_edges.Count;
        if (size<1) return null;

        int position = _rand.Next(size - 1);

        Edge e = (Edge) unstructured_edges[position];
        if (e!=from) return e;

        if (size==1) return null;

        if (position == size-1) {
          position = 0;
        } else {
          position++;
        }
        return (Edge) unstructured_edges[position];
      }
    }

    /**
     * Before we can use a ConnectionType, that type must
     * be initialized 
     */
    public void Init(ConnectionType t)
    {
     lock(_sync) {
      type_to_addlist[t] = ArrayList.Synchronized(new ArrayList());
      type_to_edgelist[t] = ArrayList.Synchronized(new ArrayList());
      ta_to_edge[t] = new Hashtable();
     }
    }

    /**
     * @param t the ConnectionType
     * @param a the Address you want to know the index of
     * @return the index.  If it is negative, the bitwise
     * compliment would indicate where it should be in the
     * list.
     */
    public int IndexOf(ConnectionType t, Address a)
    {
     lock(_sync) {
      int index = 0;
      if( Count(t) == 0 ) {
	//This item would be the first in the list
        index = ~index;
      }
      else {
	//Search for the item
        /**
	 * @throw an ArgumentNullException (ArgumentException)for the
	 * the BinarySearch.
	 */
        ArrayList add_list = (ArrayList)type_to_addlist[t];
        index = add_list.BinarySearch(a, _cmp);
      }
      return index;
     }
    }
   
    /**
     * @param t the ConnectionType
     * @param e the Edge you want to know the index of
     * @return the index.  If it equals -1, the edge is
     * not of this type
     */
    public int IndexOf(ConnectionType t, Edge e)
    {
      lock ( _sync )  {
        ArrayList edge_list = (ArrayList)type_to_edgelist[t];
	return edge_list.IndexOf(e);
      }
    }
    /**
     * @param a the Address to lock
     * @param locker the object wishing to hold the lock
     *
     * We use this to make sure that two linkers are not
     * working on the same address
     *
     * @throws System.InvalidOperationException if we cannot get the lock
     */
    public void Lock(Address a, ConnectionType t, object locker)
    {
      if( a == null ) { return; }
      
      lock( _sync ) {
	Hashtable locks = (Hashtable)_address_locks[t];
        if( !locks.ContainsKey(a) ) {
	  locks[a] = locker;
#if LOCK_DEBUG
	  Console.WriteLine("{0}, locker: {1} Locking: {2}", _cmp.Zero,
			    locker, a);
#endif
	  return;
	}
	else {
#if LOCK_DEBUG
          Console.WriteLine(
			  "{0}, {1} tried to lock {2}, but {3} holds the lock",
			    _cmp.Zero,
			    locker,
			    a,
			    locks[a]);
#endif
	  throw new System.InvalidOperationException("Could not get lock on: " +
			                             a.ToString());
	}
      }
    }
   
    /**
     * Remove the connection associated with an edge from the table
     * Should only be called by ConnectionOverlord or
     * if you REALLY, REALLY know what you are doing!
     * param e Edge whose connection should be removed
     */
    protected void Remove(Edge e)
    {
      ConnectionType t;
      int index;
      Address remote;
      bool have_con = false;
      e.CloseEvent -= new EventHandler(this.RemoveHandler);
      lock(_sync) {
        have_con = GetConnection(e, out t, out index, out remote);
	if( have_con ) 
          Remove(t, index);
	else
          unconnected.Remove(e);
      }
      if( have_con ) {
      /*_log.Info("DisconnectionEvent: address: " + remote.ToString() + 
		                ", edge: " + e.ToString() +
				", type: " + t.ToString() +
				", index: " + index);*/
      #if DEBUG
      System.Console.WriteLine("Remove: DisconnectionEvent: address: " + remote.ToString() + 
		                ", edge: " + e.ToString() +
				", type: " + t.ToString() +
				", index: " + index);    
      #endif
        //Announce the disconnection:
        if( DisconnectionEvent != null )
          DisconnectionEvent(this, new ConnectionEventArgs(remote, e, t, index));
      }
    }
    
    /**
     * Remove an index from the table.
     * @param t ConnectionType of the removed edge
     * @param index index of the removed edge
     */
    protected void Remove(ConnectionType t, int index)
    { 
     lock(_sync) {
      //Get the edge we are removing:
      Edge e = (Edge)((ArrayList)type_to_edgelist[t])[index];
      //Remove the edge from the lists:
      ((ArrayList)type_to_addlist[t]).RemoveAt(index);
      ((ArrayList)type_to_edgelist[t]).RemoveAt(index);
      //Remove the edge from the tables:
      edge_to_add.Remove(e);
      edge_to_type.Remove(e);

      ((Hashtable)ta_to_edge[t]).Remove(e.RemoteTA.ToString());
     }
    }

    /**
     * When an Edge closes, this handler is called
     * This is just a wrapper for Remove
     */
    protected void RemoveHandler(object edge, EventArgs args)
    {
      Remove((Edge)edge);
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
      myEnumerator = type_to_addlist.GetEnumerator();
      while (myEnumerator.MoveNext()) {
        sb.Append("Type: ");
        sb.Append(myEnumerator.Key.ToString() + "\n");
        sb.Append("Address Table:\n");
        ArrayList t = (ArrayList) myEnumerator.Value;
        for (int i=0; i<t.Count; i++) {
        System.Object o = (System.Object)t[i];
        sb.Append("\t" + i + "---" + o.ToString() + "\n");
        }
        /*foreach(System.Object o in t) {
          sb.Append("\t" + o.ToString() + "\n");
          }*/
      }
      sb.Append("\nType : Edge Table\n");
      myEnumerator = type_to_edgelist.GetEnumerator();
      while (myEnumerator.MoveNext()) {
        sb.Append("Type: ");
        sb.Append(myEnumerator.Key.ToString() + "\n");
        sb.Append("Edge Table:\n");
        ArrayList t = (ArrayList) myEnumerator.Value;
        foreach(System.Object o in t) {
          sb.Append("\t" + o.ToString() + "\n");
        }
      }
      sb.Append("\nEdge : Address\n");
      myEnumerator = edge_to_add.GetEnumerator();
      while (myEnumerator.MoveNext()) {
        sb.Append("Edge: ");
        sb.Append(myEnumerator.Key.ToString() + "\n");
        sb.Append("Address: ");
        sb.Append(myEnumerator.Value.ToString() + "\n");
      }
      sb.Append("\nEdge : Type\n");
      myEnumerator = edge_to_type.GetEnumerator();
      while (myEnumerator.MoveNext()) {
        sb.Append("Edge: ");
        sb.Append(myEnumerator.Key.ToString() + "\n");
        sb.Append("Type: ");
        sb.Append(myEnumerator.Value.ToString() + "\n");
      }
     }
      sb.Append("\n------End of Table------\n\n");
      return sb.ToString();
    }
    
    /**
     * We use this to make sure that two linkers are not
     * working on the same address
     * @param a Address to unlock
     * @param locker the object which holds the lock.
     * @throw Exception if the lock is not held by locker
     */
    public void Unlock(Address a, ConnectionType t, object locker)
    {
     if( a != null ) {
      lock( _sync ) {
        Hashtable locks = (Hashtable)_address_locks[t];
#if LOCK_DEBUG
        Console.WriteLine("{0} Unlocking {1}",
			    _cmp.Zero,
			    a);
#endif
	if( !locks.ContainsKey(a) ) {
#if LOCK_DEBUG
          Console.WriteLine("On node " +
			     _cmp.Zero.ToString() +
			     ", " + locker.ToString() + " tried to unlock " +
			     a.ToString() + " but no such lock" );

#endif
          throw new Exception("On node " +
			     _cmp.Zero.ToString() +
			     ", " + locker.ToString() + " tried to unlock " +
			     a.ToString() + " but no such lock" );
	}
	object real_locker = locks[a];
	if( real_locker != locker ) {
#if LOCK_DEBUG
          Console.WriteLine("On node " +
			     _cmp.Zero.ToString() +
			     ", " + locker.ToString() + " tried to unlock " +
			     a.ToString() + " but not the owner" );
#endif

          throw new Exception("On node " +
			     _cmp.Zero.ToString() +
			     ", " + locker.ToString() + " tried to unlock " +
			     a.ToString() + " but not the owner" );
	}
        locks.Remove(a);
      }
     }
    }

  /**
   * When a new Edge is created by the the Linker or received
   * by the ConnectionPacketHandler, they tell the ConnectionTable
   * about it.  It is sort of a null connection.  The ConnectionTable
   * should still know about all the Edge objects for the Node.
   *
   * When a connection is made, there is never a need to remove
   * unconnected edges.  Either a connection is made (which will
   * remove the edge from this list) or the edge will be closed
   * (which will remove the edge from this list).
   *
   * @param e the new unconnected Edge
   */
  public void AddUnconnected(Edge e)
  {
    //System.Console.WriteLine("ADDING EDGE {0} TO UNCONNECTED", e.ToString());
    lock( _sync ) {
      unconnected.Add(e);
    }
    e.CloseEvent += new EventHandler(this.RemoveHandler);
  }

  /**
   * @param remote TransportAddress of an edge to check
   * @return true if there is an edge with this Remote TransportAddress
   * in the the Unconnected Edge list
   */
  public bool IsInUnconnected(TransportAddress remote)
  {
    lock ( _sync ) {
      foreach(Edge nextEdge in unconnected) {
        // consider upper/lower case
        if (remote.CompareTo(nextEdge.RemoteTA)==0) return true;
      }
    }
    return false;
  }
  /**
   * @param edge Edge to check to see if it is an Unconnected Edge
   * @return true if this edge is an unconnected edge
   */
  public bool IsUnconnected(Edge e)
  {
    lock( _sync ) {
      return unconnected.Contains(e);
    }
  }

  public bool IsConnectedToRemoteTA(ConnectionType t, TransportAddress remote)
    {
    return ((Hashtable)ta_to_edge[t]).ContainsKey(remote.ToString());
    }

  public bool ConnectionReserved(ConnectionType t, TransportAddress remote)
  {
    return (IsInUnconnected(remote) || IsConnectedToRemoteTA(t,remote));
  }

  /*public bool ContainsUnconnectedTo(TransportAddress rta)
    {
    return unconnected.ContainsKey(rta.ToString());
    }*/

  public void PrintHashKeys( Hashtable hash ) 
    { 

    foreach (string s in hash.Keys)
      {
      Console.WriteLine("{0} = {1}", s, hash[s]);
      }

    }

  public void PrintListValues(ArrayList myList)
  {
    Console.WriteLine("active connections count: {0}", myList.Count);
    foreach(object el in myList) {
      Console.WriteLine(" {0}",el);
    }
  }

  }


}
