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
   * A EdgeListener that uses UDP for the underlying
   * protocol.  This listener creates UDP edges.
   * 
   * The UdpEdgeListener creates two threads, one for reading from the socket
   * and the other writing to the socket.  Tests suggest that having a single
   * thread for writing improves bandwidth and latency performance over using 
   * asynchronous sockets or calling a send over a threadpool.
   */
  public class UdpEdgeListener : EdgeListener, IEdgeSendHandler
  {
    /**
     * This is a simple little class just to hold the
     * two objects needed to do a send
     */
    protected class SendQueueEntry {
      public SendQueueEntry(ICopyable p, UdpEdge udpe) {
        Packet = p;
        Sender = udpe;
        ErrorCount = 0;
        Control = false;
      }
      public SendQueueEntry(MemBlock p, EndPoint e) {
        Data = p;
        End = e;
        Control = true;
      }

      public readonly ICopyable Packet;
      public readonly UdpEdge Sender;
      public readonly EndPoint End;
      public readonly MemBlock Data;
      public readonly bool Control;
      public int ErrorCount;
    }
    //After this many SocketException errors stop trying to send a packet
    protected const int MAX_ERROR_COUNT = 3;
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
        if( _id_ht.Contains( e.ID ) ) {
          _id_ht.Remove( e.ID );
          object re = _remote_id_ht[ e.RemoteID ];
          if( re == e ) {
            //_remote_id_ht only keeps track of incoming edges,
            //so, there could be two edges with the same remoteid
            //that are not equivalent.
            _remote_id_ht.Remove( e.RemoteID );
          }
          NatDataPoint dp = new EdgeClosePoint(DateTime.UtcNow, e);
          _nat_hist = _nat_hist + dp;
          _nat_tas = new NatTAs( _tas, _nat_hist );
        }
      }
    }

    protected IPEndPoint GuessLocalEndPoint(IEnumerable tas) {
      IPAddress ipa = IPAddress.Loopback;
      bool stop = false;
      int port = _port;
      foreach(TransportAddress ta in tas) {
        ArrayList ips = ((IPTransportAddress) ta).GetIPAddresses();
        port = ((IPTransportAddress) ta).Port;
        foreach(IPAddress ip in ips) {
          byte[] addr = ip.GetAddressBytes();
          bool any_addr = ((addr[0] | addr[1] | addr[2] | addr[3]) == 0);
          if( !IPAddress.IsLoopback(ip) && !any_addr ) {

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
          if(ProtocolLog.UdpEdge.Enabled)
            ProtocolLog.Write(ProtocolLog.UdpEdge, String.Format(
              "Got control {1} from: {0}", e, code));
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
                if(ProtocolLog.UdpEdge.Enabled)
                  ProtocolLog.Write(ProtocolLog.UdpEdge, String.Format(
                    "Local NAT Mapping changed on Edge: {0}\n{1} => {2}",
                 e, old_ta, new_ta));
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
          if(ProtocolLog.Exceptions.Enabled)
            ProtocolLog.Write(ProtocolLog.Exceptions, x.ToString());
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
            if(ProtocolLog.UdpEdge.Enabled)
              ProtocolLog.Write(ProtocolLog.UdpEdge, String.Format(
                "Denying: {0}", rta));
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
            try {
              edge.CloseEvent += this.CloseHandler;
            }
            catch {
              CloseHandler(edge, null);
            }
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
        if(ProtocolLog.UdpEdge.Enabled)
          ProtocolLog.Write(ProtocolLog.UdpEdge, String.Format(
            "Remote NAT Mapping changed on Edge: {0}\n{1} -> {2}",
            edge, edge.End, end)); 
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
        if( !edge.IsClosed ) {
          SendEdgeEvent(edge);
        }
      }
      if( read_packet ) {
        //We have the edge, now tell the edge to announce the packet:
        try {
          edge.Push(packet);
        }
        catch(EdgeException) {
          if( edge.IsClosed ) {
            SendControlPacket(end, remoteid, localid, ControlCode.EdgeClosed, state);
            //Make sure we record that this edge has been closed
            CloseHandler(edge, null);
          }
        }
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
        NatDataPoint dp = new NewEdgePoint(DateTime.UtcNow, e);
        _nat_hist = _nat_hist + dp;
        _nat_tas = new NatTAs( _tas, _nat_hist );

        /* Tell me when you close so I can clean up the table */
        try {
          e.CloseEvent += this.CloseHandler;
          ecb(true, e, null);
        }
        catch {
          CloseHandler(e, null);
        }
      }
    }

    protected IPEndPoint ipep;
    protected Socket _s;
    protected new BlockingQueue _send_queue;

    ///this is the thread were the socket is read:
    protected Thread _listen_thread, _send_thread;

    public UdpEdgeListener() : this(0, null, null)
    {
    }

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
      _s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
      ipep = new IPEndPoint(IPAddress.Any, port);
      _s.Bind(ipep);
      _port = port = ipep.Port;
      /**
       * We get all the IPAddresses for this computer
       */
      if( local_config_ips == null ) {
        _tas = TransportAddressFactory.CreateForLocalHost(TransportAddress.TAType.Udp, _port);
      }
      else {
        _tas = TransportAddressFactory.Create(TransportAddress.TAType.Udp, _port, local_config_ips);
      }
      _nat_hist = null;
      _nat_tas = new NatTAs( _tas, _nat_hist );
      _ta_auth = ta_auth;
      if( _ta_auth == null ) {
        //Always authorize in this case:
        _ta_auth = new ConstantAuthorizer(TAAuthorizer.Decision.Allow);
      }
      //We start out expecting around 30 edges with
      //a load factor of 0.15 (to make edge lookup fast)
      _id_ht = new Hashtable(30, 0.15f);
      _remote_id_ht = new Hashtable();
      _sync = new object();
      _running = false;
      _isstarted = false;
      ///@todo, we need a system for using the cryographic RNG
      _rand = new Random();
      _send_handler = this;
      _send_queue = new BlockingQueue();
    }

    protected void SendControlPacket(EndPoint end, int remoteid, int localid,
                                     ControlCode c, object state)
    {
      using(MemoryStream ms = new MemoryStream()) {
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
            if(ProtocolLog.UdpEdge.Enabled)
              ProtocolLog.Write(ProtocolLog.UdpEdge, String.Format(
                "Problem sending EdgeData: EndPoint: {0}, remoteid: {1}, " +
                "localid: {2}, Edge: {3}", end, remoteid, localid, e));
          }
        }

        SendQueueEntry sqe = new SendQueueEntry(ms.ToArray(), end);
        _send_queue.Enqueue(sqe);
        if(ProtocolLog.UdpEdge.Enabled)
          ProtocolLog.Write(ProtocolLog.UdpEdge, String.Format(
            "Sending control {1} to: {0}", end, c));
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

        _isstarted = true;
        _running = true;
      }
      _listen_thread = new Thread( new ThreadStart(this.ListenThread) );
      _listen_thread.Start();
      _send_thread = new Thread( new ThreadStart(this.SendThread) );
      _send_thread.Start();
    }

    /**
     * To stop listening, this method is called
     */
    public override void Stop()
    {
      _running = false;
      _send_queue.Close();
    }

    /**
     * This is a System.Threading.ThreadStart delegate
     * We loop waiting for edges that need to send,
     * or data on the socket.
     *
     * This is the only thread that can touch the socket,
     * therefore, we do not need to lock the socket.
     */
    protected void ListenThread()
    {
      // Lock the socket so that the Send thread will wait for all receives to end before closing
      BufferAllocator ba = new BufferAllocator(8 + Packet.MaxLength);
      EndPoint end = new IPEndPoint(IPAddress.Any, 0);
      while(_running) {
        try {
          int max = ba.Capacity;
          int rec_bytes = _s.ReceiveFrom(ba.Buffer, ba.Offset, max, SocketFlags.None, ref end);
          //Get the id of this edge:
          if( rec_bytes >= 8 ) {
            int remoteid = NumberSerializer.ReadInt(ba.Buffer, ba.Offset);
            int localid = NumberSerializer.ReadInt(ba.Buffer, ba.Offset + 4);

            MemBlock packet_buffer = MemBlock.Reference(ba.Buffer, ba.Offset + 8, rec_bytes - 8);
            ba.AdvanceBuffer(rec_bytes);

            if( localid < 0 )
              /*
              * We never give out negative id's, so if we got one
              * back the other node must be sending us a control
              * message.
              */
              HandleControlPacket(remoteid, localid, packet_buffer, null);
            else
              HandleDataPacket(remoteid, localid, packet_buffer, end, null);
          }
        }
        catch(SocketException x) {
          if(_running)
            if(ProtocolLog.Exceptions.Enabled)
              ProtocolLog.Write(ProtocolLog.Exceptions, x.ToString());
        }
        catch(ObjectDisposedException x) {
          if(_running)
            if(ProtocolLog.Exceptions.Enabled)
              ProtocolLog.Write(ProtocolLog.Exceptions, x.ToString());
          break;
        }
        catch(Exception x) {
          //Possible socket error. Just ignore the packet.
          if(ProtocolLog.Exceptions.Enabled)
            ProtocolLog.Write(ProtocolLog.Exceptions, x.ToString());
        }
      }
    }

    private void SendThread()
    {
      byte []buffer = new byte[Packet.MaxLength];
      SendQueueEntry sqe = null;
      while(_running) {
        try {
          sqe = (SendQueueEntry) _send_queue.Dequeue();
          if(sqe.Control) {
            _s.SendTo(sqe.Data, sqe.End);
          }
          else {
            //We have a packet to send
            ICopyable p = sqe.Packet;
            UdpEdge sender = sqe.Sender;
            EndPoint e = sender.End;
            //Write the IDs of the edge:
            //[local id 4 bytes][remote id 4 bytes][packet]
            NumberSerializer.WriteInt(sender.ID, buffer, 0);
            NumberSerializer.WriteInt(sender.RemoteID, buffer, 4);
            int plength = p.CopyTo(buffer, 8);
            _s.SendTo(buffer, 8 + plength, SocketFlags.None, e);
          }
        }
        catch(SocketException x) {
        /*
          * some nodes have transient problems with their
          * networking.  We count the number of errors,
          * break out, to slow down sending a bit, and
          * hopefully things will get better.
        */
          sqe.ErrorCount++;
          if( sqe.ErrorCount < MAX_ERROR_COUNT ) {
          /*
            * Put it in the back of the queue and break out.
            * Hopefully by the time we try again matters will
            * be better
          */
            _send_queue.Enqueue(sqe);
          }
          else {
          /*
            * Oh well, it had it's chance.  Close the edge and
            * print a message.
          */
            if(ProtocolLog.Exceptions.Enabled)
              ProtocolLog.Write(ProtocolLog.Exceptions, String.Format(
                "SocketExceptions ({0}) on packet of length({1}): closing " +
                "Edge: {2}\n{3}", sqe.ErrorCount, sqe.Packet.Length,
                sqe.Sender, x));
            sqe.Sender.Close();
          }
        }
        catch(InvalidOperationException) {
          break;
        }
        catch(Exception x) {
      /*
          * Some non-socket exception.  This should never happen.
          * Print it out to hope to debug it later
      */
          if(ProtocolLog.Exceptions.Enabled)
            ProtocolLog.Write(ProtocolLog.Exceptions, String.Format(
              "Error in UdpEdgeListener.Send. Edge: {0}\n{1}", sqe.Sender, x));
        }
      }
      _s.Close();
    }

    /**
     * When UdpEdge objects call Send, it calls this packet
     * callback:
     */
    public void HandleEdgeSend(Edge from, ICopyable p)
    {
      SendQueueEntry sqe = new SendQueueEntry(p, (UdpEdge)from);
      _send_queue.Enqueue(sqe);
    }
  }
}
