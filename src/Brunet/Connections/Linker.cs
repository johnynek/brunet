/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2005 - 2008  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

//#define LINK_DEBUG

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

using System;
using System.Threading;
using System.Collections;

using Brunet.Transport;

using BC = Brunet.Concurrent;
using BU = Brunet.Util;

namespace Brunet.Connections
{

  /**
   *
   * Given a list of remote TransportAddress
   * objects, Linker creates the link between the remote node and
   * the local node
   *
   * There are several steps to linking:
   * 1) create an Edge for a TA (EdgeWorker)
   * 2) Start a LinkProtocolState (LPS) working on the Edge to get connected.
   * 3) At the completion of the LPS, we either, retry the TA, move to the
   * next, or give up.
   * 4) If we retry, we wait a time uniformly distributed over a fixed
   * interval.  After that period, we start again at the beginning.
   *
   * The code is designed to be able to handle several attempts in parallel so
   * nodes with many TAs can be connected to more quickly. 
   * 
   */

  public class Linker : BC.TaskWorker, ILinkLocker
  {

//////////////////////////////////////////
////
///  First are properties and member variables
///
///////////////////////

    protected int _added_cons; //The number of successfully added connections
    protected readonly string _contype;
    public string ConType { get { return _contype; } }
    protected readonly ConnectionType _maintype;
    public bool ConnectionInTable {
      get {
        bool result = false;
        if(  _target != null ) {
          ConnectionTable tab = _local_n.ConnectionTable;
          result = tab.Contains(_maintype, _target);
        }
        return result;
      }
    }
    
    //This is the queue that has only the address we have not tried this attempt
    protected readonly BC.LockFreeQueue<TransportAddress> _ta_queue;
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

    /** unique token for connection setup messages */
    protected readonly string _token;
    public string Token { get { return _token; } }

    /**
     * Keeps track of the restart information for each TransportAddress
     */
    protected readonly Hashtable _ta_to_restart_state;
    
    /**
     * How many times have we been asked to transfer a lock
     * to a ConnectionPacketHandler object.
     */
    protected int _cph_transfer_requests;

    //Link.Start should only be called once, this throws an exception if
    //called more than once
    protected int _started; 

    /**
     * This is where we put all the tasks we are working on
     */
    protected readonly BC.TaskQueue _task_queue;
    /**
     * When there are no active LinkProtocolState TaskWorker objects,
     * we should not be holding the lock.
     */
    protected int _active_lps_count;
    
    //Don't allow the FinishEvent to be fired until we have 
    //started all the initial TaskWorkers
    protected int _hold_fire;

#if LINK_DEBUG
    private int _lid;
    public int Lid { get { return _lid; } }
#endif
    
    protected readonly object _task;
    override public object Task {
      get { return _task; }
    }

    protected static int _last_lid = 0;
    
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
     * maximum number of parallel attempts.  If this number is too low
     * it can take a long time for NATed machines to connect to each
     * other.
     */
    protected static readonly int _MAX_PARALLEL_ATTEMPTS = 6;
    
    
////////////////
///
///  Here are the inner classes
///
////////////

    /**
     * This protected inner class handles the job of getting
     * an Edge created for a given TransportAddress.
     */
    protected class EdgeWorker : BC.TaskWorker {
      
      protected readonly TransportAddress _ta;
      public TransportAddress TA { get { return _ta; } }
      public override object Task { get { return _ta; } }
      
      public override bool IsFinished { get { return _result.IsSet; } }

      protected readonly Node _n;
      public class EWResult {
        public readonly bool Success;
        public readonly Edge Edge;
        public readonly Exception Exception;
        public EWResult(bool suc, Edge e, Exception x) {
          Success = suc;
          Edge = e;
          Exception = x; 
        }
      }
      protected readonly BC.WriteOnce<EWResult> _result;
      
      /**
       * If this was successful, it returns the edge, else
       * it throws an exception
       */
      public Edge NewEdge {
        get {
          EWResult r;
          if( false == _result.TryGet(out r) ) {
            return null;
          }
          if( r.Exception != null ) {
            throw r.Exception;
          }
          else {
            return r.Edge;
          }
        }
      }

      public EdgeWorker(Node n, TransportAddress ta) {
        _n = n;
        _ta = ta;
        _result = new BC.WriteOnce<EWResult>();
      }
      
