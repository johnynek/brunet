/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2005 - 2008  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
    protected readonly WriteOnce<Connection> _con;
    /**
     * If this state machine creates a Connection, this is it.
     * Otherwise its null
     *
     * This is only set in the Finish method.
     */
    public Connection Connection {
      get {
        Connection res;
        //Set to null if fails:
        _con.TryGet(out res);
        return res;
      }
    }
    
    protected readonly WriteOnce<LinkMessage> _lm_reply;
    /**
     * If we get a sensible reply from a remote node, this is it.
     * If we get some bad reply, we don't keep it.
     *
     * This is the only "set once" variable that is not set in the Finish
     * method.
     */
    public LinkMessage LinkMessageReply {
      get {
        LinkMessage res;
        _lm_reply.TryGet(out res);
        return res;
      }
    }
    
    protected volatile LinkProtocolState.Result _result;
    /**
     * When this object is finished, this tells the Linker
     * what to do next
     *
     * This is only set in the Finish method.
     */
    public LinkProtocolState.Result MyResult {
      get { return _result; }
    }
    protected readonly WriteOnce<Exception> _x;
    /**
     * If we catch some exception, we store it here, and call Finish
     */
    public Exception CaughtException {
      get {
        Exception x;
        _x.TryGet(out x);
        return x;
      }
    }
    
    protected Address _target_lock;

    public Object TargetLock {
      get { return _target_lock; }
      set { _target_lock = (Address) value; }
    }

    //====================
    // These variables never change after the constructor
    //====================
    protected readonly Node _node;
    protected readonly string _contype;
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
      _target_lock = null;
      _lm_reply = new WriteOnce<LinkMessage>();
      _x = new WriteOnce<Exception>();
      _con = new WriteOnce<Connection>();
      _ta = ta;
      _is_finished = 0;
      //Setup the edge:
      _e = e;
      _result = Result.None;
    }

    //Make sure we are unlocked.
    ~LinkProtocolState() {
      if( _target_lock != null ) {
        if(ProtocolLog.LinkDebug.Enabled) {
              ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
                "Lock released by destructor"));
        }
      }
      /* In .NET, there is an obervable bug, where there exists a LPS that
       * has no origin and all member properties are null.  This saves the
       * garbage collector from throwing an exception.
       */
      if(_node != null) {
        Unlock();
      }
    }

    /**
     * Note that a LinkProtocolState only gets a lock *AFTER* it has
     * received a LinkMessageReply.  Prior to that, the Linker that
     * created it holds the lock (if the _linker.Target is not null).
     *
     * So, given that we are being asked to transfer a lock, we must
     * have already gotten our LinkMessageReply set, or we wouldn't
     * hold the lock in the first place.
     *
     * So, we only transfer locks to other Linkers when we are finished
     * since us holding a lock means we have already head some
     * communication from the other side.
     * 
     * Since the CT.Lock and CT.Unlock methods can't be called when this
     * is being called, we know that _target_lock won't change during
     * this method.
     */
    public bool AllowLockTransfer(Address a, string contype, ILinkLocker l)
    {
      bool hold_lock = (a.Equals( _target_lock ) && contype == _contype);
      if( false == hold_lock ) {
        //This is a bug.
        throw new Exception(String.Format("We don't hold the lock: {0}", a));
      }
      if( (l is Linker) && IsFinished ) {
        return true;
      }
      return false;
    }
    /**
     * There are only four ways we can get here:
     * 
     * 1) We got some exception in Start and never made the first request
     * 2) There was some problem in LinkCloseHandler
     * 3) We either got a response or had a problem in StatusCloseHandler
     * 4) The Edge closed, and the CloseHandler was called.
     * 
     * The only possibility for a race is between the CloseHandler and
     * the others.
     *
     * When this state machine reaches an end point, it calls this method,
     * which fires the FinishEvent
     */
    protected void Finish(Result res) {
      /*
       * No matter what, we are done here:
       */
      if(ProtocolLog.LinkDebug.Enabled) {
        string message;
        Exception x;
        if (_x.TryGet(out x) ) {
          message = String.Format(
                      "LPS: {0} finished: {2}, with exception: {1}",
                      _node.Address, x, res);
        }
        else {
          message = String.Format("LPS: {0} finished: {1}",
                                  _node.Address, res);
        }
        ProtocolLog.Write(ProtocolLog.LinkDebug, message);
      }

      int already_finished = Interlocked.Exchange(ref _is_finished, 1);
      if(already_finished == 1) {
        //We already got here.
        //This is a barrier.  Only one Finish call will make
        //it past this point.  Only two could happen in a race:
        //Edge closing or some other failure/success.
        return;
      }
      //We don't care about close event's anymore
      _e.CloseEvent -= this.CloseHandler;

      //Set the result:
      _result = res;
      
      try {
        //Check to see if we need to close the edge
        if( _con.IsSet == false ) {
          /*
           * We didn't get a complete connection,
           * but we may have heard some response.  If so
           * close the edge gracefully.
           */
          if (LinkMessageReply != null) {
            //Let's be nice:
            _node.GracefullyClose(_e, "From LPS, did not complete a connection.");
          }
          else {
            /*
             * We never heard from the other side, so we will assume that further
             * packets will only waste bandwidth
             */
            _e.Close();
          }
          if(ProtocolLog.LinkDebug.Enabled) {
            ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
              "LPS: {0} got no connection", _node.Address));
          }
        }
        else {
          if(ProtocolLog.LinkDebug.Enabled) {
            ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
              "LPS: {0} got connection: {1}", _node.Address, _con.Value));
          }
        }
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
     * Unlock any lock which is held by this state
     */
    public void Unlock() {
      ConnectionTable tab = _node.ConnectionTable;
      tab.Unlock( _contype, this );
    }

    protected LinkMessage MakeLM() {
      NodeInfo my_info = NodeInfo.CreateInstance( _node.Address, _e.LocalTA );
      NodeInfo remote_info = NodeInfo.CreateInstance( _linker.Target, _e.RemoteTA );
      System.Collections.Specialized.StringDictionary attrs
          = new System.Collections.Specialized.StringDictionary();
      attrs["type"] = String.Intern( _contype );
      attrs["realm"] = String.Intern( _node.Realm );
      return new LinkMessage( attrs, my_info, remote_info , _linker.Token);
    }

    public override void Start() {
      //Make sure the Node is listening to this node
      try {
        //This will throw an exception if _e is already closed:
        _e.CloseEvent += this.CloseHandler; 
        //_e must not be closed, let's start listening to it:
        _e.Subscribe(_node, _e);
        /* Make the call */
        Channel results = new Channel();
        results.CloseAfterEnqueue();
        results.CloseEvent += this.LinkCloseHandler;
        RpcManager rpc = RpcManager.GetInstance(_node);
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
      /* Check that the everything matches up 
       * Make sure the link message is Kosher.
       * This are critical errors.  This Link fails if these occur
       */
      if( lm.Local == null) {
        throw new LinkException("Bad response");
      }
      if( _node.Address.Equals( lm.Local.Address ) ) {
        //Somehow, we got a response from someone claiming to be us.
        throw new LinkException("Got a LinkMessage response from our address");
      }
      if( lm.ConTypeString != _contype ) {
        throw new LinkException("Link type mismatch: " + _contype + " != " + lm.ConTypeString );
      }
      if( !lm.Attributes["realm"].Equals( _node.Realm ) ) {
        throw new LinkException("Realm mismatch: " +
                                _node.Realm + " != " + lm.Attributes["realm"] );
      }
      if( lm.Local.Address == null ) {
        throw new LinkException("LinkMessage response has null Address");
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
      /*
       * Okay, this lm looks good, we'll accept it.  This can only be done
       * once, and once it happens a future attempt will throw an exception
       */
      _lm_reply.Value = lm;
      
      ConnectionTable tab = _node.ConnectionTable;
      /*
       * This throws an exception if:
       * 0) we can't get the lock.
       * 1) we already have set _target_lock to something else
       */
      tab.Lock( lm.Local.Address, _contype, this );
    }

    /**
     * When we get a response to the sys:link method, this handled
     * is called
     */
    protected void LinkCloseHandler(object q, EventArgs args) {
      try {
        Channel resq = (Channel)q;
        //If the Channel is empty this will throw an exception:
        RpcResult res = (RpcResult)resq.Dequeue();
        /* Here's the LinkMessage response */
        LinkMessage lm = new LinkMessage( (IDictionary)res.Result );
        /**
         * This will set our LinkMessageReply variable.  It can
         * only be set once, so all future sets will fail.  It
         * will also make sure we have the lock on the target.
         * If we don't, that will throw an exception
         */
        SetAndCheckLinkReply(lm);
        //If we got here, we have our response and the Lock on _target_address
        StatusMessage sm = _node.GetStatus(_contype, lm.Local.Address);
        /* Make the call */
        Channel results = new Channel();
        results.CloseAfterEnqueue();
        results.CloseEvent += this.StatusCloseHandler;
        RpcManager rpc = RpcManager.GetInstance(_node);
	if (ProtocolLog.LinkDebug.Enabled) {
	      ProtocolLog.Write(ProtocolLog.LinkDebug, 
                String.Format(
                  "LPS target: {0} Invoking GetStatus() over edge: {1}",
                  _linker.Target, _e));
	}
        /*
         * This could throw an exception if the Edge is closed
         */
        rpc.Invoke(_e, results, "sys:link.GetStatus", sm.ToDictionary() );
      }
      catch(AdrException x) {
        /*
         * This happens when the RPC call has some kind of issue,
         * first we check for common error conditions:
         */
        _x.Value = x;
        Finish( GetResultForErrorCode(x.Code) );
      }
      catch(ConnectionExistsException x) {
        /* We already have a connection */
        _x.Value = x;
        Finish( Result.ProtocolError );
      }
      catch(CTLockException x) {
        //This is thrown when ConnectionTable cannot lock.  Lets try again:
        _x.Value = x;
        Finish( Result.RetryThisTA );
      }
      catch(LinkException x) {
        _x.Value = x;
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
        _x.Value = x;
        Finish( Result.RetryThisTA );
      } 
    }
    
    /**
     * When we're here, we have the status message
     */
    protected void StatusCloseHandler(object q, EventArgs args) {
      try {
        Channel resq = (Channel)q;
        //If we got no result
        RpcResult res = (RpcResult)resq.Dequeue();
        StatusMessage sm = new StatusMessage((IDictionary)res.Result);
        if(_node.EdgeVerifyMethod != null) {
          if(!_node.EdgeVerifyMethod(_node, _e, LinkMessageReply.Local.Address)) {
            throw new Exception("Edge verification failed!");
          }
        }

        Connection c = new Connection(_e, LinkMessageReply.Local.Address,
                                        _contype, sm, LinkMessageReply);
        _node.ConnectionTable.Add(c);
        _con.Value = c;
        Finish(Result.Success);
      }
      catch(InvalidOperationException) {
         /*
          * This is unexpected. 
          */
        string message = String.Format(
                           "No StatusMessage returned from open({1}) Edge: {0}",
                           _e, !_e.IsClosed);
        if(ProtocolLog.LinkDebug.Enabled) {
          ProtocolLog.Write(ProtocolLog.LinkDebug, message);
        }
        /*
         * We got a link message from this guy, but not a status response,
         * so let's try this TA again.
         */
        Finish(Result.RetryThisTA);
      }
      catch(Exception x) {
        /*
         * Clearly we got some response from this edge, but something
         * unexpected happened.  Let's try it this edge again if we
         * can
         */
        if(ProtocolLog.LinkDebug.Enabled) {
          ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
            "LPS.StatusResultHandler Exception: {0}", x));
        }
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
