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

  public class TcpEdge : Brunet.Edge
  {

    /**
     * Represents the state of the packet sending
     */
    private class SendState {
      /* Represents the part of the buffer we just tried to send */
      public byte[] Buffer;
      public int Offset;
      public int Length;
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
      public void Reset(byte[] buf, int off, int length, bool readingsize) {
        Buffer = buf;
        Offset = off;
        Length = length;
        LastReadOffset = off;
        LastReadLength = length;
        ReadingSize = readingsize;
      }
    }

    /**
     * Adding logger
     */
    /*private static readonly log4net.ILog log =
      log4net.LogManager.GetLogger(System.Reflection.MethodBase.
      GetCurrentMethod().DeclaringType);*/

    protected readonly Socket _sock;
    public Socket Socket { get { return _sock; } }
    protected readonly bool inbound;
    protected bool _is_closed;

    protected bool _need_to_send;
    protected readonly TcpEdgeListener _tel;
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

    /** 
     * Send(Packet) is called faster than we can send
     * the packets over the socket, we place them in
     * queue
     */
    protected readonly Queue _packet_queue;

    /**
     * These objects
     * keep track of the state
     * These are only accessed by the DoSend and DoReceive methods,
     * which are only called in the socket thread of TcpEdgeListener,
     * there is no need to lock before getting access to them.
     */
    private volatile SendState _send_state;
    private volatile ReceiveState _rec_state;

    public TcpEdge(Socket s, bool is_in, TcpEdgeListener tel) {
      _sock = s;
      _is_closed = false;
      _create_dt = DateTime.UtcNow;
      _last_out_packet_datetime = _create_dt;
      _last_in_packet_datetime = _last_out_packet_datetime;
      _packet_queue = new Queue();
      _need_to_send = false;
	_tel = tel;
      inbound = is_in;
      _local_ta = TransportAddressFactory.CreateInstance(TAType,
                             (IPEndPoint) _sock.LocalEndPoint);
      _remote_ta = TransportAddressFactory.CreateInstance(TAType,
                             (IPEndPoint) _sock.RemoteEndPoint);
      //We use Non-blocking sockets here
      s.Blocking = false;
    }

    /**
     * Closes the edges IF it is not already closed, otherwise do nothing
     */
    public override void Close()
    {
#if POB_TCP_DEBUG
      Console.Error.WriteLine("edge: {0}, Closing",this);
#endif
      bool shutdown = false;
      lock( _sync ) {
        if (!_is_closed) {
          _is_closed = true;
          shutdown = true;
        }
      }
      if( shutdown ) {
        try {
          //We don't want any more data, but try
          //to send the stuff we've sent:
          if( _sock.Connected ) {
            _sock.Shutdown(SocketShutdown.Send);
          }
          else {
            //There is no need to shutdown a socket that
            //is not connected.
          }
        }
        catch(Exception) {
          //Console.Error.WriteLine("Error shutting down socket on edge: {0}\n{1}", this, ex);
        }
        finally {
          _sock.Close();
        }
      }
      //Don't hold the lock while we close:
      base.Close();
    }

    public override bool IsClosed
    {
      get { lock(_sync) { return _is_closed; } }
    }
    public override bool IsInbound
    {
      get { return inbound; }
    }

    protected readonly DateTime _create_dt;
    public override DateTime CreatedDateTime {
      get { return _create_dt; }
    }
    protected DateTime _last_out_packet_datetime;
    public override DateTime LastOutPacketDateTime {
      get { lock( _sync ) { return _last_out_packet_datetime; } }
    }
    /**
     * @param p the Packet to send
     * @throw EdgeException if we cannot send
     */
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

    public override Brunet.TransportAddress.TAType TAType
    {
      get { return Brunet.TransportAddress.TAType.Tcp; }
    }

    protected readonly TransportAddress _local_ta;
    public override Brunet.TransportAddress LocalTA
    {
      get { return _local_ta; }
    }
    /*
     * If this is an inbound link
     * then the LocalTA is not ephemeral
     */
    public override bool LocalTANotEphemeral {
      get { return IsInbound; }
    }
    
    protected readonly TransportAddress _remote_ta;
    public override Brunet.TransportAddress RemoteTA
    {
      get { return _remote_ta; }
    }

    /*
     * If this is an outbound link, which is to say
     * not inbound, then the RemoteTA is not ephemeral
     */
    public override bool RemoteTANotEphemeral {
      get { return !IsInbound; }
    }
    
    /**
     * In the select implementation, the TcpEdgeListener
     * will tell a socket when to do a send 
     */
    public void DoSend(BufferAllocator buf)
    {
      try {
#if POB_TCP_DEBUG
        Console.Error.WriteLine("edge: {0} in DoSend", this);
#endif
        if( _send_state == null ) {
          _send_state = new SendState();
          _send_state.Length = 0;
        }
        //Must lock before we access the _packet_queue
        bool pq_has_more = false;
        lock(_sync) {
          if( _send_state.Length == 0 ) {
            /*
             * Let's try to get as many packets as will fit into
             * a buffer written:
             */
            int current_offset = buf.Offset;
            //Set up the _send_state buffer:
            _send_state.Buffer = buf.Buffer;
            _send_state.Offset = current_offset;
            bool cont_writing = (_packet_queue.Count > 0);
            while( cont_writing ) {
              //It is time to get a new packet
              ICopyable p = (ICopyable)_packet_queue.Dequeue();
              short p_length = (short)p.Length;
              //Now write into this buffer:
              NumberSerializer.WriteShort(p_length, _send_state.Buffer, current_offset);
              p.CopyTo( _send_state.Buffer, 2 + current_offset );
              int written = 2 + p_length;
              current_offset += written;
              _send_state.Length += written;
              
              cont_writing = (_packet_queue.Count > 0);
              if( cont_writing ) {
                ICopyable next = (ICopyable)_packet_queue.Peek();
                if( next.Length + _send_state.Length > buf.Capacity ) {
                  /* 
                   * There is no room for the next packet, just stop now
                   */
                  cont_writing = false;
                }
              }
            }
            //Advance the BufferAllocator so we don't reuse this space
            buf.AdvanceBuffer( _send_state.Length );
          }
          pq_has_more = (_packet_queue.Count > 0);
        }

        if( _send_state.Length > 0 ) {
          int sent = _sock.Send(_send_state.Buffer,
                                _send_state.Offset,
                                _send_state.Length,
                                SocketFlags.None);
#if POB_TCP_DEBUG
          Console.Error.WriteLine("{0} sent: {1}", this, sent);
#endif
          if( sent > 0 ) {
            _send_state.Offset += sent;
            _send_state.Length -= sent;
          }
          else {
            //The edge is now closed.
            throw new EdgeException("Edge is closed");
          }
        }
        NeedToSend = ( pq_has_more || _send_state.Length > 0 );
      }
      catch {
        Close();
      }
    }

    /**
     * In the select implementation, the TcpEdgeListener
     * will tell a socket when to do a read.
     * Get at most one packet out.
     */
    public void DoReceive(BufferAllocator buf)
    {
      MemBlock p = null;
      try {
#if POB_TCP_DEBUG
        Console.Error.WriteLine("edge: {0} in DoReceive", this);
#endif
        //Reinitialize the rec_state
        if( _rec_state == null ) {
          _rec_state = new ReceiveState();
          _rec_state.Reset(buf.Buffer, buf.Offset, 2, true);
          buf.AdvanceBuffer(2);
        }
        int got = _sock.Receive(_rec_state.Buffer,
                                _rec_state.LastReadOffset,
                                _rec_state.LastReadLength,
                                SocketFlags.None);
#if POB_TCP_DEBUG
        Console.Error.WriteLine("{0} got: {1}", this, got);
#endif
        if( got == 0 ) {
          throw new EdgeException("Got zero bytes, this edge is closed");  
        }
        _rec_state.LastReadOffset += got;
        _rec_state.LastReadLength -= got;


        bool parse_packet = false;

        if( _rec_state.LastReadLength == 0 ) {
          //Something is ready to parse
          if( _rec_state.ReadingSize ) {
            short size = NumberSerializer.ReadShort(_rec_state.Buffer, _rec_state.Offset);
            if( size < 0 ) { Console.Error.WriteLine("ERROR: negative packet size: {0}", size); }
            //Reinitialize the rec_state
            _rec_state.Reset(buf.Buffer, buf.Offset, size, false);
            buf.AdvanceBuffer(size);
            
            if( _sock.Available > 0 ) {
              got = _sock.Receive( _rec_state.Buffer,
                                   _rec_state.LastReadOffset,
                                   _rec_state.LastReadLength,
                                   SocketFlags.None);
#if POB_TCP_DEBUG
              Console.Error.WriteLine("{0} got: {1}", this, got);
#endif
              if( got == 0 ) {
                throw new EdgeException("Got zero bytes, this edge is closed");  
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
            p = MemBlock.Reference(_rec_state.Buffer, _rec_state.Offset, _rec_state.Length);
#if POB_TCP_DEBUG
            //Console.Error.WriteLine("edge: {0}, got packet {1}",this, p);
#endif
            //Reinitialize the rec_state
            _rec_state.Reset(buf.Buffer, buf.Offset, 2, true);
            buf.AdvanceBuffer(2);
            //Now we just finish and wait till next time to start reading
          }
        }
        else {
          //There is more to read, we have to wait until it is here!
#if POB_TCP_DEBUG
          Console.Error.WriteLine("edge: {0}, can't read",this);
#endif
        }
#if POB_TCP_DEBUG
        Console.Error.WriteLine("edge: {0} out of DoReceive", this);
#endif
        if( p != null ) {
          //We don't hold the lock while we announce the packet
          ReceivedPacketEvent(p);
        }
      }
      catch {
        Close();
      }
    }
  }
}
