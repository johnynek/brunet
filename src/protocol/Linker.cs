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

//#define DEBUG

//#define POB_LINK_DEBUG

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

  public class Linker : IPacketHandler
  {

    /*private static readonly log4net.ILog log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/

    /**
     * Holds the next request id to be used
     */
    protected int _id;

    protected string _contype;
    protected Edge _e;
    protected Connection _con;
    /**
     * This is the Connection established by this Linker
     */
    public Connection Connection { get { return _con; } }
    
    protected bool _is_finished;
    public bool IsFinished
    {
      get
      {
        lock(_sync) {
          return _is_finished;
        }
      }
    }
    //This is the queue that has only the address we have not tried this attempt
    protected Queue _ta_queue;
    protected Node _local_n;
    protected DateTime _last_packet_datetime;

    /* If we know the address of the node we are trying
     * to make an outgoing connection to, we lock it, and
     * remember it here
     */
    protected Address _target;

    /** global lock for thread synchronization */
    protected object _sync;

    /**
     * When we are all done working, this event is fired
     */
    public event EventHandler FinishEvent;
    /**
     * @todo we should probably signal if we fail
     */
    /**
     * How many time outs are allowed before assuming failure
     */
    protected readonly int _max_timeouts = 3;
    protected int _timeouts;
    /**
     * The timeout is adaptive.  It goes up
     * by a factor of 2
     * after each timeout.  It starts at 2 second.
     * Then 4 seconds, then 8 seconds.
     */
    protected int _ms_timeout = 2000;
    protected TimeSpan _timeout;

    //If we get an ErrorCode.InProgress, we restart after
    //a period of time
    protected static readonly int _ms_restart_time = 5000;
    protected static readonly int _MAX_RESTARTS = 16;
    protected int _restart_attempts = _MAX_RESTARTS;
    protected DateTime _last_start;

    protected Random _rand;
   
    protected LinkProtocolState _link_state;
    protected IEnumerator _link_enumerator;
#if POB_LINK_DEBUG
    private int _lid;
#endif
    /**
     * @param local the local Node to connect to the remote node
     */
    public Linker(Node local)
    {
#if POB_LINK_DEBUG
      _lid = GetHashCode() + (int)DateTime.Now.Ticks;
      Console.WriteLine("Making Linker: {0}",_lid);
#endif
      _timeout = new TimeSpan(0,0,0,0,_ms_timeout);
      _sync = new object();
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
      }
    }

    public void Link(Address target, ICollection target_list, ConnectionType ct)
    {
      Link(target, target_list, Connection.ConnectionTypeToString(ct) );
    }

    /**
     * When we want to initiate a connection of a given connection
     * type, use this
     *
     * @param target the address of the node you are trying to connect
     * to.  Set to null if you don't know
     * @param target_list an enumerable list of TransportAddress of the
     *                    Host we want to connect to
     * @param t ConnectionType string of the new connection
     */
    public void Link(Address target, ICollection target_list, string ct)
    {
      lock (_sync ) {
        //If we retry, we need an original copy of the list
        _ta_queue = new Queue( target_list );
        _contype = ct;
        _target = target;
      }
      StartLink();
    }

    protected void StartLink() {
      try {
        /*
         * If we cannot set this address as our target, we
         * stop before we even try to make an edge.
         */
        //This manages the link protocol state, but not the sending
        //of packets, resends, restarts, etc...
	TransportAddress next_ta = null;
	lock( _sync ) {
          if( _ta_queue.Count > 0 ) {
            next_ta = (TransportAddress)_ta_queue.Peek();
            _link_state = new LinkProtocolState(_local_n, _contype, _id);
            _link_state.SetTarget(_target);
	  }
	}
        /*
         * Asynchronously gets an edge, and then begin the
         * link attempt with it.  If it fails, and there
         * are more TransportAddress objects, this method
         * will call itself again.  If there are no more TAs
         * the Link attempt Fails.
         */
        if( next_ta != null ) {
          _local_n.EdgeFactory.CreateEdgeTo( next_ta,
			    new EdgeListener.EdgeCreationCallback(this.TryNext) );
        }
        else {
          Fail("No more TAs");
        }
      }
      catch(InvalidOperationException x) {
        //This is thrown when ConnectionTable cannot lock.  Lets try again:
        RetryThisTA();
      }
      catch(Exception x) {
        Fail(x.Message);
      }
    }

    /**
     * Is a state machine to handle the link protocol
     */
    protected class LinkProtocolState {
     
      protected ConnectionMessageParser _cmp;
            
      protected Packet _last_r_packet;
      protected ConnectionMessage _last_r_mes;
      /**
       * Set the last packet and check for error conditions.
       * If the packet contains an ErrorMessage, that ErrorMessage
       * is returned, else null
       */
      public ErrorMessage SetLastRPacket(Packet p) {
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
	  return null;
      }
      
      protected ConnectionMessage _last_s_mes;
      protected Packet _last_s_packet;
      public Packet LastSPacket {
        get {
          return _last_s_packet;
        }
      }
      protected Node _node;
      protected string _contype;
      protected Address _target_lock;
      public Address TargetLock { get { return _target_lock; } }
      protected int _id;
      
      public LinkProtocolState(Node n, string contype, int id) {
        _node = n;
        _contype = contype;
        _id = id;
        _target_lock = null;
        _cmp = new ConnectionMessageParser();
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
      public void SetTarget(Address target)
      {
        if ( target == null )
          return;
  
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
            tab.Lock( target, Connection.StringToMainType( _contype ), this );
            _target_lock = target;
          }
        }
      }
      
      public IEnumerator GetPacketEnumerator(Edge e) {
        //Here we make and yield the LinkMessage request.
	NodeInfo my_info = new NodeInfo( _node.Address, e.LocalTA );
	NodeInfo remote_info = new NodeInfo( null, e.RemoteTA );
	System.Collections.Specialized.StringDictionary attrs
		 = new System.Collections.Specialized.StringDictionary();
        attrs["type"] = _contype;
	attrs["realm"] = _node.Realm;
        _last_s_mes = new LinkMessage( attrs, my_info, remote_info );
        _last_s_mes.Dir = ConnectionMessage.Direction.Request;
        _last_s_mes.Id = _id++;
        _last_s_packet = _last_s_mes.ToPacket();
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
	SetTarget( lm.Local.Address );
	
	_last_s_mes = _node.GetStatus(lm.ConTypeString, lm.Local.Address);
	_last_s_mes.Id = _id++;
	_last_s_mes.Dir = ConnectionMessage.Direction.Request;
        _last_s_packet = _last_s_mes.ToPacket();
        yield return _last_s_packet;
	StatusMessage sm = (StatusMessage)_last_r_mes;
        Connection con = new Connection(e, lm.Local.Address, lm.ConTypeString,
				        sm, lm);
        //Return the connection, now we are done!
        yield return con;
      }
    }
    
    public void HandlePacket(Packet p, Edge edge)
    {
      if( _is_finished ) { return; }
      ErrorMessage em = null;
      try {
        em = _link_state.SetLastRPacket(p);
        //Note the time we got this packet
        _last_packet_datetime = DateTime.Now;
        _timeouts = 0;
      }
      catch(Exception x) {
        /*
         * SetLastRPacket can throw an exception on expected packets
         * for now, we just ignore them and resend the most recently
         * sent packet:
         */
        edge.Send( _link_state.LastSPacket );
        return;
      }
      //If we get here, the packet must have been what we were expecting,
      //or an ErrorMessage
      if( em != null ) {
        //We got an error
	if( em.Ec == ErrorMessage.ErrorCode.InProgress ) {
          RetryThisTA();
        }
        else {
          //We failed.
          Fail( "Got error: " + em.ToString() );
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
            Connection c = (Connection)o;
            Succeed(c);
          }
        }
        catch(InvalidOperationException x) {
          //This is thrown when ConnectionTable cannot lock.  Lets try again:
          RetryThisTA();
        }
        catch(Exception x) {
          //The protocol was not followed correctly by the other node, fail
          Fail(x.Message);
        }
      }
    }

    /************  protected methods ***************/

    /**
     * Called when there is a successful completion
     */
    protected void Succeed(Connection c)
    {
      ConnectionTable tab = _local_n.ConnectionTable;
      //log.Info("Link Success");
      if( _is_finished ) { return; }
      lock(_sync) {
        _is_finished = true;
        _con = c;
        /* Stop the timer */
        _local_n.HeartBeatEvent -= new EventHandler(this.PacketResendCallback);
        /* Stop listening for close events */
        _e.ClearCallback(Packet.ProtType.Connection);
        _e.CloseEvent -= new EventHandler(this.CloseHandler);
      }
      try {
        /* Announce the connection */
	tab.Add(c);
      }
      catch(Exception x) {
        /* Looks like we could not add the connection */
        //log.Error("Could not Add:", x);
        _local_n.GracefullyClose( _e );
      }
      finally {
        /*
         * It is essential that we Unlock the address AFTER we
         * add the connection.  Otherwise, we could have a race
         * condition
         */
	lock( _sync ) {
          Address tlock = _link_state.TargetLock;
          tab.Unlock( tlock, Connection.StringToMainType(_contype), _link_state );
	}
        if( FinishEvent != null )
          FinishEvent(this, null);
      }
    }

    /**
     * Stops this attempt in preparation for a Fail or restart
     */
    protected void Stop(string log_message)
    {
      Edge edge_to_clean = null;
      lock( _sync ) {
        if( _link_state != null ) {
	  Address tlock = _link_state.TargetLock;
          if ( tlock != null ) {
            ConnectionTable tab = _local_n.ConnectionTable;
            tab.Unlock( tlock, Connection.StringToMainType(_contype), _link_state );
          }
	  _link_state = null;
	  _link_enumerator = null;
	}
	edge_to_clean = _e;
	_e = null;
      }
      //log.Error(log_message);
      /* Stop the timer */
      _local_n.HeartBeatEvent -= new EventHandler(this.PacketResendCallback);
      //Release the lock
      
      if( edge_to_clean != null ) {
        edge_to_clean.ClearCallback(Packet.ProtType.Connection);
        /* Stop listening for close events */
        edge_to_clean.CloseEvent -= new EventHandler(this.CloseHandler);
        /* Close the edge if it is not already in the table */
        CloseMessage close = new CloseMessage(log_message);
        close.Dir = ConnectionMessage.Direction.Request;
        _local_n.GracefullyClose(edge_to_clean, close);
      }      
    }
    /**
     * Stop the linker, and fire the FinishEvent
     * Called when the linker fails in either direction
     */
    protected void Fail(string log_message)
    {
#if POB_LINK_DEBUG
      Console.WriteLine("Start Linker({0}).Fail({1})",_lid,log_message);
#endif
      lock(_sync) {
        if( _is_finished ) { return; }
        _is_finished = true;
	Stop(log_message);
      }
      if( FinishEvent != null ) {
        FinishEvent(this, null);
      }
#if POB_LINK_DEBUG
      Console.WriteLine("End Linker({0}).Fail",_lid);
#endif
    }

    protected void PacketResendCallback(object node, EventArgs args)
    {
      try {
        lock( _sync ) {
          if( (! _is_finished) &&
              (DateTime.Now - _last_packet_datetime > _timeout ) &&
              (_timeouts <= _max_timeouts) ) {
            _e.Send( _link_state.LastSPacket );
            _last_packet_datetime = DateTime.Now;
	    //Increase the timeout by a factor of 4
	    _ms_timeout = 4 * _ms_timeout;
            _timeout = new TimeSpan(0,0,0,0,_ms_timeout);
            _timeouts++;
          }
          else if( _timeouts > _max_timeouts ) {
            //This edge is not working, we need to restart on a new edge.
            MoveToNextTA();   
          }
        }
      }
      catch(Exception ex) {
	MoveToNextTA();
      }
    }

    /*
     * This happens when an edge is bad: too much packet loss, unexpected
     * edge closure, etc..
     * There is no waiting here: we try immediately.
     */
    protected void MoveToNextTA() {
      /*
       * Here we drop the lock, and close the edge properly
       */
      Stop("restarting");
      lock( _sync ) {
	_link_state = null;
	_link_enumerator = null;
        //Time to go on to the next TransportAddress, and give up on this one
        _restart_attempts = _MAX_RESTARTS;
        _ta_queue.Dequeue();
      }
      StartLink();
    }

    /**
     * We sleep some random period, and retry to connect without moving
     * to the next TA.  This happens when we have the "double-lock" error.
     */
    protected void RetryThisTA() {
      /*
       * Here we drop the lock, and close the edge properly
       */
      Stop("restarting");
      bool fail = false;
      lock( _sync ) {
	_link_state = null;
	_link_enumerator = null;
        //We can restart on this TA *if* 
        if ( _restart_attempts > 0 ) {
          _restart_attempts--;
        }
        else {
          //Time to go on to the next TransportAddress, and give up on this one
	  if( _ta_queue.Count > 0 ) {
            _restart_attempts = _MAX_RESTARTS;
            _ta_queue.Dequeue();
	  }
	  else {
            //No more addresses to try, oh no!!
            fail = true;
	  }
	}
	_last_start = DateTime.Now;
      }
      if( fail ) {
        Fail("no more tas to restart with");
      }
      else {
        _local_n.HeartBeatEvent += new EventHandler(this.RestartLink);
      }
    }
    
    /**
     * When we fail due to a ErrorMessage.ErrorCode.InProgress error
     * we wait restart to verify that we eventually got connected
     */
    protected void RestartLink(object node, EventArgs args)
    {
      ConnectionTable tab = _local_n.ConnectionTable;
      if( _target != null 
	  && tab.Contains( Connection.StringToMainType( _contype ), _target) ) {
        //Looks like we got connected in the mean time, stop now...
        _local_n.HeartBeatEvent -= new EventHandler(this.RestartLink);
	Fail("Connected before needed restart");
      }
      else {
        TimeSpan restart_time = new TimeSpan(0,0,0,0,_ms_restart_time);
        if( DateTime.Now - _last_start > restart_time ) { 
	  if ( _rand.NextDouble() < 0.1 ) {
            //Time to start up again
            _local_n.HeartBeatEvent -= new EventHandler(this.RestartLink);
            StartLink();
	  }
	}
      }
    }
    
    /**
     * @param success if this is true, we have a new edge to try else, make a new edge
     * @param e the new edge, if success
     * @param x the exception which may be present if sucess is false
     */
    protected void TryNext(bool success, Edge e, Exception x)
    {
      try {
        if( success ) {
          Packet p = null;
          lock( _sync ) {
            _e = e;
            //We consider each edge a fresh start.
            _timeouts = 0;
            _restart_attempts = _MAX_RESTARTS;
            _link_enumerator = _link_state.GetPacketEnumerator(e);
            _link_enumerator.MoveNext(); //Move the protocol forward:
            p = (Packet)_link_enumerator.Current;
            _last_packet_datetime = DateTime.Now;
          }
          e.SetCallback(Packet.ProtType.Connection, this);
          e.CloseEvent += new EventHandler(this.CloseHandler);
          e.Send(p);
          //Register the call back:
          _local_n.HeartBeatEvent += new EventHandler(this.PacketResendCallback);
        }
        else {
          TransportAddress ta = null;
          lock( _sync ) {
            //Try to get another edge:
            _ta_queue.Dequeue();
            if( _ta_queue.Count > 0 ) {
              ta = (TransportAddress)_ta_queue.Peek();
            }
          }
          if( ta != null ) {
            _local_n.EdgeFactory.CreateEdgeTo( ta,
			    new EdgeListener.EdgeCreationCallback(this.TryNext) );
          }
          else {
            Fail("No more TAs");
          }
        }
      }
      catch(Exception ex) {
        //Fail(ex.Message);
	MoveToNextTA();
      }
    }

    /**
     * When the edge gets closed unexpectedly, this method is called
     */
    protected void CloseHandler(object edge, EventArgs args)
    {
      /*
       * This edge is no good
       * Try the next available edge:
       */
      MoveToNextTA();
    }
  }
}
