/**
 * Dependencies : 
 * Brunet.ConnectionEventArgs
 * Brunet.ConnectionOverlord
 * Brunet.ConnectionTable
 * Brunet.ConnectionType
 * Brunet.Edge
 * Brunet.Linker
 * Brunet.Node
 * Brunet.TransportAddress
 */

using System;
using System.Collections;
//using log4net;
namespace Brunet
{
  /**
   * Makes sure we have at least one Leaf connection at all times.
   * This is to ensure that we can make other types of connections
   * which use Leaf connections to start connecting.
   */
  public class LeafConnectionOverlord:ConnectionOverlord
  {
    /*private static readonly log4net.ILog log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/

    /**
     * The LeafConnectionOverlord wants 2 connections
     */
    private static readonly int _desired_cons = 2;

    protected Node _local;

    protected Random _rnd;

    /**
     * This list keeps all of our active linkers
     * in scope.  They are removed from this list
     * when their finish event is called.
     */
    protected ArrayList _linkers;

    public LeafConnectionOverlord(Node local)
    {
      _compensate = false;
      _local = local;
      _linkers = new ArrayList();
      _rnd = new Random( _local.GetHashCode() ^ this.GetHashCode()
                         ^ unchecked((int)DateTime.Now.Ticks) );
      /*
       * When a node is removed from the ConnectionTable,
       * we should check to see if we need to work to get a
       * replacement
       */
      local.ConnectionTable.DisconnectionEvent +=
        new EventHandler(this.CheckAndConnectHandler);
      local.ConnectionTable.ConnectionEvent +=
        new EventHandler(this.CheckAndConnectHandler);
      /**
       * Every heartbeat we check to see if we need to act
       */
      local.HeartBeatEvent +=
        new EventHandler(this.CheckAndConnectHandler);
    }

    override public void Activate()
    {
      //Starts the process of looking for a Leaf connection
      CheckAndConnectHandler(null, null);
    }

    protected bool _compensate;
    /**
     * If we start compensating, we check to see if we need to
     * make a connection : 
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


    /**
     * The ConnectionType this object is the Overlord of
     */
    override public ConnectionType ConnectionType
    {
      get
      {
        return ConnectionType.Leaf;
      }
    }

    /**
     * Linker objects call this when they are done, and we start
     * again if we need to
     */
    public void CheckAndConnectHandler(object linker, EventArgs args)
    {
      if (IsActive && NeedConnection) {
        //log.Info("LeafConnectionOverlord :  seeking connection");
        //Get a random address to connect to:

        //Make a randomize the list of TransportAddress nodes to connect to:
        TransportAddress[] tas;
        lock( _local.RemoteTAs.SyncRoot ) {
          Hashtable hit_list = new Hashtable();
          tas = new TransportAddress[ _local.RemoteTAs.Count ];
          for(int j = 0; j < _local.RemoteTAs.Count; j++) {
            int i = _rnd.Next(0, _local.RemoteTAs.Count);
            //Keep looking until we find a new one:
            while( hit_list.Contains(i) )
            {
              i++;
              i %= _local.RemoteTAs.Count;
            }
            //Have not added this one yet:
            tas[j] = (TransportAddress)_local.RemoteTAs[i];
            hit_list[i] = true;
          }
        }
        /**
         * Make a Link to a remote node 
         */
        Linker l = new Linker(_local);
        //Add the linker to our list of linker so it won't get GC'ed
        _linkers.Add(l);
        l.FinishEvent += new EventHandler(this.LinkerFinishHandler);
        l.Link(null, tas, ConnectionType.Leaf);
      }
      else if (args is ConnectionEventArgs) {
        /**
        * When we get a new connection, we check to see if we have
        * too many.  If we do, we close one of the OLD ONES!!
        */
        ConnectionEventArgs ce = (ConnectionEventArgs)args;

        Edge to_close = null;
        lock ( _local.ConnectionTable.SyncRoot ) {
          ArrayList leafs =
            _local.ConnectionTable.GetEdgesOfType(ConnectionType.Leaf);
          if( leafs.Count > 2 * _desired_cons ) {
            int idx = _rnd.Next(0, leafs.Count);
            to_close = (Edge)leafs[ idx ];
            //Don't close the edge we just got, close the one after it.
            if( to_close == ce.Edge ) {
              idx = (idx + 1) % leafs.Count;
              to_close = (Edge)leafs[ idx ];
            }
          }
        }
        //Release the lock
        if( to_close != null ) {
          _local.GracefullyClose( to_close );
        }
        //We are not seeking another connection
        //log.Info("LeafConnectionOverlord :  not seeking connection");
      }
    }

    /**
     * When a Linker finishes, this method is called.  This is
     * to do memory management of the Linker objects
     */
    protected void LinkerFinishHandler(object linker, EventArgs args)
    {
      //We need to remove this linker from our list:
      _linkers.Remove(linker);
    }

    /**
     * @return true if you need a connection
     */
    override public bool NeedConnection
    {
      get {
        return ( _local.ConnectionTable.Count(ConnectionType.Leaf) < _desired_cons);
      }
    }
  }

}
