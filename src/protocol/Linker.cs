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

/*
 * Dependencies : 
 * Brunet.Address;
 * Brunet.AHPacket
 * Brunet.CloseMessage
 * Brunet.ConnectionPacket
 * Brunet.ConnectionMessage
 * Brunet.ConnectionMessageParser
 * Brunet.ConnectionType
 * Brunet.ConnectionTable
 * Brunet.Edge
 * Brunet.EdgeException
 * Brunet.EdgeFactory
 * Brunet.LinkMessage
 * Brunet.LinkException
 * Brunet.Node
 * Brunet.Packet
 * Brunet.ParseException
 * Brunet.PingMessage
 * Brunet.TransportAddress
 * Brunet.ErrorMessage
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

    protected Address _local_add;
    public Address Address
    {
      get
      {
        return _local_add;
      }
    }
    protected string _contype;
    public string ConnectionType
    {
      get
      {
        return _contype;
      }
    }
    protected LinkMessage _peer_link_mes;
    public LinkMessage PeerLinkMessage
    {
      get
      {
        return _peer_link_mes;
      }
    }

    protected Edge _e;
    public Edge Edge
    {
      get
      {
        return _e;
      }
    }
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
    protected ConnectionTable _tab;
    protected EdgeFactory _ef;
    //This is the queue that has only the address we have not tried this attempt
    protected Queue _ta_queue;
    //This is a copy of the original list of TAs
    protected Queue _tas;
    protected Node _local_n;
    protected ConnectionMessageParser _cmp;
    protected Packet _last_sent_packet;
    protected DateTime _last_packet_datetime;

    /* If we know the address of the node we are trying
     * to make an outgoing connection to, we lock it, and
     * remember it here
     */
    protected Address _target_lock;
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
    protected readonly int _ms_restart_time = 5000;
    protected int _restart_attempts = 16;
    protected DateTime _last_start;

    protected Random _rand;
    
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
        _local_add = local.Address;
        _cmp = new ConnectionMessageParser();
        _ef = local.EdgeFactory;
        //Hopefully at least one of these will be somewhat unpredictable
        ///@todo think seriously about getting truely random ids
        _id = GetHashCode()
              ^ _local_add.GetHashCode()
              ^ _cmp.GetHashCode();
        _rand = new Random(_id);
        /* We do not use negative ids, the spec says they
         * are reserved for future use
         */
        if( _id < 0 ) {
          _id = ~_id;
        }
        _tab = local.ConnectionTable;
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
      try {
#if POB_LINK_DEBUG
        Console.WriteLine("Start for {3} Linker({0}).Link({1},{2})",
                          _lid,target,ct,_local_add);
#endif
        lock (_sync ) {
          //If we retry, we need an original copy of the list
          _tas = new Queue(target_list);
          _ta_queue = new Queue( _tas );
          _contype = ct;
          _timeouts = 0;
        }
        /**
         * If we cannot set this address as our target, we
         * stop before we even try to make an edge.
         */
	_target = target;
#if ARI_LINK_DEBUG
	Console.WriteLine("Linker ({0}) attempting to lock {1}", _lid, target);
#endif 

        SetTarget( target );
#if ARI_LINK_DEBUG
	Console.WriteLine("Linker ({0}) acquired lock on {1}", _lid, target);
#endif 
        /**
         * TryNext Asynchronously gets an edge, and then begin the
         * link attempt with it.  If it fails, and there
         * are more TransportAddress objects, this method
         * will call itself again.  If there are no more TAs
         * the Link attempt Fails.
         */
        TryNext(false, null, null);
      }
      catch(InvalidOperationException x) {
        //This is thrown when ConnectionTable cannot lock.  Lets try again:
#if ARI_LINK_DEBUG
	Console.WriteLine("Linker ({0}) failed to lock {1}", _lid, target);
#endif         
	if ( _restart_attempts > 0 ) {
	  _restart_attempts--;
	  //There is no need to Stop, because this is the case where
	  //we never got going
	  _last_start = DateTime.Now;
          _local_n.HeartBeatEvent += new EventHandler(this.RestartLink);
#if ARI_LINK_DEBUG
          Console.WriteLine("Scheduling restart Linker({0})",_lid);
#endif
	}
	else {
          Fail("restarted maximum number of times");
	}
      }
      catch(Exception x) {
        Fail(x.Message);
      }
