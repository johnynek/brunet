/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

//#define REQREP_DEBUG
using System;
using System.Threading;
using System.Collections;

namespace Brunet {
	
/**
 * This class manages the Request-Reply protocol
 * for semi-reliable Brunet messaging.
 *
 * By semi-reliable we mean that in most cases, packet loss or duplication
 * will not cause a problem, but it some cases (of extreme loss or delay)
 * a problem could remain.
 *
 * This protocol is useful for simple applications that only need a best
 * effort attempt to deal with lost packets.
 */
	
public class ReqrepManager : SimpleSource, IDataHandler {
  
  public enum ReqrepType : byte
  {
    Request = 1, //A standard request that must be replied to at least once.
    LossyRequest = 2, //A request that does not require a response
    Reply = 3, //The response to a request
    ReplyAck = 4, //Acknowledge a reply
    RequestAck = 5, //Acknowledge a request, used when a request takes a long time to complete
    Error = 6//Some kind of Error
  }

  public enum ReqrepError : byte
  {
    NoHandler = 1, //There is no handler for this protocol
    HandlerFailure = 2, //There is a Handler, but it could not reply.
    Timeout = 3, //This is a "local" error, there was no response before timeout
    Send = 4 //Some kind of error resending
  }

  /**
   * Protected constructor, we want to control ReqrepManager instances
   * running on a node. 
   * @param node The Node we work for
   */
  public ReqrepManager(string info) {
    _info = info;

    _rand = new Random();
    _req_handler_table = new Hashtable();
    _req_state_table = new Hashtable();
    _rep_handler_table = new Hashtable();

    /**
     * We keep a list of the most recent 1000 replies until they
     * get too old.  If the reply gets older than reptimeout, we
     * remove it
     */
    _reply_cache = new Cache(1000);
    /*
     * Here we set the timeout mechanisms.  There is a default
     * value, but this is now dynamic based on the observed
     * RTT of the network
     */
    //resend the request after 5 seconds.
    _edge_reqtimeout = new TimeSpan(0,0,0,0,5000);
    _nonedge_reqtimeout = new TimeSpan(0,0,0,0,5000);
    //Start with 50 sec timeout
    _acked_reqtimeout = new TimeSpan(0,0,0,0,50000);
    //Here we track the statistics to improve the timeouts:
    _nonedge_rtt_stats = new TimeStats(_nonedge_reqtimeout.TotalMilliseconds, 0.98);
    _edge_rtt_stats = new TimeStats(_edge_reqtimeout.TotalMilliseconds, 0.98);
    _acked_rtt_stats = new TimeStats(_acked_reqtimeout.TotalMilliseconds, 0.98);
    _last_check = DateTime.UtcNow;
  }

  /** 
   * Static method to create ReqrepManager objects
   * @param node The node we work for
   * @deprecated use node.Rrm;
   */
  public static ReqrepManager GetInstance(Node node) {
    return node.Rrm;
  }

  public class Statistics {
    public int SendCount;
    public Statistics() {
    }
  }
   /**
    * This is an inner class used to keep track
    * of all the information for a request
    */
   protected class RequestState {
     public RequestState() {
       Timeouts = _MAX_RESENDS;
       _send_count = 0;
       _repliers = new ArrayList();
     }
     //Send the request again
     public void Send() {
       //Increment atomically:
       Interlocked.Increment(ref _send_count);
       _req_date = DateTime.UtcNow;
       Sender.Send( Request );
     }

     public int Timeouts;
     public IReplyHandler ReplyHandler;
     protected readonly ArrayList _repliers;
     public ArrayList Repliers { get { return _repliers; } }
     protected ArrayList _ackers;
     
     protected DateTime _req_date;
     public DateTime ReqDate { get { return _req_date; } }
     public ICopyable Request;
     public ISender Sender;
     public ReqrepType RequestType;
     public int RequestID;
     public object UserState;
     protected int _send_count;
     //number of times request has been sent out
     public int SendCount { get { return _send_count; } }
     
