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

//#define LINK_DEBUG

using System;
using System.Collections;


namespace Brunet 
{

  /**
   * Is a state machine to handle the link protocol for
   * one particular attempt, on one particular Edge, which
   * was created using one TransportAddress
   */
  public class LinkProtocolState : TaskWorker, ILinkLocker {
    /**
     * When this state machine reaches the end, it fires this event
     */
    protected bool _is_finished;
    public override bool IsFinished {
      get {
        lock( _sync ) { return _is_finished; }
      }
    }
    protected Connection _con;
    /**
     * If this state machine creates a Connection, this is it.
     * Otherwise its null
     */
    public Connection Connection { get { lock( _sync) { return _con; } } }
    protected readonly Linker _linker;
    /**
     * The Linker that created this LinkProtocolState
     */
    public Linker Linker { get { return _linker; } }
    
    protected LinkMessage _lm_reply;
    public LinkMessage LinkMessageReply { get { lock( _sync ) { return _lm_reply; } }  }

    volatile protected Exception _x;
    /**
     * If we catch some exception, we store it here, and call Finish
     */
    public Exception CaughtException { get { return _x; } }

    protected readonly Node _node;
    protected readonly string _contype;
    protected Address _target_lock;
    protected object _sync;
    protected Edge _e;
    protected readonly TransportAddress _ta;
    public TransportAddress TA { get { return _ta; } }

    //This is an object that represents the task
    //we are working on.
    public override object Task {
      get { return _ta; }
    }
  
    public enum Result {
      ///Everything succeeded and we created a Connection
      Success,
      ///This TransportAddress or Edge did not work.
      MoveToNextTA,
      ///This TransportAddress may/should work if we try again
      RetryThisTA,
      ///Received some ErrorMessage from the other node (other than InProgress)
      ProtocolError,
      ///There was some Exception
      Exception,
      ///No result yet
      None
    }

    protected LinkProtocolState.Result _result;
    /**
     * When this object is finished, this tells the Linker
     * what to do next
     */
    public LinkProtocolState.Result MyResult { get { return _result; } }

    public LinkProtocolState(Linker l, TransportAddress ta, Edge e) {
      _linker = l;
      _node = l.LocalNode;
      _contype = l.ConType;
      _sync = new object();
      _target_lock = null;
      _lm_reply = null;
      _ta = ta;
      _is_finished = false;
      //Setup the edge:
      _e = e;
      _e.CloseEvent += this.CloseHandler;
    }

    //Make sure we are unlocked.
    ~LinkProtocolState() {
      try {
        lock( _sync ) {
          if( _target_lock != null ) {
            Console.Error.WriteLine("Lock released by destructor");
            Unlock();
          }
        }
      }
      catch{
        if( _target_lock != null) {
          Unlock();
        }
      }
    }

