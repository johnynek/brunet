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

//#define LINK_DEBUG

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

//////////////////////////////////////////
////
///  First are properties and member variables
///
///////////////////////

    protected readonly string _contype;
    public string ConType { get { return _contype; } }
    protected Connection _con;
    /**
     * This is the Connection established by this Linker
     */
    public Connection Connection { get { return _con; } }
    
    public bool ConnectionInTable {
      get {
        bool result = false;
        if(  _target != null ) {
          ConnectionTable tab = _local_n.ConnectionTable;
          result = tab.Contains( Connection.StringToMainType( _contype ),
                                _target);
        }
        return result;
      }
    }

    //This is the queue that has only the address we have not tried this attempt
    protected readonly Queue _ta_queue;
    protected readonly Node _local_n;
    public Node LocalNode { get { return _local_n; } }

    public Object TargetLock {
      get { return _target_lock; }
      set { _target_lock = (Address) value; }
    }

    protected Address _target_lock;

    protected readonly Address _target;
    /** If we know the address of the node we are trying
     * to make an outgoing connection to, we lock it, and
     * remember it here
     */
    public Address Target { get { return _target; } }
    
    /** global lock for thread synchronization */
    protected readonly object _sync;
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
    /**
     * Keeps track of the restart information for each TransportAddress
     */
    protected readonly Hashtable _ta_to_restart_state;
    
    //We don't try a TA twice, this makes sure we know
    //which we have tried, and which we haven't
    protected readonly Hashtable _completed_tas;

    /**
     * How many times have we been asked to transfer a lock
     * to a ConnectionPacketHandler object.
     */
    protected int _cph_transfer_requests;

    protected readonly Random _rand;
    //Link.Start should only be called once, this throws an exception if
    //called more than once
    protected bool _started; 

    /**
     * This is where we put all the tasks we are working on
     */
    protected readonly TaskQueue _task_queue;

    /**
     * When there are no active LinkProtocolState TaskWorker objects,
     * we should not be holding the lock.
     */
    protected int _active_lps_count;
    
    //Don't allow the FinishEvent to be fired until we have 
    //started all the initial TaskWorkers
    protected bool _hold_fire;

#if LINK_DEBUG
    private int _lid;
    public int Lid { get { return _lid; } }
#endif
    
    protected readonly object _task;
    override public object Task {
      get { return _task; }
    }

    protected static int _last_lid;
    static Linker() {
      _last_lid = 0;
    }
    
//////////////
///
/// Here are the constants
///
/////////////
    
    /**
     * In some cases, we will retry our link attempt on a
     * given TA.  This happens when we get the ErrorCode.InProgress,
     * or when the other node thinks we are connected, but we don't
     * think so.
     * This is how long (in millisec) we wait between attempts.
     */
    protected static readonly int _MS_RESTART_TIME = 5000;
    /**
     * This is the number of times we will restart or retry
     * a particular TA before moving on.
     */
    protected static readonly int _MAX_RESTARTS = 8;
    /**
     * If we are passed some insane number of TAs it could take
     * a long time to get through all of them.  Only try the first
     * ones given by _MAX_REMOTETAS.  This is motivated by old (and
     * now considered buggy) clients passing large TA lists
     */
    protected static readonly int _MAX_REMOTETAS = 12;
    /**
     * As an optimization, we may make more than one attempt to
     * different TAs simulataneously.  This controls the
     * maximum number of parallel attempts
     */
    protected static readonly int _MAX_PARALLEL_ATTEMPTS = 3;
    
    