     ///True if we should resend
     public bool NeedToResend {
       get {
         if (RequestType != ReqrepType.LossyRequest) {
           if (_repliers.Count == 0) {
             return (_ackers == null); 
           }
           else {
             return false; 
           }
         }
         else {
           return false;
         }
       }
     }
     public bool GotAck { get { return _ackers != null; } }

     /** Record an ACK to this request
      * @return true if this is a new Ack
      */
     public bool AddAck(ISender acksender) {
       if( _ackers == null ) {
         _ackers = new ArrayList();
         _ackers.Add(acksender);
         return true;
       }
       else if( false == _ackers.Contains(acksender) ) {
         _ackers.Add(acksender);
         return true;
       }
       else {
         return false;
       }
     }
     /** Add to the set of repliers
      * @return true if this is a new replier
      */
     public bool AddReplier(ISender rep) {
       if( false == _repliers.Contains(rep) ) {
         _repliers.Add(rep);
         return true;
       }
       else {
         return false;
       }
     }
   }
   /**
    * When a request comes in, we give this reply state
    * to any handler of the data.  When they do a Send on
    * it, we will send the reply
    */
   public class ReplyState : ISender {
     public int RequestID { get { return RequestKey.RequestID; } }
     protected ICopyable Reply;
     protected DateTime _rep_date;
     public DateTime RepDate { get { return _rep_date; } }
     public readonly DateTime RequestDate;
     public ISender ReturnPath { get { return RequestKey.Sender; } }
     public readonly RequestKey RequestKey;
     protected int have_sent = 0;
     public bool HaveSent { get { return (have_sent == 1); } }
     protected int _have_sent_ack = 0;
     public bool HaveSentAck { get { return (_have_sent_ack == 1); } }

     protected int _reply_timeouts;
     public int ReplyTimeouts { get { return _reply_timeouts; } }

     public ReplyState(RequestKey rk) {
       RequestKey = rk;
       RequestDate = DateTime.UtcNow;
       _reply_timeouts = 0;
     }

     public int IncrementRepTimeouts() {
       _reply_timeouts++;
       return _reply_timeouts;
     }

     public void Send(ICopyable data) {
       if( 0 == Interlocked.Exchange(ref have_sent, 1) ) {
         //Make the header:
         byte[] header = new byte[5];
         header[0] = (byte)ReqrepType.Reply;
         NumberSerializer.WriteInt(RequestID, header, 1);
         MemBlock mb_header = MemBlock.Reference(header);
         Reply = new CopyList(PType.Protocol.ReqRep, mb_header, data);
         Resend();
       }
       else {
         /*
          * Something goofy is going on here.  Multiple
          * sends for one request.  we are ignoring it for
          * now
          */
       }
     }

     public void SendAck() {
       _have_sent_ack = 1;
       byte[] header = new byte[5];
       header[0] = (byte)ReqrepType.RequestAck;
       NumberSerializer.WriteInt(RequestID, header, 1);
       MemBlock mb_header = MemBlock.Reference(header);
       ReturnPath.Send( new CopyList(PType.Protocol.ReqRep, mb_header) );
     }

     public string ToUri() {
       throw new System.NotImplementedException();
     }
     
     /**
      * Resend if we already have the reply,
      * if we don't have the reply yet, send an Ack
      */
     public void Resend() {
       if( Reply != null ) {
         _rep_date = DateTime.UtcNow;
         try {
           ReturnPath.Send( Reply );
         }
         catch { /* If this doesn't work, oh well */ }
       }
       else {
         SendAck();
       }
     }
   }
   /**
    * We use these to lookup requests in the reply
    * cache
    */
   public class RequestKey {
     public readonly int RequestID;
     public readonly ISender Sender;
     public RequestKey(int id, ISender s) {
       RequestID = id;
       Sender = s;
     }
     public override int GetHashCode() { return RequestID; }
     public override bool Equals(object o) {
       if( o == this ) { return true; }
       RequestKey rk = o as RequestKey;
       if( rk != null ) {
         return (( rk.RequestID == this.RequestID) && rk.Sender.Equals( this.Sender ) );
       }
       else {
         return false;
       }
     }
   }
   // Member variables:

