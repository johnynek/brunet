/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2005,2006  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

//#define DEBUG

#define LINK_DEBUG

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

using System;
using System.Collections;

namespace Brunet
{

  /**
   *
   * Given a list of remote TransportAddress
   * objects, Linker creates the link between the remote node and
   * the local node
   * 
   */

  public class Linker : TaskWorker, ILinkLocker 
  {

    /*private static readonly log4net.ILog log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/

    /**
     * Holds the next request id to be used
     */
    protected int _id;

    protected string _contype;
    public string ConType { get { return _contype; } }
    protected Connection _con;
    /**
     * This is the Connection established by this Linker
     */
    public Connection Connection { get { return _con; } }
    
    //This is the queue that has only the address we have not tried this attempt
    protected Queue _ta_queue;
    protected Node _local_n;
    public Node LocalNode { get { return _local_n; } }

    protected Address _target_lock;
    protected Address _target;
    /** If we know the address of the node we are trying
     * to make an outgoing connection to, we lock it, and
     * remember it here
     */
    public Address Target { get { return _target; } }
    
    /** global lock for thread synchronization */
    protected object _sync;
    public object SyncRoot {
      get {
	return _sync;
      }
    }
    protected bool _is_finished;
    override public bool IsFinished {
      get {
        lock( _sync ) { return _is_finished; }
      }
    }
    //If we get an ErrorCode.InProgress, we restart after
    //a period of time
    protected static readonly int _MS_RESTART_TIME = 10000;
    protected static readonly int _MAX_RESTARTS = 16;
    /*
     * This inner class keeps state information for restarting
     * on a particular TransportAddress
     */
    protected class RestartState {
      protected int _restart_attempts;
      public int RemainingAttempts { get { return _restart_attempts; } }
      protected DateTime _last_start;
      protected DateTime _next_start;
      protected Random _rand;
      protected bool _is_waiting;
      public bool IsWaiting { get { return _is_waiting; } }

      protected TransportAddress _ta;
      public TransportAddress TA { get { return _ta; } }

      /**
       * When it is time to restart, we fire this
       * event
       */
      public event EventHandler RestartEvent;

      public RestartState(TransportAddress ta) {
        _ta = ta;
        _restart_attempts = _MAX_RESTARTS;
        _rand = new Random();
        _is_waiting = false;
      }

      /**
       * Schedule the restart using the Heartbeat of the given node
       */
      public void ScheduleRestart(Node n) {
        lock( this ) {
        _restart_attempts--;
	if( _restart_attempts < 0 ) { throw new Exception("restarted too many times"); }
        _last_start = DateTime.Now;
	int restart_sec = (int)(_rand.NextDouble() * _MS_RESTART_TIME);
	TimeSpan interval = new TimeSpan(0,0,0,0,restart_sec);
	_next_start = _last_start + interval; 
        n.HeartBeatEvent += this.RestartLink;
        _is_waiting = true;
        }
      }
      /**
       * When we fail due to a ErrorMessage.ErrorCode.InProgress error
       * we wait restart to verify that we eventually got connected
       */
      protected void RestartLink(object node, EventArgs args)
      {
        bool fire_event = false;
        lock( this ) {
          if( DateTime.Now > _next_start ) { 
  	    if ( _rand.NextDouble() < 0.5 ) {
              Node local_n = (Node)node;
              //Time to start up again
              local_n.HeartBeatEvent -= new EventHandler(this.RestartLink);
              fire_event = true;
              _is_waiting = false; 
  	    }
          }
        }
        if( fire_event ) {
          //Fire the event without holding a lock
          if( RestartEvent != null ) {
            RestartEvent(this, EventArgs.Empty);
            RestartEvent = null;
          }
        }
      }
    }
    
    /**
     * Keeps track of the restart information for each TransportAddress
     */
    protected Hashtable _ta_to_restart_state;

    protected Random _rand;
    //Link.Start should only be called once, this throws an exception if
    //called more than once
    protected bool _started; 
    protected Hashtable _active_lps;
#if LINK_DEBUG
    private int _lid;
    public int Lid { get { return _lid; } }
#endif
    /**
     * These represent the task of linking used by TaskWorked
     */
    protected class LinkerTask {
      protected Address _local;
      protected Address _target;
      protected ConnectionType _ct;

