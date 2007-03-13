/*
This program is part of BruNet, a library for the creation of efficient overlay networks.
Copyright (C) 2005  University of California
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

//#define KML_DEBUG
//#define LOCK_DEBUG
#define PRINT_CONNECTIONS

#if BRUNET_NUNIT
#undef PRINT_CONNECTIONS
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
    protected Hashtable type_to_conlist;
    protected Hashtable edge_to_con;

    //We mostly deal with structured connections,
    //so we keep a ref to the address list for sructured
    //this is an optimization.
    protected ArrayList _struct_addlist;
    protected ArrayList _struct_conlist;

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
      _rand = new Random();

      _sync = new Object();
      lock( _sync ) {
        _local = local;
        type_to_addlist = new Hashtable();
        type_to_conlist = new Hashtable();
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
        /*
         * Copy so we don't mess up an old list
         */
        ArrayList copy;
        copy = (ArrayList)((ArrayList)type_to_addlist[t]).Clone();
        copy.Insert(index, a);
        type_to_addlist[t] = copy;
        if( t == ConnectionType.Structured ) {
          //Optimize the most common case to avoid the hashtable
          _struct_addlist = copy;
        }
        
        copy = (ArrayList)((ArrayList)type_to_conlist[t]).Clone();
        copy.Insert(index, c);
        type_to_conlist[t] = copy;
        
        edge_to_con[e] = c;
        if( t == ConnectionType.Structured ) {
          //Optimize the most common case to avoid the hashtable
          _struct_conlist = copy;
        }


        //Now that we have registered the new CloseEvent handler,
        //we can remove the old one
        int ucidx = unconnected.IndexOf(e);
        if( ucidx >= 0 ) {
          //Remove the edge from the unconnected table
          copy = (ArrayList)unconnected.Clone();
          copy.RemoveAt(ucidx);
          unconnected = copy;
        }
        else {
          //This is a new connection, so we need to add the CloseEvent
          /* Tell the edge to let you know when it dies: */
          e.CloseEvent += new EventHandler(this.RemoveHandler);
        }
      } /* we release the lock */
      
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
      bed.ConnectTime = DateTime.UtcNow.Ticks;
      bed.SubType = c.ConType;     
      bed.StructureDegree = Count(ConnectionType.Structured);

      _logger.LogBrunetEvent( bed );
      //Console.WriteLine("Table size is: {0}", TotalCount);
#endif

#if PRINT_CONNECTIONS
      System.Console.WriteLine("New Connection[{0}]: {1}", index, c);