   protected readonly string _info;
   public string Info { get { return _info; } }
   protected readonly Random _rand;
   protected Hashtable _req_state_table;
   protected Cache _reply_cache;
   protected Hashtable _rep_handler_table;
   protected Hashtable _req_handler_table;
   protected TimeSpan _edge_reqtimeout;
   protected TimeSpan _nonedge_reqtimeout;
   protected TimeSpan _acked_reqtimeout;
   //This is to keep track of when we looked for timeouts last
   protected DateTime _last_check;
  
   //When a message times out, how many times should
   //we resend before giving up
   protected const int _MAX_RESENDS = 5;
   protected const int _MINIMUM_TIMEOUT = 2000;
   protected TimeStats _nonedge_rtt_stats;
   protected TimeStats _edge_rtt_stats;
   protected TimeStats _acked_rtt_stats;

   /**
    * If f = _exp_factor we use:
    * a[t+1] = f a[t] + (1-f) a'
    * to update moving averages.  We need: 0 < f < 1
    * When f = 0, we change instantaneously: a[t+1] = a'
    * When f = 1, we never change: a[t+1] = a[t]
    */
   //How many standard deviations to wait:
   protected const int _STD_DEVS = 6;

   protected class TimeStats {
     protected readonly double _exp_factor = 0.98; //approximately use the last 50
     protected double _exp_moving_rtt;
     
     public double Average { get { return _exp_moving_rtt; } }
     protected double _exp_moving_square_rtt;

     protected double _exp_moving_stdev;
     public double StdDev { get { return _exp_moving_stdev; } }
     
     protected double _max_rtt;
     public double Max { get { return _max_rtt; } }

     public TimeStats(double init, double exp_fact) {
       _exp_moving_rtt = init;
       _exp_moving_square_rtt = init * init;
       _exp_factor = exp_fact;
       _max_rtt = init;
     }

   /**
    * When we observe a Value, we record it here.
    */
     public void AddSample(double ms_rtt) {
       if( ms_rtt > _max_rtt ) { _max_rtt = ms_rtt; }
       double ms_rtt2 = ms_rtt * ms_rtt;
       _exp_moving_rtt = _exp_factor * (_exp_moving_rtt - ms_rtt) + ms_rtt;
       _exp_moving_square_rtt = _exp_factor * (_exp_moving_square_rtt - ms_rtt2) + ms_rtt2;
       /*
        * Now we can compute the std_dev:
        */
       double sd2 =  _exp_moving_square_rtt - _exp_moving_rtt * _exp_moving_rtt;
       if( sd2 > 0 ) {
         _exp_moving_stdev = Math.Sqrt( sd2 );
       }
       else {
         _exp_moving_stdev = 0.0;
       }
  #if REQREP_DEBUG
       double timeout = _exp_moving_rtt + _STD_DEVS * std_dev;
       Console.Error.WriteLine("mean: {0}, std-dev: {1}, max: {2}, timeout: {3}", _exp_moving_rtt, std_dev, _max_rtt, timeout);
  #endif
     }

   }
   // Methods /////
   /**
    * This is either a request or response.  Look up the handler
    * for it, and pass the packet to the handler
    */
   public void HandleData(MemBlock p, ISender from, object state) {
     //Simulate packet loss
     //if ( _rand.NextDouble() < 0.1 ) { return; }
     //Is it a request or reply?
     ReqrepType rt = (ReqrepType)((byte)p[0]);
     int idnum = NumberSerializer.ReadInt(p,1);
     MemBlock rest = p.Slice(5); //Skip the type and the id
     if( rt == ReqrepType.Request || rt == ReqrepType.LossyRequest ) {
       HandleRequest(rt, idnum, rest, from);
     }
     else if( rt == ReqrepType.Reply ) {
       HandleReply(rt, idnum, rest, from);
     }
     else if (rt == ReqrepType.ReplyAck ) {
       HandleReplyAck(rt, idnum, rest, from);
     }
     else if (rt == ReqrepType.RequestAck ) {
       HandleRequestAck(rt, idnum, rest, from);
     }
     else if( rt == ReqrepType.Error ) {
       HandleError(rt, idnum, rest, from);
     }
   }

