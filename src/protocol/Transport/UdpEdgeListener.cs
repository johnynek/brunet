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

using Brunet;
using System;
using System.IO;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Collections;

namespace Brunet
{
  /**
   * There are multiple implementations of Udp transports for
   * Brunet.  This is a base class with the shared code.
   */
  public abstract class UdpEdgeListenerBase : EdgeListener
  {
    ///Here is the queue for outgoing packets:
    protected Queue _send_queue;
    //This is true if there is something in the queue
    volatile protected bool _queue_not_empty;
    /**
     * This is a simple little class just to hold the
     * two objects needed to do a send
     */
    protected class SendQueueEntry {
      public SendQueueEntry(ICopyable p, UdpEdge udpe) {
        Packet = p;
        Sender = udpe;
      }
      public ICopyable Packet;
      public UdpEdge Sender;
    }
    /*
     * This is the object which we pass to UdpEdges when we create them.
     */
    protected IEdgeSendHandler _send_handler;
    /**
     * Hashtable of ID to Edges
     */
    protected Hashtable _id_ht;
    protected Hashtable _remote_id_ht;

    protected Random _rand;

    protected IEnumerable _tas;
    volatile protected NatHistory _nat_hist;
    volatile protected IEnumerable _nat_tas;
    public override IEnumerable LocalTAs
    {
      get
      {
        return _nat_tas;
      }
    }

    public override TransportAddress.TAType TAType
    {
      get
      {
        return TransportAddress.TAType.Udp;
      }
    }
    
    ///used for thread for the socket synchronization
    protected object _sync;
    
    volatile protected bool _running;
    volatile protected bool _isstarted;
    public override bool IsStarted
    {
      get { return _isstarted; }
    }
    
    protected int _port;
    //This is our best guess of the local endpoint
    protected IPEndPoint _local_ep {
      get {
        return GuessLocalEndPoint(_tas); 
      }
    }
    
    protected enum ControlCode : int
    {
      EdgeClosed = 1,
      EdgeDataAnnounce = 2 ///Send a dictionary of various data about the edge
    }
    
    override public TAAuthorizer TAAuth {
      /**
       * When we add a new TAAuthorizer, we have to check to see
       * if any of the old addresses are no good, in which case, we
       * close them
       */
      set {
        ArrayList bad_edges = new ArrayList();
        lock( _id_ht ) {
          _ta_auth = value;
          IDictionaryEnumerator en = _id_ht.GetEnumerator();
          while( en.MoveNext() ) {
            Edge e = (Edge)en.Value;
            if( _ta_auth.Authorize( e.RemoteTA ) == TAAuthorizer.Decision.Deny ) {
              bad_edges.Add(e);
            }
          }
        }
        //Close the newly bad Edges.
        foreach(Edge e in bad_edges) {
          e.Close();   
        }
      }
    }
    
    /**
     * When a UdpEdge closes we need to remove it from
     * our table, so we will know it is new if it comes
     * back.
     */
    public void CloseHandler(object edge, EventArgs args)
    {
      UdpEdge e = (UdpEdge)edge;
      lock( _id_ht ) {
        _id_ht.Remove( e.ID );
	object re = _remote_id_ht[ e.RemoteID ];
	if( re == e ) {
          //_remote_id_ht only keeps track of incoming edges,
	  //so, there could be two edges with the same remoteid
	  //that are not equivalent.
	  _remote_id_ht.Remove( e.RemoteID );
	}
      }
      NatDataPoint dp = new EdgeClosePoint(DateTime.UtcNow, e);
      _nat_hist = _nat_hist + dp;
      _nat_tas = new NatTAs( _tas, _nat_hist );
    }
   