#endif
      /* Send the event: */
      if( ConnectionEvent != null ) {
        try {
          ConnectionEvent(this, new ConnectionEventArgs(c, index) );
        }
        catch(Exception x) {
          Console.Error.WriteLine("ConnectionEvent triggered exception: {0}\n{1}", c, x);
        }
      }
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
      return (IndexOf(t,a) >= 0);
    }
    /**
     * @param t the ConnectionType we want to know the count of
     * @return the number of connections of this type
     */
    public int Count(ConnectionType t)
    {
      if( t == ConnectionType.Structured ) {
        //Optimize the most common case to avoid the hashtable
        return _struct_conlist.Count;
      }
      else {
        ArrayList list = (ArrayList)type_to_conlist[t];
        if( list == null ) {
          return 0;
        }
        else {
          return list.Count;
        }
      }
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
        return unconnected.Count;
      }
    }
    /**
     * Required for IEnumerable Interface
     */
    public IEnumerator GetEnumerator()
    {
      IDictionaryEnumerator en = edge_to_con.GetEnumerator();
      while( en.MoveNext() ) {
        yield return en.Value;
      }
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
      ArrayList list;
      if( t == ConnectionType.Structured ) {
        //Optimize the most common case to avoid the hashtable
        list = _struct_conlist;
      }
      else {
        list = (ArrayList)type_to_conlist[t];
      }
      int count = list.Count;
      if( count == 0 ) {
        return null;
      }
      index %= count;
      if( index < 0 ) {
        index += count;
      }
      return (Connection)list[index];
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
      if( e != null ) {
        c = (Connection)edge_to_con[e];
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
        if( t == ConnectionType.Structured ) {
          _struct_addlist = new ArrayList(); 
          _struct_conlist = new ArrayList(); 
          type_to_addlist[t] = _struct_addlist;
          type_to_conlist[t] = _struct_conlist;
        }
        else {
          type_to_addlist[t] = new ArrayList();
          type_to_conlist[t] = new ArrayList();
        }
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
      int index = 0;
      ArrayList add_list;
      if( t == ConnectionType.Structured ) {
        //Optimize the most common case to avoid the hashtable
        add_list = _struct_addlist;
      }
      else {
        add_list = (ArrayList)type_to_addlist[t];
      }
      if( add_list.Count == 0 ) {
        //This item would be the first in the list
        index = ~index;
      }
      else {
        //Search for the item
        /**
        * @throw an ArgumentNullException (ArgumentException)for the
        * the BinarySearch.
        */
        index = add_list.BinarySearch(a);
      }
      return index;
    }

    /**
     * @return the number of Structured Connections in the interval
     * (a1, a2) (not including the end points) when we move to the left.
     */
    public int LeftInclusiveCount(AHAddress a1, AHAddress a2) {
      if( a1.Equals(a2) ) { return 0; }
      int dist;
      lock( _sync ) {
        /*
         * There are four cases, we deal with each separately:
         * 0) neither a1 nor a2 are in the table
         * 1) a1 is not, but a2 is
         * 2) a1 is, but a2 is not
         * 3) a1 and a2 are.
         */
        int a2_idx = IndexOf(ConnectionType.Structured, a2);
        int a1_idx = IndexOf(ConnectionType.Structured, a1);
        int count = Count(ConnectionType.Structured);

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
          int max_dist = count;
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
            dist += count;
          }
          if( a1_present ) {
            /*
             * In thie case, our calculations are too much by one, in both
             * cases (dist > 0, dist < 0), so we decrease by one:
             */
            dist = dist - 1;
          }
        }
      }
      return dist;
    }
    /**
     * @return the number of Structured Connections in the interval
     * (a1, a2) (not including the end points) when we move to the right.
     */
    public int RightInclusiveCount(AHAddress a1, AHAddress a2) {
      if( a1.Equals(a2) ) { return 0; }
      int dist;
      lock( _sync ) {
        /*
         * There are four cases, we deal with each separately:
         * 0) neither a1 nor a2 are in the table
         * 1) a1 is not, but a2 is
         * 2) a1 is, but a2 is not
         * 3) a1 and a2 are.
         */
        int a2_idx = IndexOf(ConnectionType.Structured, a2);
        int a1_idx = IndexOf(ConnectionType.Structured, a1);
        int count = Count(ConnectionType.Structured);

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
          int max_dist = count;
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
            dist += count;
          }
          if( a2_present ) {
            /*
             * In thie case, our calculations are too much by one, in both
             * cases (dist > 0, dist < 0), so we decrease by one:
             */
            dist = dist - 1;
          }
        }
      }
      return dist;
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
     * @param e Edge whose connection should be removed
     * @param add_unconnected if true, keep a reference to the edge in the
     * unconnected list
     */
    protected void Remove(Edge e, bool add_unconnected)
    {
      int index = -1;
      bool have_con = false;
      Connection c = null;
      lock(_sync) {
        c = GetConnection(e);	
        have_con = (c != null);
        if( have_con )  {
          index = IndexOf(c.MainType, c.Address);
          Remove(c.MainType, index);
	  if( add_unconnected ) {
            ArrayList copy = (ArrayList)unconnected.Clone();
            copy.Add(e);
            unconnected = copy;
	  }
        }
        else {
	  //We didn't have a connection, so, check to see if we have it in
	  //unconnected:
	  if( !add_unconnected ) {
	    //Don't keep this edge around at all:
            int idx = unconnected.IndexOf(e);
            if( idx >= 0 ) {
              ArrayList copy = (ArrayList)unconnected.Clone();
              copy.RemoveAt(idx);
              unconnected = copy;
            }
	  }
        }
      }
      if( have_con ) {

#if PLAB_CONNECTION_LOG
        BrunetEventDescriptor bed = new BrunetEventDescriptor();
        bed.EventDescription = "disconnection";
        bed.ConnectionType = t;
        bed.LocalTAddress = e.LocalTA.ToString();
        bed.RemoteTAddress = e.RemoteTA.ToString();
        bed.RemoteAHAddress = c.Address.ToBigInteger().ToString();
        bed.RemoteAHAddressBase32 = c.Address.ToString();
        bed.ConnectTime = DateTime.UtcNow.Ticks;
        bed.SubType = c.ConType;
        bed.StructureDegree = Count(ConnectionType.Structured);

        _logger.LogBrunetEvent( bed );
#endif


      #if DEBUG
        System.Console.WriteLine("Remove: DisconnectionEvent[{0}]: {1}", index, c);
      #endif
#if PRINT_CONNECTIONS
        System.Console.WriteLine("New disconnection[{0}]: {1}", index, c);
#endif
        //Announce the disconnection:
        if( DisconnectionEvent != null ) {
          try {
            DisconnectionEvent(this, new ConnectionEventArgs(c, index));
          }
          catch(Exception x) {
            Console.Error.WriteLine("DisconnectionEvent triggered exception: {0}\n{1}", c, x);
          }
        }
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
        Connection c = (Connection)((ArrayList)type_to_conlist[t])[index];
        Edge e = c.Edge;
        //Remove the edge from the lists:
        /*
         * We never modify lists after putting them into the hashtables.
         * This guarantees that reading operations are sane, and only
         * "writing" operations need to lock.  Since writing operations
         * are rare compared to reading, this makes a lot of sense.
         */
        ArrayList this_list = (ArrayList)type_to_addlist[t];
        ArrayList copy = new ArrayList(this_list.Count - 1);
        //Put all but the index we don't want into the copy:
        for(int i = 0; i < this_list.Count; i++) {
          if( i != index ) { copy.Add( this_list[i] ); }
        }
        if( t == ConnectionType.Structured ) {
          //Optimize the most common case to avoid the hashtable
          _struct_addlist = copy;
        }
        type_to_addlist[t] = copy;
        
        //Now change the conlist:
        this_list = (ArrayList)type_to_conlist[t];
        copy = new ArrayList(this_list.Count - 1);
        //Put all but the index we don't want into the copy:
        for(int i = 0; i < this_list.Count; i++) {
          if( i != index ) { copy.Add( this_list[i] ); }
        }
        if( t == ConnectionType.Structured ) {
          //Optimize the most common case to avoid the hashtable
          _struct_conlist = copy;
        }
        type_to_conlist[t] = copy;
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
      Edge e = (Edge)edge;
      e.CloseEvent -= new EventHandler(this.RemoveHandler);
      //Get rid of the edge and don't add it to our unconnected list
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
        myEnumerator = type_to_conlist.GetEnumerator();
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
        //Make the new connection and replace it in our data structures:
        newcon = new Connection(e,a,con_type,sm,plm);
        edge_to_con[e] = newcon;
        ArrayList l = (ArrayList)type_to_conlist[t];
        l[ index ] = newcon;

      } /* we release the lock */
     
      /* Send the event: */
      if( StatusChangedEvent != null ) {
        try {
          StatusChangedEvent(sm, new ConnectionEventArgs(newcon, index) );
        }
        catch(Exception x) {
          Console.Error.WriteLine("StatusChangedEvent triggered exception: {0}\n{1}", newcon, x);
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
        int idx = unconnected.IndexOf(e);
        if( idx < 0 ) {
          ArrayList copy = (ArrayList)unconnected.Clone();
          copy.Add(e);
          unconnected = copy;
        }
      }
      e.CloseEvent += new EventHandler(this.RemoveHandler);
    }
    /**
     * @param edge Edge to check to see if it is an Unconnected Edge
     * @return true if this edge is an unconnected edge
     */
    public bool IsUnconnected(Edge e)
    {
      return unconnected.Contains(e);
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
        _ct = Connection.StringToMainType(contype);
      }
    
     /**
      * Required for IEnumerable Interface
      */
      public IEnumerator GetEnumerator()
      {
        ArrayList cons = (ArrayList)_tab.type_to_conlist[ _ct ];
        if( _contype == null ) {
          foreach(Connection c in cons) {
            yield return c;
          }
        }
        else {
          foreach(Connection c in cons) {
            if (c.ConType == _contype ) {
              yield return c;
            }
          }
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
        //Console.WriteLine("LIC(a,b): {0}, RIC(b,a): {1}", l_c2, r_c);
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
          int before = tab.Count(ConnectionType.Structured);
          int uc_count = tab.UnconnectedCount;
          tab.Disconnect(c.Edge);
          int after = tab.Count(ConnectionType.Structured);
          int uc_count_a = tab.UnconnectedCount;
          Assert.IsTrue( before == (after + 1), "Disconnect subtracted one");
          Assert.IsTrue( uc_count == (uc_count_a - 1), "Disconnect added one unconnected");
          Assert.IsTrue( tab.IndexOf(ConnectionType.Structured, c.Address) < 0, "Removal worked");
          Assert.IsNull( tab.GetConnection(c.Edge), "Connection is gone");
          Assert.IsTrue( tab.IsUnconnected( c.Edge ), "Edge is unconnected" );
          c.Edge.Close(); //Should trigger removal completely:
          Assert.IsFalse( tab.IsUnconnected( c.Edge ), "Edge is completely gone");
          Assert.IsNull( tab.GetConnection( c.Edge ), "Connection is still gone");
        }
      }
    }
  }

#endif

}