   protected void HandleRequest(ReqrepType rt, int idnum,
                                MemBlock rest, ISender retpath)
   {
     /**
      * Lets see if we have been asked this question before
      */
     ReplyState rs = null;
     bool resend = false;
#if REQREP_DEBUG
	 Console.Error.WriteLine("[ReqrepManager: {0}] Receiving request id: {1}, from: {2}", 
			     _info, idnum, retpath);
#endif
     RequestKey rk = new RequestKey(idnum, retpath);
     lock( _sync ) {
       rs = (ReplyState)_reply_cache[rk];
       if( rs == null ) {
         rs = new ReplyState(rk);
	 //Add the new reply state before we drop the lock
         _reply_cache[rk] = rs;
       }
       else {
         resend = true;
       }
     }
     if( resend ) {
       //This is an old request:
       rs.Resend();
     }
     else {
       //This is a new request:
       try {
         _sub.Handle(rest, rs);
       }
       catch {
         lock( _sync ) {
           _reply_cache.Remove( rs.RequestKey );
         }
         //This didn't work out:
         try {
           MemBlock err_data = MemBlock.Reference(
                        new byte[]{ (byte) ReqrepError.HandlerFailure } );
           ICopyable reply = MakeRequest(ReqrepType.Error, idnum, err_data);
           retpath.Send(reply);
         }
         catch {
           //If this fails, we may think about logging.
           //The return path could fail, that's the only obvious exception
           ///@todo log exception
         }
       }
     }
   }

   protected void HandleRequestAck(ReqrepType rt, int idnum, MemBlock rest, ISender ret_path) {
     RequestState reqs = (RequestState)_req_state_table[idnum];
     if( reqs != null ) {
       IReplyHandler handler = null;
       lock( _sync ) {
         if (reqs.AddAck(ret_path)) {
           /*
            * Let's look at how long it took to get this reply:
            */
           TimeSpan rtt = DateTime.UtcNow - reqs.ReqDate;
           if( ret_path is Edge ) {
             _edge_reqtimeout = ComputeNewTimeOut(rtt.TotalMilliseconds,
                                                  _edge_rtt_stats,
                                                  _MINIMUM_TIMEOUT, _STD_DEVS);
           }
           else {
             _nonedge_reqtimeout = ComputeNewTimeOut(rtt.TotalMilliseconds,
                                                  _nonedge_rtt_stats,
                                                  _MINIMUM_TIMEOUT, _STD_DEVS);
           }
         }
       }
     }
   }