    protected IPEndPoint GuessLocalEndPoint(IEnumerable tas) {
      IPAddress ipa = IPAddress.Loopback;
      bool stop = false;
      int port = _port;
      foreach(TransportAddress ta in tas) {
        ArrayList ips = ((IPTransportAddress) ta).GetIPAddresses();
        port = ((IPTransportAddress) ta).Port;
	foreach(IPAddress ip in ips) {
          if( !IPAddress.IsLoopback(ip) && (ip.Address != 0) ) {
		  //0 is the 0.0.0.0, or any address
            ipa = ip;
	    stop = true;
	    break;
	  }
	}
	if( stop ) { break; }
      }
      //ipa, now holds our best guess for an endpoint..
      return new IPEndPoint(ipa, port);
    }
    /**
     * This handles lightweight control messages that may be sent
     * by UDP
     */
    protected void HandleControlPacket(int remoteid, int n_localid, MemBlock buffer,
                                       object state)
    {
      int local_id = ~n_localid;
      //Reading from a hashtable is treadsafe
      UdpEdge e = (UdpEdge)_id_ht[local_id];
      if( (e != null) && (e.RemoteID == remoteid) ) {
        //This edge has some control information.
        try {
	  ControlCode code = (ControlCode)NumberSerializer.ReadInt(buffer, 0);
          System.Console.Error.WriteLine("Got control {1} from: {0}", e, code);
	  if( code == ControlCode.EdgeClosed ) {
            //The edge has been closed on the other side
	    e.Close();
 	  }
          else if( code == ControlCode.EdgeDataAnnounce ) {
            //our NAT mapping may have changed:
            IDictionary info =
              (IDictionary)AdrConverter.Deserialize( buffer.Slice(4) );
            string our_local_ta = (string)info["RemoteTA"]; //his remote is our local
            if( our_local_ta != null ) {
              //Update our list:
              TransportAddress new_ta = TransportAddressFactory.CreateInstance(our_local_ta);
              TransportAddress old_ta = e.PeerViewOfLocalTA;
              if( ! new_ta.Equals( old_ta ) ) {
                System.Console.Error.WriteLine(
	        "Local NAT Mapping changed on Edge: {0}\n{1} => {2}",
                 e, old_ta, new_ta); 
                //Looks like matters have changed:
                this.UpdateLocalTAs(e, new_ta);
                /**
                 * @todo, maybe we should ping the other edges sharing this
                 * EndPoint, but we need to be careful not to do some O(E^2)
                 * operation, which could easily happen if each EdgeDataAnnounce
                 * triggered E packets to be sent
                 */
              }
            }
          }
        }
        catch(Exception x) {
        //This could happen if this is some control message we don't understand
          Console.Error.WriteLine(x);
        }
      }
    }

    /**
     * This reads a packet from buf which came from end, with
     * the given ids
     */
    protected void HandleDataPacket(int remoteid, int localid,
                                    MemBlock packet, EndPoint end, object state)
    {
      bool read_packet = true;
      bool is_new_edge = false;
      //It is threadsafe to read from Hashtable
      UdpEdge edge = (UdpEdge)_id_ht[localid];
      if( localid == 0 ) {
        //This is a potentially a new incoming edge
        is_new_edge = true;

        //Check to see if it is a dup:
        UdpEdge e_dup = (UdpEdge)_remote_id_ht[remoteid];
        if( e_dup != null ) {
          //Lets check to see if this is a true dup:
          if( e_dup.End.Equals( end ) ) {
            //Same id from the same endpoint, looks like a dup...
            is_new_edge = false;
            //Console.Error.WriteLine("Stopped a Dup on: {0}", e_dup);
            //Reuse the existing edge:
            edge = e_dup;
          }
          else {
            //This is just a coincidence.
          }
        }
        if( is_new_edge ) {
          TransportAddress rta = TransportAddressFactory.CreateInstance(this.TAType,(IPEndPoint)end);
          if( _ta_auth.Authorize(rta) == TAAuthorizer.Decision.Deny ) {
            //This is bad news... Ignore it...
            ///@todo perhaps we should send a control message... I don't know
            is_new_edge= false;
            read_packet = false;
            Console.Error.WriteLine("Denying: {0}", rta);
          }
          else {
            //We need to assign it a local ID:
            lock( _id_ht ) {
              /*
               * Now we need to lock the table so that it cannot
               * be written to by anyone else while we work
               */
              do {
                localid = _rand.Next();
                //Make sure not to use negative ids
                if( localid < 0 ) { localid = ~localid; }
              } while( _id_ht.Contains(localid) || localid == 0 );
              /*
               * We copy the endpoint because (I think) .Net
               * overwrites it each time.  Since making new
               * edges is rare, this is better than allocating
               * a new endpoint each time
               */
              IPEndPoint this_end = (IPEndPoint)end;
              IPEndPoint my_end = new IPEndPoint(this_end.Address,
                                                 this_end.Port);
              edge = new UdpEdge(_send_handler, true, my_end,
                             _local_ep, localid, remoteid);
              _id_ht[localid] = edge;
              _remote_id_ht[remoteid] = edge;
            }
            edge.CloseEvent += new EventHandler(this.CloseHandler);
          }
        }
      }
      else if ( edge == null ) {
        /*
         * This is the case where the Edge is not a new edge,
         * but we don't know about it.  It is probably an old edge
         * that we have closed.  We can ignore this packet
         */
        read_packet = false;
	 //Send a control packet
        SendControlPacket(end, remoteid, localid, ControlCode.EdgeClosed, state);
      }
      else if ( edge.RemoteID == 0 ) {
        /* This is the response to our edge creation */
        edge.RemoteID = remoteid;
      }
      else if( edge.RemoteID != remoteid ) {
        /*
         * This could happen as a result of packet loss or duplication
         * on the first packet.  We should ignore any packet that
         * does not have both ids matching.
         */
        read_packet = false;
	 //Tell the other guy to close this ignored edge
        SendControlPacket(end, remoteid, localid, ControlCode.EdgeClosed, state);
        edge = null;
      }
      if( (edge != null) && !edge.End.Equals(end) ) {
        //This happens when a NAT mapping changes
        System.Console.Error.WriteLine(
	    "Remote NAT Mapping changed on Edge: {0}\n{1} -> {2}",
           edge, edge.End, end); 
        //Actually update:
        TransportAddress rta = TransportAddressFactory.CreateInstance(this.TAType,(IPEndPoint)end);
        if( _ta_auth.Authorize(rta) != TAAuthorizer.Decision.Deny ) {
          edge.End = end;
          NatDataPoint dp = new RemoteMappingChangePoint(DateTime.UtcNow, edge);
          _nat_hist = _nat_hist + dp;
          _nat_tas = new NatTAs( _tas, _nat_hist );
          //Tell the other guy:
          SendControlPacket(end, remoteid, localid, ControlCode.EdgeDataAnnounce, state);
        }
        else {
          /*
           * Looks like the new TA is no longer authorized.
           */
          SendControlPacket(end, remoteid, localid, ControlCode.EdgeClosed, state);
          edge.Close();
        }
      }
      if( is_new_edge ) {
       NatDataPoint dp = new NewEdgePoint(DateTime.UtcNow, edge);
       _nat_hist = _nat_hist + dp;
       _nat_tas = new NatTAs( _tas, _nat_hist );
       SendEdgeEvent(edge);
      }
      if( read_packet ) {
        //We have the edge, now tell the edge to announce the packet:
        edge.Push(packet);
      }
    }
    /**
     * When a new Connection is added, we may need to update the list
     * of TAs to make sure it is not too long, and that the it is sorted
     * from most likely to least likely to be successful
     * @param e the new Edge
     * @param ta the TransportAddress our TA according to our peer
     */
    public override void UpdateLocalTAs(Edge e, TransportAddress ta) {
      if( e.TAType == this.TAType ) {
        UdpEdge ue = (UdpEdge)e;
        ue.PeerViewOfLocalTA = ta;
        NatDataPoint dp = new LocalMappingChangePoint(DateTime.UtcNow, e, ta);
        _nat_hist = _nat_hist + dp;
        _nat_tas = new NatTAs( _tas, _nat_hist );
      }
    }

 