////////////////
///
///  Here are the inner classes
///
////////////

    /**
     * This protected inner class handles the job of getting
     * an Edge created for a given TransportAddress.
     */
    protected class EdgeWorker : TaskWorker {
      
      protected readonly TransportAddress _ta;
      public TransportAddress TA { get { return _ta; } }
      public override object Task { get { return _ta; } }
      
      protected bool _is_finished;
      public override bool IsFinished { get { return _is_finished; } }

      protected readonly EdgeFactory _ef;
    
      protected Exception _x;
      protected Edge _edge;
      
      protected object _sync;
      /**
       * If this was successful, it returns the edge, else
       * it throws an exception
       */
      public Edge NewEdge {
        get {
         lock( _sync ) {
          if( _x != null ) {
            throw _x;
          }
          else {
            return _edge;
          }
         }
        }
      }

      public EdgeWorker(EdgeFactory ef, TransportAddress ta) {
        _sync = new object();
        _ef = ef;
        _ta = ta;
        _is_finished = false;
      }
      
      public override void Start() {
         _ef.CreateEdgeTo(_ta, this.HandleEdge);
      }

      protected void HandleEdge(bool success, Edge e, Exception x) {
        if(ProtocolLog.LinkDebug.Enabled) {
          if (success) {
            if(ProtocolLog.LinkDebug.Enabled)
                ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
                  "(Linker) Handle edge success: {0}", e));
          } else {
            if(ProtocolLog.LinkDebug.Enabled) {
              ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
              "(Linker) Handle edge failure: {0} done.", x));
	    }
          }
        }
        lock( _sync ) {
          _is_finished = true;
          _x = x;
          _edge = e;
        }  
        FireFinished();
      }

    }
    /**
     * This inner class keeps state information for restarting
     * on a particular TransportAddress
     */
    protected class RestartState : TaskWorker {
      protected int _restart_attempts;
      public int RemainingAttempts { get { return _restart_attempts; } }
      protected readonly Linker _linker;
      protected DateTime _last_start;
      protected DateTime _next_start;
      protected readonly Random _rand;
      protected bool _is_waiting;
      public bool IsWaiting { get { return _is_waiting; } }

      public override bool IsFinished { get { return ! _is_waiting; } }

      protected readonly TransportAddress _ta;
      public TransportAddress TA { get { return _ta; } }

      public override object Task { get { return _ta; } }

      public RestartState(Linker l, TransportAddress ta,
                          int remaining_attempts) {
        _linker = l;
        _ta = ta;
        _restart_attempts = remaining_attempts;
        _rand = new Random();
        _is_waiting = false;
      }
      public RestartState(Linker l, TransportAddress ta)
             : this(l, ta, _MAX_RESTARTS) {
      }

      /**
       * Schedule the restart using the Heartbeat of the given node
       */
      public override void Start() {
        lock( this ) {
          if( _restart_attempts < 0 ) {
            throw new Exception("restarted too many times");
          }
          _last_start = DateTime.UtcNow;
          int restart_sec = (int)(_rand.NextDouble() * _MS_RESTART_TIME);
          TimeSpan interval = new TimeSpan(0,0,0,0,restart_sec);
          _next_start = _last_start + interval; 
          _is_waiting = true;
        }
        Node n = _linker.LocalNode;
        n.HeartBeatEvent += this.RestartLink;
      }
      /**
       * When we fail due to a ErrorMessage.ErrorCode.InProgress error
       * we wait restart to verify that we eventually got connected
       */
      protected void RestartLink(object node, EventArgs args)
      {
        bool fire_event = false;
        lock( this ) {
          if( _linker.ConnectionInTable ) {
            //We are already connected, stop waiting...
            fire_event = true;
          }
          else if( DateTime.UtcNow > _next_start ) { 
              if ( _rand.NextDouble() < 0.5 ) {
              fire_event = true;
              }
          }
          if( fire_event ) {
            /*
             * No matter why we are firing the event, we are no longer waiting,
             * and we don't need to hear from the heartbeat event any longer
             */
            _is_waiting = false; 
            Node n = _linker.LocalNode;
            n.HeartBeatEvent -= this.RestartLink;
          }
        }
        if( fire_event ) {
          //Fire the event without holding a lock
          FireFinished();
        }
      }
      public override string ToString() {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendFormat("RestartState: TA: {0}, RemainingAttempts: {1}\n{2}"
                        ,_ta, RemainingAttempts, _linker);
        return sb.ToString();
      }
    }
    
    /**
     * These represent the task of linking used by TaskWorked
     */
    protected class LinkerTask {
      protected readonly Address _local;
      protected readonly Address _target;
      protected readonly ConnectionType _ct;

      public LinkerTask(Address local, Address target, string ct) {
        _local = local;
        _target = target;
        _ct = Connection.StringToMainType(ct);
      }
      override public int GetHashCode() {
        int code;
        if( _target != null ) {
          code = _target.GetHashCode();
        }
        else {
          code = _ct.GetHashCode();
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

///////////////
///
///  Here is the constructor
///
////////////////

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
      _sync = new object();
      _task = new LinkerTask(local.Address, target, ct);
      lock(_sync) {
        _is_finished = false;
        _local_n = local;
        _rand = new Random();
        _active_lps_count = 0;
        _task_queue = new TaskQueue();
        _task_queue.EmptyEvent += this.FinishCheckHandler;
        _ta_queue = new Queue();
        if( target_list != null ) {
          int count = 0;
          foreach(TransportAddress ta in target_list ) {
            _ta_queue.Enqueue(ta);
            if( target != null ) {
              /*
               * Make sure we don't go insane with TAs
               * we know who we want to try to connect to,
               * if it doesn't work after some number of
               * attempts, give up.  Don't go arbitrarily
               * long
               */
              count++;
              if( count >= _MAX_REMOTETAS ) { break; }
            }
          }
        }
        _contype = ct;
        _target = target;
        _ta_to_restart_state = new Hashtable( _MAX_REMOTETAS );
        _completed_tas = new Hashtable( _MAX_REMOTETAS );
        _started = false;
        _hold_fire = true;
        _cph_transfer_requests = 0;
      }
#if LINK_DEBUG
      _last_lid++;
      _lid = _last_lid;
      if(ProtocolLog.LinkDebug.Enabled) {
	ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format("Making {0}",this));
	if( target_list != null ) {
	  ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format("TAs:"));
	  foreach(TransportAddress ta in target_list) {
	    ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format("{0}", ta));
	  }
	}
      }