   protected void HandleReply(ReqrepType rt, int idnum, MemBlock rest, ISender ret_path) {
     RequestState reqs = (RequestState)_req_state_table[idnum];
     if( reqs != null ) {
       IReplyHandler handler = null;
       lock( _sync ) {
         if (reqs.AddReplier(ret_path)) {
           TimeSpan rtt = DateTime.UtcNow - reqs.ReqDate;
           /*
            * Let's look at how long it took to get this reply:
            */
           if( reqs.GotAck ) {
             //Use more standard deviations for acked messages.  We
             //just don't want to let it run forever.
             _acked_reqtimeout = ComputeNewTimeOut(rtt.TotalMilliseconds,
                                                _acked_rtt_stats,
                                                _MINIMUM_TIMEOUT, 3 *_STD_DEVS);

           }
           else if( ret_path is Edge ) {
             _edge_reqtimeout = ComputeNewTimeOut(rtt.TotalMilliseconds,
                                                  _edge_rtt_stats,
                                                  _MINIMUM_TIMEOUT, _STD_DEVS);
           }
           else {
             _nonedge_reqtimeout = ComputeNewTimeOut(rtt.TotalMilliseconds,
                                                  _nonedge_rtt_stats, _MINIMUM_TIMEOUT, _STD_DEVS);
           }
           handler = reqs.ReplyHandler;
         }
       }
       /*
        * Now handle this reply
        */
       if( null != handler ) {
         MemBlock payload;
         PType pt = PType.Parse(rest, out payload);
         Statistics statistics = new Statistics();
         statistics.SendCount = reqs.SendCount;
  #if REQREP_DEBUG
  Console.Error.WriteLine("[ReqrepManager: {0}] Receiving reply on request id: {1}, from: {2}", 
  			     _info, idnum, ret_path);
  #endif
  
         //Don't hold the lock while calling the ReplyHandler:
         bool continue_listening = handler.HandleReply(this, rt, idnum, pt, payload,
                                                   ret_path, statistics, reqs.UserState);
         //the request has been served
         if( !continue_listening ) {
           StopRequest(idnum, handler);
         }
       }
     }
     else {
       //We are ignoring this reply, it either makes no sense, or we have
       //already handled it
     }
   }

   /**
    * When we get a reply ack, we can remove the item from our cache,
    * we know the other guy got our reply
    */
   protected void HandleReplyAck(ReqrepType rt, int idnum,
                              MemBlock err_data, ISender ret_path) {
     RequestKey rk = new RequestKey(idnum, ret_path);
     lock( _sync ) {
       /**
        * This is not completely safe, but probably fine.  Consider the
        * case where:
        * A -(req)-> B 
        * A timeout but B does get the req
        * A <-(rep)- B
        * A -(req)-> B (these cross in flight)
        * A -(repack)-> B
        *
        * but then the repack passes the req retransmission (out of order
        * delivery)
        *
        * This is unlikely, but we could improve it.
        * @todo improve the reply caching algorithm
        */
       _reply_cache.Remove(rk);
     }
   }

   protected void HandleError(ReqrepType rt, int idnum,
                              MemBlock err_data, ISender ret_path)
   {
     //Get the request:
     RequestState reqs = (RequestState)_req_state_table[idnum];
     if( reqs != null ) {
       bool handle_error = false;
       lock( _sync ) {
         //Check to see if the request is still good, don't handle
         //the error twice:
         handle_error = _req_state_table.Contains(idnum);
         if( handle_error ) {
           ///@todo, we might not want to stop listening after one error
	   _req_state_table.Remove(idnum);
         }
       }
       if( handle_error ) {
#if REQREP_DEBUG
	 Console.Error.WriteLine("[ReqrepManager: {0}] Receiving error on request id: {1}, from: {2}", 
			     _info, idnum, ret_path);
#endif
         ///@todo make sure we are checking that this ret_path makes sense for
         ///our request
         ReqrepError rrerr = (ReqrepError)err_data[0];
	 reqs.ReplyHandler.HandleError(this, idnum, rrerr, ret_path, reqs.UserState);
       }
     }
     else {
       //We have already dealt with this Request
     }
   }

   protected ICopyable MakeRequest(ReqrepType rt, int next_rep, ICopyable data) {
     byte[] header = new byte[ 5 ];
     header[0] = (byte)rt;
     NumberSerializer.WriteInt( next_rep, header, 1 );
     MemBlock mb_header = MemBlock.Reference(header);
     return new CopyList(PType.Protocol.ReqRep, mb_header, data);
   }

