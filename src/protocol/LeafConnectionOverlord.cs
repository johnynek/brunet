/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2005  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

    private static readonly int _desired_cons = 3;
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
          return _desired_cons;
        }
        else {
           /*
            * Linearly decrease the number we want so that after zero seconds,
            * we want _desired_cons, and after TIME_SCALE, we want zero
            * y = mx + b
            */
           double b = (double)_desired_cons;
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

    protected Node _local;

    protected Random _rnd;

    //We start at a 10 second interval
    protected TimeSpan _default_retry_interval;
    protected TimeSpan _current_retry_interval;
    protected DateTime _last_retry;
    protected DateTime _last_non_leaf_connection_event;
    protected DateTime _last_trim;
    //Every _trim_interval we check to see if we should trim any leaf
    //connections (currently 2 minutes)
    private static readonly TimeSpan _trim_interval = new TimeSpan(0,2,0);
    /**
     * This is our active linker.  We only have one
     * at a time.  Otherwise, we may waste a lot
     * of bandwidth.
     */
    protected Linker _linker;

    protected object _sync;

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
     * Linker objects call this when they are done, and we start
     * again if we need to
     */
    public void CheckAndConnectHandler(object linker, EventArgs args)
    {
     ConnectionEventArgs cea = args as ConnectionEventArgs;
     DateTime now = DateTime.UtcNow;
     if( cea != null ) {
       //This is a connection event.
       if( cea.Connection.MainType != ConnectionType.Leaf ) {
         _last_non_leaf_connection_event = now;
       }
     }
     lock(_sync) {
      //Check in order of cheapness, so we can avoid hard work...
      bool time_to_start = (now - _last_retry >= _current_retry_interval );
      if ( (_linker == null) &&
           IsActive && NeedConnection && time_to_start ) {
        //Now we double the retry interval.  When we get a connection
	//We reset it back to the default value:
	_last_retry = now;
	_current_retry_interval = _current_retry_interval + _current_retry_interval;
        //log.Info("LeafConnectionOverlord :  seeking connection");
        //Get a random address to connect to:

	//Make a copy:
        TransportAddress[] tas;
        lock( _local.RemoteTAs.SyncRoot ) {
          tas = new TransportAddress[ _local.RemoteTAs.Count ];
	  _local.RemoteTAs.CopyTo( tas );
	}
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
            TransportAddress temp_ta = tas[i];
	    tas[i] = tas[j];
	    tas[j] = temp_ta;
	  }
	}
        /**
         * Make a Link to a remote node 
         */
        _linker = new Linker(_local, null, tas, "leaf");
        _linker.FinishEvent += new EventHandler(this.LinkerFinishHandler);
        _linker.Start();
      }
      else if (cea != null) {
        //Reset the connection interval to the default value:
	_current_retry_interval = _default_retry_interval;
        //We are not seeking another connection
        //log.Info("LeafConnectionOverlord :  not seeking connection");
      }
      //Check to see if it is time to trim.
      Trim();
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
      if( now - _last_trim > _trim_interval ) {
        _last_trim = now;
        
        Edge to_close = null;
        lock ( _local.ConnectionTable.SyncRoot ) {
          int leafs = _local.ConnectionTable.Count(ConnectionType.Leaf);
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
	      Connection c = _local.ConnectionTable.GetRandom(ConnectionType.Leaf);
              //Then we will delete an old node:
              //as surplus -> infinity, prob -> 1, and we always close.
	      to_close = c.Edge;
            }
            else {
              //We just add the new edge without closing
            }
          }
        }
        //Release the lock
        if( to_close != null ) {
          _local.GracefullyClose( to_close );
        }
      }
    }
  }

}