#if POB_LINK_DEBUG
      Console.WriteLine("End Linker({0}).Link",_lid);
#endif
    }

    /**
     * Handles packets to perform the outgoing link protocol
     */
    public void HandlePacket(Packet p, Edge edge)
    {
#if POB_LINK_DEBUG
      Console.WriteLine("Start Linker({0}).OutLinkHandler({1})",_lid,edge);
#endif
      if( _is_finished ) { return; }
      try {
        ConnectionPacket packet = (ConnectionPacket) p;
        ConnectionMessage cm;
        lock( _sync ) {
          cm = _cmp.Parse(packet);
          //Note the time we got this packet
          _last_packet_datetime = DateTime.Now;
        }
#if POB_LINK_DEBUG
        Console.WriteLine("Start Linker({0}).OutLinkHandler got {1}",
                          _lid,cm);
#endif
        if (cm.Dir != ConnectionMessage.Direction.Response) {
          throw new LinkException("Got request, expected response"
                                  + cm.ToString());
        }
        else if (cm is LinkMessage) {
#if ARI_LINK_DEBUG
	  Console.WriteLine("Linker({0}).OutLinkHandler got link response.",
			    _lid);
#endif

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
	  LinkMessage lm = (LinkMessage)cm;
          /*
	   * Make sure the link message is Kosher:
	   */
	  if( lm.ConTypeString != _contype ) {
            throw new LinkException("Link type mismatch: " + _contype + " != " + lm.ConTypeString );
	  }
	  if( lm.Attributes["realm"] != _local_n.Realm ) {
            throw new LinkException("Realm mismatch: " + _local_n.Realm + " != " + lm.Attributes["realm"] );
	  }
	  
	  StatusMessage req = _local_n.GetStatus(lm.ConTypeString, lm.Local.Address);
	  req.Id = _id++;
	  req.Dir = ConnectionMessage.Direction.Request;
	  lock( _sync ) {
	    Address target = lm.Local.Address;
	    //This will throw an exception if the target does not match
	    //any previous value.
#if ARI_LINK_DEBUG
	    Console.WriteLine("Linker({0}).OutLinkHandler attempting to lock {1}",
			      _lid, target );
#endif
	    SetTarget( target );
#if ARI_LINK_DEBUG
	    Console.WriteLine("Linker({0}).OutLinkHandler locks {1}",
			      _lid, target);
#endif
	    _peer_link_mes = lm;
	    _last_sent_packet = req.ToPacket();
	    _timeouts = 0;
	  }
#if POB_LINK_DEBUG
	  Console.WriteLine("Start Linker({0}).OutLinkHandler send {1}",
			    _lid,req);
#endif
#if ARI_LINK_DEBUG
	  Console.WriteLine("Start Linker({0}).OutLinkHandler send status request edge: {1} ; length: {2}",
			    _lid, edge, _last_sent_packet.Length);
	  
#endif
	  edge.Send( _last_sent_packet );
        }
	else if (cm is StatusMessage) {
#if ARI_LINK_DEBUG
	  Console.WriteLine("Linker({0}).OutLinkHandler got Status reponse",
			    _lid);
#endif
          if( _peer_link_mes != null ) {
            /**
             * Once we get a StatusMessage response we know that
             * the recipient has seen our ping, we Succeed 
             */

	    _con = new Connection(edge, _peer_link_mes.Local.Address,
			              _peer_link_mes.ConTypeString,
				      (StatusMessage)cm, _peer_link_mes);
#if ARI_LINK_DEBUG
	  Console.WriteLine("Linker({0}).OutLinkHandler creating a new connection: {1}",
			    _lid, _con);
#endif
            Succeed();

            //send extra status messages to new connection's neighbors
            SendStatusMessagesToNeighbors();
            
	    return;
	  }
	  else {

	  }
	}
	else if (cm is ErrorMessage) {
#if ARI_LINK_DEBUG
	  Console.WriteLine("Linker({0}).OutLinkHandler got error message",
			    _lid);
#endif
          //Looks like things are not working out for us.
	  ///@todo we should probably react differently to different types of errors.
	  ErrorMessage em = (ErrorMessage)cm;
#if PLAB_LOG
    Console.Write("{0}:{1} {2} \n", DateTime.Now.ToUniversalTime().ToString("MM'/'dd'/'yyyy' 'HH':'mm':'ss"), 
          DateTime.Now.ToUniversalTime().Millisecond, em.ToString() );
#endif
	  int error_code = (int)em.Ec;
	  if( em.Ec == ErrorMessage.ErrorCode.InProgress ) {
            ///@todo handle "double lock" condition.
            //This may be a "double lock" condition.
	    //We probably need to release our lock.  Wait
	    //a random period, and try again.
#if ARI_LINK_DEBUG
	    Console.WriteLine("Linker({0}).OutLinkHandler looks like connection is progress",
			    _lid);
#endif
	    lock(_sync) {
	      if ( _restart_attempts > 0 ) {
	        _restart_attempts--;
	        Stop("Restarting Linker");
	        _last_start = DateTime.Now;
                _local_n.HeartBeatEvent += new EventHandler(this.RestartLink);
#if POB_LINK_DEBUG
                Console.WriteLine("Restarting Linker({0})",_lid);
#endif
	      }
	      else {
                throw new LinkException("No more restarts");
	      }
	    }
	  }
	  else {
            //Otherwise, this is some other kind of error
	    Fail("connection attempt failed due to receiving Error: " + 
	       error_code.ToString() + ", " + em.Message  );
	  }
	}
        else if (cm is PingMessage) {
            /**
            * This should never happen
            */
#if POB_LINK_DEBUG
            Console.WriteLine("Saw Ping response before Link response");
#endif
        }
        else {
          /**
          * If there is an ErrorMessage response, or some other
          * response, we Fail
          */
          throw new LinkException("Got unexpected response" + cm.ToString());
        }
      }
      catch(Exception ex) {
        /* Something generally bad has happened */
        //log.Error("OutLink exception:", ex);
        Fail("exception: " + ex.Message);
      }
#if POB_LINK_DEBUG
      Console.WriteLine("End Linker({0}).OutLinkHandler",_lid);
#endif
    }

    /************  protected methods ***************/

    /**
     * Sends a StatusMessage request (local node) to the nearest right and 
     * left neighbors (in the local node's ConnectionTable) of the new Connection.
     */
    protected void SendStatusMessagesToNeighbors()
    {
      
      //the new connection's status response
      StatusMessage new_status = _con.Status;
      
      //our request for the new connections' neighbors
      string con_type_string = _peer_link_mes.ConTypeString;
      StatusMessage req = _local_n.GetStatus(con_type_string, null);
      req.Dir = ConnectionMessage.Direction.Request;
      
      AHAddress new_address = (AHAddress)_peer_link_mes.Local.Address;
      Edge left_edge = _tab.GetLeftStructuredNeighborOf(new_address);
      Edge right_edge = _tab.GetRightStructuredNeighborOf(new_address);

      Packet tp = null;
      if( left_edge != null ) {
        req.Id = _id++;
        tp = req.ToPacket();
        left_edge.Send(tp);
      }
      if( right_edge != null && !left_edge.Equals(right_edge) ) {
        req.Id = _id++;
        tp = req.ToPacket();
        right_edge.Send(tp);
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
        if( _target_lock != null ) {
          //This is the case where _target_lock has been set once
          if( ! target.Equals( _target_lock ) ) {
            throw new LinkException("Target lock already set to a different address");
          }
        }
        else if( target.Equals( _local_n.Address ) )
          throw new LinkException("cannot connect to self");
        else {
          lock( _tab.SyncRoot ) {
            if( _tab.Contains( Connection.StringToMainType( _contype ), target) ) {
              throw new LinkException("already connected");
            }
            //Lock throws an InvalidOperationException if it cannot get the lock
            _tab.Lock( target, Connection.StringToMainType( _contype ), this );
            _target_lock = target;
          }
        }
      }
    }

    /**
     * Called when there is a successful completion
     */
    protected void Succeed()
    {
#if POB_LINK_DEBUG
      Console.WriteLine("Start Linker({0}).Succeed",_lid);
#endif
      //log.Info("Link Success");
      if( _is_finished ) { return; }
      lock(_sync) {
        _is_finished = true;
        /* Stop the timer */
        _local_n.HeartBeatEvent -= new EventHandler(this.OutgoingResendCallback);
        /* Stop listening for close events */
        _e.ClearCallback(Packet.ProtType.Connection);
        _e.CloseEvent -= new EventHandler(this.CloseHandler);
      }
      try {
#if POB_LINK_DEBUG
        //This should never happen
        if( _peer_link_mes == null ) {
          Console.WriteLine("PeerLink is null!!!");
        }
        if( _e == null ) {
          Console.WriteLine("Edge is null!!!");
        }
#endif
        /* Announce the connection */
	_tab.Add(_con);
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
        _tab.Unlock( _target_lock, Connection.StringToMainType(_contype), this );
        if( FinishEvent != null )
          FinishEvent(this, null);
      }
#if POB_LINK_DEBUG
      Console.WriteLine("End Linker({0}).Succeed",_lid);
#endif
    }

    /**
     * Stops this attempt in preparation for a Fail or restart
     */
    protected void Stop(string log_message)
    {
      Edge e_to_close;
      if ( _target_lock != null ) {
        _tab.Unlock( _target_lock, Connection.StringToMainType(_contype), this );
	_target_lock = null;
      }
      //log.Error(log_message);
      /* Stop the timer */
      _local_n.HeartBeatEvent -= new EventHandler(this.OutgoingResendCallback);
      e_to_close = _e;
      //Release the lock
      if( e_to_close != null ) {
        e_to_close.ClearCallback(Packet.ProtType.Connection);
        /* Stop listening for close events */
        e_to_close.CloseEvent -= new EventHandler(this.CloseHandler);
        /* Close the edge if it is not already in the table */
        CloseMessage close = new CloseMessage(log_message);
        close.Dir = ConnectionMessage.Direction.Request;
        _local_n.GracefullyClose(_e, close);
      }      
    }
    /**
     * Stop the linker, and fire the FinishEvent
     * Called when the linker fails in either direction
     */
    protected void Fail(string log_message)
    {
      //log.Info("Link Failure");
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

    /**
     * Pops off the next TransportAddress in the _ta_queue if
     * there is one, else it returns null
     * @throw LinkException if there are no more TransportAddress objects
     */
    protected TransportAddress GetNextTA()
    {
      lock( _ta_queue ) {
        if( _ta_queue.Count <= 0 )
          throw new LinkException("no more TransportAddress objects");

        /**
         * As long as we have more TransportAddress objects, we just
         * return the top of the queue
         */
        return (TransportAddress) _ta_queue.Dequeue();
      }
    }

    protected void OutgoingResendCallback(object node, EventArgs args)
    {
#if POB_LINK_DEBUG
      //Console.WriteLine("Start Linker({0}).OutgoingResendCallback",_lid);
#endif
      try {
        lock( _sync ) {
          if( (! _is_finished) &&
              (DateTime.Now - _last_packet_datetime > _timeout ) &&
              (_timeouts <= _max_timeouts) ) {
#if POB_LINK_DEBUG
            Console.WriteLine("Linker({0}).Resending",_lid);
#endif
            _e.Send( _last_sent_packet );
            _last_packet_datetime = DateTime.Now;
	    //Increase the timeout by a factor of 4
	    _ms_timeout = 4 * _ms_timeout;
            _timeout = new TimeSpan(0,0,0,0,_ms_timeout);
            _timeouts++;
          }
          else if( _timeouts > _max_timeouts ) {
            throw new LinkException("Linker timed out");
          }
        }
      }
      catch(LinkException lx) {
        Fail(lx.Message);
      }
      catch(EdgeException ex) {
        Fail(ex.Message);
      }
#if POB_LINK_DEBUG
      //Console.WriteLine("End Linker({0}).OutgoingResendCallback",_lid);
#endif
    }

    /**
     * When we fail due to a ErrorMessage.ErrorCode.InProgress error
     * we wait restart to verify that we eventually got connected
     */
    protected void RestartLink(object node, EventArgs args)
    {
      if( _target != null 
	  && _tab.Contains( Connection.StringToMainType( _contype ), _target) ) {
        //Looks like we got connected in the mean time, stop now...
        _local_n.HeartBeatEvent -= new EventHandler(this.RestartLink);
	Fail("Connected before needed restart");
      }
      else {
        TimeSpan restart_time = new TimeSpan(0,0,0,0,_ms_restart_time);
        if( DateTime.Now - _last_start > restart_time ) { 
	  if ( _rand.NextDouble() < 0.1 ) {
#if POB_LINK_DEBUG
            Console.WriteLine("restart: about to call Link({0})",_lid);
#endif
            Link( _target, _tas, _contype);
            _local_n.HeartBeatEvent -= new EventHandler(this.RestartLink);
	    //Now we are done and their should be
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
#if POB_LINK_DEBUG
      Console.WriteLine("TryNext ({0}): {1},{2},{3}",_local_n.Address,success,e,x);
#endif
      try {
        if( success ) {
          bool have_con = false;
          ConnectionType ct;
          lock( _tab.SyncRoot ) {
            if( _tab.IsUnconnected( e ) ) {
              //This edge is already in the midst of connected
              success = false;
            }
            int index;
            Address add;
            have_con = _tab.GetConnection(e, out ct, out index, out add);
            if( have_con == false ) {
              /*
              * This edge has no connection on it already.
              */
              _tab.AddUnconnected(e);
            }
          } //End of lock on ConnectionTable:
                _tab.Disconnect(e);
        }
        if( success ) {
          StartNextAttempt(e);
          //Register the call back:
          _local_n.HeartBeatEvent
          += new EventHandler(this.OutgoingResendCallback);
        }
        else {
          //Stop listening to heartbeat event
          _local_n.HeartBeatEvent -= new EventHandler(this.OutgoingResendCallback);
          //Try to get another edge:
          _ef.CreateEdgeTo( GetNextTA(),
                            new EdgeListener.EdgeCreationCallback(this.TryNext) );
        }
      }
      catch(LinkException lx) {
	//GetNextTA will throw a link exception when it is out of TAs
#if POB_LINK_DEBUG
        System.Console.WriteLine("LinkException in Link:{0} ", lx.ToString() );
#endif        
        Fail(lx.Message);
      }
      catch(Exception ex) {
#if POB_LINK_DEBUG
        System.Console.WriteLine("Exception in Link:{0} ", ex.ToString() );
#endif
        Fail(ex.Message);
      }
    }

    /**
     * When the edge gets closed unexpectedly, this method is called
     */
    protected void CloseHandler(object edge, EventArgs args)
    {
#if POB_LINK_DEBUG
      Console.WriteLine("Start Linker({0}).CloseHandler({1})",_lid,edge);
#endif
      Fail("edge closed");
#if POB_LINK_DEBUG
      Console.WriteLine("End Linker({0}).CloseHandler",_lid);
#endif
    }

    /**
     * @throw LinkException if we cannot start
     */
    protected void StartNextAttempt(Edge e)
    {
      try {
        e.SetCallback(Packet.ProtType.Connection, this);
        e.CloseEvent += new EventHandler(this.CloseHandler);
	NodeInfo my_info = new NodeInfo( _local_add, e.LocalTA );
	NodeInfo remote_info = new NodeInfo( null, e.RemoteTA );
	System.Collections.Specialized.StringDictionary attrs
		 = new System.Collections.Specialized.StringDictionary();
        attrs["type"] = _contype;
	attrs["realm"] = _local_n.Realm;
        LinkMessage request = new LinkMessage( attrs, my_info, remote_info );
        request.Dir = ConnectionMessage.Direction.Request;
        request.Id = _id++;
#if POB_LINK_DEBUG
        Console.WriteLine("Linker({0}) on ({1}) sending ({2})",GetHashCode(),
                          e, request);
#endif
        Packet rpack = request.ToPacket();
        e.Send(rpack);
        //Note the time we got this packet
        lock( _sync ) {
          //Update all the member variables
          _e = e;
          _last_packet_datetime = DateTime.Now;
          _last_sent_packet = rpack;
          _timeouts = 0;
        }
      }
      catch(EdgeException x) {
#if POB_LINK_DEBUG
        Console.WriteLine("Linker({0}) got exception {1})",GetHashCode(), x);
#endif
        throw new LinkException("could not start", x);
      }
    }
  }
}