      public LinkerTask(Address local, Address target, string ct) {
        _local = local;
        _target = target;
        _ct = Connection.StringToMainType(ct);
      }
      override public int GetHashCode() {
        int code = _local.GetHashCode() ^ _ct.GetHashCode();
        if( _target != null ) {
          code ^= _target.GetHashCode();
        }
        return code;
      }
      override public bool Equals(object o) {
        LinkerTask lt = o as LinkerTask;
        bool eq = false;
        if( lt != null ) {
          eq = (lt._local.Equals( this._local) )
              && (lt._ct.Equals( this._ct) );
          if( _target != null ) {
            eq &= _target.Equals( lt._target );
          }
        }
        return eq;
      }
    }
    protected object _task;
    override public object Task {
      get { return _task; }
    }
    /**
     * @param local the local Node to connect to the remote node
     * @param target the address of the node you are trying to connect
     * to.  Set to null if you don't know
     * @param target_list an enumerable list of TransportAddress of the
     *                    Host we want to connect to
     * @param t ConnectionType string of the new connection
     */
    public Linker(Node local, Address target, ICollection target_list, string ct)
    {
#if LINK_DEBUG
      _lid = GetHashCode() + (int)DateTime.Now.Ticks;
      Console.WriteLine("Making Linker: {0}",_lid);
#endif
      _sync = new object();
      _task = new LinkerTask(local.Address, target, ct);
      lock(_sync) {
        _is_finished = false;
        _local_n = local;
        //Hopefully at least one of these will be somewhat unpredictable
        ///@todo think seriously about getting truely random ids
        _id = GetHashCode() ^ local.Address.GetHashCode();
        _rand = new Random(_id);
        /* We do not use negative ids, the spec says they
         * are reserved for future use
         */
        if( _id < 0 ) {
          _id = ~_id;
        }
        //If we retry, we need an original copy of the list
        if( target_list != null ) {
          _ta_queue = new Queue( target_list );
        }
        else {
          _ta_queue = new Queue();
        }
        _contype = ct;
        _target = target;
        _active_lps = new Hashtable();
        _ta_to_restart_state = new Hashtable();
        _started = false;
      }
    }

    /**
     * This tells the Linker to make its best effort to create
     * a connection to another node
     */
    override public void Start() {
      //Just move to the next (first) TA
      lock( _sync ) {
        if( _started ) { throw new Exception("Linker already Started"); }
        _started = true;
      }
      MoveToNextTA(null);
    } 
    /**
     * This manages a new attempt to connect on a given TransportAddress
     */
    protected void StartAttempt(TransportAddress next_ta) {
      if( next_ta == null ) {
        //We only get a null TA if we are totally out.  Time to fail:
        Fail(next_ta, "no more tas to restart with");
        return;
      }
      ConnectionTable tab = _local_n.ConnectionTable;
      if( _target != null 
          && tab.Contains( Connection.StringToMainType( _contype ), _target) ) { 
          //Looks like we are already connected...
        Fail(next_ta, "Connected before we could StartAttempt");
        return;
      }
      else {
        //Looks like it is time to really move:
      }
      try {
        /*
         * If we cannot set this address as our target, we
         * stop before we even try to make an edge.
         */
#if LINK_DEBUG
	  Console.WriteLine("Linker ({0}) attempting to lock {1}", _lid, _target);
#endif
         /*
          * Locks flow around in complex ways, but we (or one of our LinkProtocolState)
          * will hold the lock
          */
          SetTarget(_target);
#if LINK_DEBUG
	  Console.WriteLine("Linker ({0}) acquired lock on {1}", _lid, _target);
#endif
        /*
         * Asynchronously gets an edge, and then begin the
         * link attempt with it.  If it fails, and there
         * are more TransportAddress objects, this method
         * will call itself again.  If there are no more TAs
         * the Link attempt Fails.
         */
#if LINK_DEBUG
	  Console.WriteLine("Linker: ({0}) Trying TA: {1}", _lid, next_ta);
#endif
         //Sadly, the CreateEdgeTo callback doesn't pass any state, so we are doing
         //it with an anonymous delegate:
         EdgeListener.EdgeCreationCallback cb = delegate(bool s, Edge e,
                                                         Exception x) {
           this.TryNext(s, next_ta, e, x);
         };
         _local_n.EdgeFactory.CreateEdgeTo( next_ta, cb);
      }
      catch(InvalidOperationException x) {
#if LINK_DEBUG
        Console.WriteLine("Linker ({0}) failed to lock {1}", _lid, _target);
#endif
        //This is thrown when ConnectionTable cannot lock.  Lets try again:
        RetryThis(next_ta);
      }
      catch(Exception x) {
        Fail(next_ta, x.Message);
      }
    }

