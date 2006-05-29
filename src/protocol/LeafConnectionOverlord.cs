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

    //We start at a 10 second interval
    protected TimeSpan _default_retry_interval;
    protected TimeSpan _current_retry_interval;
    protected DateTime _last_retry;
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
      _rnd = new Random( _local.GetHashCode() ^ this.GetHashCode()
                         ^ unchecked((int)DateTime.Now.Ticks) );
      _default_retry_interval = new TimeSpan(0,0,0,0,10000);
      _current_retry_interval = new TimeSpan(0,0,0,0,10000);
      //We initialize at at year 1 to start with:
      _last_retry = DateTime.MinValue;
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
     lock(_sync) {
      //Check in order of cheapness, so we can avoid hard work...
      if ( (_linker == null) &&
           IsActive && NeedConnection ) {
	DateTime now = DateTime.Now;
	if( _last_retry == DateTime.MinValue ) {
          //This is the first time through:
	  _last_retry = now;
	}
	else if( now - _last_retry < _current_retry_interval ) {
          //It is not yet time to restart.
	  return;
	}
	else {
          //Now we double the retry interval.  When we get a connection
	  //We reset it back to the default value:
	  _last_retry = now;
	  _current_retry_interval = _current_retry_interval + _current_retry_interval;
	}
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
        _linker = new Linker(_local);
        _linker.FinishEvent += new EventHandler(this.LinkerFinishHandler);
        _linker.Link(null, tas, ConnectionType.Leaf);
      }
      else if (args is ConnectionEventArgs) {
        //Reset the connection interval to the default value:
	_current_retry_interval = _default_retry_interval;
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
        return ( _local.ConnectionTable.Count(ConnectionType.Leaf) < _desired_cons);
      }
    }
  }

}