      public override void Start() {
        EWResult res;
        if( false == _result.TryGet(out res) ) {
          //This is the first time we've been called.
          _n.EdgeFactory.CreateEdgeTo(_ta, this.HandleEdge);
        }
        else {
          //This is the second time we've been called:
          if(BU.ProtocolLog.LinkDebug.Enabled) {
            if (res.Success) {
              if(BU.ProtocolLog.LinkDebug.Enabled)
                  BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug, String.Format(
                    "(Linker) Handle edge success: {0}", res.Edge));
            } else {
              if(BU.ProtocolLog.LinkDebug.Enabled) {
                BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug, String.Format(
                "(Linker) Handle edge failure: {0} done.", res.Exception));
  	          }
            }
          }
          FireFinished();
        }
      }

      protected void HandleEdge(bool success, Edge e, Exception x) {
        _result.Value = new EWResult(success, e, x);
        //Finish this job in the AnnounceThread:
        try {
          _n.EnqueueAction(this);
        }
        catch(Exception eax) {
          Console.Error.WriteLine("ERROR Could not Enqueue: {0}", eax);
          this.Start();
        }
      }

    }
    /**
     * This is a TaskQueue where new TaskWorkers are started
     * by EnqueueAction, so they are executed in the announce thread
     * and without the call stack growing arbitrarily
     */
    protected class NodeTaskQueue : BC.TaskQueue {
      protected readonly Node LocalNode;
      public NodeTaskQueue(Node n) {
        LocalNode = n;
      }
      protected override void Start(BC.TaskWorker tw) {
        try {
          LocalNode.EnqueueAction(tw);
        }
        catch {
          /*
           * We could get an exception if queue in LocalNode is closed
           */
          tw.Start();
        }
      }
    }
    /**
     * This inner class keeps state information for restarting
     * on a particular TransportAddress
     */
    protected class RestartState : BC.TaskWorker {
      protected readonly int _restart_attempts;
      public int RemainingAttempts { get { return _restart_attempts; } }
      protected readonly Linker _linker;

      protected readonly TransportAddress _ta;
      public TransportAddress TA { get { return _ta; } }

      public override object Task { get { return _ta; } }
      protected int _first_start;

      public RestartState(Linker l, TransportAddress ta,
                          int remaining_attempts) {
        _linker = l;
        _ta = ta;
        _restart_attempts = remaining_attempts;
        _first_start = 1; 
      }
      public RestartState(Linker l, TransportAddress ta)
             : this(l, ta, _MAX_RESTARTS) {
      }

      /**
       * Schedule the restart using the Heartbeat of the given node
       */
      public override void Start() {
        if( _restart_attempts < 0 ) {
          throw new Exception("restarted too many times");
        }
        if( Interlocked.Exchange(ref _first_start, 0) == 1) {
          //Compute the interval:
#if BRUNET_SIMULATOR
          Random rand = Node.SimulatorRandom;
#else
          Random rand = new Random();
#endif
          int restart_msec = rand.Next(_MS_RESTART_TIME);
          Action<DateTime> torun = delegate(DateTime now) {
            //Tell the node to call Start on us:
            _linker.LocalNode.EnqueueAction(this);
          };
          //Schedule a waiting period:
          Brunet.Util.FuzzyTimer.Instance.DoAfter(torun, restart_msec, 1000);
        }
        else {
          //Time to finish
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
      protected readonly string _task_diff;

      public LinkerTask(Address local, Address target, string ct, string task_diff) {
        _local = local;
        _target = target;
        _ct = Connection.StringToMainType(ct);
        _task_diff = task_diff;
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
        if(lt == null) {
          return false;
        } else if(false == lt._local.Equals(_local) ||
            false == lt._ct.Equals(_ct) ||
            false == lt._task_diff.Equals(_task_diff))
        {
          return false;
        } else if((_target != null && false == _target.Equals(lt._target)) ||
           (lt._target != null && false == lt._target.Equals(_target)))
        {
          return false;
        }
        return true;
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
     * @token unique token to associate the different connection setup messages
     */
    public Linker(Node local, Address target, ICollection target_list, string ct, string token) :
      this(local, target, target_list, ct, token, string.Empty)
    {
    }

    public Linker(Node local, Address target, ICollection target_list, string ct, string token, string task_diff)
    {
      _task = new LinkerTask(local.Address, target, ct, task_diff);
      _local_n = local;
      _active_lps_count = 0;
      //this TaskQueue starts new tasks in the announce thread of the node.
      _task_queue = new NodeTaskQueue(local);
      _task_queue.EmptyEvent += this.FinishCheckHandler;
      _ta_queue = new BC.LockFreeQueue<TransportAddress>();
      if( target_list != null ) {
        int count = 0;
        Hashtable tas_in_queue = new Hashtable( _MAX_REMOTETAS );
        foreach(TransportAddress ta in target_list ) {
          if(tas_in_queue.ContainsKey(ta) ) {
//            Console.Error.WriteLine("TA: {0} appeared in list twice", ta);
          }
          else {
            _ta_queue.Enqueue(ta);
            tas_in_queue[ta] = null; //Remember that we've seen this one
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
      }
      _added_cons = 0; //We have not added a connection yet
      _contype = ct;
      _maintype = Connection.StringToMainType( _contype );
      _target = target;
      _token = token;
      _ta_to_restart_state = new Hashtable( _MAX_REMOTETAS );
      _started = 0;
      _hold_fire = 1;
      _cph_transfer_requests = 0;
#if LINK_DEBUG
      _lid = Interlocked.Increment(ref _last_lid);
      if(BU.ProtocolLog.LinkDebug.Enabled) {
        BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug, String.Format("{0}: Making {1}",
              _local_n.Address, this));
        if( target_list != null ) {
	        BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug, String.Format("TAs:"));
          foreach(TransportAddress ta in target_list) {
            BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug, String.Format("{0}", ta));
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
      if (BU.ProtocolLog.LinkDebug.Enabled) {
        BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug, String.Format("{0}, Linker({1}).Start at: {2}", _local_n.Address, this, DateTime.UtcNow));
      }
#endif
      //Try to set _started to 1, if already set to one, throw an exception
      if( Interlocked.Exchange(ref _started, 1) == 1) {
        throw new Exception("Linker already Started");
      }
      //Just move to the next (first) TA
      //Get the set of addresses to try
      int parallel_attempts = _MAX_PARALLEL_ATTEMPTS;
      if( _target == null ) {
        //Try more attempts in parallel to get leaf connections.
        //This is a hack to make initial connection faster
        parallel_attempts = 2 * parallel_attempts;
      }
      //This would be an ideal place for a list comprehension
      ArrayList tasks_to_start = new ArrayList(parallel_attempts);
      for(int i = 0; i < parallel_attempts; i++) {
        BC.TaskWorker t = StartAttempt( NextTA() );
        if( t != null ) {
          tasks_to_start.Add( t );
        }
      }
      foreach(BC.TaskWorker t in tasks_to_start) {
        _task_queue.Enqueue(t);
      }
      /*
       * We have so far prevented ourselves from sending the
       * FinishEvent.  Now, we have laid all the ground work,
       * if there are no active tasks, there won't ever be,
       * so lets check to see if we need to fire the finish
       * event
       */
      Interlocked.Exchange(ref _hold_fire, 0);
      if( _task_queue.WorkerCount == 0 ) {
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
     * Note this method does not change anything, if the transfer is done, it
     * is done by the ConnectionTable while it holds its lock.
     */
    public bool AllowLockTransfer(Address a, string contype, ILinkLocker l) {
      bool allow = false;
      bool hold_lock = (a.Equals( _target_lock ) && contype == _contype);
      if( false == hold_lock ) {
        //We don't even hold this lock!
        throw new Exception(
                            String.Format("{2} asked to transfer a lock({0}) we don't hold: ({1})",
                                          a, _target_lock, this));
      }
      if( l is Linker ) {
        //Never transfer to another linker:
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
        int reqs = Interlocked.Increment(ref _cph_transfer_requests);
        if ( (reqs >= 3 ) || ( a.CompareTo( LocalNode.Address ) > 0) ) {
          allow = true;
        }
      }
      else if( l is LinkProtocolState ) {
        LinkProtocolState lps = (LinkProtocolState)l;
        /**
         * Or Transfer the lock to a LinkProtocolState if:
         * 1) We created this LinkProtocolState
         * 2) The LinkProtocolState has received a packet
         */
        if( (lps.Linker == this ) && ( lps.LinkMessageReply != null ) ) {
          allow = true;
        }
      }
#if LINK_DEBUG
      if (BU.ProtocolLog.LinkDebug.Enabled) {
	  BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug,
                            String.Format("{0}: Linker({1}) {2}: transfering lock on {3} to {4}",
                              _local_n.Address, _lid, (_target_lock == null), a, l));
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
     * @param success if this is true, we have a new edge to try else, make a new edge
     * @param target_ta the transport address this edge was created with
     * @param e the new edge, if success
     * @param x the exception which may be present if sucess is false
     */
    protected void EdgeWorkerHandler(object edgeworker, EventArgs args)
    {
      EdgeWorker ew = (EdgeWorker)edgeworker;
      bool close_edge = false;
      BC.TaskWorker next_task = null;
      try {
        Edge e = ew.NewEdge; //This can throw an exception
        SetTarget(); //This can also throw an exception

        //If we make it here, we did not have any problem.
        
        next_task = new LinkProtocolState(this, ew.TA, e);
        next_task.FinishEvent +=  this.LinkProtocolStateFinishHandler;
        //Keep a proper track of the active LinkProtocolStates:
        Interlocked.Increment(ref _active_lps_count);
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
        next_task = GetRestartState( ew.TA );
        if( next_task == null ) {
          //We've restarted too many times:
          next_task = StartAttempt( NextTA() );
        }
      }
      catch(EdgeException) {
        /*
         * If there is some problem creating the edge,
         * we wind up here.  Just move on
         */
        next_task = StartAttempt( NextTA() );
      }
      catch(Exception ex) {
        /*
         * The edge creation didn't work out so well
         */
        BU.ProtocolLog.WriteIf(BU.ProtocolLog.LinkDebug, ex.ToString());
        next_task = StartAttempt( NextTA() );
      }
      if( close_edge ) {
        try {
          ew.NewEdge.Close();
        }
        catch(Exception) {
          //Ignore any exception
        }
      }
      if( next_task != null ) {
        /*
         * We should start a new task now
         */
        _task_queue.Enqueue(next_task);
      }
    }
    
    /**
     * The queue has just become completely empty.  Our task 
     * is finally over.
     */
    protected void FinishCheckHandler(object taskqueue, EventArgs args)
    {
      if( _hold_fire == 0 ) {
        Unlock();
#if LINK_DEBUG
        if (BU.ProtocolLog.LinkDebug.Enabled) {
          BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug, 
			      String.Format("{0}: Linker({1}) finished at: {2}",
              _local_n.Address, _lid, DateTime.UtcNow));
        }
#endif
        FireFinished();
      }
    }
  
  /**
   * Given a TransportAddress, return the associated RestartState.
   * If there are no more restarts, this returns null.
   */
  protected RestartState GetRestartState(TransportAddress ta) {
    RestartState rss = null;
    lock( _ta_to_restart_state ) {
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
        rss = null;
      }
    }
    if( rss != null ) {
      _ta_to_restart_state[rss.TA] = rss;
      rss.FinishEvent += this.RestartHandler;
#if LINK_DEBUG
      if (BU.ProtocolLog.LinkDebug.Enabled) {
        BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug, 
          String.Format("{0}: Linker({1}) restarting; remaining attempts: {2}",
            _local_n.Address, _lid, rss.RemainingAttempts));
        }
#endif
    }
    return rss;
  }

   protected void LinkProtocolStateFinishHandler(object olps, EventArgs args) {
     LinkProtocolState lps = (LinkProtocolState)olps;
#if LINK_DEBUG
     if (BU.ProtocolLog.LinkDebug.Enabled) {
	BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug, 
			  String.Format("{0}: Linker({1}): {2} finished with result: {3} at: {4}",
          _local_n.Address, _lid, lps, lps.MyResult, DateTime.UtcNow));
     }
#endif
     BC.TaskWorker next_task = null;
     switch( lps.MyResult ) {
       case LinkProtocolState.Result.Success:
         /*
          * Great, the Connection is up and in our table now
          * Just do nothing now and wait for the other tasks
          * to finish, at which point, the Linker will fire
          * its FinishEvent.
          */
         Interlocked.Increment(ref _added_cons);
#if LINK_DEBUG
         if (BU.ProtocolLog.LinkDebug.Enabled) {
           BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug, 
             String.Format("{0}: Linker({1}) added {2} at: {3}", _local_n.Address,
               _lid, lps.Connection, DateTime.UtcNow));
         }
#endif
         break;
       case LinkProtocolState.Result.RetryThisTA:
         next_task = GetRestartState(lps.TA);
         if( next_task == null ) {
           goto case LinkProtocolState.Result.MoveToNextTA;
         }
         break;
       case LinkProtocolState.Result.MoveToNextTA:
         //Hold the lock, it will be transferred:
         // old LPS -> Linker -> new LPS
         next_task = StartAttempt( NextTA() );
         break;
       case LinkProtocolState.Result.ProtocolError:
         break;
       case LinkProtocolState.Result.Exception:
         break;
       default:
         //This should not happen.
         Console.Error.WriteLine("unrecognized result: " + lps.MyResult.ToString());
         break;
     }
     if( next_task != null ) {
       //We have some new task to start
       _task_queue.Enqueue(next_task);
     }
     int current_active = Interlocked.Decrement(ref _active_lps_count);
     if( current_active == 0 ) {
       //We have finished handling this lps finishing,
       //if we have not started another yet, we are not
       //going to right away.  In the mean time, release
       //the lock
       Unlock();
     }
   }

   /**
    * If there is another TA in the _ta_queue, dequeue and return it,
    * otherwise, return null
    */
   protected TransportAddress NextTA() {
     bool succ;
     TransportAddress next_ta = _ta_queue.TryDequeue(out succ);
     if( succ ) {
       return next_ta;
     }
     else {
       return null;
     }
   }
    /**
     * When a RestartState finishes its task, this is the
     * EventHandler that is called.
     *
     * At the end of a RestartState, we call StartAttempt for
     * the TA we are waiting on.  If we have restarted too many
     * times, we move to the next TA, and StartAttempt with that one.
     */
    protected void RestartHandler(object orss, EventArgs args) {
      RestartState rss = (RestartState)orss;
      BC.TaskWorker next_task = StartAttempt( rss.TA );
      if( next_task == null ) {
        //Looks like it's time to move on:
        next_task = StartAttempt( NextTA() ); 
      }
      if( next_task != null ) {
        _task_queue.Enqueue(next_task);
      }
    }

    /**
     * Set the _target_lock member variable and check for sanity
     * We only set the target if we can get a lock on the address
     * We can call this method more than once as long as we always
     * call it with the same value for target
     * If target is null we just return
     * 
     * @throws LinkException if the target is already * set to a different address
     * @throws System.InvalidOperationException if we cannot get the lock
     */
    protected void SetTarget()
    {
      if ( _target == null )
        return;
      if( _target.Equals( LocalNode.Address ) ) {
        throw new LinkException("cannot connect to self");
      }
      if( ConnectionInTable ) {
        throw new LinkException("Connection already present");
      }
      /*
       * This throws an exception if:
       * 0) we can't get the lock.
       * 1) we already have set _target_lock to something else
       */
      LocalNode.LockMgr.Lock( _target, _contype, this);
    }
    
    /**
     * This creates a TaskWorker that represents the next step that should
     * be taken for the ta.  It can only be two tasks: create the edge
     * (EdgeWorker) or wait and try again (RestartState).
     *
     * We return null if:
     *  - the TA is null
     *  - Linker is finished
     *  - a Connection was already created
     *  - this TA has been restarted too many times
     *
     * If we cannot get a ConnectionTable.Lock with SetTarget, we return a
     * RestartState to wait a little while to try to get the lock again.
     *
     * @returns the next TaskWorker that should be enqueued, does not start or
     * Enqueue it.
     */
    protected BC.TaskWorker StartAttempt(TransportAddress next_ta) {
      BC.TaskWorker next_task = null;
      if ( (next_ta == null) || (_added_cons != 0) || IsFinished || ConnectionInTable ) {
        //Looks like we are already connected...
        return null;
      }
      try {
#if LINK_DEBUG
        if (BU.ProtocolLog.LinkDebug.Enabled) {
          BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug, 
            String.Format("{0}: Linker ({1}) attempting to lock {2}", _local_n.Address, _lid, _target));
        }
#endif
        /*
         * If we cannot set this address as our target, we
         * stop before we even try to make an edge.
          * 
          * Locks flow around in complex ways, but we
          * (or one of our LinkProtocolState)
          * will hold the lock
          */
        SetTarget();
#if LINK_DEBUG
        if (BU.ProtocolLog.LinkDebug.Enabled) {
            BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug, 
			        String.Format("{0}: Linker ({1}) acquired lock on {2}", _local_n.Address, _lid, _target));
            BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug, 
              String.Format("{0}: Linker: ({1}) Trying TA: {2}", _local_n.Address, _lid, next_ta));
        }
#endif
        next_task = new EdgeWorker(_local_n, next_ta);
        next_task.FinishEvent += this.EdgeWorkerHandler;
      }
      catch(CTLockException) {
        /*
         * If we cannot get a lock on the address in SetTarget()
         * we wait and and try again
         */
#if LINK_DEBUG
        if (BU.ProtocolLog.LinkDebug.Enabled) {
          BU.ProtocolLog.Write(BU.ProtocolLog.LinkDebug, 
            String.Format("{0}: Linker ({1}) failed to lock {2}", _local_n.Address, _lid, _target));
        }
#endif
        next_task = GetRestartState(next_ta);
      }
      catch(ConnectionExistsException) {
        //We already have a connection to the target
      }
      catch(Exception) {

      }
      return next_task;
    }

    /**
     * If we hold a lock permanently, we may prevent connections
     * to a given address
     * This is called to release a lock from the ConnectionTable.
     * If the lock is not held, it is still safe to call this method
     * (in which case nothing happens).
     */
    protected void Unlock() {
      LocalNode.LockMgr.Unlock( _contype, this );
    }
  }
}
