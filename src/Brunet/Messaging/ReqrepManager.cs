/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

//#define REQREP_DEBUG
using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using Brunet.Collections;
using Brunet.Util;
using Brunet.Concurrent;

namespace Brunet.Messaging {
	
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


  static ReqrepManager() {
    SenderFactory.Register("replystate", LookupReplyStateByUri);
    _inst_tab_sync = new object();
    lock(_inst_tab_sync) {
      _instance_table = new WeakValueTable<string, ReqrepManager>();
    } 
  }
  /**
   * Protected constructor, we want to control ReqrepManager instances
   * running on a node.  
   * If you want a Singleton-like behavior, use GetInstance()
   * @param info some context that we work for
   */
  public ReqrepManager(string info) : this(info, PType.Protocol.ReqRep) {

  }

  public ReqrepManager(string info, PType prefix) {
    lock( _inst_tab_sync ) {
      _instance_table.Replace(info, this);
    }
    _info = info;
    _prefix = prefix;
    Random r = new Random();
    //Don't use negative numbers:
    _req_state_table = new UidGenerator<RequestState>(r, true);
    //Don't use negative numbers:
    _reply_id_table = new UidGenerator<ReplyState>(r, true);

    /**
     * We keep a list of the most recent 1000 replies until they
     * get too old.  If the reply gets older than reptimeout, we
     * remove it
     */
    _reply_cache = new Cache(1000);
    _reply_cache.EvictionEvent += HandleReplyCacheEviction;
    _to_mgr = new TimeOutManager();
  }

  protected static object _inst_tab_sync;
  protected static WeakValueTable<string, ReqrepManager> _instance_table;

  /** 
   * Static method to create ReqrepManager objects
   * @param context the object that this manager works for
   */
  public static ReqrepManager GetInstance(string context) {
    lock(_inst_tab_sync) {
      var inst = _instance_table.GetValue(context);
      if(null == inst) {
        //This puts the item into _instance_table:
        inst = new ReqrepManager(context);
      }
      return inst;
    }
  }

  public static ReplyState LookupReplyStateByUri(object ctx, string uri) {
    var rrm = _instance_table.GetValue(ctx.ToString());
    if(null == rrm) {
      throw new Exception(String.Format("Invalid Context for ReqrepManager: {0}", ctx));
    }
    string scheme;
    IDictionary<string, string> kvpairs = SenderFactory.DecodeUri(uri, out scheme);
    int id = Int32.Parse(kvpairs["id"]);
    ReplyState rs;
    if( rrm._reply_id_table.TryGet(id, out rs)) {
      return rs;
    }
    else {
      throw new Exception(String.Format("Invalid id: {0}", id));
    }
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
     public RequestState(TimeSpan to, TimeSpan ack_to) {
       _timeouts = _MAX_RESENDS;
       _send_count = 0;
       _repliers = new ArrayList();
       _timeout = to;
       _ack_timeout = ack_to;
     }
     //Send the request again
     public void Send() {
       //Increment atomically:
       Interlocked.Increment(ref _send_count);
       _req_date = DateTime.UtcNow;
       Sender.Send( Request );
     }

     protected int _timeouts;
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
     protected TimeSpan _timeout;
     protected TimeSpan _ack_timeout;
    
     public bool IsTimedOut {
       get { return (_timeouts < 0); }
     }
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
     public bool IsTimeToAct(DateTime now) {
       TimeSpan timeout = _ackers == null ? _timeout : _ack_timeout;
       if( now - _req_date > timeout ) {
         _timeouts--;
         if( _ackers != null ) {
           if( _timeouts < 0 ) {
             //Now, the ACK has timed out, reset them:
             _ackers = null;
             _timeouts = _MAX_RESENDS;
           }
         }
         return true;
       }
       return false;
     }
   }
   /**
    * When a request comes in, we give this reply state
    * to any handler of the data.  When they do a Send on
    * it, we will send the reply
    */
   public class ReplyState : ISender, IWrappingSender {
     protected int _lid;
     public int LocalID {
       get {
         int val = _lid;
         if( val == 0 ) {
           throw new Exception("LocalID has not yet been set");
         }
         return val;
       }
       set {
         if( 0 != Interlocked.CompareExchange(ref _lid, value, 0) ) {
           throw new Exception(String.Format("Already set local id: {0}", _lid));
         }
       }
     }

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
     
     protected readonly PType _prefix;

     protected readonly WriteOnce<string> _uri;

     public ISender WrappedSender {
       get { return RequestKey.Sender; }
     }

