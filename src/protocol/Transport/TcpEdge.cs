/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com> University of Florida

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

//#define POB_TCP_DEBUG

/**
 * There are three implementations of TcpEdge:
 * 1) Asynchronous socket calls
 * 2) a thread loop calling Poll to check for packets
 * 3) a thread in TcpEdgeListener calling Select on all sockets
 *
 * Currently in Mono, 1) uses the ThreadPool and can result in
 * deadlocks if there are too many waiting edges.  2) is not scalable
 * because it uses 1 thread per edge.  3) SHOULD be scalable, but
 * Select is considered worse than OS level Asynchronous IO.
 *
 * In the future, we should work to get kernel level async IO supported
 * in Mono, then move to the Async IO Version.
 */

//#define TCP_ASYNC
//#define TCP_POLL
#define TCP_SELECT

using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Brunet
{

  /**
   * A Edge which does its transport over the Tcp protocol.
   * The UDP protocol is really better for Brunet.
   */

  public class TcpEdge:Brunet.Edge
  {

#if PLAB_LOG
    private BrunetLogger _logger;
    public BrunetLogger Logger{
	get{
	  return _logger;
	}
	set
	{
	  _logger = value;          
	}
    }
#endif

    /**
     * Represents the state of the packet sending
     */
    private class SendState {
      /* Represents the part of the buffer we just tried to send */
      public byte[] Buffer;
      public int Offset;
      public int Length;
      public bool SendingSize;

      /* This is the packet we are sending */
      public Packet PacketToSend;
    }

    /**
     * Represents the state of the packet receiving 
     */
    private class ReceiveState {
      public byte[] Buffer;
      public int Offset;
      public int Length;
      public int LastReadOffset;
      public int LastReadLength;
      public bool ReadingSize;
    }

    /**
     * Adding logger
     */
    /*private static readonly log4net.ILog log =
      log4net.LogManager.GetLogger(System.Reflection.MethodBase.
      GetCurrentMethod().DeclaringType);*/

    protected Socket _sock;
    public Socket Socket { get { return _sock; } }
    protected bool inbound;
    protected bool _is_closed;
    protected bool _is_sending;
    protected byte[] _size_buffer;

#if TCP_SELECT
    protected bool _need_to_send;
    protected TcpEdgeListener _tel;
    public bool NeedToSend {
      get {
        lock( _sync ) {
          return _need_to_send;
        }
      }
      set {
#if POB_TCP_DEBUG
        Console.Error.WriteLine("In NeedToSend");
#endif
        bool send_event = false;
        lock( _sync ) {
          if( _need_to_send != value ) {
            _need_to_send = value;
            send_event = true;
          }
        }
        //Release the lock and fire the event
        if( send_event ) {
#if POB_TCP_DEBUG
          Console.Error.WriteLine("About to send event");
#endif
          _tel.SendStateChange(this);
        }
      }
    }
#endif


#if TCP_POLL || TCP_SELECT
    /** Send(Packet) is called faster than we can send
     * the packets over the socket, we place them in
     * queue
     */
    protected Queue _packet_queue;
#endif

#if TCP_POLL
    /**
     * In the polling implementation, there is
     * a thread that spins waiting for packets to come in
     */
    protected Thread _poll_thread;
#endif

#if TCP_SELECT
    /**
     * When we are using the TCP_SELECT mode, these objects
     * keep track of the state
     */
    private SendState _send_state;
    private ReceiveState _rec_state;
#endif

    public TcpEdge(Socket s, bool is_in, TcpEdgeListener tel)
    {
      _sync = new Object();
      lock(_sync)
      {
        _sock = s;
        _is_closed = false;
        _is_sending = false;
        _create_dt = DateTime.UtcNow;
        _last_out_packet_datetime = _create_dt;
        _last_in_packet_datetime = _last_out_packet_datetime;
        _size_buffer = new byte[2];
#if TCP_POLL
        _packet_queue = Queue.Synchronized( new Queue() );
#elif TCP_SELECT
        _packet_queue = new Queue();
        _need_to_send = false;
	_tel = tel;
#endif
        inbound = is_in;
        _local_ta =
          TransportAddressFactory.CreateInstance(TAType,
                               (IPEndPoint) _sock.LocalEndPoint);
        _remote_ta =
          TransportAddressFactory.CreateInstance(TAType,
                               (IPEndPoint) _sock.RemoteEndPoint);
#if TCP_POLL
        _poll_thread = new Thread(new ThreadStart(PollLoop));
#endif

#if TCP_SELECT
        //We use Non-blocking sockets here
        s.Blocking = false;
        _send_state = new SendState();
        //This can hold 2 bytes + the largest packet
        _send_state.Buffer = new byte[ 2 + Packet.MaxLength ];
        _send_state.Offset = 0;
        _send_state.Length = 0;
        _rec_state = new ReceiveState();
        //This can hold 2 bytes + the largest packet
        _rec_state.Buffer = new byte[2 + Packet.MaxLength ];
        _rec_state.Offset = 0;
        //How many bytes left to read to get the first size
        _rec_state.Length = 2;
        _rec_state.LastReadOffset = 0;
        _rec_state.LastReadLength = 2;
        _rec_state.ReadingSize = true;
#endif

      }
    }

    /**
     * Closes the edges IF it is not already closed, otherwise do nothing
     */
    public override void Close()
    {
#if POB_TCP_DEBUG
      Console.Error.WriteLine("edge: {0}, Closing",this);
#endif
      //Don't hold the lock while we close:
      base.Close();

      lock( _sync ) {
        try {
          if (!_is_closed) {
            _is_closed = true;
            _sock.Shutdown(SocketShutdown.Both);
            _sock.Close();
          }
        }
        catch(Exception) {
          //log.Error("Problem Closing", ex);
        }
        finally {
#if TCP_POLL
          _poll_thread.Abort();
#endif
        }
      }
    }

    public override bool IsClosed
    {
      get
      {
        lock(_sync) {
          return _is_closed;
        }
      }
    }
    public override bool IsInbound
    {
      get
      {
        return inbound;
      }
    }

    protected DateTime _create_dt;
    public override DateTime CreatedDateTime {
      get { return _create_dt; }
    }
    protected DateTime _last_out_packet_datetime;
    public override DateTime LastOutPacketDateTime {
      get { return _last_out_packet_datetime; }
    }
    /**
     * @param p the Packet to send
     * @throw EdgeException if we cannot send
     */
#if TCP_POLL
    //Here is the Polling version
    public override void Send(Packet p)
    {
#if POB_TCP_DEBUG
      Console.Error.WriteLine("edge: {0}, Entering Send",this);
#endif
      if( _is_closed ) {
        throw new EdgeException("Tried to send on a closed socket");
      }
      _last_out_packet_datetime = DateTime.UtcNow;
#if POB_TCP_DEBUG
      Console.Error.WriteLine("edge: {0}, About to enqueue packet of length: {1}",
                        this, p.Length);
#endif
      //Else just queue up the packet
      _packet_queue.Enqueue(p);
#if POB_TCP_DEBUG
      Console.Error.WriteLine("edge: {0}, Leaving Send",this);
#endif
    }
#endif

#if TCP_SELECT
    //Here is the Select version
    public override void Send(ICopyable p)
    {
      if( p == null ) {
        throw new System.NullReferenceException(
           "TcpEdge.Send: argument can't be null");
      }

      lock( _sync ) {
#if POB_TCP_DEBUG
        Console.Error.WriteLine("edge: {0}, Entering Send",this);
#endif
        if( _is_closed ) {
          throw new EdgeException("Tried to send on a closed socket");
        }
        _last_out_packet_datetime = DateTime.UtcNow;
#if POB_TCP_DEBUG
        Console.Error.WriteLine("edge: {0}, About to enqueue packet of length: {1}",
                          this, p.Length);
#endif
        //Else just queue up the packet
	if( _packet_queue.Count < 30 ) {
          //Don't queue indefinitely...
          _packet_queue.Enqueue(p);
	}
	//Console.Error.WriteLine("Queue length: {0}", _packet_queue.Count);
      }
#if POB_TCP_DEBUG
      Console.Error.WriteLine("Setting NeedToSend");
#endif
      NeedToSend = true;
#if POB_TCP_DEBUG
      Console.Error.WriteLine("Need to send: {0}", NeedToSend);
      Console.Error.WriteLine("edge: {0}, Leaving Send",this);
#endif
    }
#endif

#if TCP_ASYNC
    //Here is the Overlapped IO Version
    public override void Send(Brunet.Packet p)
    {
      try {
        //compute the buffer representing the size
        ushort temp_length = (ushort) p.Length;
        byte[] tbuff = new byte[2];
        NumberSerializer.WriteShort((short) temp_length, tbuff, 0);
        //Send the size of the data  :
        lock(_sync) {
          if( _is_closed ) {
#if POB_TCP_DEBUG
            Console.Error.WriteLine("Exception edge: {0}",this);
#endif
            throw new EdgeException("Tried to send on a closed socket");
          }
          _last_out_packet_datetime = DateTime.UtcNow;
#if POB_TCP_DEBUG
          Console.Error.WriteLine("edge: {0}, BeginSend: {1}",this,p);
#endif

          if( _is_sending ) {
#if POB_TCP_DEBUG
            Console.Error.WriteLine("edge: {0}, Queueing",this);
#endif
            //We can't do two simulateous sends
            //System.Console.Error.WriteLine("queueing");
            //log.Error("Already sending, dropping: " + p.ToString() );
            //return;
            _packet_queue.Enqueue(p);
          }
          else {
            _is_sending = true;
            SendState state = new SendState();
            state.Buffer = tbuff;
            state.Offset = 0;
            state.Length = 2;
            state.SendingSize = true;
            state.PacketToSend = p;
#if POB_TCP_DEBUG
            Console.Error.WriteLine("edge: {0}, about to call BeginSend",this);
#endif
            _sock.BeginSend(state.Buffer, state.Offset, state.Length,
                            SocketFlags.None,
                            new AsyncCallback(ContinueSend),
                            state);
          }
        } //End of lock

      }
      catch(SocketException x) {
#if POB_TCP_DEBUG
        Console.Error.WriteLine("Exception edge: {0}, {1}",this,x);
#endif
        throw new EdgeException("Could not Send", x);
      }
#if POB_TCP_DEBUG
      Console.Error.WriteLine("edge: {0}, Leaving Send(Packet)",this);
#endif
    }
    //End of the Overlapped IO version
#endif
    /**
     * Start listening for packets and sending packet events when they
     * come
     */
    public void Start() {
#if TCP_POLL
      _poll_thread.Start();
#elif TCP_SELECT

#elif TCP_ASYNC
      lock(_sync) {
        ReceiveState state = new ReceiveState();
        state.Buffer = _size_buffer;
        state.Offset = 0;
        state.Length = 2;
        state.LastReadOffset = 0;
        state.LastReadLength = 2;
        state.ReadingSize = true;
        _sock.BeginReceive(state.Buffer,
                           state.LastReadOffset,
                           state.LastReadLength,
                           SocketFlags.None,
                           new AsyncCallback(ContinueRead),
                           state);
      }
#endif
    }

    public override Brunet.TransportAddress.TAType TAType
    {
      get
      {
        return Brunet.TransportAddress.TAType.Tcp;
      }
    }

    protected TransportAddress _local_ta;
    public override Brunet.TransportAddress LocalTA
    {
      get
      {
        return _local_ta;
      }
    }
    /*
     * If this is an inbound link
     * then the LocalTA is not ephemeral
     */
    public override bool LocalTANotEphemeral {
      get { return IsInbound; }
    }
    
    protected TransportAddress _remote_ta;
    public override Brunet.TransportAddress RemoteTA
    {
      get
      {
        return _remote_ta;
      }
    }

    /*
     * If this is an outbound link, which is to say
     * not inbound, then the RemoteTA is not ephemeral
     */
    public override bool RemoteTANotEphemeral {
      get { return !IsInbound; }
    }
    
    //********************
    //Protected Methods  :
#if TCP_ASYNC
    protected void ContinueRead(IAsyncResult ar)
    {
#if POB_TCP_DEBUG
      Console.Error.WriteLine("edge: {0}, In: ContinueRead",this);
#endif
      try {
        bool parse_packet = false;
        ReceiveState state;
        lock( _sync ) {
          int got_bytes = _sock.EndReceive(ar);
          if( got_bytes == 0) {
            //The socket closed
            Close();
            return;
          }
          state = (ReceiveState)ar.AsyncState;
          state.LastReadOffset += got_bytes;
          state.LastReadLength -= got_bytes;
          if( state.LastReadLength > 0 ) {
            //Keep reading
#if POB_TCP_DEBUG
            Console.Error.WriteLine("edge: {0}, Continuing to read",this);
#endif
            _sock.BeginReceive(state.Buffer,
                               state.LastReadOffset,
                               state.LastReadLength,
                               SocketFlags.None,
                               new AsyncCallback(ContinueRead),
                               state);
          }
          else {
            //We just finished reading:
            if( state.ReadingSize ) {
              //Now we know how long the packet is!
              ushort temp_length =
                (ushort) NumberSerializer.ReadShort(state.Buffer,
                                                    state.Offset);
#if POB_TCP_DEBUG
              Console.Error.WriteLine("edge: {0}, Just read size: {1}",this, temp_length);
#endif
              if( temp_length > 0 ) {
                byte[] packet_buffer = new byte[temp_length];
                state.Buffer = new byte[temp_length];
                state.Offset = 0;
                state.Length = temp_length;
                state.LastReadOffset = state.Offset;
                state.LastReadLength = state.Length;
                state.ReadingSize = false;
#if POB_TCP_DEBUG
                Console.Error.WriteLine("edge: {0}, About to read the packet",this);
#endif
                _sock.BeginReceive(state.Buffer,
                                   state.LastReadOffset,
                                   state.LastReadLength,
                                   SocketFlags.None,
                                   new AsyncCallback(ContinueRead),
                                   state);
              }
              else {
                //We just got a 0 length packet, huh?
#if POB_TCP_DEBUG
                Console.Error.WriteLine("edge: {0}, got zero length packet",this);
#endif
                Close();
                return;
              }
            }
            else {
              //We just got a packet!
              parse_packet = true;
            }
          }
        }//End of lock

        //We don't hold the lock across this event:
        if( parse_packet ) {
          Packet p = PacketParser.Parse(state.Buffer,
                                        state.Offset,
                                        state.Length);
#if POB_TCP_DEBUG
          Console.Error.WriteLine("edge: {0}, got packet {1}",this, p);
#endif
          ReceivedPacketEvent(p);
        }

        //Read the next packet:
        lock( _sync ) {
          if( parse_packet ) {
#if POB_TCP_DEBUG
            Console.Error.WriteLine("edge: {0}, Starting Next Packet Read",this);
#endif
            //Read the next length
            state.Buffer = _size_buffer;
            state.Offset = 0;
            state.Length = 2;
            state.LastReadOffset = 0;
            state.LastReadLength = 2;
            state.ReadingSize = true;
            _sock.BeginReceive(state.Buffer,
                               state.LastReadOffset,
                               state.LastReadLength,
                               SocketFlags.None,
                               new AsyncCallback(ContinueRead),
                               state);
          }
        }
      }
      catch(Exception x) {
#if POB_TCP_DEBUG
        Console.Error.WriteLine("edge: {0}, ContinueRead got exception {1}",this, x);
#endif
        //log.Error("ContinueRead Exception: edge:" + ToString(), x);
        Close();
      }
#if POB_TCP_DEBUG
      Console.Error.WriteLine("edge: {0}, Leaving ContinueRead",this);
#endif
    }

    protected void ContinueSend(IAsyncResult ar)
    {
#if POB_TCP_DEBUG
      Console.Error.WriteLine("edge: {0}, In: ContinueSend",this);
#endif
      lock(_sync) {

        try {
          int sent_bytes = _sock.EndSend(ar);
          if( sent_bytes == 0) {
            //This means the connection closed:
            Close();
            return;
          }
          else {
            SendState state = (SendState)ar.AsyncState;
            state.Offset += sent_bytes;
            state.Length -= sent_bytes;
            if( state.Length > 0 ) {
#if POB_TCP_DEBUG
              Console.Error.WriteLine("edge: {0}, sent {1} bytes, keep sending",this,
                                sent_bytes);
#endif
              //We have to keep sending
              _sock.BeginSend(state.Buffer,
                              state.Offset,
                              state.Length,
                              SocketFlags.None,
                              new AsyncCallback(ContinueSend),
                              state);

            }
            else {
              //We are done sending that part.
              if( state.SendingSize ) {
#if POB_TCP_DEBUG
                Console.Error.WriteLine("edge: {0}, just sent size, send packet now",this);
#endif
                //Now we must send the packet:
                state.SendingSize = false;
                Packet p = state.PacketToSend;
                state.Buffer = p.Buffer;
                state.Offset = p.Offset;
                state.Length = p.Length;
                _sock.BeginSend(state.Buffer,
                                state.Offset,
                                state.Length,
                                SocketFlags.None,
                                new AsyncCallback(ContinueSend),
                                state);
              }
              else {
                //We just finished sending the packet.
                Packet p = state.PacketToSend;
                /**
                  * logging of outgoing packets
                  */
                string base64String =
                  Convert.ToBase64String(p.Buffer,p.Offset,p.Length);
                string GeneratedLog = "OutPacket: edge: " + ToString()
                                      + ", packet: " + base64String;
#if POB_TCP_DEBUG
                Console.Error.WriteLine("edge: {0}, sent {1}",this, p);
#endif
                //log.Info(GeneratedLog);
                if( _packet_queue.Count > 0 ) {
#if POB_TCP_DEBUG
                  Console.Error.WriteLine("edge: {0}, sending the next packet in queue",this);
#endif
                  _is_sending = true;
                  //We can have a next packet to start sending:
                  p = (Packet)_packet_queue.Dequeue();

                  ushort temp_length = (ushort) p.Length;
                  byte[] tbuff = new byte[2];
                  NumberSerializer.WriteShort((short) temp_length, tbuff, 0);

                  state.Buffer = tbuff;
                  state.Offset = 0;
                  state.Length = 2;
                  state.SendingSize = true;
                  state.PacketToSend = p;

                  _sock.BeginSend(state.Buffer, state.Offset, state.Length,
                                  SocketFlags.None,
                                  new AsyncCallback(ContinueSend),
                                  state);
                }
                else {
                  _is_sending = false;
                }
              }
            }
          }
        }
        catch(Exception x) {
          //In this case, we close:
#if POB_TCP_DEBUG
          Console.Error.WriteLine("edge: {0}, got exception {1}",this, x);
#endif
          //log.Error("ContinueSend Exception: edge:" + ToString(), x);
          Close();
        }
      }
#if POB_TCP_DEBUG
      Console.Error.WriteLine("edge: {0}, Leaving ContinueSend",this);
#endif
    }
#endif


#if TCP_POLL
    protected void PollLoop()
    {
      try {
        bool reading_size = true;
        byte[] rsize_buf = new byte[2];
        byte[] ssize_buf = new byte[2];
        byte[] rpacket_buf = null;
        int rsize_pos = 0;
        int rpacket_pos = 0;

        while(!_is_closed)
        {
          lock( _sync ) {
            //Ever 100 ms, or 10 times a second
            if( _sock.Poll(100000, SelectMode.SelectRead) ) {
              if( reading_size ) {
                int got = _sock.Receive(rsize_buf,
                                        rsize_pos,
                                        2 - rsize_pos,
                                        SocketFlags.None);
                rsize_pos += got;
                if(got == 0) {
                  //The edge is closed
                  throw new Exception("Edge Closed");
                }

                if( rsize_pos == rsize_buf.Length ) {
                  //Here is the size:
                  ushort temp_length =
                    (ushort) NumberSerializer.ReadShort(rsize_buf, 0);
                  rpacket_buf = new byte[temp_length];
                  rpacket_pos = 0;
                  reading_size = false;
                }
              }
              else {
                //We are reading the packet:
                int got = _sock.Receive(rpacket_buf,
                                        rpacket_pos,
                                        rpacket_buf.Length - rpacket_pos,
                                        SocketFlags.None);
                rpacket_pos += got;
                if(got == 0) {
                  //The edge is closed
                  throw new Exception("Edge Closed");
                }
                if( rpacket_pos == rpacket_buf.Length ) {
                  Packet p = PacketParser.Parse(rpacket_buf);
#if POB_TCP_DEBUG
                  Console.Error.WriteLine("edge: {0}, got packet {1}",this, p);
#endif
                  ReceivedPacketEvent(p);
                  //Start reading the size of the next packet:
                  reading_size = true;
                  rsize_pos = 0;
                  rpacket_buf = null;
                }
              }
            }
            //Check to see if there are any packets to send:
#if POB_TCP_DEBUG
            Console.Error.WriteLine("edge: {0}, About to send all the packets in queue",this);
#endif
#if POB_TCP_DEBUG
            Console.Error.WriteLine("edge: {0}, Queue Count: {1}",this, _packet_queue.Count);
#endif
            while( _packet_queue.Count > 0 ) {
              Packet p = (Packet)_packet_queue.Dequeue();
#if POB_TCP_DEBUG
              Console.Error.WriteLine("edge: {0}, There is a packet to send length: {1}",
                                this, p.Length);
#endif
              NumberSerializer.WriteShort((short)p.Length, ssize_buf, 0);
              SendBuffer(ssize_buf, 0, 2);
              SendBuffer(p.Buffer, p.Offset, p.Length);
            }
          }
        }
      }
      catch(ThreadAbortException tae) {
        //Looks like it is time to stop
      }
      catch(Exception x) {
        Close();
      }
    }

    /**
     * Keeps sending until the whole buffer is sent.
     * @throws Exception if it does not work.
     */
    protected void SendBuffer(byte[] buf, int offset, int length)
    {
      lock(_sync) {
        while(length > 0) {
          int got = _sock.Send(buf, offset, length, SocketFlags.None);
          if( got == 0 ) {
            throw new Exception("Socket is closed");
          }
          offset += got;
          length -= got;
        }
      }
    }
#endif


#if TCP_SELECT
    /**
     * In the select implementation, the TcpEdgeListener
     * will tell a socket when to do a send 
     */
    public void DoSend()
    {
      try {
#if POB_TCP_DEBUG
        Console.Error.WriteLine("edge: {0} in DoSend", this);
#endif
        bool need_to_send = false;
        lock(_sync) {
          if( _send_state.Length == 0 && _packet_queue.Count > 0 ) {
            //It is time to get a new packet
            ICopyable p = (ICopyable)_packet_queue.Dequeue();
            NumberSerializer.WriteShort((short)p.Length,_send_state.Buffer, 0);
            _send_state.Offset = 0;
            _send_state.Length = p.Length + 2;
            p.CopyTo( _send_state.Buffer, 2 );
#if PLAB_RDP_LOG
	    //Console.Error.WriteLine("*******In TcpEdge.DoSend() function");
	    if(p.type == Packet.ProtType.AH){	    
		//Console.Error.WriteLine("ProtoType is AH in DoSend()");
	        AHPacket ahp = (AHPacket)p;
	        if(ahp.PayloadType == AHPacket.Protocol.Echo && ahp.Source.Equals(_logger.LocalAHAddress)
				&& p.PayloadStream.ToArray()[0] > 0 && p.PayloadStream.ToArray()[1] == 0){
    		    //Console.Error.WriteLine("Type is Echo in DoSend()");
		    _logger.LogBrunetPing(p, false); 
	        }
	    }
#endif
#if PLAB_PACKET_LOG
	    _logger.LogPacketTimeStamp(p, false); //logging the packet sent, false because it is sent
#endif
          }

          if( _send_state.Length > 0 ) {
            int sent = _sock.Send(_send_state.Buffer,
                                  _send_state.Offset,
                                  _send_state.Length,
                                  SocketFlags.None);
            if( sent > 0 ) {
              _send_state.Offset += sent;
              _send_state.Length -= sent;
            }
            else {
              //The edge is now closed.
#if POB_TCP_DEBUG
              Console.Error.WriteLine("{0} sent: {1}", this, sent);
#endif
              throw new Exception("Edge is closed");
            }
          }
          need_to_send = ( _send_state.Length > 0 ||
                           _packet_queue.Count > 0 );
        }
        //Be careful not to hold the lock here, because this sends an event
        NeedToSend = need_to_send;
      }
      catch(Exception) {
        Close();
      }
    }

    /**
     * In the select implementation, the TcpEdgeListener
     * will tell a socket when to do a read.
     * Get at most one packet out.
     */
    public void DoReceive()
    {
      try {
#if POB_TCP_DEBUG
        Console.Error.WriteLine("edge: {0} in DoReceive", this);
#endif
        MemBlock p = null;
        lock(_sync) {
          int got = _sock.Receive(_rec_state.Buffer,
                                  _rec_state.LastReadOffset,
                                  _rec_state.LastReadLength,
                                  SocketFlags.None);
#if POB_TCP_DEBUG
          Console.Error.WriteLine("{0} got: {1}", this, got);
#endif
          if( got == 0 ) {
            Close();
            return;
          }
          _rec_state.LastReadOffset += got;
          _rec_state.LastReadLength -= got;


          bool parse_packet = false;

          if( _rec_state.LastReadLength == 0 ) {
            //Something is ready to parse
            if( _rec_state.ReadingSize ) {
              short size = NumberSerializer.ReadShort(_rec_state.Buffer, 0);
              //Reinitialize the rec_state
              _rec_state.Offset = 0;
              _rec_state.Length = size;
              _rec_state.LastReadOffset = 0;
              _rec_state.LastReadLength = size;
              _rec_state.ReadingSize = false;

              if( _sock.Available > 0 ) {
                got = _sock.Receive( _rec_state.Buffer,
                                     _rec_state.LastReadOffset,
                                     _rec_state.LastReadLength,
                                     SocketFlags.None);
#if POB_TCP_DEBUG
                Console.Error.WriteLine("{0} got: {1}", this, got);
#endif
                if( got == 0 ) {
                  Close();
                  return;
                }
                _rec_state.LastReadOffset += got;
                _rec_state.LastReadLength -= got;

                if( _rec_state.LastReadLength == 0 ) {
                  parse_packet = true;
                }
              }
            }
            else {
              //We are reading a whole packet:
              parse_packet = true;
            }

            if( parse_packet ) {
              //We have the whole packet
              p = MemBlock.Copy(_rec_state.Buffer, _rec_state.Offset, _rec_state.Length);
#if POB_TCP_DEBUG
              //Console.Error.WriteLine("edge: {0}, got packet {1}",this, p);
#endif
              //Reinit the rec_state
              _rec_state.Offset = 0;
              _rec_state.Length = 2;
              _rec_state.LastReadOffset = 0;
              _rec_state.LastReadLength = 2;
              _rec_state.ReadingSize = true;
              //Now we just finish and wait till next time to start reading
            }
          }
          else {
            //There is more to read, we have to wait until it is here!
#if POB_TCP_DEBUG
            Console.Error.WriteLine("edge: {0}, can't read",this);
#endif
          }
        }
        if( p != null ) {
          //We don't hold the lock while we announce the packet
          ReceivedPacketEvent(p);
        }
#if POB_TCP_DEBUG
        Console.Error.WriteLine("edge: {0} out of DoReceive", this);
#endif
      }
      catch(Exception) {
        Close();
      }

    }
#endif

  }
}