  /**
   * @param sender how to send the request
   * @param reqt the type of request to make
   * @param data the data to encapsulate and send
   * @param reply the handler to handle the reply
   * @param state some state object to attach to this request
   * @return the identifier for this request
   *
   */
  public int SendRequest(ISender sender, ReqrepType reqt, ICopyable data,
		         IReplyHandler reply, object state)
  {
    if ( reqt != ReqrepType.Request && reqt != ReqrepType.LossyRequest ) {
      throw new Exception("Not a request");
    }
    RequestState rs = new RequestState();
    rs.Sender = sender;
    rs.ReplyHandler = reply;
    rs.RequestType = reqt;
    rs.UserState = state;
    lock( _sync ) {
      //Get the index 
      int next_req = 0;
      do {
        next_req = _rand.Next();
      } while( _req_state_table.ContainsKey( next_req ) );
      /*
       * Now we store the request
       */
      rs.RequestID = next_req;
      rs.Request = MakeRequest(reqt, next_req, data);
      _req_state_table[ rs.RequestID ] = rs;
    }
#if REQREP_DEBUG
    Console.Error.WriteLine("[ReqrepClient: {0}] Sending a request: {1} to node: {2}",
		      _info, rs.RequestID, sender);
#endif
    try {
      rs.Send();
      return rs.RequestID;
    }
    catch {
      //Clean up:
      StopRequest(rs.RequestID, reply);
      throw;
    }
  }

  /**
   * Abandon any attempts to get requests for the given ID.
   * @throw Exception if handler is not the original handler for this Request
   */
  public void StopRequest(int request_id, IReplyHandler handler) {
    RequestState rs = null;
    lock( _sync ) {
      rs = (RequestState)_req_state_table[request_id];
      if( rs != null ) {
        if( rs.ReplyHandler != handler ) {
          throw new Exception( String.Format("Handler mismatch: {0} != {1}",
                                             handler, rs.ReplyHandler));
        }
        _req_state_table.Remove( request_id );
      }
    }
    if( rs != null ) {
       /*
        * Send an ack for this reply:
        */
       byte[] ack_payload = new byte[5];
       ack_payload[0] = (byte)ReqrepType.ReplyAck;
       NumberSerializer.WriteInt(request_id, ack_payload, 1);
       ICopyable data = new CopyList(PType.Protocol.ReqRep, MemBlock.Reference(ack_payload));
       foreach(ISender ret_path in rs.Repliers) {
         try {
           //Try to send an ack, but if we can't, oh well...
           ret_path.Send(data);
         }
         catch { }
       }
    }
  }