#endif
    }

/////////////
///
///  Public methods
///
///////////

    /**
     * This tells the Linker to make its best effort to create
     * a connection to another node
     */
    override public void Start() {
#if LINK_DEBUG
      if (ProtocolLog.LinkDebug.Enabled) {
	ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format("Linker({0}).Start at: {1}", _lid, DateTime.Now));
      }
#endif
      //Just move to the next (first) TA
      lock( _sync ) {
        if( _started ) { throw new Exception("Linker already Started"); }
        _started = true;
        _hold_fire = true;
      }
      //Get the set of addresses to try
      int parallel_attempts = _MAX_PARALLEL_ATTEMPTS;
      if( _target == null ) {
        //Try more attempts in parallel to get leaf connections.
        //This is a hack to make initial connection faster
        parallel_attempts = 3 * parallel_attempts;
      }
      for(int i = 0; i < parallel_attempts; i++) {
        TransportAddress next_ta = MoveToNextTA(null);
        if( next_ta != null ) {
          StartAttempt(next_ta);
        }
      }
      /*
       * We have so far prevented ourselves from sending the
       * FinishEvent.  Now, we have laid all the ground work,
       * if there are no active tasks, there won't ever be,
       * so lets check to see if we need to fire the finish
       * event
       */
      bool fire = false;
      lock( _sync ) {
        //We have started all our initial tasks:
        _hold_fire = false;
        if( _task_queue.WorkerCount == 0 ) {
          //We are not putting any more workers in, so go ahead and finish:
          fire = true;
        }
      }
      if( fire ) {
        FinishCheckHandler(_task_queue, EventArgs.Empty);
      }
    } 
    
    /**
     * Standard object override
     */
    public override string ToString() {
      System.Text.StringBuilder sb = new System.Text.StringBuilder();
      sb.AppendFormat("Linker");
#if LINK_DEBUG
      sb.AppendFormat("({0})", _lid);
#endif
      sb.AppendFormat(": Target: {0} Contype: {1}\n", _target, _contype);
      return sb.ToString();
    }

    /**
     * Allow if we are transfering to a LinkProtocolState or ConnectionPacketHandler
     */
    public bool AllowLockTransfer(Address a, string contype, ILinkLocker l) {
      bool entered = System.Threading.Monitor.TryEnter(_sync); //like a lock(_sync) .
      bool allow = false;
      if (false == entered ) {
	if (ProtocolLog.LinkDebug.Enabled) {
	  ProtocolLog.Write(ProtocolLog.LinkDebug,
          String.Format("Cannot acquire Linker lock for transfer (potential deadlock)."));
	}
        allow = false;
      }
      else {
	try {
          if( l is Linker ) {
            //Never transfer to another linker:
            allow = false;
          }
          else if ( l is ConnectionPacketHandler.CphState ) {
          /**
           * The ConnectionPacketHandler only locks when it
           * has actually received a packet.  This is a "bird in the
           * hand" situation, however, if both sides in the double
           * link case transfer the lock, then we have accomplished
           * nothing.
           *
           * There is a specific case to worry about: the case of
           * a firewall, where only one node can contact the other.
           * In this case, it may be very difficult to connect if
           * we don't eventually transfer the lock to the
           * ConnectionPacketHandler.  In the case of bi-directional
           * connectivity, we only transfer the lock if the
           * address we are locking is greater than our own (which
           * clearly cannot be true for both sides).
           * 
           * To handle the firewall case, we keep count of how
           * many times we have been asked to transfer the lock.  On
           * the third time we are asked, we assume we are in the firewall
           * case and we allow the transfer, this is just a hueristic.
           */
            if( a.Equals( _target_lock ) ) {
              _cph_transfer_requests++;
              if ( ( _cph_transfer_requests >= 3 ) ||
                 ( a.CompareTo( LocalNode.Address ) > 0) ) {
                _target_lock = null; 
                allow = true;
              }
            }
          }
          else if( l is LinkProtocolState ) {
            LinkProtocolState lps = (LinkProtocolState)l;
            /**
             * Or Transfer the lock to a LinkProtocolState if:
             * 1) We created this LinkProtocolState
             * 2) The LinkProtocolState has received a packet
             */
            if( (lps.Linker == this )
                && ( lps.LinkMessageReply != null ) )
            {
                _target_lock = null; 
                allow = true;
            }
          }
	} finally {
          // If we don't have a try ... finally here, an exception in the
          // above could cause us to never release the lock on _sync.
	  System.Threading.Monitor.Exit(_sync);
	}
      }
#if LINK_DEBUG
      if (ProtocolLog.LinkDebug.Enabled) {
	  ProtocolLog.Write(ProtocolLog.LinkDebug,
                            String.Format("Linker({0}) {1}: transfering lock on {2} to {3}",
                                          _lid, allow, a, l));
      }
#endif
      return allow;
    }

