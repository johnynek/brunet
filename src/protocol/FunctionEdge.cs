/*
 * Dependencies : 
 * 
 * Brunet.Edge;
 * Brunet.EdgeException
 * Brunet.Packet;
 * Brunet.TransportAddress;
 */

//#define DEBUG

//#define USE_FEDGE_QUEUE

#if USE_FEDGE_QUEUE
using System.Threading;
#endif

using System;
using System.Collections;

namespace Brunet
{

        /**
	 * A Edge which does its transport locally
	 * by calling a method on the other edge
	 *
	 * This Edge is for debugging purposes on
	 * a single machine in a single process.
	 */

  public class FunctionEdge:Brunet.Edge
  {
    
    public static Random _rand = new Random(DateTime.Now.Millisecond);

    public static ArrayList edgeList = ArrayList.Synchronized(new ArrayList());

    public Queue packetQueue;
  
    /**
     * Adding logger
     */
    /*private static readonly log4net.ILog log =
      log4net.LogManager.GetLogger(System.Reflection.MethodBase.
      GetCurrentMethod().DeclaringType);*/

    protected int _l_id;
#if USE_FEDGE_QUEUE
    protected static Queue _packet_queue;
    protected static Queue _edge_queue;
    protected static Thread _queue_thread;
    protected static object _queue_lock;
    /**
     * This initializes the packet queue and the timer
     * which pushes the packets out
     */
    static FunctionEdge()
    {
      _packet_queue = new Queue();
      _edge_queue = new Queue();
      _queue_lock = new System.Object();
      _queue_thread = new Thread(new ThreadStart(StartQueueProcessing));
      _queue_thread.Start();
    }
#endif
    public FunctionEdge(int local_id, bool is_in)
    {
      _l_id = local_id;
      inbound = is_in;
      _is_closed = false;
      
      _rand_id = _rand.Next(1, Int32.MaxValue);

      packetQueue = Queue.Synchronized(new Queue());     
    }

    private int   _rand_id;
    public int RandId
    {
      get 
      { 
        return _rand_id; 
      }
      set 
      { 
        _rand_id = value; 
      }
    }
    protected DateTime _last_out_packet_datetime;
    public override DateTime LastOutPacketDateTime {
      get { return _last_out_packet_datetime; }
    }

    protected bool _is_closed;
    public override void Close()
    {
      FunctionEdge.edgeList.Remove(this);
      base.Close();      
      _is_closed = true;
    }

    public override bool IsClosed
    {
      get
      {
        return (_is_closed);
      }
    }
    protected bool inbound;
    public override bool IsInbound
    {
      get
      {
        return inbound;
      }
    }

    protected FunctionEdge _partner;
    public FunctionEdge Partner
    {
      get
      {
        return _partner;   
      }
      set
      {
        _partner = value;
      }
    }
    

  /**
   *  @return true if the calling Edge is equal to the Edge argument
   *  We define two edges to be equal to each other if either of the following two cases hold : 
   *  Case one, the two local Transport Addresses match each other and the two remote TA's match
   *  Case two, the local TA of one edge matches with the remote TA of the second edge and vice versa
   *  Also the random ids of both edges(assigned at creation time) should match.
   */
  public override bool Equals(object e)
  {
    if (e is Edge) {
      FunctionEdge edge = e as FunctionEdge;
      bool LocalEq = this.LocalTA.Equals(edge.LocalTA);
      bool RemoteEq = this.RemoteTA.Equals(edge.RemoteTA);
      bool RandIdEq = (this.RandId==edge.RandId);
      return (LocalEq && RemoteEq && RandIdEq);
    }
    else {
      return false;
    }
  }

  /**
   *  @return the hash code of an edge
   *  We take the hash codes of the local and remote TA's and the edge's random id and XOR them
   *  The result is the Hash Code for the edge
   */
  public override int GetHashCode()
  {
    int num1 = this.LocalTA.GetHashCode();
    int num2 = ~this.RemoteTA.GetHashCode();

    return ( (num1 ^ num2) + RandId );
  }

    public override void Send(Brunet.Packet p)
    {
     if( !_is_closed ) {
	_last_out_packet_datetime = DateTime.Now;
        /**
         * log before the send because else the send will show
	 * up after the receive in the log
         */
	string base64String;
        try {
	   byte[] buffer = new byte[p.Length];
	   p.CopyTo(buffer, 0);
	   base64String = Convert.ToBase64String(buffer);
	}
        catch (System.ArgumentNullException){
        //log.Error("Error: Packet is Null");
               return;
        }
	string GeneratedLog = "OutPacket: edge: " + ToString() +
		              ", packet: " + base64String;
        //log.Info(GeneratedLog);
	// logging finished
#if USE_FEDGE_QUEUE
	lock(_queue_lock) {
          _packet_queue.Enqueue( p );
	  _edge_queue.Enqueue( _partner );
	}
#else
	//Tell the partner to send it:
        //_partner.ReceivedPacketEvent(p);
        _partner.packetQueue.Enqueue(p);
#endif
     }
     else {
     // THE FOLLOWING LINE SHOULD BE COMMENTED OUT FOR FUNCTION EDGE ONLY
     //throw new EdgeException("Trying to send on a closed edge");
     }
    }

    public override Brunet.TransportAddress.TAType TAType
    {
      get
      {
        return Brunet.TransportAddress.TAType.Function;
      }
    }

    public override Brunet.TransportAddress LocalTA
    {
      get
      {
        return new TransportAddress("brunet.function://localhost:"
			              + _l_id.ToString());
      }
    }
    public override Brunet.TransportAddress RemoteTA
    {
      get
      {
        return _partner.LocalTA;
      }
    }
   
#if USE_FEDGE_QUEUE
    static protected void StartQueueProcessing()
    {
      Packet p = null;
      FunctionEdge e = null;
      bool send_packet = false;
      int no_packets = 0;
      while( no_packets < 1000 )
      {
	System.Threading.Thread.Sleep(50);
        lock(_queue_lock) {
          if( _packet_queue.Count > 0 ) {
            p = (Packet)_packet_queue.Dequeue();
	    e = (FunctionEdge)_edge_queue.Dequeue();
	    send_packet = true;
	    no_packets = 0;
	  }
	  else {
	    no_packets++;
            send_packet = false;
	  }
	}
	if( send_packet ) {
         try {
          e.ReceivedPacketEvent(p);
	 }
	 catch(EdgeException x) {
         //log.Error("StartQueueProcessing: ", x); 
	 }
	}
      }
    }
#endif

    public static void simulate()
    {
#if USE_FEDGE_QUEUE

#else
    string command;
    bool all_done = false;
    int num_done = 0;
    while(true) {

    //Console.WriteLine("Next iteration of all network edges. Press a key to continue.");
    //System.Threading.Thread.Sleep(3000);

      #if DEBUG
      command = Console.ReadLine();
      if (command=="end"){
        return;
      }
      #else
      if ( (all_done) && (num_done==2) ) {
        //Console.ReadLine();
        return;
        }
      #endif

      int i=0;

      if (!all_done) {
        num_done=0;
      } else {
        num_done++;
      }

      all_done = true;
      while(i<edgeList.Count) {
        FunctionEdge nextEdge = (FunctionEdge)edgeList[i];
        if (nextEdge.packetQueue.Count>0) {
          Packet p = (Packet)nextEdge.packetQueue.Dequeue();
          all_done = all_done && (nextEdge.packetQueue.Count==0);      
          nextEdge.ReceivedPacketEvent(p);
        }
        i++;
      }

    }
#endif
    }

  }
}