  /**
   * This method listens for the HeartBeatEvent from the
   * node and checks for timeouts.
   */
  public void TimeoutChecker(object o, EventArgs args)
  {
    DateTime now = DateTime.UtcNow;
    TimeSpan interval = now - _last_check;
    ArrayList timeout_hands = null;
    ArrayList to_resend = null;
    ArrayList to_ack = null;
    ArrayList reps_to_resend = null;

    if( interval > _edge_reqtimeout || interval > _nonedge_reqtimeout ) {
      //Here is a list of all the handlers for the requests that timed out
      lock( _sync ) {
        _last_check = now;
        IDictionaryEnumerator reqe = _req_state_table.GetEnumerator();
        TimeSpan timeout;
        while( reqe.MoveNext() ) {
          RequestState reqs = (RequestState)reqe.Value;
          if( reqs.GotAck ) {
            timeout = _acked_reqtimeout;
          }
          else if( reqs.Sender is Edge ) {
            timeout = _edge_reqtimeout;
          }
          else {
            timeout = _nonedge_reqtimeout;
          }
          if( now - reqs.ReqDate > timeout ) {
            reqs.Timeouts--;
            if( reqs.Timeouts >= 0 ) {
              if( reqs.NeedToResend ) {
                ///@todo improve the logic of resending to be less wasteful
                if( to_resend == null ) { to_resend = new ArrayList(); }
                to_resend.Add( reqs );
              }
            }
            else {
              //We have timed out.
              if( timeout_hands == null ) { timeout_hands = new ArrayList(); }
              timeout_hands.Add( reqs ); 
            }
          }
        }
        //Clean up the req_state_table:
        if( timeout_hands != null ) {
          foreach(RequestState reqs in timeout_hands) {
            _req_state_table.Remove( reqs.RequestID );
          }
        }
        //Look for any Replies it might be time to clean:
        foreach(DictionaryEntry de in _reply_cache) {
          ReplyState reps = (ReplyState)de.Value;
          TimeSpan reptimeout;
          if(reps.ReturnPath is Edge) {
            reptimeout = _edge_reqtimeout;
          }
          else {
            reptimeout = _nonedge_reqtimeout;
          }
          if( reps.HaveSent ) {
            /*
             * See if we need to resend our reply
             */
            if( now - reps.RepDate > reptimeout ) {
              //We have already sent and we've kept it for a while...
              if( reps.IncrementRepTimeouts() <= _MAX_RESENDS ) {
                if( reps.HaveSentAck ) {
                /*
                 * If we sent an ack, we must keep sending the reply, until
                 * we get a reply ack
                 */
                  if( reps_to_resend == null ) { reps_to_resend = new ArrayList(); }
                  reps_to_resend.Add( reps ); 
                }
              }
              else {
                /*
                 * This reply has timed out:
                 */
                _reply_cache.Remove( de.Key );
              }
            }
          }
          else if(false == reps.HaveSentAck)  {
            //This one is taking a long time to answer, just ack it:
            if( to_ack == null ) { to_ack = new ArrayList(); }
            to_ack.Add( reps ); 
          }
        }
      }
    }
    else {
      //At each heartbeat check to see if we need send request acks:
      lock(_sync) {
        foreach(DictionaryEntry de in _reply_cache) {
          ReplyState reps = (ReplyState)de.Value;
          if((false == reps.HaveSentAck) && (false == reps.HaveSent))  {
            //This one is taking a long time to answer, just ack it:
            if( to_ack == null ) { to_ack = new ArrayList(); }
            to_ack.Add( reps ); 
          }
        }
      }
    }
    /*
     * It is important not to hold the lock while we call
     * functions that could result in this object being
     * accessed.
     *
     * We have released the lock, now we can send the packets:
     */
    if ( to_resend != null ) {
     foreach(RequestState req in to_resend) {
      try {
        req.Send();
      }
      catch {
        //This send didn't work, but maybe it will next time, who knows...
        req.ReplyHandler.HandleError(this, req.RequestID, ReqrepError.Send,
                                     null, req.UserState);
      }
     }
    }
    /*
     * Once we have released the lock, tell the handlers
     * about the timeout that have occured
     */
    if( timeout_hands != null ) {
     foreach(RequestState reqs in timeout_hands) {
      reqs.ReplyHandler.HandleError(this, reqs.RequestID, ReqrepError.Timeout,
                                    null, reqs.UserState);
     }
    }
    /*
     * Send acks for those that have been waiting for a long time
     */
    if(to_ack != null) {
      foreach(ReplyState reps in to_ack) {
        try {
          reps.SendAck();
        }
        catch(Exception x) {
          ///@todo, log this exception.
        } 
      }
    }
    /*
     * Resend replies for unacked replies
     */
    if( reps_to_resend != null ) {
      foreach(ReplyState reps in reps_to_resend) {
        reps.Resend();
      }
    }
  }
  protected TimeSpan ComputeNewTimeOut(double ms, TimeStats stats, double min, double stdevs) {
    stats.AddSample(ms);
    double timeout = stats.Average + stdevs * stats.StdDev;
    timeout = Math.Max(min, timeout);
    return TimeSpan.FromMilliseconds( timeout );
  }
}
  
}