   /**
     * Each implementation may have its own way of doing this
     */
    protected abstract void SendControlPacket(EndPoint end, int remoteid, int localid,
                                     ControlCode c, object state);

  }

  /**
  * A EdgeListener that uses UDP for the underlying
  * protocol.  This listener creates UDP edges.
  * 
  * The UdpEdgeListener creates one thread.  In that
  * thread it loops processing reads.  The UdpEdgeListener
  * keeps a Queue of packets to send also.  After each
  * read attempt, it sends all the packets in the Queue.
  *
  */

  public class UdpEdgeListener : UdpEdgeListenerBase, IEdgeSendHandler
  {

    protected IPEndPoint ipep;
    protected Socket _s;

    ///this is the thread were the socket is read:
    protected Thread _thread;

    public UdpEdgeListener(int port)
    : this(port, null, null)
    {
      
    }
    public UdpEdgeListener(int port, IEnumerable ips)
       : this(port, ips, null)  { }
    /**
     * @param port the local port to bind to
     * @param local_tas an IEnumerable object which gives the list of local
     * IPs.  This is consulted every time LocalTAs is accessed, so it can
     * change as new interfaces are added
     * @param ta_auth the TAAuthorizer for packets incoming
     */
    public UdpEdgeListener(int port, IEnumerable local_config_ips, TAAuthorizer ta_auth)
    {
      /**
       * We get all the IPAddresses for this computer
       */
      if( local_config_ips == null ) {
        _tas = TransportAddressFactory.CreateForLocalHost(TransportAddress.TAType.Udp, port);
      }
      else {
        _tas = TransportAddressFactory.Create(TransportAddress.TAType.Udp, port, local_config_ips);
      }
      _nat_hist = null;
      _nat_tas = new NatTAs( _tas, _nat_hist );
      _ta_auth = ta_auth;
      if( _ta_auth == null ) {
        //Always authorize in this case:
        _ta_auth = new ConstantAuthorizer(TAAuthorizer.Decision.Allow);
      }
      /*
       * Use this to listen for data
       */
      _port = port;
      ipep = new IPEndPoint(IPAddress.Any, port);
      //We start out expecting around 30 edges with
      //a load factor of 0.15 (to make edge lookup fast)
      _id_ht = new Hashtable(30, 0.15f);
      _remote_id_ht = new Hashtable();
      _sync = new object();
      _running = false;
      _isstarted = false;
      _send_queue = new Queue();
      _queue_not_empty = false;
      ///@todo, we need a system for using the cryographic RNG
      _rand = new Random();
      _send_handler = this;
    }

