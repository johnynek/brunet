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

using System;
using System.Threading;
using System.Collections;


namespace Brunet 
{

  /**
   * Is a state machine to handle the link protocol for
   * one particular attempt, on one particular Edge, which
   * was created using one TransportAddress
   */
  public class LinkProtocolState : TaskWorker, ILinkLocker {
    
    //====================
    // Write Once Variables.
    //====================

    protected int _is_finished;
    public override bool IsFinished {
      get {
        return (Thread.VolatileRead(ref _is_finished) == 1);
      }
    }
    protected object _con;
    /**
     * If this state machine creates a Connection, this is it.
     * Otherwise its null
     */
    public Connection Connection {
      get { return (Connection)Thread.VolatileRead(ref _con); }
      set {
        object old_v = Interlocked.CompareExchange(ref _con, value, null);
        if( old_v != null ) {
          //We didn't really exchange
          throw new LinkException(String.Format("Connection already set to: {0}", old_v));
        }
      }
    }
    
    protected object _lm_reply;
    public LinkMessage LinkMessageReply {
      get { return (LinkMessage)Thread.VolatileRead(ref _lm_reply); }
      set {
        object old_v = Interlocked.CompareExchange(ref _lm_reply, value, null);
        if( old_v != null ) {
          //We didn't really exchange
          throw new LinkException(
                        String.Format("LinkMessageReply already set to: {0}", old_v));
        }
      }
    }
    
    /*
     * We can't use Interlocked on enums, so we have to get the lock
     * to change this value.
     */
    protected volatile LinkProtocolState.Result _result;
    /**
     * When this object is finished, this tells the Linker
     * what to do next
     */
    public LinkProtocolState.Result MyResult {
      get { return _result; }
      set {
        lock( _sync ) {
          if( _result != Result.None) {
            throw new LinkException(
                        String.Format("LinkProtocolState.Result already set to: {0}", _result));

          }
          _result = value;
        }
      }
    }
    volatile protected Exception _x;
    /**
     * If we catch some exception, we store it here, and call Finish
     */
    public Exception CaughtException { get { return _x; } }
    
    protected Address _target_lock;

    //====================
    // These variables never change after the constructor
    //====================
    protected readonly Node _node;
    protected readonly string _contype;
    protected readonly object _sync;
    protected readonly Edge _e;
    protected readonly Linker _linker;
    public Linker Linker { get { return _linker; } }
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

    public LinkProtocolState(Linker l, TransportAddress ta, Edge e) {
      _linker = l;
      _node = l.LocalNode;
      _contype = l.ConType;
      _sync = new object();
      _target_lock = null;
      _lm_reply = null;
      _ta = ta;
      _is_finished = 0;
      //Setup the edge:
      _e = e;
      _result = Result.None;
      try {
        e.CloseEvent += this.CloseHandler;
      }
      catch {
        CloseHandler(e, null);
        throw;
      }
    }