    /**
     * Allow if we are transfering to a LinkProtocolState or ConnectionPacketHandler
     */
    public bool AllowLockTransfer(Address a, string contype, ILinkLocker l) {
	bool allow = false;
        LinkProtocolState lps = l as LinkProtocolState;
        if( l is Linker ) {
          //Never transfer to another linker:
          allow = false;
        }
        else if ( lps == null) {
          /**
	   * We only allow a lock transfer in the following case:
	   * 1) We are not transfering to another LinkProtocolState
	   * 2) The lock matches the lock we hold
	   * 3) The address we are locking is greater than our own address
	   */
	  lock( _sync ) {
            if( a.Equals( _target_lock )
		&& ( a.CompareTo( LocalNode.Address ) > 0) ) {
              _target_lock = null; 
              allow = true;
	    }
	  }
	}
        else {
          /**
           * Or Transfer the lock to a LinkProtocolState if:
           * 1) We created this LinkProtocolState
           * 2) The LinkProtocolState has received a packet
           */
          if( (lps.Linker == this )
             && ( lps.LastRPacket != null ) ) {
            _target_lock = null; 
            allow = true;
          }
        }
	return allow;       
    }
    /************  protected methods ***************/
   protected bool AreActiveElements {
     get {
       lock( _sync ) {
         return ((_active_lps.Count > 0) || (_ta_to_restart_state.Count > 0));
       }
     }
   }

   protected void LinkProtocolStateFinishHandler(object olps, EventArgs args) {
     LinkProtocolState lps = (LinkProtocolState)olps;
     switch( lps.MyResult ) {
       case LinkProtocolState.Result.Success:
         //Succeed will release the lock at the approprate time
         Succeed(lps);
         break;
       case LinkProtocolState.Result.MoveToNextTA:
         //Hold the lock, it will be transferred:
         // old LPS -> Linker -> new LPS
         MoveToNextTA(lps.TA);
         break;
       case LinkProtocolState.Result.RetryThisTA:
         //Release the lock while we wait:
         lps.Unlock();
         RetryThis(lps.TA);
         break;
       case LinkProtocolState.Result.ProtocolError:
         //We should fail here, since we were talking to the node, but something bad
         //happened.
         Fail(lps.TA, lps.EM.ToString() );
         break;
       case LinkProtocolState.Result.Exception:
         Fail(lps.TA, lps.EM.ToString() );
         break;
       default:
         //This should not happen.
         lock( _sync ) {
           //Clean up this guy
           _active_lps.Remove(lps.TA);
         }
         lps.Unlock();
         throw new Exception("unrecognized result: " + lps.MyResult.ToString());
     }
   }
    /**
     * Set the _target member variable and check for sanity
     * We only set the target if we can get a lock on the address
     * We can call this method more than once as long as we always
     * call it with the same value for target
     * If target is null we just return
     * 
     * @param target the value to set the target to.
     * 
     * @throws LinkException if the target is already * set to a different address
     * @throws System.InvalidOperationException if we cannot get the lock
     */
    protected void SetTarget(Address target)
    {
      if ( target == null )
        return;
 
     lock( _sync ) {
      ConnectionTable tab = LocalNode.ConnectionTable;
      if( _target_lock != null ) {
        //This is the case where _target_lock has been set once
        if( ! target.Equals( _target_lock ) ) {
          throw new LinkException("Target lock already set to a different address");
        }
      }
      else if( target.Equals( LocalNode.Address ) )
        throw new LinkException("cannot connect to self");
      else {
        lock( tab.SyncRoot ) {
          if( tab.Contains( Connection.StringToMainType( _contype ), target) ) {
            throw new LinkException("already connected");
          }
          //Lock throws an InvalidOperationException if it cannot get the lock
          tab.Lock( target, _contype, this );
          _target_lock = target;
        }
      }
     }
    }

    /**
     * Called when there is a successful completion
     */
    protected void Succeed(LinkProtocolState lps)
    {
      ConnectionTable tab = _local_n.ConnectionTable;
      //log.Info("Link Success");
      lock(_sync) {
        if( _is_finished ) {
          /*
           * This shouldn't ever happen
           */
          Console.Error.WriteLine(
                        "There were two finishes, and this one succeeded {0}",
                        lps.Connection);
          _local_n.GracefullyClose( lps.Connection.Edge );
          return;
        }
        _is_finished = true;
        _con = lps.Connection;
      }
      try {
        /* Announce the connection */
	tab.Add(lps.Connection);
      }
      catch(Exception x) {
        /* Looks like we could not add the connection */
        //log.Error("Could not Add:", x);
        _local_n.GracefullyClose( lps.Connection.Edge );
      }
      finally {
        /*
         * It is essential that we Unlock the address AFTER we
         * add the connection.  Otherwise, we could have a race
         * condition
         */
	lps.Unlock();
        FireFinished();
      }
    }