     public ReplyState(PType prefix, RequestKey rk) {
       _prefix = prefix;
       RequestKey = rk;
       RequestDate = DateTime.UtcNow;
       _reply_timeouts = 0;
       _uri = new WriteOnce<string>();
       _lid = 0;
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
         Reply = new CopyList(_prefix, mb_header, data);
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
       ReturnPath.Send( new CopyList(_prefix, mb_header) );
     }

     public string ToUri() {
       string uri;
       if( _uri.TryGet(out uri) == false ) {
         Dictionary<string, string> kvpairs = new Dictionary<string, string>();
         kvpairs["id"] = LocalID.ToString();
         kvpairs["retpath"] = RequestKey.Sender.ToUri();
         uri = SenderFactory.EncodeUri("replystate", kvpairs);
         _uri.TrySet(uri);
       }
       return uri;
     }
     
     /**
      * Resend if we already have the reply,
      * if we don't have the reply yet, send an Ack
      */
     public void Resend() {
       try {
         if( Reply != null ) {
           _rep_date = DateTime.UtcNow;
           ReturnPath.Send( Reply );
         }
         else {
           SendAck();
         }
       }
       catch { /* If this doesn't work, oh well */ }
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
   /*
    * This handles all the Timeout maintainence
    */
   protected sealed class TimeOutManager {
   // //////
   //  Constants
   // /////////

     private const int _MINIMUM_TIMEOUT = 2000;
     //How many standard deviations to wait:
     private const int _STD_DEVS = 6;

   // ///////
   // Member variables
   // ////////
     //This is to keep track of when we looked for timeouts last
     private DateTime _last_check;
     private double _min_timeout;

     private readonly object _sync;
     private readonly Cache _send_stats;
     private readonly Cache _type_stats;
     private readonly TimeStats _global_stats;
     private readonly TimeStats _acked_rtt_stats;

     // ///////
     // Properties
     // ///////
     public TimeSpan MinimumTimeOut {
       get {
         return TimeSpan.FromMilliseconds( _min_timeout );
       }
     }
     public DateTime LastCheck {
       get { return _last_check; }
       set { _last_check = value; }
     }

     public TimeSpan AckedTimeOut {
       get {
         TimeStats ts = _acked_rtt_stats;
         double timeout = ts.AvePlusKStdDev(_STD_DEVS);
         timeout = Math.Max(_MINIMUM_TIMEOUT, timeout);
         return TimeSpan.FromMilliseconds( timeout );
       }
     }

     public TimeOutManager() {
       /*
        * Here we set the timeout mechanisms.  There is a default
        * value, but this is now dynamic based on the observed
        * RTT of the network
        */
       //resend the request after 5 seconds by default
       _min_timeout = 5000;
       _global_stats = new TimeStats(_min_timeout, 0.98);
       //Start with 50 sec timeout
       _acked_rtt_stats = new TimeStats(_min_timeout * 10, 0.98);
       _last_check = DateTime.UtcNow;
       _send_stats = new Cache(1000);
       _type_stats = new Cache(100);
       _sync = new object();
     }
     private Pair<TimeStats,TimeStats> GetStatsFor(ISender s) {
       System.Type st = s.GetType();
       TimeStats typets = (TimeStats)_type_stats[st];
       if(null == typets) {
         typets = _global_stats.Clone();
         _type_stats[st] = typets;
       }
       
       TimeStats sendts = (TimeStats)_send_stats[s];
       if(null == sendts) {
         sendts = typets.Clone();
         _send_stats[s] = sendts;
       }
       return new Pair<TimeStats,TimeStats>(typets, sendts);
     }
     public TimeSpan GetTimeOutFor(ISender s) {
       double timeout;
       lock(_sync) {
         //Has to be locked because we modify the cache
         var ts = GetStatsFor(s).Second;
         timeout = ts.AvePlusKStdDev(_STD_DEVS);
       }
       timeout = Math.Max(_MINIMUM_TIMEOUT, timeout);
       return TimeSpan.FromMilliseconds( timeout );
     }
     public void AddAckSampleFor(RequestState reqs, ISender s, TimeSpan rtt) {
       lock( _sync ) {
         var tsp = GetStatsFor(s);
         tsp.First.AddSample(rtt.TotalMilliseconds);
         tsp.Second.AddSample(rtt.TotalMilliseconds);
         _global_stats.AddSample(rtt.TotalMilliseconds);
         //Update the minimum:
         _min_timeout = Math.Min(_min_timeout, tsp.First.AvePlusKStdDev(_STD_DEVS));
         _min_timeout = Math.Min(_min_timeout, tsp.Second.AvePlusKStdDev(_STD_DEVS));
         _min_timeout = Math.Min(_min_timeout, _global_stats.AvePlusKStdDev(_STD_DEVS));
       }
     }
     public void AddReplySampleFor(RequestState reqs, ISender s, TimeSpan rtt) {
       /*
        * Let's look at how long it took to get this reply:
        */
       if( reqs.GotAck ) {
         lock(_sync) {
           TimeStats ts = _acked_rtt_stats;
           ts.AddSample(rtt.TotalMilliseconds);
           _min_timeout = Math.Min(_min_timeout, ts.AvePlusKStdDev(_STD_DEVS));
         }
       }
       else {
         AddAckSampleFor(reqs, s, rtt);
       }
     }
     public bool IsTimeToCheck(DateTime dt) {
       return dt - _last_check > MinimumTimeOut;
     }
   }
   // Member variables:

   protected readonly string _info;
   public string Info { get { return _info; } }
   protected readonly UidGenerator<RequestState> _req_state_table;
   protected readonly UidGenerator<ReplyState> _reply_id_table;
   protected readonly TimeOutManager _to_mgr;
   protected readonly Cache _reply_cache;
   protected readonly PType _prefix;
   //When a message times out, how many times should
   //we resend before giving up
   private const int _MAX_RESENDS = 5;
   /**
    * If f = _exp_factor we use:
    * a[t+1] = f a[t] + (1-f) a'
    * to update moving averages.  We need: 0 < f < 1
    * When f = 0, we change instantaneously: a[t+1] = a'
    * When f = 1, we never change: a[t+1] = a[t]
    */
   protected class TimeStats {
     protected readonly double _exp_factor;
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

     public double AvePlusKStdDev(double k) {
       return _exp_moving_rtt + k * _exp_moving_stdev; 
     }

     public TimeStats Clone() {
       var ret = new TimeStats(_exp_moving_rtt, _exp_factor);
       ret._exp_moving_square_rtt = _exp_moving_square_rtt;
       ret._exp_moving_stdev = _exp_moving_stdev;
       ret._max_rtt = _max_rtt;
       return ret;
     }

   }
   // Methods /////

   /** Create a ReplyState for a new Request
    * Note, this is not synchronized, you must hold the lock when calling!
    */
   protected ReplyState GenerateReplyState(PType prefix, RequestKey rk) {
     var rs = new ReplyState(_prefix, rk);
     _reply_cache[rk] = rs;
     rs.LocalID = _reply_id_table.GenerateID(rs);
     return rs;
   }

   /** If our cache evicts items, make sure to pull them out of the Uid table
    */
   protected void HandleReplyCacheEviction(object cache, EventArgs ev_args) {
     var cea = (Cache.EvictionArgs)ev_args;
     var rs = (ReplyState)cea.Value;
     lock( _sync ) {
       ReleaseReplyState(rs);
     }
   }

   /**
    * This is either a request or response.  Look up the handler
    * for it, and pass the packet to the handler
    */
   public void HandleData(MemBlock p, ISender from, object state) {
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
         rs = GenerateReplyState(_prefix, rk);
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
           ReleaseReplyState(rs);
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
     RequestState reqs;
     lock( _sync ) {
       if( _req_state_table.TryGet(idnum, out reqs)) {
         if (reqs.AddAck(ret_path)) {
           /*
            * Let's look at how long it took to get this reply:
            */
           TimeSpan rtt = DateTime.UtcNow - reqs.ReqDate;
           _to_mgr.AddAckSampleFor(reqs, ret_path, rtt);
         }
       }
     }
   }

   protected void HandleReply(ReqrepType rt, int idnum, MemBlock rest, ISender ret_path) {
     RequestState reqs;
     if( _req_state_table.TryGet(idnum, out reqs) ) {
       IReplyHandler handler = null;
       lock( _sync ) {
         if (reqs.AddReplier(ret_path)) {
           TimeSpan rtt = DateTime.UtcNow - reqs.ReqDate;
           _to_mgr.AddReplySampleFor(reqs, ret_path, rtt);
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
       ReplyState rs = (ReplyState)_reply_cache[rk]; 
       if( rs != null ) {
         ReleaseReplyState(rs);
       }
     }
   }

   protected void HandleError(ReqrepType rt, int idnum,
                              MemBlock err_data, ISender ret_path)
   {
     //Get the request:
     RequestState reqs;
     bool act;
     lock( _sync ) {
       ///@todo, we might not want to stop listening after one error
       act = _req_state_table.TryTake(idnum, out reqs);
     }
     if( act ) {
#if REQREP_DEBUG
    Console.Error.WriteLine("[ReqrepManager: {0}] Receiving error on request id: {1}, from: {2}", 
			     _info, idnum, ret_path);
#endif
         ///@todo make sure we are checking that this ret_path makes sense for
         ///our request
       ReqrepError rrerr = (ReqrepError)err_data[0];
       reqs.ReplyHandler.HandleError(this, idnum, rrerr, ret_path, reqs.UserState);
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
     return new CopyList(_prefix, mb_header, data);
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
    TimeSpan timeout = _to_mgr.GetTimeOutFor(sender);
    RequestState rs = new RequestState(timeout, _to_mgr.AckedTimeOut);
    rs.Sender = sender;
    rs.ReplyHandler = reply;
    rs.RequestType = reqt;
    rs.UserState = state;
    lock( _sync ) {
      rs.RequestID = _req_state_table.GenerateID(rs);
      rs.Request = MakeRequest(reqt, rs.RequestID, data);
    }
    //Make sure that when we drop the lock, rs is totally initialized
#if REQREP_DEBUG
    Console.Error.WriteLine("[ReqrepClient: {0}] Sending a request: {1} to node: {2}",
		      _info, rs.RequestID, sender);
#endif
    try {
      rs.Send();
      return rs.RequestID;
    }
    catch(SendException sx) {
      if( sx.IsTransient ) {
        //I guess we will just try to resend again in the future:
        return rs.RequestID;
      }
      else {
        //This is certainly going to fail, so fail now:
        StopRequest(rs.RequestID, reply);
        throw;
      }
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
      if( !_req_state_table.TryTake(request_id, out rs)) {
        rs = null;
      }
    }
    if( rs != null ) {
       /*
        * Send an ack for this reply:
        */
       byte[] ack_payload = new byte[5];
       ack_payload[0] = (byte)ReqrepType.ReplyAck;
       NumberSerializer.WriteInt(request_id, ack_payload, 1);
       ICopyable data = new CopyList(_prefix, MemBlock.Reference(ack_payload));
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
   * The guy below may want to ignore errors until the request has concluded.
   * Since someone could potentially send a Timeout error to this node, we
   * verify that this is indeed the case.  It may be better to add to the 
   * HandleError a boolean that specifies the state of the request as well.
   **/
  public bool RequestActive(int request_id) {
    RequestState rs;
    return _req_state_table.TryGet(request_id, out rs);
  }

  /** Forget all state associated with this ReplyState
   * Not synchronized!  You have to hold the lock!
   */
  protected void ReleaseReplyState(ReplyState rs) {
    _reply_cache.Remove(rs.RequestKey);
    ReplyState tmp_rs;
    _reply_id_table.TryTake(rs.LocalID, out tmp_rs);
  }

  /**
   * This method listens for the HeartBeatEvent from the
   * node and checks for timeouts.
   */
  public void TimeoutChecker(object o, EventArgs args)
  {
    DateTime now = DateTime.UtcNow;
    ArrayList timeout_hands = null;
    ArrayList to_resend = null;
    ArrayList to_ack = null;
    ArrayList reps_to_resend = null;

    if(_to_mgr.IsTimeToCheck(now)) {
      //Here is a list of all the handlers for the requests that timed out
      lock( _sync ) {
        _to_mgr.LastCheck = now;
        foreach(RequestState reqs in _req_state_table) {
          if( reqs.IsTimeToAct(now) ) {
            //We need to act:
            if( reqs.IsTimedOut ) {
              //We have timed out.
              if( timeout_hands == null ) { timeout_hands = new ArrayList(); }
              timeout_hands.Add( reqs ); 
            }
            else if( reqs.NeedToResend ) {
              ///@todo improve the logic of resending to be less wasteful
              if( to_resend == null ) { to_resend = new ArrayList(); }
              to_resend.Add( reqs );
            }
          }
        }
        //Clean up the req_state_table:
        if( timeout_hands != null ) {
          RequestState tmprs;
          foreach(RequestState reqs in timeout_hands) {
            _req_state_table.TryTake( reqs.RequestID, out tmprs );
          }
        }
        //Look for any Replies it might be time to clean:
        foreach(DictionaryEntry de in _reply_cache) {
          ReplyState reps = (ReplyState)de.Value;
          TimeSpan reptimeout = _to_mgr.GetTimeOutFor(reps.ReturnPath);
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
                ReleaseReplyState(reps);
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
        catch(SendException sx) {
          if( sx.IsTransient ) {
            /*
             * Just ignore it and wait until later
             */
            //This send didn't work, but maybe it will next time, who knows...
            req.ReplyHandler.HandleError(this, req.RequestID, ReqrepError.Send,
                                     null, req.UserState);
          }
          else {
            /*
             * This is a permanent failure, we don't have a way to denote
             * permanent failures currently, so let's just pass it on as a
             * Timeout (which is a kind of permanent failure).
             */
            StopRequest(req.RequestID, req.ReplyHandler);
            req.ReplyHandler.HandleError(this, req.RequestID, ReqrepError.Timeout,
                                     null, req.UserState);
          }
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
        catch(Exception) {
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
}
  
}
