/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>,  University of Florida

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

/**
 * Logging packets is expensive because they must be converted to
 * base64 to be printable in the log.
 * If we need a complete packet log, define LOG_PACKET
 */
//#define LOG_PACKET
//#define POB_DEBUG

using System;
using System.Collections;

namespace Brunet
{
  /**
   * Abstract base class used for all Edges.  Manages
   * the sending of Packet objects and sends and event
   * when a Packet arrives.
   */

  public abstract class Edge : IComparable, ISender, ISource
  {
    protected static long _edge_count;

    static Edge() {
      _edge_count = 0;
    }

    public Edge()
    {
      _sync = new object();
      _have_fired_close = false;
      //Atomically increment and update _edge_no
      _edge_no = System.Threading.Interlocked.Increment( ref _edge_count );
    }

    protected class Sub {
      public readonly IDataHandler Handler;
      public readonly object State;
      public Sub(IDataHandler h, object s) { Handler = h; State =s; }
      public void Handle(MemBlock b, ISender f) { Handler.HandleData(b, f, State); }
    }
    protected volatile Sub _sub;
    protected object _sync;
    /**
     * Set to true once CloseEvent is fired.  This prevents it from
     * being fired twice
     */
    protected bool _have_fired_close;
    /**
     * Closes the Edge, further Sends are not allowed
     */
    public virtual void Close()
    {
      /*
       * This makes sure CloseEvent is null after the first
       * call, this guarantees that there is only one CloseEvent
       */
      EventHandler ch = null;
      lock( _sync ) {
        if( !_have_fired_close ) {
          _have_fired_close = true;
          ch = _close_event;
          _close_event = null;
        }
      }
      if( ch != null ) { ch(this, null); }
#if POB_DEBUG
      Console.Error.WriteLine("EdgeClose: edge: {0}", this);
#endif
    }

    protected readonly long _edge_no;
    /**
     * Each time an Edge is created on a node, it is
     * assigned a unique number.  This is that number
     */
    public long Number { get { return _edge_no; } }

    public abstract Brunet.TransportAddress LocalTA
    {
      get;
    }

    /**
     * @return true if a peer CAN connect to the LocalTA.
     * For some edges, one end of the edge is ephemeral (TCP for instance).
     * If it is not ephemeral, this TA can be shared with peers
     * to connect to us
     */
    public virtual bool LocalTANotEphemeral { get { return false; } }
    
    protected EventHandler _close_event;
    /**
     * When an edge is closed (either due to the Close method
     * being called or due to some error during the receive loop)
     * this event is fired.
     *
     * If the CloseEvent has already been fired, adding a handler will
     * throw an EdgeException.
     */
    public event EventHandler CloseEvent {
      add {
        lock(_sync) {
          if( !_have_fired_close ) {
            _close_event = (EventHandler)Delegate.Combine(_close_event, value);
          }
          else {
            // We've already fired the close event!!
            throw new EdgeException(String.Format("Edge: {0} already fired CloseEvent",this));
          }
        }
      }

      remove {
        lock(_sync) {
          _close_event = (EventHandler)Delegate.Remove(_close_event, value);
        }
      }
    }

    public abstract Brunet.TransportAddress RemoteTA
    {
      get;
    }
    /**
     * @return true if a peer CAN connect on this RemoteTA.
     * For some edges, one end of the edge is ephemeral (TCP for instance).
     * If it is not ephemeral, this TA can be shared with peers
     * or stored to disk to connect to a peer later.
     */
    public virtual bool RemoteTANotEphemeral { get { return false; } }


    public abstract Brunet.TransportAddress.TAType TAType
    {
      get;
    }

    /**
     * @param p a Packet to send to the host on the other
     * side of the Edge.
     * @throw EdgeException if any problem happens
     */
    public abstract void Send(ICopyable p);

    /**
     * This is the time (in UTC) when the edge
     * was created.
     */
    public abstract DateTime CreatedDateTime { get; }

    /**
     * The DateTime of the last received packet (in UTC)
     */
    public abstract DateTime LastOutPacketDateTime {
      get;
    }

    protected DateTime _last_in_packet_datetime;
    /**
     * The DateTime (UTC) of the last received packet
     */
    public virtual DateTime LastInPacketDateTime {
      get { lock( _sync ) { return _last_in_packet_datetime; } }
    }

    public abstract bool IsClosed
    {
      get;
    }

   /**
    * @return true if the edge is an in-degree
    */
    public abstract bool IsInbound
    {
      get;
    }

    public int CompareTo(object e)
    {
      if( Equals(e) ) { return 0; }
      if (e is Edge) {
        Edge edge = (Edge) e;
        if( this.Number < edge.Number ) { return -1; }
        else { return 1; }
      }
      else {
        return -1;
      }
    }

    /**
     * This method is used by subclasses.
     * @param b the packet to send a ReceivedPacket event for
     */
    protected void ReceivedPacketEvent(MemBlock b)
    {
      if( IsClosed ) {
        //We should not be receiving packets on closed edges:
        // Comment this out for now until we are debugging with
        // FunctionEdge.
        throw new EdgeException(
                      String.Format("Trying to Receive on a Closed Edge: {0}",
                                    this) );
      }
      /**
       * logging of incoming packets
       */
      //string GeneratedLog = " a new packet was recieved on this edge ";
#if LOG_PACKET
      string base64String;
      try {
        base64String = b.ToBase64String();
        string GeneratedLog = "InPacket: edge: " + ToString() + ", packet: "
                              + base64String;
        //log.Info(GeneratedLog);
        // logging finished
      }
      catch (System.ArgumentNullException){
        //log.Error("Error: Packet is Null");
      }
#endif
      //_sub is volatile, so there is no chance for a race here 
      Sub s = _sub;
      if( s != null ) {
        s.Handle(b, this);
        lock( _sync ) { _last_in_packet_datetime = DateTime.UtcNow; }
      }
      else {
        //We don't record the time of this packet.  We don't
        //want unhandled packets to keep edges open.
        //
        //This packet is going into the trash:
        //log.Error("Packet LOST: " + p.ToString());
          Console.Error.WriteLine("{0} lost packet {1}",this,b.ToBase16String());
      }
    }

    public virtual void Subscribe(IDataHandler hand, object state) {
      _sub = new Sub(hand, state);
    }
    public virtual void Unsubscribe(IDataHandler hand) {
      if( _sub.Handler == hand ) {
        _sub = null;
      }
      else {
        throw new Exception(String.Format("Handler: {0}, not subscribed", hand));
      }
    }
    /**
     * Prints the local address, the direction and the remote address
     */
    public override string ToString()
    {
      if( IsInbound ) {
        return String.Format("local: {0} <- remote: {1}, num: {2}", LocalTA, RemoteTA, Number);
      }
      else {
        return String.Format("local: {0} -> remote: {1}, num: {2}", LocalTA, RemoteTA, Number);
      }
    }
  }
  
  /**
   * Interfaces are much faster than delegates.
   * Since every packet must be handled, we use
   * an interface to represent handlers
   */
  public interface IEdgeSendHandler {
    void HandleEdgeSend(Edge from, ICopyable data);
  }
}