    ///We should allow it as long as it is not another LinkProtocolState:
    public bool AllowLockTransfer(Address a, string contype, ILinkLocker l)
    {
      bool allow = false;
      lock( _sync ) {
        if( l is Linker ) {
          //We will allow it if we are done:
          if( _is_finished ) {
            allow = true;
            _target_lock = null;
          }
        }
        else if ( false == (l is LinkProtocolState) ) {
        /**
         * We only allow a lock transfer in the following case:
         * 0) We have not sent the StatusRequest yet.
         * 1) We are not transfering to another LinkProtocolState
         * 2) The lock matches the lock we hold
         * 3) The address we are locking is greater than our own address
         */
          if( ( _lm_reply == null )
              && a.Equals( _target_lock )
              && contype == _contype 
              && ( a.CompareTo( _node.Address ) > 0) )
          {
              _target_lock = null; 
              allow = true;
          }
        }
        if( allow ) {
          _target_lock = null;
        }
      }
      return allow;
    }
    /**
     * When this state machine reaches an end point, it calls this method,
     * which fires the FinishEvent
     */
    protected void Finish(Result res) {
      /*
       * No matter what, we are done here:
       */
#if LINK_DEBUG
      Console.Error.WriteLine("LPS: {0} finished: {2}, with exception: {1}", _node.Address, _x, res);
#endif
      Edge to_close = null;
      bool close_gracefully = false;
      lock( _sync ) {
        if( _is_finished ) {
          /*
           * We could call Finish and then the Edge could be closed.
           * So, we can't guarantee that Finish is not called twice,
           * just ignore future calls
           */
          return;
        }
        _is_finished = true;
        _result = res;
        _e.CloseEvent -= this.CloseHandler;
        if( this.Connection == null ) {
          to_close = _e;
          _e = null;
          //Close gracefully if we heard something from the other node
          close_gracefully = (_lm_reply != null);
        }
      }
      /*
       * In some cases, we close the edge:
       */
      if( to_close != null ) {
        if( close_gracefully ) {
        /*
         * We close the edge if we did not get a Connection AND we received
         * some response from this edge
         */
          _node.GracefullyClose(to_close);
        }
        else {
          /*
           * We never heard from the other side, so we will assume that further
           * packets will only waste bandwidth
           */
          to_close.Close();
        }
      }
      else {
        //We got a connection, don't close it!
      }
#if LINK_DEBUG
      Console.Error.WriteLine("LPS: {0} got connection: {1}", _node.Address, _con);
#endif
      try {
        //This could throw an exception, but make sure we unlock if it does.
        FireFinished();
      }
      finally {
        /**
         * We have to make sure the lock is eventually released:
         */
        this.Unlock();
      }
    }

