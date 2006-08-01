/*
This program is part of BruNet, a library for the creation of efficient overlay networks.
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

/**
 * Dependencies
 * Brunet.Address
 * Brunet.AHAddress
 * Brunet.AHAddressComparer
 * Brunet.BrunetLogger
 * Brunet.ConnectionType
 * Brunet.ConnectionEventArgs
 * Brunet.Edge
 * Brunet.TransportAddress
 */

//#define KML_DEBUG
//#define LOCK_DEBUG


#if BRUNET_NUNIT
using NUnit.Framework;
#endif

using System;
using System.Collections;
using System.IO;
using System.Globalization;
using System.Xml.Serialization;
using System.Xml;
using System.Text;


namespace Brunet
{

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

    /*private static readonly log4net.ILog _log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/

    protected Random _rand;

#if PLAB_CONNECTION_LOG
    private BrunetLogger _logger;
    public BrunetLogger Logger{
      get{
        return _logger;
      }
      set
      {
        _logger = value;          
      }
    }
#endif
    protected Hashtable type_to_addlist;
    protected Hashtable type_to_edgelist;
    protected Hashtable edge_to_con;

    protected ArrayList unconnected;

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
    /**
     * When status changes, this event is fired
     */
    public event EventHandler StatusChangedEvent;

    protected Address _local;
    
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
     * Note, that ConnectionTable objects are synchronized, but if
     * you don't want the table to change between method calls, you
     * need to explicitly lock SyncRoot.
     */
    public bool IsSynchronized {
      get { return true; }
    }

   
  /**
   * @param local the Address associated with the local node
   */