    /**
     * When a particular attempt for a TransportAddress fails,
     * we call this.
     * @param ta TransportAddress that we failed on, may be null
     * @param log_message any message appropriate to why we failed
     */
    protected void Fail(TransportAddress ta, string log_message)
    {
#if LINK_DEBUG
      Console.WriteLine("Start Linker({0}).Fail({1})",_lid,log_message);
#endif
      lock( _sync ) {
        //Finish cleaning this up:
        if( ta != null ) {
          LinkProtocolState lps = (LinkProtocolState)_active_lps[ta];
          _active_lps.Remove(ta);
          if( lps != null ) {
            lps.Unlock();
          }
          //Check to see if there if this TA corresponds to a waiting Restart:
          RestartState rss = (RestartState)_ta_to_restart_state[ta];
          if( rss != null ) {
            if( rss.IsWaiting ) {
              //This should not happen:
              throw new Exception("Failed a waiting TA: " + ta.ToString());
            }
            else {
              //This guy is done, remove it:
              //Console.WriteLine("Fail removing: {0}\nReason: {1}", ta, log_message);
              _ta_to_restart_state.Remove(ta);
            }
          }
        }
        if( AreActiveElements ) { 
          //There are more active LinkProtocolState machines working.
          //Wait till the last one goes, to really fail:
          Console.WriteLine("Still Active\nReason: {0}", log_message);
          return;
        }
        if( _is_finished ) { return; }
        _is_finished = true;
        //Unlock:
        ConnectionTable tab = LocalNode.ConnectionTable;
        tab.Unlock( _target_lock, _contype, this );
      }

      FireFinished();
#if LINK_DEBUG
      Console.WriteLine("End Linker({0}).Fail",_lid);
#endif
    }

    /*
     * This happens when an edge is bad: too much packet loss, unexpected
     * edge closure, etc..
     * There is no waiting here: we try immediately.
     * @param old_ta the previous TransportAddress that we tried
     */
    protected void MoveToNextTA(TransportAddress old_ta) {
      /*
       * Here we drop the lock, and close the edge properly
       */
      TransportAddress next_ta = null;
      lock( _sync ) {
        if( old_ta != null ) {
          _active_lps.Remove(old_ta);
        }
        //Time to go on to the next TransportAddress, and give up on this one
        if( (false == _is_finished ) &&
            ( _ta_queue.Count > 0 ) ) {
          //Don't start another if we have already succeeded
          next_ta = (TransportAddress)_ta_queue.Dequeue();
          //Make sure we record that we are starting activity on a new TA
          _active_lps[next_ta] = null;
        }
      }
#if LINK_DEBUG
      Console.WriteLine("Linker: {0} Move on to the next TA: {1}", _lid, next_ta);
#endif
      StartAttempt(next_ta);
    }

    /**
     * We sleep some random period, and retry to connect without moving
     * to the next TA.  This happens when we have the "double-lock" error.
     */
    protected void RetryThis(TransportAddress ta) {
      RestartState rss = null;
      lock( _sync ) {
        if( ta != null ) {
          //Clean out the previous:
          _active_lps.Remove(ta);
          rss = (RestartState)_ta_to_restart_state[ta];
          if( rss == null ) {
            //This is the first time we are restarting
            rss = new RestartState(ta);
            _ta_to_restart_state[ta] = rss;
          }
          //We can restart on this TA *if* 
          if ( rss.RemainingAttempts == 0 ) {
          /*
           * The old TA has had it
           */
            _ta_to_restart_state.Remove(ta);
            rss = null;
            //Time to go on to the next TransportAddress, and give up on this one
    	    if( _ta_queue.Count > 0 ) {
              ta = (TransportAddress)_ta_queue.Dequeue();
              rss = new RestartState(ta);
              _ta_to_restart_state[ta] = rss;
    	    }
          }
        }
	else {
          //No more addresses to try, oh no!!
          rss = null;
	}
      }
      if( rss == null ) {
        Fail(ta, "no more tas to restart with");
      }
      else {
#if LINK_DEBUG
        Console.WriteLine("Linker ({0}) restarting; remaining attempts: {1}",
                            _lid, rss.RemainingAttempts);
#endif
        //Actually schedule the restart
        rss.RestartEvent += this.RestartHandler;
        rss.ScheduleRestart( _local_n );
      }
    }
    protected void RestartHandler(object orss, EventArgs args) {
      RestartState rss = (RestartState)orss;
      //Call StartAttempt:
      StartAttempt( rss.TA );
    }
    
    /**
     * @param success if this is true, we have a new edge to try else, make a new edge
     * @param target_ta the transport address this edge was created with
     * @param e the new edge, if success
     * @param x the exception which may be present if sucess is false
     */
    protected void TryNext(bool success, TransportAddress target_ta,
                           Edge e, Exception x) {
      try {
        if( success ) {
          LinkProtocolState lps = new LinkProtocolState(this, target_ta, e);
          lps.FinishEvent +=  this.LinkProtocolStateFinishHandler;
          lock( _sync ) {
            //We consider each edge a fresh start.
            _active_lps[target_ta] = lps;
          }
          lps.Start();
        }
        else {
	  MoveToNextTA(target_ta);
        }
      }
      catch(Exception ex) {
        /*
         * This should never happen
         */
        System.Console.Error.WriteLine(ex);
        Fail(target_ta, ex.Message);
      }
    }
  }
}
