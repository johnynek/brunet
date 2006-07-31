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
  public class LinkProtocolState : ILinkLocker, IPacketHandler {
   
    /**
     * When this state machine reaches the end, it fires this event
     */
    public event EventHandler FinishEvent;
    protected bool _is_finished;
    protected Packet _last_r_packet;
    public Packet LastRPacket {
      get { return _last_r_packet; }
    }
    protected DateTime _last_packet_datetime;
    /**
     * When was the last packet received or sent
     */
    public DateTime LastPacketDateTime {
      get { return _last_packet_datetime; }
    }
    protected ConnectionMessage _last_r_mes;
    protected bool _sent_status;
    
    protected ConnectionMessageParser _cmp;
    protected Connection _con;
    /**
     * If this state machine creates a Connection, this is it.
     * Otherwise its null
     */
    public Connection Connection { get { return _con; } }
    protected ConnectionMessage _last_s_mes;
    protected Linker _linker;
    /**
     * The Linker that created this LinkProtocolState
     */
    public Linker Linker { get { return _linker; } }
    protected Packet _last_s_packet;
    /**
     * The Packet we last sent
     */
    public Packet LastSPacket { get { return _last_s_packet; } }

    protected Exception _x;
    /**
     * If we catch some exception, we store it here, and call Finish
     */
    public Exception CaughtException { get { return _x; } }

    protected ErrorMessage _em;
    /**
     * If we receive an ErrorMessage from the other node, this is it.
     */
    public ErrorMessage EM { get { return _em; } }

    protected Node _node;
    protected string _contype;
    protected Address _target_lock;
    protected int _id;
    protected object _sync;
    protected Edge _e;
    protected TransportAddress _ta;
    public TransportAddress TA { get { return _ta; } }
  
    /**
     * How many time outs are allowed before assuming failure
     */
    protected static readonly int _MAX_TIMEOUTS = 3;
    protected int _timeouts;
    /**
     * The timeout is adaptive.  It goes up
     * by a factor of _TIMEOUT_FACTOR
     * after each timeout.  It starts at DEFAULT_TIMEOUT second.
     * Then _TIMEOUT_FACTOR * DEFAULT_TIMEOUT ...
     */
    protected static readonly int _TIMEOUT_FACTOR = 4;
    protected static readonly int DEFAULT_TIMEOUT = 1000;
    protected int _ms_timeout = DEFAULT_TIMEOUT;
    
    /**
     * Holds a TimeSpan representing the current timeout, which
     * is a function of how many previous timeouts there have been
     */
    protected TimeSpan _timeout;

    /*
     * The enumerator holds the state of the current attempt
     */
    protected IEnumerator _link_enumerator = null;

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
      Exception
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
      _id = 1;
      _target_lock = null;
      _sent_status = false;
      _timeout = new TimeSpan(0,0,0,0,_ms_timeout);
      _cmp = new ConnectionMessageParser();
      _ta = ta;
      _is_finished = false;
      //Setup the edge:
      _e = e;
      _e.SetCallback(Packet.ProtType.Connection, this);
      _e.CloseEvent += new EventHandler(this.CloseHandler);
    }

    //Make sure we are unlocked.
    ~LinkProtocolState() {
      if( _target_lock != null ) {
        Console.Error.WriteLine("Lock released by destructor");
        Unlock();
      }
    }

    ///We should allow it as long as it is not another LinkProtocolState:
    public bool AllowLockTransfer(Address a, string contype, ILinkLocker l)
    {
	bool allow = false;
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
	  lock( _sync ) {
            if( (!_sent_status )
                && a.Equals( _target_lock )
	        && contype == _contype 
		&& ( a.CompareTo( _node.Address ) > 0) ) {
              _target_lock = null; 
              allow = true;
	    }
	  }
	}
	return allow;
    }
    /**
     * When this state machine reaches an end point, it calls this method,
     * which fires the FinishEvent
     */
    protected void Finish() {
      /*
       * No matter what, we are done here:
       */
      lock( _sync ) {
        if( _is_finished ) { throw new Exception("Finished called twice!"); }
        _is_finished = true;
        _node.HeartBeatEvent -= new EventHandler(this.PacketResendCallback);
        _e.ClearCallback(Packet.ProtType.Connection);
        _e.CloseEvent -= new EventHandler(this.CloseHandler);
      }
      /*
       * In some cases, we close the edge:
       */
      if( this.Connection == null ) {
        if( LastRPacket != null ) {
        /*
         * We close the edge if we did not get a Connection AND we received
         * some response from this edge
         */
          CloseMessage close = new CloseMessage();
          close.Dir = ConnectionMessage.Direction.Request;
          _node.GracefullyClose(_e, close);
        }
        else {
          /*
           * We never heard from the other side, so we will assume that further
           * packets will only waste bandwidth
           */
          _e.Close();
        }
        _e = null;
      }
      else {
        //We got a connection, don't close it!
      }
      if( FinishEvent != null ) {
        FinishEvent(this, EventArgs.Empty);
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
     * Unlock any lock which is held by this state
     */
    public void Unlock() {
      lock( _sync ) {
        ConnectionTable tab = _node.ConnectionTable;
        tab.Unlock( _target_lock, _contype, this );
        _target_lock = null;
      }
    }
    
    protected IEnumerator GetEnumerator() {
      //Here we make and yield the LinkMessage request.
      NodeInfo my_info = new NodeInfo( _node.Address, _e.LocalTA );
      NodeInfo remote_info = new NodeInfo( null, _e.RemoteTA );
      System.Collections.Specialized.StringDictionary attrs
          = new System.Collections.Specialized.StringDictionary();
      attrs["type"] = _contype;
      attrs["realm"] = _node.Realm;
      _last_s_mes = new LinkMessage( attrs, my_info, remote_info );
      _last_s_mes.Dir = ConnectionMessage.Direction.Request;
      _last_s_mes.Id = _id++;
      _last_s_packet = _last_s_mes.ToPacket();
      _last_packet_datetime = DateTime.Now;
#if LINK_DEBUG
      Console.WriteLine("LinkState: To send link request: {0}; Length: {1}", _last_s_mes, _last_s_packet.Length);
#endif
      yield return _last_s_packet;
      //We should now have the response:
      /**
       * When we receive a LinkMessage response, we know
       * the other party is willing to link with us.
       * To acknowledge that we can complete the link,
       * we send them a StatusMessage request.
       *
       * The other node must not consider the Edge connected
       * until the StatusMessage request is received.
       */
      //Build the neighbor list:
	LinkMessage lm = (LinkMessage)_last_r_mes;
      /*
       * So, we must have our link message now:
	 * Make sure the link message is Kosher.
       * This are critical errors.  This Link fails if these occur
	 */
	if( lm.ConTypeString != _contype ) {
        throw new LinkException("Link type mismatch: "
                                + _contype + " != " + lm.ConTypeString );
	}
	if( lm.Attributes["realm"] != _node.Realm ) {
        throw new LinkException("Realm mismatch: " +
                                _node.Realm + " != " + lm.Attributes["realm"] );
	}
      //Make sure we have the lock on this address, this could 
      //throw an exception halting this link attempt.
      lock( _sync ) {
	  SetTarget( lm.Local.Address );
        //At this point, we cannot be pre-empted.
        _sent_status = true;
      }
	
      _last_s_mes = _node.GetStatus(lm.ConTypeString, lm.Local.Address);
      _last_s_mes.Id = _id++;
      _last_s_mes.Dir = ConnectionMessage.Direction.Request;
      _last_s_packet = _last_s_mes.ToPacket();
#if LINK_DEBUG
      Console.WriteLine("LinkState: To send status request: {0}; Length: {1}", _last_s_mes, _last_s_packet.Length);
#endif
      yield return _last_s_packet;
      StatusMessage sm = (StatusMessage)_last_r_mes;
      Connection con = new Connection(_e, lm.Local.Address, lm.ConTypeString,
				        sm, lm);
#if LINK_DEBUG
      Console.WriteLine("LinkState: New connection added. ");
#endif
      //Return the connection, now we are done!
      yield return con;
    }
    /**
     * When we get packets from the Edge, this is how we handle them
     */
    public void HandlePacket(Packet p, Edge edge)
    {
      Packet p_to_resend = null;
#if LINK_DEBUG
      Console.WriteLine("From: {0}\nPacket: {1}\n\n",edge, p);
#endif
      lock( _sync ) {
       if( _is_finished ) { return; }
       try {
        _em = SetLastRPacket(p);
        _timeouts = 0;
       }
       catch(Exception x) {
        /*
         * SetLastRPacket can throw an exception on expected packets
         * for now, we just ignore them and resend the most recently
         * sent packet:
         */
	  p_to_resend = LastSPacket;
       }
      }
      if( null != p_to_resend) {
        edge.Send( p_to_resend );
        return;
      }
      //If we get here, the packet must have been what we were expecting,
      //or an ErrorMessage
      bool finish = false;
      if( _em != null ) {
        //We got an error
	if( _em.Ec == ErrorMessage.ErrorCode.InProgress ) {
#if LINK_DEBUG
        Console.WriteLine("Linker ({0}) InProgress: from: {1}", _linker.Lid, edge);
#endif
          _result = Result.RetryThisTA;
          finish = true;
        }
        else {
          //We failed.
          _result = Result.ProtocolError;
          finish = true;
        }
      }
      else {
        try {
          //Advance one step in the protocol
	  object o = null;
	  lock( _sync ) {
            if( _link_enumerator.MoveNext() ) {
              o = _link_enumerator.Current;
	    }
            else {
              //We should never get here
            }
	  }
          if( o is Packet ) {
            //We need to send this packet
            edge.Send( (Packet)o );
          }
          else if ( o is Connection ) {
            //We have created our connection, Success!
            _con = (Connection)o;
            _result = Result.Success;
            finish = true;
          }
        }
        catch(InvalidOperationException x) {
          //This is thrown when ConnectionTable cannot lock.  Lets try again:
#if LINK_DEBUG
        Console.WriteLine("Linker ({0}): Could not lock in HandlePacket", _linker.Lid);
#endif
          _x = x;
          _result = Result.RetryThisTA;
          finish = true;
        }
        catch(Exception x) {
          //The protocol was not followed correctly by the other node, fail
          _x = x;
          _result = Result.RetryThisTA;
          finish = true;
        }
      }
      if( finish ) {
        Finish();
      }
    }

    public void Start() {
      _link_enumerator = GetEnumerator();
      _link_enumerator.MoveNext(); //Move the protocol forward:
      Packet p = (Packet)_link_enumerator.Current;
      _e.Send(p);
      //Register the call back:
      _node.HeartBeatEvent += new EventHandler(this.PacketResendCallback);
    }
  
    /**
     * This only gets called if the Edge closes unexpectedly.  If the
     * Edge closes normally, we would have already stopped listening
     * for CloseEvents.  If the Edge closes unexpectedly, we MoveToNextTA
     * to signal that this is not a good candidate to retry.
     */
    protected void CloseHandler(object sender, EventArgs args) {
      _result = Result.MoveToNextTA;
      Finish();
    }

    protected void PacketResendCallback(object node, EventArgs args)
    {
      bool finish = false;
      try {
        lock( _sync ) {
	  DateTime now = DateTime.Now;
          if( (! _is_finished) &&
              (now - LastPacketDateTime > _timeout ) ) {
            /*
             * It is time to check to see if we should resend, or move on
             */

            if (_timeouts < _MAX_TIMEOUTS) {
#if LINK_DEBUG
            Console.WriteLine("Linker ({0}) resending packet; attempt # {1}; length: {2}", _linker.Lid, _timeouts,                              LastSPacket.Length);
#endif
            _e.Send( LastSPacket );
            _last_packet_datetime = now;
	    //Increase the timeout by a factor of 4
	    _ms_timeout = _TIMEOUT_FACTOR * _ms_timeout;
            _timeout = new TimeSpan(0,0,0,0,_ms_timeout);
            _timeouts++;
            }
            else if( _timeouts >= _MAX_TIMEOUTS ) {
              //This edge is not working, we need to restart on a new edge.
#if LINK_DEBUG
              Console.WriteLine("Linker ({0}) giving up the TA, moving on to next", _linker.Lid);
#endif
              _result = Result.MoveToNextTA;
              finish = true;

            }
          }
        }
      }
      catch(Exception ex) {
        _x = ex;
        _result = Result.MoveToNextTA;
        finish = true;
      }
      if( finish ) {
        Finish();
      }
    }
    
    /**
     * Set the last packet and check for error conditions.
     * If the packet contains an ErrorMessage, that ErrorMessage
     * is returned, else null
     */
    protected ErrorMessage SetLastRPacket(Packet p) {
        ConnectionPacket cp = (ConnectionPacket)p;
        ConnectionMessage cm = _cmp.Parse(cp);
        //Check to see that the id matches and it is a request:
        if( cm.Id != _last_s_mes.Id ) {
          //There is an ID mismatch
          throw new LinkException("ID number mismatch");
        }
        if( cm.Dir != ConnectionMessage.Direction.Response ) {
          //This is not a response, as we expect.
          throw new LinkException("received a message that is not a Response");
        }
        if( cm is ErrorMessage ) {
          return (ErrorMessage)cm;
        }
        //Everything else looks good, lets make sure it is the right type:
        if( ! cm.GetType().Equals( _last_s_mes.GetType() ) ) {
          //This is not the same type 
          throw new LinkException("ConnectionMessage type mismatch");
        }
        //They must be sane, or the above would have thrown exceptions
        _last_r_packet = cp;
        _last_r_mes = cm;
        _last_packet_datetime = DateTime.Now;
	  return null;
    }
  }
}
