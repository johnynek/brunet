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
	
public class ReqrepManager : IDataHandler, ISource {
  
  public enum ReqrepType : byte
  {
    Request = 1, //A standard request that must be replied to at least once.
    LossyRequest = 2, //A request that does not require a response
    Reply = 3, //The response to a request
    ReplyAck = 4, //Acknowledge a reply
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

    _sync = new Object();
    _rand = new Random();
    _req_handler_table = new Hashtable();
    _req_state_table = new Hashtable();
    _rep_handler_table = new Hashtable();

    //Hold on to a reply for 50 seconds.
    _reptimeout = new TimeSpan(0,0,0,50,0);
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
    _reqtimeout = new TimeSpan(0,0,0,0,5000);
    _exp_moving_rtt = _reqtimeout.TotalMilliseconds;
    _exp_moving_square_rtt = _exp_moving_rtt * _exp_moving_rtt;
    _max_rtt = 0.0;
    _last_check = DateTime.UtcNow;
  }


  protected class Sub {
    public readonly IDataHandler Handler;
    public readonly object State;
    public Sub(IDataHandler h, object s) { Handler = h; State =s; }
    public void Handle(MemBlock b, ISender f) { Handler.HandleData(b, f, State); }
  }

  protected volatile Sub _sub;

  public virtual void Subscribe(IDataHandler hand, object state) {
    lock( _sync ) {
      _sub = new Sub(hand, state);
    }
  }

  public virtual void Unsubscribe(IDataHandler hand) {
    lock( _sync ) {
      if( _sub.Handler == hand ) {
        _sub = null;
      }
      else {
        throw new Exception(String.Format("Handler: {0}, not subscribed", hand));
      }
    }
  }

  /** static hashtable to keep track of ReqrepManager objects. */
  protected static Hashtable _rrm_table  = new Hashtable();
  /** static lock for protecting the Hashtable above. */
  protected static object _class_lock = new object();

  /** 
   * Static method to create ReqrepManager objects
   * @param node The node we work for
   */
  public static ReqrepManager GetInstance(Node node) {
    ReqrepManager rrm;
    lock(_rrm_table) {
      //check if there is already an instance object for this node
      if (_rrm_table.ContainsKey(node)) {
        return (ReqrepManager) _rrm_table[node];
      }
      //in case no instance exists, create one
      rrm = new ReqrepManager(node.Address.ToString());
      _rrm_table[node] = rrm;
    }
    rrm.Subscribe(node, null);
    node.HeartBeatEvent += rrm.TimeoutChecker;
    //Subscribe on the node:
    node.GetTypeSource(PType.Protocol.ReqRep).Subscribe(rrm, null);

    node.StateChangeEvent += delegate(Node n, Node.ConnectionState s) {
      if( s == Node.ConnectionState.Disconnected ) {
        lock(_rrm_table) {
          _rrm_table.Remove(n);
        }
        ISource source = n.GetTypeSource(PType.Protocol.ReqRep);
        try {
          source.Unsubscribe(rrm);
        }
        catch { }
      }
    };
    return rrm;
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
       System.Threading.Interlocked.Increment(ref _send_count);
       _req_date = DateTime.UtcNow;
       Sender.Send( Request );
     }

     public int Timeouts;
     public IReplyHandler ReplyHandler;
     protected ArrayList _repliers;
     public ArrayList Repliers { get { return _repliers; } }
     protected DateTime _req_date;
     public DateTime ReqDate { get { return _req_date; } }
     public ICopyable Request;
     public ISender Sender;
     public ReqrepType RequestType;
     public int RequestID;
     public object UserState;
     //this is something we need to get rid of 
     //public bool Replied;
     protected int _send_count;
     //number of times request has been sent out
     public int SendCount { get { return _send_count; } }
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
     protected volatile bool have_sent = false;

     public ReplyState(RequestKey rk) {
       RequestKey = rk;
       RequestDate = DateTime.UtcNow;
     }