    /**
     * When the other node gives us an error code, this
     * method tells us what to do based on that.
     * We always finish, but our result is not
     * fixed
     */
    protected Result GetResultForErrorCode(int c) {
      Result result = Result.ProtocolError;
      if( c == (int)ErrorMessage.ErrorCode.InProgress ) {
        result = Result.RetryThisTA;
      }
      else if ( c == (int)ErrorMessage.ErrorCode.AlreadyConnected ) {
        /*
         * The other side thinks we are already connected.  This is
         * odd, let's see if we agree
         */
        Address target = _linker.Target;
        ConnectionTable tab = _node.ConnectionTable;
        if( target == null ) {
          //This can happen with leaf connections.  In this case, we
          //should move on to another TA.
          result = Result.MoveToNextTA;
        }
        else if( tab.Contains( Connection.StringToMainType( _contype ), target) ) {
          //This shouldn't happen
          result = Result.ProtocolError;
          Console.Error.WriteLine("LPS: already connected: {0}, {1}", _contype, _target_lock);
        }
        else {
          //The other guy thinks we are connected, but we disagree,
          //let's retry.  This can happen if we get disconnected
          //and reconnect, but the other node hasn't realized we
          //are disconnected.
          result = Result.RetryThisTA;
        }
      }
      else if ( c == (int)ErrorMessage.ErrorCode.TargetMismatch ) {
        /*
         * This could happen in some NAT cases, or perhaps due to
         * some other as of yet undiagnosed bug.
         *
         * Move to the next TA since this TA definitely connects to
         * the wrong guy.
         */
        Console.Error.WriteLine("LPS: from {0} target mismatch: {1}", _e, _target_lock);
        result = Result.MoveToNextTA;
      }
      else if ( c == (int)ErrorMessage.ErrorCode.ConnectToSelf ) {
        /*
         * Somehow we connected to ourself, this TA is no good.
         */
        result = Result.MoveToNextTA;
      }
      else if ( c == (int)ErrorMessage.ErrorCode.Disconnecting ) {
        /* The other node is going offline */
        if( _linker.Target == null ) {
          result = Result.MoveToNextTA;
        } else {
          result = Result.ProtocolError; //Give up now
        }
      }
      else {
        Console.Error.WriteLine("Unrecognized error code: {0}", c);
      }
      return result;
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
     * @throws ConnectionExistsException if we already have a connection
     * @throws CTLockException if we cannot get the lock
     */
    protected void SetTarget(Address target)
    {
      if ( target == null )
        return;
 
      lock(_sync) {
        ConnectionTable tab = _node.ConnectionTable;
        if( _target_lock != null ) {
          //This is the case where _target_lock has been set once
          if( ! target.Equals( _target_lock ) ) {
            throw new LinkException("Target lock already set to a different address");
          }
        }
        else if( target.Equals( _node.Address ) )
          throw new LinkException("cannot connect to self");
        else {
          //Lock throws an Exception if it cannot get the lock
          tab.Lock( target, _contype, this );
          _target_lock = target;
        }
      }
    }

    /**
     * Unlock any lock which is held by this state
     */
    public void Unlock() {
      lock( _sync ) {
        ConnectionTable tab = _node.ConnectionTable;
        tab.Unlock( _target_lock, _contype, this );
        _target_lock = null;
      }
    }
    
    protected LinkMessage MakeLM() {
      NodeInfo my_info = NodeInfo.CreateInstance( _node.Address, _e.LocalTA );
      NodeInfo remote_info = NodeInfo.CreateInstance( _linker.Target, _e.RemoteTA );
      System.Collections.Specialized.StringDictionary attrs
          = new System.Collections.Specialized.StringDictionary();
      attrs["type"] = String.Intern( _contype );
      attrs["realm"] = String.Intern( _node.Realm );
      return new LinkMessage( attrs, my_info, remote_info );
    }

    public override void Start() {
      //Make sure the Node is listening to this node
      _e.Subscribe(_node, _e);
      
      /* Make the call */
      BlockingQueue results = new BlockingQueue();
      results.EnqueueEvent += this.LinkResultHandler;
      results.CloseEvent += this.LinkCloseHandler;
      RpcManager rpc = RpcManager.GetInstance(_node);
      rpc.Invoke(_e, results, "sys:link.Start", MakeLM().ToDictionary() );
    }
    
    /**
     * Checks that everything matches up and the protocol
     * can continue, throws and exception if anything is
     * not okay
     */
    protected void SetAndCheckLinkReply(LinkMessage lm) {
      //At this point, we cannot be pre-empted.
      _lm_reply = lm;
      
      /* Check that the everything matches up 
       * Make sure the link message is Kosher.
       * This are critical errors.  This Link fails if these occur
       */
      if( lm.ConTypeString != _contype ) {
        throw new LinkException("Link type mismatch: " + _contype + " != " + lm.ConTypeString );
      }
      if( !lm.Attributes["realm"].Equals( _node.Realm ) ) {
        throw new LinkException("Realm mismatch: " +
                                _node.Realm + " != " + lm.Attributes["realm"] );
      }
      if( (_linker.Target != null) && (!lm.Local.Address.Equals( _linker.Target )) ) {
        /*
         * This is super goofy.  Somehow we got a response from some node
         * we didn't mean to connect to.
         * This can happen in some cases with NATs since nodes behind NATs are
         * guessing which ports are correct, their guess may be incorrect, and
         * the NAT may send the packet to a different node.
         * In this case, we have a critical error, this TA is not correct, we
         * must move on to the next TA.
         */
        throw new LinkException(String.Format("Target mismatch: {0} != {1}",
                                              _linker.Target, lm.Local.Address), true, null );
      }
      
      //Make sure we have the lock on this address, this could 
      //throw an exception halting this link attempt.
      SetTarget( lm.Local.Address );
    }

    /**
     * When we get a response to the sys:link method, this handled
     * is called
     */
    protected void LinkResultHandler(object q, EventArgs args) {
      try {
        BlockingQueue resq = (BlockingQueue)q;
        lock( _sync ) {
          resq.EnqueueEvent -= this.LinkResultHandler;
          resq.CloseEvent -= this.LinkCloseHandler;
          if( _is_finished ) { return; }
          RpcResult res = (RpcResult)resq.Dequeue();
          //Stop listening for results:
          resq.Close();
  
          /* Here's the LinkMessage response */
          LinkMessage lm = new LinkMessage( (IDictionary)res.Result );
          SetAndCheckLinkReply(lm);
  	
          /* Make our status message */
          StatusMessage sm = _node.GetStatus(_contype, lm.Local.Address);
         
          /* Make the call */
          BlockingQueue results = new BlockingQueue();
          results.EnqueueEvent += this.StatusResultHandler;
          results.CloseEvent += this.StatusCloseHandler;
          RpcManager rpc = RpcManager.GetInstance(_node);
          rpc.Invoke(_e, results, "sys:link.GetStatus", sm.ToDictionary() );
        }
      }
      catch(AdrException x) {
        /*
         * This happens when the RPC call has some kind of issue,
         * first we check for common error conditions:
         */
        _x = x;
        Finish( GetResultForErrorCode(x.Code) );
      }
      catch(ConnectionExistsException x) {
        /* We already have a connection */
        _x = x;
        Finish( Result.ProtocolError );
      }
      catch(CTLockException x) {
        //This is thrown when ConnectionTable cannot lock.  Lets try again:
        _x = x;
        Finish( Result.RetryThisTA );
      }
      catch(LinkException x) {
        _x = x;
        if( x.IsCritical ) { Finish( Result.MoveToNextTA ); }
        else { Finish( Result.RetryThisTA ); }
      }
      catch(Exception x) {
        //The protocol was not followed correctly by the other node, fail
        _x = x;
        Finish( Result.RetryThisTA );
      }
    }
    
    /**
     * If the RPC call never gets a result, eventually the
     * BlockingQueue is closed, in that case, this handler is
     * invoked.
     */
    protected void LinkCloseHandler(object q, EventArgs args) {
      BlockingQueue resq = (BlockingQueue)q;
      lock( _sync ) {
        resq.CloseEvent -= this.LinkCloseHandler;
        resq.EnqueueEvent -= this.LinkResultHandler;
        if( _is_finished ) { return; }
      }
      /*
       * I guess this edge is no good
       */
      Finish(Result.MoveToNextTA);
    }
    /**
     * When we're here, we have the status message
     */
    protected void StatusResultHandler(object q, EventArgs args) {
      try {
        BlockingQueue resq = (BlockingQueue)q;
        lock( _sync ) {
          resq.CloseEvent -= this.StatusCloseHandler;
          resq.EnqueueEvent -= this.StatusResultHandler;
          if( _is_finished ) { return; }
          RpcResult res = (RpcResult)resq.Dequeue();
          resq.Close(); //Close the queue
          StatusMessage sm = new StatusMessage((IDictionary)res.Result);
          _con = new Connection(_e, _lm_reply.Local.Address, _contype, sm, _lm_reply);
        }
        Finish(Result.Success);
      }
      catch(Exception x) {
        /*
         * Clearly we got some response from this edge, but something
         * unexpected happened.  Let's try it this edge again if we
         * can
         */
        Console.Error.WriteLine("LPS.StatusResultHandler Exception: {0}", x);
        Finish(Result.RetryThisTA);
      }
    }
    /**
     * If the RPC call never gets a result, eventually the
     * BlockingQueue is closed, in that case, this handler is
     * invoked.
     */
    protected void StatusCloseHandler(object q, EventArgs args) {
      BlockingQueue resq = (BlockingQueue)q;
      bool fire_finish = false;
      lock( _sync ) {
        resq.CloseEvent -= this.StatusCloseHandler;
        resq.EnqueueEvent -= this.StatusResultHandler;
        if( !_is_finished ) {
          fire_finish = true; 
        }
      }
      /*
       * We must have heard something, maybe we should retry and see
       * if we can hear something else soon.
       */
      if( fire_finish ) {
        Finish(Result.RetryThisTA);
      }
    }

    /**
     * This only gets called if the Edge closes unexpectedly.  If the
     * Edge closes normally, we would have already stopped listening
     * for CloseEvents.  If the Edge closes unexpectedly, we MoveToNextTA
     * to signal that this is not a good candidate to retry.
     */
    protected void CloseHandler(object sender, EventArgs args) {
      Finish(Result.MoveToNextTA);
    }
  }
}