    /**
     * Implements the EdgeListener function to 
     * create edges of this type.
     */
    public override void CreateEdgeTo(TransportAddress ta, EdgeCreationCallback ecb)
    {
      if( !IsStarted )
      {
        ecb(false, null,
            new EdgeException("UdpEdgeListener is not started") );
      }
      else if( ta.TransportAddressType != this.TAType ) {
        ecb(false, null,
            new EdgeException(ta.TransportAddressType.ToString()
                              + " is not my type: " + this.TAType.ToString() ) );
      }
      else if( _ta_auth.Authorize(ta) == TAAuthorizer.Decision.Deny ) {
        //Too bad.  Can't make this edge:
        ecb(false, null,
            new EdgeException( ta.ToString() + " is not authorized") );
      }
      else {
        Edge e = null;
        ArrayList ip_addresses = ((IPTransportAddress) ta).GetIPAddresses();
        IPAddress first_ip = (IPAddress)ip_addresses[0];
  
        IPEndPoint end = new IPEndPoint(first_ip, ((IPTransportAddress) ta).Port);
        /* We have to keep our mapping of end point to edges up to date */
        lock( _id_ht ) {
          //Get a random ID for this edge:
          int id;
          do {
            id = _rand.Next();
  	  //Make sure we don't have negative ids
  	  if( id < 0 ) { id = ~id; }
          } while( _id_ht.Contains(id) || id == 0 );
          e = new UdpEdge(this, false, end, _local_ep, id, 0);
          _id_ht[id] = e;
        }
        /* Tell me when you close so I can clean up the table */
        e.CloseEvent += new EventHandler(this.CloseHandler);
        NatDataPoint dp = new NewEdgePoint(DateTime.UtcNow, e);
        _nat_hist = _nat_hist + dp;
        _nat_tas = new NatTAs( _tas, _nat_hist );
        ecb(true, e, null);
      }
    }
   
    protected override void SendControlPacket(EndPoint end, int remoteid, int localid,
                                     ControlCode c, object state)
    {
        Socket s = (Socket)state;
        MemoryStream ms = new MemoryStream();
        NumberSerializer.WriteInt(localid, ms);
        //Bit flip to indicate this is a control packet
        NumberSerializer.WriteInt(~remoteid, ms);
        NumberSerializer.WriteInt((int)c, ms);
        if( c == ControlCode.EdgeDataAnnounce ) {
          UdpEdge e = (UdpEdge)_id_ht[localid];
          if( (e != null) && (e.RemoteID == remoteid) ) {
            Hashtable t = new Hashtable();
            t["RemoteTA"] = e.RemoteTA.ToString();
            t["LocalTA"] = e.LocalTA.ToString();
            AdrConverter.Serialize(t, ms);
          }
          else {
            Console.Error.WriteLine("Problem sending EdgeData: EndPoint: {0}, remoteid: {1}, localid: {2}, Edge: {3}", end, remoteid, localid, e);
          }
        }

        try {	//catching SocketException
          s.SendTo( ms.ToArray(), end);
          System.Console.Error.WriteLine("Sending control {1} to: {0}", end, c);
        }
        catch (SocketException sc) {
          Console.Error.WriteLine(
            "Error in Socket.SendTo. Endpoint: {0}\n{1}", end, sc);
        }
    }
    /**
     * This method may be called once to start listening.
     * @throw Exception if start is called more than once (including
     * after a Stop
     */
    public override void Start()
    {
      lock( _sync ) {
        if( _isstarted ) {
          //We can't start twice... too bad, so sad:
          throw new Exception("Restart never allowed");
        }
        _s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _s.Bind(ipep);
        _isstarted = true;
        _running = true;
      }
      _thread = new Thread( new ThreadStart(this.SocketThread) );
      _thread.Start();
    }

    /**
     * To stop listening, this method is called
     */
    public override void Stop()
    {
      _running = false;
    }