    public ConnectionTable(Address local)
    {
      _rand = new Random(DateTime.Now.Millisecond);

      _sync = new Object();
      lock( _sync ) {
        _local = local;
        type_to_addlist = new Hashtable();
        type_to_edgelist = new Hashtable();
        edge_to_con = new Hashtable();

        unconnected = new ArrayList();

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
     */
    public void Add(Connection c)
    {
      int index;
      ConnectionType t = c.MainType;
      Address a = c.Address;
      Edge e = c.Edge;

      lock(_sync) {
        index = IndexOf(t, a);
        if (index < 0) {
          //This is a new address:
          index = ~index;
        }
        else {
          //This is an old address, no good
          throw new Exception("Address: " + a.ToString() + " already in ConnectionTable");
        }
        /*
         * Here we actually do the storing:
         */
        ((ArrayList)type_to_addlist[t]).Insert(index, a);
        ((ArrayList)type_to_edgelist[t]).Insert(index, e);
        edge_to_con[e] = c;

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
#if PLAB_CONNECTION_LOG
      BrunetEventDescriptor bed = new BrunetEventDescriptor();
      bed.EventDescription = "connection";
      bed.ConnectionType = t;
      bed.LocalTAddress = e.LocalTA.ToString();     
      bed.LocalPort = e.LocalTA.Port.ToString();
      bed.RemoteTAddress = e.RemoteTA.ToString();
      bed.RemoteAHAddress = a.ToBigInteger().ToString();
      bed.RemoteAHAddressBase32 = a.ToString();
      bed.ConnectTime = DateTime.Now.Ticks;
      bed.SubType = c.ConType;     
      bed.StructureDegree = Count(ConnectionType.Structured);

      _logger.LogBrunetEvent( bed );
      //Console.WriteLine("Table size is: {0}", TotalCount);
#endif

      #if KML_DEBUG
      System.Console.WriteLine("ConnectionEvent: address: " + a.ToString() +
                               ", edge: " + e.ToString() +
                               ", type: " + t.ToString() +
                               ", index: " + index);
      //System.Console.ReadLine();
      #endif

      /* Send the event: */
      if( ConnectionEvent != null )
        ConnectionEvent(this, new ConnectionEventArgs(c, index) );
     // return index;
    }

    /**
     * This function is to check if a given address of a given type
     * is already in the table.  It is a synonym for IndexOf(t,a) >= 0.
     * This function is just to eliminate any chance of confusion arising
     * from using IndexOf to check for existence
     */
    public bool Contains(ConnectionType t, Address a)
    {
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
      int index = -1;
      Connection c = null;
      bool have_con = false;
      lock(_sync) {
        c = GetConnection(e);	
        have_con = (c != null);
        if( have_con )  {
          index = IndexOf(c.MainType, c.Address);
          Remove(c.MainType, index);
          unconnected.Add(e);
        }
      }
      if( have_con ) {

#if PLAB_CONNECTION_LOG
        BrunetEventDescriptor bed = new BrunetEventDescriptor();
        bed.EventDescription = "disconnection";
        bed.ConnectionType = c.MainType;
        bed.LocalTAddress = c.Edge.LocalTA.ToString();
        bed.RemoteTAddress = c.Edge.RemoteTA.ToString();
        bed.RemoteAHAddress = c.Address.ToBigInteger().ToString();
        bed.RemoteAHAddressBase32 = c.Address.ToString();
        bed.ConnectTime = DateTime.Now.Ticks;
        bed.SubType = c.ConType;
        bed.StructureDegree = Count(ConnectionType.Structured);

        _logger.LogBrunetEvent( bed );
#endif


      #if KML_DEBUG
        System.Console.WriteLine("Disconnect: DisconnectionEvent: address: " + remote.ToString() +
                                 ", edge: " + e.ToString() +
                                 ", type: " + t.ToString() +
                                 ", index: " + index);
      #endif
        //Announce the disconnection:
        if( DisconnectionEvent != null )
          DisconnectionEvent(this, new ConnectionEventArgs(c, index));
      }
    }

    public int UnconnectedCount
    {
      get
      {
        lock (_sync ) {
          return unconnected.Count;
        }
      }
    }
    /**
     * Required for IEnumerable Interface
     */
    public IEnumerator GetEnumerator()
    {
      return new ConnectionEnumerator(this);
    }
    
    /**
     * Gets the Connection for the left structured neighbor of a given AHAddress
     */
    public Connection GetLeftStructuredNeighborOf(AHAddress address)
    {
      lock( _sync ) {
        int i = IndexOf(ConnectionType.Structured, address);
        if (i<0) {
          i = ~i;
        }
        else {
          i++;
        }
        return GetConnection(ConnectionType.Structured, i);
      }
    }

    /**
     * Gets the Connection for the right structured neighbor of a given AHAddress
     */
    public Connection GetRightStructuredNeighborOf(AHAddress address)
    {
      lock( _sync ) {
        int i = IndexOf(ConnectionType.Structured, address);
        if (i<0) {
          i = ~i;
        }
        i--;
        return GetConnection(ConnectionType.Structured, i);
      }
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
      Connection c = null;
      lock(_sync ) {
        ArrayList list = (ArrayList)type_to_edgelist[t];
        int count = list.Count;
        if( count == 0 ) {
          return null;
        }
        index %= count;
        if( index < 0 ) {
          index += count;
        }
        Edge e = (Edge)list[index];
        c = GetConnection(e);
      }
      return c;
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
      Connection c = null;
      lock( _sync ) {
        int idx = IndexOf(t, a);
        if( idx >= 0 ) {
          c = GetConnection(t, idx);
        }
      }
      return c;
    }
    /**
     * Returns a Connection for the given edge:
     * @return Connection
     */
    public Connection GetConnection(Edge e)
    {
      Connection c = null;
      lock(_sync) {
        if( e != null && edge_to_con.ContainsKey(e) ) {
          c = (Connection)edge_to_con[e];
	}
      }
      return c;
    }

    /**
     * Return all the connections of type t.
     * @param t the Type of Connections we want
     * @return an enumerable that we can foreach over
     */
    public IEnumerable GetConnections(ConnectionType t)
    {
      return new ConnectionTypeEnumerable(this, t);
    }
    /**
     * Return all the connections of type t.
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
     */
    public ArrayList GetNearestTo(AHAddress dest, int max_count)
    {
      ArrayList ret_val = new ArrayList();
      lock( _sync ) {
        int max = Count(ConnectionType.Structured);
	if( max_count > max ) {
          max_count = max;
	}
        int idx = IndexOf(ConnectionType.Structured, dest);
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
        if( skip_idx ) { end++; }
	for( int pos = start; pos < end; pos++) {
          if( skip_idx && ( pos == idx ) ) { pos++; }
          Connection c = GetConnection(ConnectionType.Structured, pos);
          ret_val.Add( c );
        }
      }
      return ret_val;
    }
    /**
     * @return a random connection of type t
     */
    public Connection GetRandom(ConnectionType t)
    {
      lock(_sync) {
        int size = Count(t);
	if( size == 0 ) {
          return null;
	}
	else {
          int pos = _rand.Next( size );
	  return GetConnection(t, pos);
	}
      }
    }

    /**
     * Returns an IEnumerable of the unconnected edges
     */
    public IEnumerable GetUnconnectedEdges()
    {
      return new ArrayList(unconnected);
    }

    /**
     * Before we can use a ConnectionType, that type must
     * be initialized 
     */
    protected void Init(ConnectionType t)
    {
      lock(_sync) {
        type_to_addlist[t] = new ArrayList();
        type_to_edgelist[t] = new ArrayList();
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
          index = add_list.BinarySearch(a);
        }
        return index;
      }
    }
    /**
     * @param a the Address to lock
     * @param t the type of connection
     * @param locker the object wishing to hold the lock
     *
     * We use this to make sure that two linkers are not
     * working on the same address for the same connection type
     *
     * @throws System.InvalidOperationException if we cannot get the lock
     */
    public void Lock(Address a, string t, ILinkLocker locker)
    {
      if( a == null ) { return; }
      ConnectionType mt = Connection.StringToMainType(t);
      lock( _sync ) {
        Hashtable locks = (Hashtable)_address_locks[mt];
	if( locks == null ) {
          locks = new Hashtable();
	  _address_locks[mt] = locks;
	}
	ILinkLocker old_locker = (ILinkLocker)locks[a];
        if( null == old_locker ) {
          locks[a] = locker;
#if LOCK_DEBUG
          Console.WriteLine("{0}, locker: {1} Locking: {2}", _local,
                            locker, a);
#endif
          return;
        }
	else if ( old_locker.AllowLockTransfer(a,t,locker) ) {
	  //See if we can transfer the lock:
          locks[a] = locker;
	}
        else {
#if LOCK_DEBUG
          Console.WriteLine(
            "{0}, {1} tried to lock {2}, but {3} holds the lock",
            _local,
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
      int index = -1;
      bool have_con = false;
      Connection c = null;
      e.CloseEvent -= new EventHandler(this.RemoveHandler);
      lock(_sync) {
        c = GetConnection(e);	
        have_con = (c != null);
        if( have_con )  {
          index = IndexOf(c.MainType, c.Address);
          Remove(c.MainType, index);
        }
        else
          unconnected.Remove(e);
      }
      if( have_con ) {

#if PLAB_CONNECTION_LOG
        BrunetEventDescriptor bed = new BrunetEventDescriptor();
        bed.EventDescription = "disconnection";
        bed.ConnectionType = t;
        bed.LocalTAddress = e.LocalTA.ToString();
        bed.RemoteTAddress = e.RemoteTA.ToString();
        bed.RemoteAHAddress = remote.ToBigInteger().ToString();
        bed.RemoteAHAddressBase32 = remote.ToString();
        bed.ConnectTime = DateTime.Now.Ticks;
        bed.SubType = c.ConType;
        bed.StructureDegree = Count(ConnectionType.Structured);

        _logger.LogBrunetEvent( bed );
#endif


      #if DEBUG
        System.Console.WriteLine("Remove: DisconnectionEvent: address: " + remote.ToString() +
                                 ", edge: " + e.ToString() +
                                 ", type: " + t.ToString() +
                                 ", index: " + index);
      #endif
        //Announce the disconnection:
        if( DisconnectionEvent != null )
          DisconnectionEvent(this, new ConnectionEventArgs(c, index));
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
        edge_to_con.Remove(e);

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
        sb.Append("\nEdge : Type\n");
        myEnumerator = edge_to_con.GetEnumerator();
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
     * @param a Address to unlock
     * @param t the type of connection.
     * @param locker the object which holds the lock.
     * @throw Exception if the lock is not held by locker
     */
    public void Unlock(Address a, string t, ILinkLocker locker)
    {
      if( a != null ) {
        lock( _sync ) {
          ConnectionType mt = Connection.StringToMainType(t);
          Hashtable locks = (Hashtable)_address_locks[mt];
#if LOCK_DEBUG
          Console.WriteLine("{0} Unlocking {1}",
                            _local,
                            a);
#endif
          if( !locks.ContainsKey(a) ) {
#if LOCK_DEBUG
            Console.WriteLine("On node " +
                              _local.ToString() +
                              ", " + locker.ToString() + " tried to unlock " +
                              a.ToString() + " but no such lock" );

#endif
            throw new Exception("On node " +
                                _local.ToString() +
                                ", " + locker.ToString() + " tried to unlock " +
                                a.ToString() + " but no such lock" );
          }
          object real_locker = locks[a];
          if( real_locker != locker ) {
#if LOCK_DEBUG
            Console.WriteLine("On node " +
                              _local.ToString() +
                              ", " + locker.ToString() + " tried to unlock " +
                              a.ToString() + " but not the owner" );
#endif

            throw new Exception("On node " +
                                _local.ToString() +
                                ", " + locker.ToString() + " tried to unlock " +
                                a.ToString() + " but not the owner" );
          }
          locks.Remove(a);
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
     */
    public void UpdateStatus(Connection con, StatusMessage sm)
    {
      int index;
      ConnectionType t = con.MainType;
      Address a = con.Address;
      Edge e = con.Edge;
      string con_type = con.ConType;
      LinkMessage plm = con.PeerLinkMessage;

      Connection newcon = null;
      lock(_sync) {
        index = IndexOf(t, a);
        if ( index < 0 )
        {
          //This is a new address:
          throw new Exception("Address: " + a.ToString()
                              + " not in ConnectionTable. Cannot UpdateStatus.");
        }
        newcon = new Connection(e,a,con_type,sm,plm);
        edge_to_con[e] = newcon;

      } /* we release the lock */
     
      /* Send the event: */
      if( StatusChangedEvent != null )
        StatusChangedEvent(sm, new ConnectionEventArgs(newcon, index) );
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
     * @param edge Edge to check to see if it is an Unconnected Edge
     * @return true if this edge is an unconnected edge
     */
    public bool IsUnconnected(Edge e)
    {
      lock( _sync ) {
        return unconnected.Contains(e);
      }
    }
    //Private
    private class ConnectionEnumerator : IEnumerator {
      
      IDictionaryEnumerator _edge_enumer;
      ConnectionTable _tab;
      bool _filter;
      ConnectionType _filter_type;
      string _filter_string_type;
	    
      public ConnectionEnumerator(ConnectionTable tab) {
	_tab = tab;
	_filter = false;
	_filter_string_type = null;
	Reset();
      }

      public ConnectionEnumerator(ConnectionTable tab, ConnectionType ct) : this(tab) {
        _filter = true;
	_filter_type = ct;
      }
      
      public ConnectionEnumerator(ConnectionTable tab, string contype) : this(tab) {
	_filter_string_type = contype;
      }

      
      public bool MoveNext() {
	if( _filter_string_type != null ) {
          bool ret_val = false;
	  Connection c = null;
	  do {
            ret_val = _edge_enumer.MoveNext();
	    if( ret_val ) {
	      c = (Connection)_edge_enumer.Value;
	    }
	  }
	  while( (ret_val == true) && ( c.ConType != _filter_string_type ) );
          return ret_val;
	}
	else if( _filter ) {
          bool ret_val = false;
	  Connection c = null;
	  do {
            ret_val = _edge_enumer.MoveNext();
	    if( ret_val ) {
	      c = (Connection)_edge_enumer.Value;
	    }
	  }
	  while( (ret_val == true) && ( c.MainType != _filter_type ) );
          return ret_val;
	}
	else {
          return _edge_enumer.MoveNext();
	}
      }

      public object Current {
        get { return _edge_enumer.Value; }
      }

      public void Reset() {
        _edge_enumer = _tab.edge_to_con.GetEnumerator();
      }
    }

    /**
     * Handles enumerating connection types, not just all
     * connections
     */
    private class ConnectionTypeEnumerable : IEnumerable {

      private ConnectionTable _tab;
      private ConnectionType _ct;
      private string _contype;
      
      public ConnectionTypeEnumerable(ConnectionTable tab, ConnectionType ct)
      {
        _tab = tab;
	_ct = ct;
	_contype = null;
      }
      
      public ConnectionTypeEnumerable(ConnectionTable tab, string contype)
      {
        _tab = tab;
	_contype = contype;
      }
    
     /**
      * Required for IEnumerable Interface
      */
      public IEnumerator GetEnumerator()
      {
        if( _contype == null ) {
          return new ConnectionEnumerator(_tab, _ct);
	}
	else {
          return new ConnectionEnumerator(_tab, _contype);
	}
      }
    }
  }

#if BRUNET_NUNIT

  [TestFixture]
  public class ConnectionTableTest
  {
    public ConnectionTableTest() { }

    [Test]
    public void LoopTest() {
      //Make some fake edges: 
      TransportAddress home_ta =
        new TransportAddress("brunet.tcp://127.0.27.1:5000");
      TransportAddress ta1 =
        new TransportAddress("brunet.tcp://158.7.0.1:5000");
      TransportAddress ta2 =
        new TransportAddress("brunet.tcp://169.0.5.1:5000");
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
       
       	//Console.WriteLine("{0}\n",c);
      }
      Assert.AreEqual(total,2,"all connections");
     
      int struct_tot = 0;
      foreach(Connection c in tab.GetConnections(ConnectionType.Structured)) {
        struct_tot++;
	//Mostly a hack to make sure the compiler doesn't complain about an
	//unused variable
	Assert.IsNotNull(c);
        //Console.WriteLine("{0}\n",c);
      }
      Assert.AreEqual(struct_tot, 2, "structured connections");
      int near_tot = 0;
      foreach(Connection c in tab.GetConnections("structured.near")) {
        near_tot++;
	//Mostly a hack to make sure the compiler doesn't complain about an
	//unused variable
	Assert.IsNotNull(c);
        //Console.WriteLine("{0}\n",c);
      }
      Assert.AreEqual(near_tot, 1, "structured near");
    }
  }

#endif

}