//////////////////
///
/// Protected/Private methods
///
/////////////////
    
    /**
     * Called when there is a successful completion
     */
    protected void AnnounceConnection(LinkProtocolState lps)
    {
      /**
       * We can potentially create more than one connection since
       * more than one parallel attempt might succeed.  We just
       * take what we can get.
       */
      Connection c = lps.Connection;
      try {
        /* Announce the connection */
        _local_n.ConnectionTable.Add(c);
        lock( _sync ) {
          _con = c;
        }  
#if LINK_DEBUG
	if (ProtocolLog.LinkDebug.Enabled) {
	  ProtocolLog.Write(ProtocolLog.LinkDebug, 
			    String.Format("Linker({0}) added {1} at: {2}", _lid, c, DateTime.Now));
	}
#endif
      }
      catch(Exception) {
#if LINK_DEBUG
        if (ProtocolLog.LinkDebug.Enabled) {
	  ProtocolLog.Write(ProtocolLog.LinkDebug, 
			    String.Format("Linker({0}) exception trying to add: {1}", _lid, c));
	}
#endif
        /* Looks like we could not add the connection */
        //log.Error("Could not Add:", x);
        _local_n.GracefullyClose( c.Edge );
        //Oh well, things didn't go so hot.
        StartAttempt( MoveToNextTA(lps.TA) );
      }
    }
    
    /**
     * @param success if this is true, we have a new edge to try else, make a new edge
     * @param target_ta the transport address this edge was created with
     * @param e the new edge, if success
     * @param x the exception which may be present if sucess is false
     */
    protected void EdgeWorkerHandler(object edgeworker, EventArgs args)
    {
      EdgeWorker ew = (EdgeWorker)edgeworker;
      bool close_edge = false;
      try {
        TaskWorker lps = new LinkProtocolState(this, ew.TA, ew.NewEdge);
        lps.FinishEvent +=  this.LinkProtocolStateFinishHandler;
        SetTarget(_target);
        //Keep a proper track of the active LinkProtocolStates:
        System.Threading.Interlocked.Increment(ref _active_lps_count);
        _task_queue.Enqueue(lps);
      }
      catch(ConnectionExistsException) {
        //We already have a connection to the target
        close_edge = true;
      }
      catch(LinkException) {
        //This happens if SetTarget sees that we are already connected
        //Our only choice here is to close the edge and give up.
        close_edge = true;
      }
      catch(CTLockException) {
        /*
         * SetTarget could not get the lock on the address.
         * Try again later
         */
        close_edge = true;
        RetryThis( ew.TA );
      }
      catch(EdgeException) {
        /*
         * If there is some problem creating the edge,
         * we wind up here.  Just move on
         */
        TransportAddress next_ta = MoveToNextTA(ew.TA);
        StartAttempt(next_ta);
      }
      catch(Exception ex) {
        /*
         * The edge creation didn't work out so well
         */
        Console.Error.WriteLine(ex);
        TransportAddress next_ta = MoveToNextTA(ew.TA);
        StartAttempt(next_ta);
      }
      if( close_edge ) {
        try {
          ew.NewEdge.Close();
        }
        catch(Exception) {
          //Ignore any exception
        }
      }
    }
    
    /**
     * The queue has just become completely empty.  Our task 
     * is finally over.
     */
    protected void FinishCheckHandler(object taskqueue, EventArgs args)
    {
      bool fire_finished = false;
      lock( _sync ) {
        if( (!_is_finished) && (!_hold_fire) ) {
          _is_finished = true;
          fire_finished = true;
        }
      }
      if( fire_finished ) {
        //Unlock:
        Unlock();
#if LINK_DEBUG
        if (ProtocolLog.LinkDebug.Enabled) {
	  ProtocolLog.Write(ProtocolLog.LinkDebug, 
			    String.Format("Linker({0}) finished at: {1}", _lid, DateTime.Now));
	}
#endif
        FireFinished();
      }
    }

   protected void LinkProtocolStateFinishHandler(object olps, EventArgs args) {
     LinkProtocolState lps = (LinkProtocolState)olps;
#if LINK_DEBUG
     if (ProtocolLog.LinkDebug.Enabled) {
	ProtocolLog.Write(ProtocolLog.LinkDebug, 
			  String.Format("Linker({0}): {1} finished with result: {2} at: {3}", _lid,
					lps, lps.MyResult, DateTime.Now));
     }
#endif
     switch( lps.MyResult ) {
       case LinkProtocolState.Result.Success:
         //Add this connection to our ConnectionTable
         AnnounceConnection(lps);
         break;
       case LinkProtocolState.Result.MoveToNextTA:
         //Hold the lock, it will be transferred:
         // old LPS -> Linker -> new LPS
         TransportAddress next = MoveToNextTA(lps.TA);
         StartAttempt(next);
         break;
       case LinkProtocolState.Result.RetryThisTA:
         RetryThis(lps.TA);
         break;
       case LinkProtocolState.Result.ProtocolError:
         //Fail(lps.TA, lps.EM.ToString() );
         break;
       case LinkProtocolState.Result.Exception:
         //Fail(lps.TA, lps.EM.ToString() );
         break;
       default:
         //This should not happen.
         Console.Error.WriteLine("unrecognized result: " + lps.MyResult.ToString());
         break;
     }
     int current_active = System.Threading.Interlocked.Decrement(ref _active_lps_count);
     if( current_active == 0 ) {
       //We have finished handling this lps finishing,
       //if we have not started another yet, we are not
       //going to right away.  In the mean time, release
       //the lock
       Unlock();
     }
   }
    /**
     * Clean up any state information for the given TransportAddress
     * and setup for the next run and return the next TransportAddress
     * to try, or null if there is no other address to try.
     * @param old_ta the previous TransportAddress that we tried
     * @return the next TransportAddress to try, or null if there are no more
     */
    protected TransportAddress MoveToNextTA(TransportAddress old_ta) {
      /*
       * Here we drop the lock, and close the edge properly
       */
      TransportAddress next_ta = null;
      lock( _sync ) {
        //Time to go on to the next TransportAddress, and give up on this one
        bool keep_looking = (_con == null) && (!_is_finished)
                             && (_ta_queue.Count > 0);
        while( keep_looking ) {
          //Don't start another if we have already succeeded
          next_ta = (TransportAddress)_ta_queue.Dequeue();
          if( _completed_tas.ContainsKey(next_ta) ) {
            Console.Error.WriteLine("TA: {0} appeared in list twice", next_ta);
            next_ta = null;
            //Keep looking only if there are more to look at:
            keep_looking = (_ta_queue.Count > 0);
          }
          else {
            //Make sure we don't try this again
            _completed_tas[next_ta] = null;
            //We have our next TA
            keep_looking = false;
          }
        }
      }
#if LINK_DEBUG
      if (ProtocolLog.LinkDebug.Enabled) {
	ProtocolLog.Write(ProtocolLog.LinkDebug, 
			  String.Format("Linker({0}) Move on to the next TA: {1}", _lid, next_ta));
      }
#endif
      return next_ta;
    }
   
    /**
     * When a RestartState finishes its task, this is the
     * EventHandler that is called.
     */
    protected void RestartHandler(object orss, EventArgs args) {
      RestartState rss = (RestartState)orss;
      //Call StartAttempt:
      StartAttempt( rss.TA );
    }

    /**
     * We sleep some random period, and retry to connect without moving
     * to the next TA.  This happens when we have the "double-lock" error.
     */
    protected void RetryThis(TransportAddress ta) {
      RestartState rss = null;
      lock( _sync ) {
        rss = (RestartState)_ta_to_restart_state[ta];
        if( rss == null ) {
          //This is the first time we are restarting
          rss = new RestartState(this, ta);
        }
        else if (rss.RemainingAttempts > 0) {
          //We have to decrement the remainingAttempts:
          int ra = rss.RemainingAttempts - 1;
          rss = new RestartState(this, ta, ra);
        }
        else {
        /*
         * The old TA has had it
         */
          _ta_to_restart_state.Remove(ta);
          rss = null;
          //Time to go on to the next TransportAddress, and give up on this one
          ta = MoveToNextTA(ta);
            if( ta != null ) {
            rss = new RestartState(this, ta);
            }
        }
        if( rss != null ) {
          _ta_to_restart_state[ta] = rss;
        }
      }
      if( rss == null ) {
#if LINK_DEBUG
        //Fail(ta, "no more tas to restart with");
        if (ProtocolLog.LinkDebug.Enabled) {
	  ProtocolLog.Write(ProtocolLog.LinkDebug, 
			    String.Format("Linker({0}), no more tas to restart with", _lid));
	}
#endif
      }
      else {
#if LINK_DEBUG
        if (ProtocolLog.LinkDebug.Enabled) {
	ProtocolLog.Write(ProtocolLog.LinkDebug, 
			  String.Format("Linker({0}) restarting; remaining attempts: {1}",
					_lid, rss.RemainingAttempts));
	}
#endif
        //Actually schedule the restart
        rss.FinishEvent += this.RestartHandler;
        _task_queue.Enqueue( rss );
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
      if( target.Equals( LocalNode.Address ) ) {
        throw new LinkException("cannot connect to self");
      }
 
      ConnectionTable tab = LocalNode.ConnectionTable;
      /*
       * This throws an exception if:
       * 0) we can't get the lock.
       * 1) we already have set _target_lock to something else
       */
      tab.Lock( target, _contype, this);
    }
    
    /**
     * This manages a new attempt to connect on a given TransportAddress
     */
    protected void StartAttempt(TransportAddress next_ta) {
      if( next_ta == null ) {
        //We only get a null TA if we are totally out.  Time to fail:
        //Fail(next_ta, "no more tas to restart with");
        return;
      }
      if( ConnectionInTable ) {
        //Looks like we are already connected...
        //Fail(next_ta, "Connected before we could StartAttempt");
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
          if (ProtocolLog.LinkDebug.Enabled) {
	    ProtocolLog.Write(ProtocolLog.LinkDebug, 
			      String.Format("Linker ({0}) attempting to lock {1}", _lid, _target));
	  }
#endif
         /*
          * Locks flow around in complex ways, but we
          * (or one of our LinkProtocolState)
          * will hold the lock
          */
          SetTarget(_target);
#if LINK_DEBUG
          if (ProtocolLog.LinkDebug.Enabled) {
	    ProtocolLog.Write(ProtocolLog.LinkDebug, 
			      String.Format("Linker ({0}) acquired lock on {1}", _lid, _target));
	  }
#endif
        /*
         * Asynchronously gets an edge, and then begin the
         * link attempt with it.  If it fails, and there
         * are more TransportAddress objects, this method
         * will call itself again.  If there are no more TAs
         * the Link attempt Fails.
         */
#if LINK_DEBUG
          if (ProtocolLog.LinkDebug.Enabled) {
	    ProtocolLog.Write(ProtocolLog.LinkDebug, 
			      String.Format("Linker: ({0}) Trying TA: {1}", _lid, next_ta));
	  }
#endif
         TaskWorker ew = new EdgeWorker(_local_n.EdgeFactory, next_ta);
         ew.FinishEvent += this.EdgeWorkerHandler;
         //Start it going
         _task_queue.Enqueue(ew);
      }
      catch(ConnectionExistsException) {
        //We already have a connection to the target
      }
      catch(CTLockException) {
#if LINK_DEBUG
        if (ProtocolLog.LinkDebug.Enabled) {
	  ProtocolLog.Write(ProtocolLog.LinkDebug, 
			    String.Format("Linker ({0}) failed to lock {1}", _lid, _target));
	}
#endif
        //This is thrown when ConnectionTable cannot lock.  Lets try again:
        RetryThis(next_ta);
      }
      catch(Exception) {
        //Fail(next_ta, x.Message);
      }
    }

    /**
     * If we hold a lock permanently, we may prevent connections
     * to a given address
     * This is called to release a lock from the ConnectionTable.
     * If the lock is not held, it is still safe to call this method
     * (in which case nothing happens).
     */
    protected void Unlock() {
      ConnectionTable tab = LocalNode.ConnectionTable;
      tab.Unlock( _contype, this );
    }
  }
}