    /**
     * This is a System.Threading.ThreadStart delegate
     * We loop waiting for edges that need to send,
     * or data on the socket.
     *
     * This is the only thread that can touch the socket,
     * therefore, we do not need to lock the socket.
     */
    protected void SocketThread() // error happening here
    {
      //Wait 1 ms before giving up on a read
      int microsecond_timeout = 1000;
      //Make sure only this thread can see the socket from now on!
      Socket s = null;
      lock( _sync ) { 
        s = _s;
        _s = null;
      }
      EndPoint end = new IPEndPoint(IPAddress.Any, 0);
      byte[] send_buffer = new byte[ Packet.MaxLength + 8];
      
      BufferAllocator ba = new BufferAllocator(8 + Packet.MaxLength);
      byte[] buffer = ba.Buffer;
      int offset = ba.Offset;

      while(_running) {
        bool read = false;

        /**
         * Note that at no time do we hold two locks, or
         * do we hold a lock across an external function call or event
         */
        //Read if we can:
        int rec_bytes = 0;
        //this is the only thread that can touch the socket!!!!!
        //otherwise we must lock!!!
        try {
          read = s.Poll( microsecond_timeout, SelectMode.SelectRead );
          if( read ) {
	    int max = buffer.Length - offset;
            rec_bytes = s.ReceiveFrom(buffer, offset, max, SocketFlags.None, ref end);
            //Get the id of this edge:
            int remoteid = NumberSerializer.ReadInt(buffer, offset);
            int localid = NumberSerializer.ReadInt(buffer, offset + 4);
	    /*
	     * Make a reference to this memory, don't copy.
	     */
	    MemBlock packet_buffer = MemBlock.Reference(buffer, offset + 8, rec_bytes - 8);
	    ba.AdvanceBuffer(rec_bytes);
	    buffer = ba.Buffer;
	    offset = ba.Offset;
  	    
	    if( localid < 0 ) {
  	    /*
  	     * We never give out negative id's, so if we got one
  	     * back the other node must be sending us a control
  	     * message.
  	     */
              HandleControlPacket(remoteid, localid, packet_buffer, s);
  	    }
  	    else {
  	      HandleDataPacket(remoteid, localid, packet_buffer, end, s);
  	    }
          }
        }
        catch(Exception x) {
          //Possible socket error. Just ignore the packet.
          Console.Error.WriteLine(x);
        }
        /*
         * We are done with handling the reads.  Now lets
         * deal with all the pending sends:
         *
         * Note, we don't get a lock before checking the queue.
         * There is no race condition or deadlock here because
         * if we don't get the packets this round, we get them
         * next time.  Getting locks is expensive, so we don't 
         * want to do it here since we don't have to, and this
         * is a tight loop.
         */
        if( _queue_not_empty ) {
          lock( _send_queue ) {
            while( _send_queue.Count > 0 ) {
              SendQueueEntry sqe = (SendQueueEntry)_send_queue.Dequeue();
              Send(sqe, s, send_buffer);
            }
            //Before we unlock the send_queue, reset the flag:
            _queue_not_empty = false;
          }
        }
        //Now it is time to see if we can read...
      }
      s.Close();
    }

    private void Send(SendQueueEntry sqe, Socket s, byte[] buffer)
    {
      //We have a packet to send
      ICopyable p = sqe.Packet;
      UdpEdge sender = sqe.Sender;
      EndPoint e = sender.End;
      try {
        //Write the IDs of the edge:
        //[local id 4 bytes][remote id 4 bytes][packet]
        NumberSerializer.WriteInt(sender.ID, buffer, 0);
        NumberSerializer.WriteInt(sender.RemoteID, buffer, 4);
        p.CopyTo(buffer, 8);
        s.SendTo(buffer, 0, 8 + p.Length, SocketFlags.None, e);
      }
      catch (Exception x) {
        Console.Error.WriteLine("Error in Socket send. Edge: {0}\n{1}",
                                sender, x);
      }
    }

    /**
     * When UdpEdge objects call Send, it calls this packet
     * callback:
     */
    public void HandleEdgeSend(Edge from, ICopyable p)
    {
      lock( _send_queue ) {
        SendQueueEntry sqe = new SendQueueEntry(p, (UdpEdge)from);
        _send_queue.Enqueue(sqe);
        int count = _send_queue.Count;
        if( (count > 0) && (count % 1000 == 0) ) {
          Console.Error.WriteLine("UdpEdgeListener has {1} elements in Send Queue", count);
        }
      }
      _queue_not_empty = true;
    }

  }
}
