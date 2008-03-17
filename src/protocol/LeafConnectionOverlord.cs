/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2005-2007  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

    private static readonly int DESIRED_CONS = 3;
    //After this many secs of not seeing any new connections or disconnections,
    //we decide we don't need leafs anymore.
    private static readonly double TIME_SCALE = 600.0;
    /*
     * The LeafConnectionOverlord wants 2 connections by default,
     * but if we go a long time without any non-leaf connection events,
     * we decrease this.
     */
    protected int DesiredConnections {
      get {
        if( _last_non_leaf_connection_event == DateTime.MinValue ) {
          return DESIRED_CONS;
        }
        else if (_local.ConnectionTable.Count(ConnectionType.Structured) == 0) {
          /*
           * We have no structured connections, so we definitely should have
           * leaf connections.  They are used to create structured
           * connections, without any leaf connections, we can't get
           * structured connections
           */
          return DESIRED_CONS;
        }
        else {
           /*
            * Linearly decrease the number we want so that after zero seconds,
            * we want DESIRED_CONS, and after TIME_SCALE, we want zero
            * y = mx + b
            */
           double b = (double)DESIRED_CONS;
           double m = - b / TIME_SCALE;
           double x = (DateTime.UtcNow - _last_non_leaf_connection_event).TotalSeconds;
           double y = m*x + b;
           if( y > 0 ) {
             return (int)Math.Round(y);
           }
           else {
            return 0;
           }
        }
      }
    }

    protected readonly Node _local;

    protected readonly Random _rnd;

    //We start at a 10 second interval
    protected TimeSpan _default_retry_interval;
    protected TimeSpan _current_retry_interval;
    static protected readonly TimeSpan _MAX_RETRY_INTERVAL = TimeSpan.FromSeconds(60);
    protected DateTime _last_retry;
    protected DateTime _last_non_leaf_connection_event;
    protected DateTime _last_trim;
    //Every TRIM_INTERVAL we check to see if we should trim any leaf
    //connections (currently 2 minutes)
    private static readonly TimeSpan TRIM_INTERVAL = new TimeSpan(0,2,0);
    /**
     * This is our active linker.  We only have one
     * at a time.  Otherwise, we may waste a lot
     * of bandwidth.
     */
    protected Linker _linker;

    protected readonly object _sync;

    public LeafConnectionOverlord(Node local)
    {
      _compensate = false;
      _local = local;
      _linker = null;
      _sync = new object();
      _rnd = new Random( _local.GetHashCode()
                         ^ unchecked((int)DateTime.UtcNow.Ticks) );
      _default_retry_interval = new TimeSpan(0,0,0,0,10000);
      _current_retry_interval = new TimeSpan(0,0,0,0,10000);
      //We initialize at at year 1 to start with:
      _last_retry = DateTime.MinValue;
      _last_non_leaf_connection_event = DateTime.MinValue;
      _last_trim = DateTime.MinValue;
      /*
       * When a node is removed from the ConnectionTable,
       * we should check to see if we need to work to get a
       * replacement
       */
      lock(_sync) {
        local.ConnectionTable.DisconnectionEvent += this.CheckAndConnectHandler;
        local.ConnectionTable.ConnectionEvent += this.CheckAndConnectHandler;
        /**
         * Every heartbeat we check to see if we need to act
         */
        local.HeartBeatEvent += this.CheckAndConnectHandler;
      }
    }

    //We use this to get the oldest edge
    protected class EdgeDateComparer : IComparer {
      public int Compare(object o1, object o2) {
        if ( o1 == o2 ) {
          return 0;
        }
        Edge e1 = (Edge)o1;
        Edge e2 = (Edge)o2;
        return e1.CreatedDateTime.CompareTo( e2.CreatedDateTime );
      }
    }

    override public void Activate()
    {
      //Starts the process of looking for a Leaf connection
      CheckAndConnectHandler(null, null);
    }

    volatile protected bool _compensate;
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
     * Linker objects call this when they are done, and we start
     * again if we need to
     */
    public void CheckAndConnectHandler(object linker, EventArgs args)
    {
      ConnectionEventArgs cea = args as ConnectionEventArgs;
      DateTime now = DateTime.UtcNow;
      Linker new_linker = null;
      lock(_sync) {
        if( cea != null ) {
          //This is a connection event.
          if( cea.Connection.MainType != ConnectionType.Leaf ) {
            _last_non_leaf_connection_event = now;
          }
        }
        //Check in order of cheapness, so we can avoid hard work...
        bool time_to_start = (now - _last_retry >= _current_retry_interval );
        if ( (_linker == null) && time_to_start && 
             IsActive && NeedConnection ) {
          //Now we double the retry interval.  When we get a connection
          //We reset it back to the default value:
          _last_retry = now;
          _current_retry_interval = _current_retry_interval + _current_retry_interval;
          _current_retry_interval = _current_retry_interval + _current_retry_interval;
          _current_retry_interval = (_MAX_RETRY_INTERVAL < _current_retry_interval) ?
              _MAX_RETRY_INTERVAL : _current_retry_interval;

          //Get a random address to connect to:
  
          //Make a copy:
          object[] tas = _local.RemoteTAs.ToArray();
          /*
           * Make a randomized list of TransportAddress objects to connect to:
           * This is a very nice algorithm.  It is optimal in that it produces
           * a permutation of a list using N swaps and log(N!) bits
           * of entropy.
           */
          for(int j = 0; j < tas.Length; j++) {
            //Swap the j^th position with this position:
            int i = _rnd.Next(j, tas.Length);
              if( i != j ) {
              object temp_ta = tas[i];
              tas[i] = tas[j];
                tas[j] = temp_ta;
              }
          }
          /**
           * Make a Link to a remote node 
           */
          _linker = new Linker(_local, null, tas, "leaf", _local.Address);
          new_linker = _linker;
        }
        else if (cea != null) {
          /*
           * This is the case that there was a non-leaf connection
           * or disconnection, BUT it is not yet time to start OR
           * there is current linker running OR we are not active OR
           * we don't need a connection
           * 
           * In this case, we set the retry interval back to the default
           * value.  This is because we only do the exponential back
           * off when there we can't seem to get connected when we try.
           * Clearly we are getting edges here, so there is no need
           * for the back-off.
           */

          //Reset the connection interval to the default value:
            _current_retry_interval = _default_retry_interval;
          //We are not seeking another connection
          //log.Info("LeafConnectionOverlord :  not seeking connection");
        }
        //Check to see if it is time to trim.
      }//Drop the lock
      Trim();
      
      /**
       * If there is a new linker, start it after we drop the lock
       */
      if( new_linker != null ) {
        new_linker.FinishEvent += this.LinkerFinishHandler;
        new_linker.Start();
      }
    }

    /**
     * When a Linker finishes, this method is called.  This is
     * to do memory management of the Linker objects
     */
    protected void LinkerFinishHandler(object linker, EventArgs args)
    {
      //We need to remove this linker from our list:
      lock( _sync ) {
        _linker = null;
      }
    }

    /**
     * @return true if you need a connection
     */
    override public bool NeedConnection
    {
      get {
        return ( _local.ConnectionTable.Count(ConnectionType.Leaf) < DesiredConnections);
      }
    }
    /**
     * @return true if we have sufficient connections for functionality
     */
    override public bool IsConnected
    {
      get {
        throw new Exception("Not implemented! Leaf connection overlord (IsConnected)");
      }
    }

    /**
     * We periodically check to see if we have too many leafs and we trim them
     */
    protected void Trim() {
      DateTime now = DateTime.UtcNow;
      bool do_trim = false;
      lock (_sync ) {
        if( now - _last_trim > TRIM_INTERVAL ) {
          _last_trim = now;
          do_trim = true;
        }
      }
      if( do_trim ) {
        Edge to_close = null;
        /*
         * There is no need to lock the table, this IEnumerable
         * won't have problems because it never changes
         */
        IEnumerable lenum = _local.ConnectionTable.GetConnections(ConnectionType.Leaf);
        ArrayList all_leafs = new ArrayList();
        foreach(Connection c in lenum) {
          all_leafs.Add(c.Edge);
        }
        int leafs = all_leafs.Count;
        int surplus = leafs - DesiredConnections;
        if( surplus > 0 ) {
          /*
           * Since only public nodes can accept leaf connections (to a good
           * approximation), there could be insufficient public nodes to fit
           * all the connections.  Thus, we make the maximum we accept
           * "soft" by only deleting with some probability.  This makes the
           * system tend toward balance but allows nodes to have more than
           * a fixed number of leafs.
           */
          double d_s = (double)(surplus);
          double prob = 0.0;
          if( surplus > 1 ) {
            prob = 1.0 - 1.0/d_s;
          }
          else {
            //surplus == 1
            //With 25% chance trim the excess connection
            prob = 0.25;
          }
          if( _rnd.NextDouble() < prob ) {
            //as surplus -> infinity, prob -> 1, and we always close.
            //Now sort them, and get the oldest:
            all_leafs.Sort(new EdgeDateComparer());
            //Here is the oldest:
            to_close = (Edge)all_leafs[0];
          }
          else {
            //We just add the new edge without closing
          }
        }
        if( to_close != null ) {
          _local.GracefullyClose( to_close );
        }
      }
    }
  }

}