    //Make sure we are unlocked.
    ~LinkProtocolState() {
      try {
        lock( _sync ) {
          if( _target_lock != null ) {
            if(ProtocolLog.LinkDebug.Enabled)
              ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
                "Lock released by destructor"));
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
      bool entered = System.Threading.Monitor.TryEnter(_sync); //like a lock
      if ( false == entered ) {
	if (ProtocolLog.LinkDebug.Enabled) {
	  ProtocolLog.Write(ProtocolLog.LinkDebug,
          String.Format(
          "Cannot acquire LPS lock for transfer (potential deadlock)."));
	}
        return false;
      }
      else {
        // We have the lock now
        bool allow = false;
        try {
          if( l is Linker ) {
            //We will allow it if we are done:
            if( IsFinished ) {
              allow = true;
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
            if( ( LinkMessageReply == null )
                && a.Equals( _target_lock )
                && contype == _contype 
                && ( a.CompareTo( _node.Address ) > 0) )
            {
                allow = true;
            }
          }
          if( allow ) {
            _target_lock = null;
          }
        } finally {
          //We have to do a try .. finally here otherwise an
          //exception in the above could cause us to never release _sync.
          System.Threading.Monitor.Exit(_sync);
        }
        return allow;
      }
    }
    /**
     * When this state machine reaches an end point, it calls this method,
     * which fires the FinishEvent
     */
    protected void Finish(Result res) {
      /*
       * No matter what, we are done here:
       */
      if(ProtocolLog.LinkDebug.Enabled)
        ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
          "LPS: {0} finished: {2}, with exception: {1}", _node.Address, _x, res));

      Edge to_close = null;
      bool close_gracefully = false;
      lock( _sync ) {
        if( IsFinished ) {
          /*
           * We could call Finish and then the Edge could be closed.
           * So, we can't guarantee that Finish is not called twice,
           * just ignore future calls
           */
          return;
        }
        SetIsFinished();
        MyResult = res;
        _e.CloseEvent -= this.CloseHandler;
        if( this.Connection == null ) {
          to_close = _e;
          //Close gracefully if we heard something from the other node
          close_gracefully = (LinkMessageReply != null);
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
      if(ProtocolLog.LinkDebug.Enabled)
        ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
          "LPS: {0} got connection: {1}", _node.Address, _con));
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
          if(ProtocolLog.LinkDebug.Enabled)
            ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
              "Already connected: {0}, {1}", _contype, _target_lock));
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
        if(ProtocolLog.LinkDebug.Enabled)
          ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
            "LPS: from {0} target mismatch: {1}", _e, _target_lock));
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
        if(ProtocolLog.LinkDebug.Enabled)
          ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
            "Unrecognized error code: {0}", c));
      }
      return result;
    }
    /**
     * set the _is_finished variable if it is not yet true.
     * This method is atomic, and either succeeds or
     * @throws a LinkException if this method is called more than once.
     */
    protected void SetIsFinished() {
      int old_v = Interlocked.Exchange(ref _is_finished, 1);
      if(old_v != 0) {
        throw new LinkException("IsFinished already set to true");
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
      Channel results = new Channel();
      results.CloseAfterEnqueue();
      results.CloseEvent += this.LinkCloseHandler;
      RpcManager rpc = RpcManager.GetInstance(_node);
      try {
	if(ProtocolLog.LinkDebug.Enabled) {
	  ProtocolLog.Write(ProtocolLog.LinkDebug, 
			    String.Format("LPS target: {0} Invoking Start() over edge: {1}", _linker.Target, _e));
	}
        rpc.Invoke(_e, results, "sys:link.Start", MakeLM().ToDictionary() );
      }
      catch (Exception e) {
        //The Edge must have closed, move on to the next TA
	if(ProtocolLog.LinkDebug.Enabled) {
	  ProtocolLog.Write(ProtocolLog.LinkDebug, 
			    String.Format("LPS target: {0} Start() over edge: {1}, hit exception: {2}", 
					  _linker.Target, _e, e));
	}
        Finish(Result.MoveToNextTA);
      }
    }
    
    /**
     * Checks that everything matches up and the protocol
     * can continue, throws and exception if anything is
     * not okay
     */
    protected void SetAndCheckLinkReply(LinkMessage lm) {
      //At this point, we cannot be pre-empted.
      LinkMessageReply = lm;
      
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
    protected void LinkCloseHandler(object q, EventArgs args) {
      try {
        Channel resq = (Channel)q;
        lock( _sync ) {
          resq.CloseEvent -= this.LinkCloseHandler;
          if( IsFinished ) { return; }

          if( resq.Count > 0 ) {
            RpcResult res = (RpcResult)resq.Dequeue();
            /* Here's the LinkMessage response */
            LinkMessage lm = new LinkMessage( (IDictionary)res.Result );
            SetAndCheckLinkReply(lm);
  	
            /* Make our status message */
            StatusMessage sm = _node.GetStatus(_contype, lm.Local.Address);
            /* Make the call */
            Channel results = new Channel();
            results.CloseAfterEnqueue();
            results.CloseEvent += this.StatusCloseHandler;
            RpcManager rpc = RpcManager.GetInstance(_node);
	    if (ProtocolLog.LinkDebug.Enabled) {
	      ProtocolLog.Write(ProtocolLog.LinkDebug, 
				String.Format("LPS target: {0} Invoking GetStatus() over edge: {1}", _linker.Target, _e));
	    }
            rpc.Invoke(_e, results, "sys:link.GetStatus", sm.ToDictionary() );
          }
          else {
            //This causes us to move to the next TA
            throw new InvalidOperationException();
          }
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
      catch(InvalidOperationException) {
        //The queue never got anything
        Finish(Result.MoveToNextTA);
      }
      catch(EdgeException) {
        //The Edge is goofy, let's move on:
        Finish(Result.MoveToNextTA);
      }
      catch(Exception x) {
        //The protocol was not followed correctly by the other node, fail
        _x = x;
        Finish( Result.RetryThisTA );
      } 
    }
    
    /**
     * When we're here, we have the status message
     */
    protected void StatusCloseHandler(object q, EventArgs args) {
      Result r;
      try {
        Channel resq = (Channel)q;
        resq.CloseEvent -= this.StatusCloseHandler;
        lock( _sync ) {
          if( IsFinished ) { return; }
          if( resq.Count > 0 ) {
            RpcResult res = (RpcResult)resq.Dequeue();
            StatusMessage sm = new StatusMessage((IDictionary)res.Result);
            Connection = new Connection(_e, LinkMessageReply.Local.Address,
                                        _contype, sm, LinkMessageReply);
            r = Result.Success;
          }
          else {
            string message;
            if( _e != null ) {
              /*
               * This is unexpected.  If the edge is closed, or already nulled
               * out, nothing strange has happened.
               */

               message = String.Format(
                           "No StatusMessage returned from open({1}) Edge: {0}",
                           _e, !_e.IsClosed);
            }
            else {
              message = "No StatusMessage returned, edge is now null";
            }
            throw new Exception(message);
          }
        }
        Finish(r);
      }
      catch(Exception x) {
        /*
         * Clearly we got some response from this edge, but something
         * unexpected happened.  Let's try it this edge again if we
         * can
         */
        if(ProtocolLog.LinkDebug.Enabled)
          ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
            "LPS.StatusResultHandler Exception: {0}", x));
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