     public void Send(ICopyable data) {
       if( !have_sent ) {
         have_sent = true;
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
     /**
      * Resend if we already have the reply,
      * if we don't have the reply yet, do nothing.
      */
     public void Resend() {
       if( Reply != null ) {
         _rep_date = DateTime.UtcNow;
         try {
           ReturnPath.Send( Reply );
         }
         catch { /* If this doesn't work, oh well */ }
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
   protected readonly object _sync;
   protected readonly Random _rand;
   protected Hashtable _req_state_table;
   protected Cache _reply_cache;
   protected Hashtable _rep_handler_table;
   protected Hashtable _req_handler_table;
   protected TimeSpan _reptimeout;
   protected TimeSpan _reqtimeout;
   //This is to keep track of when we looked for timeouts last
   protected DateTime _last_check;
  
   //When a message times out, how many times should
   //we resend before giving up
   protected const int _MAX_RESENDS = 5;
   protected const int _MINIMUM_TIMEOUT = 2000;

   /**
    * If f = _exp_factor we use:
    * a[t+1] = f a[t] + (1-f) a'
    * to update moving averages.  We need: 0 < f < 1
    * When f = 0, we change instantaneously: a[t+1] = a'
    * When f = 1, we never change: a[t+1] = a[t]
    */
   protected const double _exp_factor = 0.98; //approximately use the last 50
   protected double _exp_moving_rtt;
   protected double _exp_moving_square_rtt;
   protected double _max_rtt;
   //How many standard deviations to wait:
   protected const int _STD_DEVS = 6;

   // Methods /////
   /**
    * When we observe a RTT, we record it here and
    * update the request timeout.
    */
   protected void AddRttStat(TimeSpan rtt) {
     double ms_rtt = rtt.TotalMilliseconds;
     if( ms_rtt > _max_rtt ) { _max_rtt = ms_rtt; }
     double ms_rtt2 = ms_rtt * ms_rtt;
     _exp_moving_rtt = _exp_factor * (_exp_moving_rtt - ms_rtt) + ms_rtt;
     _exp_moving_square_rtt = _exp_factor * (_exp_moving_square_rtt - ms_rtt2) + ms_rtt2;
     /*
      * Now we can compute the std_dev:
      */
     double sd2 =  _exp_moving_square_rtt - _exp_moving_rtt * _exp_moving_rtt;
     double std_dev;
     if( sd2 > 0 ) {
       std_dev = Math.Sqrt( sd2 );
     }
     else {
       std_dev = 0.0;
     }
     double timeout = _exp_moving_rtt + _STD_DEVS * std_dev;
#if REQREP_DEBUG
     Console.Error.WriteLine("mean: {0}, std-dev: {1}, max: {2}, timeout: {3}", _exp_moving_rtt, std_dev, _max_rtt, timeout);
#endif
     /*
      * Here's the new timeout:
      */
     timeout = (_MINIMUM_TIMEOUT > timeout) ? _MINIMUM_TIMEOUT : timeout;
     _reqtimeout = TimeSpan.FromMilliseconds( timeout );
   }
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
         MemBlock err_data = MemBlock.Reference(
                        new byte[]{ (byte) ReqrepError.HandlerFailure } );
         ICopyable reply = MakeRequest(ReqrepType.Error, idnum, err_data);
         retpath.Send(reply);
       }
     }
   }

   protected void HandleReply(ReqrepType rt, int idnum, MemBlock rest, ISender ret_path) {
     RequestState reqs = (RequestState)_req_state_table[idnum];
     if( reqs != null ) {
       IReplyHandler handler = null;
       lock( _sync ) {
         ArrayList repls = reqs.Repliers;
         if (false == repls.Contains(ret_path)) {
           /*
            * Let's look at how long it took to get this reply:
            */
           TimeSpan rtt = DateTime.UtcNow - reqs.ReqDate;
           AddRttStat(rtt);
           //Make sure we ignore replies from this sender again
           repls.Add(ret_path);
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
       //The Ack is sent AFTER no more resends will be sent, so it's totally
       //safe to remove this from the cache!
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

  public void TimeoutChecker(object o)
  {
    TimeoutChecker(o, null);
  }

  /**
   * This method listens for the HeartBeatEvent from the
   * node and checks for timeouts.
   */
  public void TimeoutChecker(object o, EventArgs args)
  {
    DateTime now = DateTime.UtcNow;
    if( now - _last_check > _reqtimeout ) {
      //Here is a list of all the handlers for the requests that timed out
      ArrayList timeout_hands = null;
      ArrayList to_resend = null;
      lock( _sync ) {
        _last_check = now;
        IDictionaryEnumerator reqe = _req_state_table.GetEnumerator();
        while( reqe.MoveNext() ) {
          RequestState reqs = (RequestState)reqe.Value;
          if( now - reqs.ReqDate > _reqtimeout ) {
            reqs.Timeouts--;
            if( reqs.Timeouts >= 0 ) {
              if( reqs.RequestType != ReqrepType.LossyRequest ) {
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
          if( now - reps.RequestDate > _reptimeout ) {
            _reply_cache.Remove( de.Key );
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
    }
  }
}
  
}
