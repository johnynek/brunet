/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>,  University of Florida

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


using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using BU = Brunet.Util;
using BM = Brunet.Messaging;
using Brunet.Concurrent;
using BCol = Brunet.Collections;
using Brunet.Messaging;
namespace Brunet.Transport
{
  /**
   * Abstract base class used for all Edges.  Manages
   * the sending of Packet objects and sends and event
   * when a Packet arrives.
   */

  public abstract class Edge : BM.SimpleSource, IComparable, BM.ISender
  {
    // ///////////////
    // Inner classes
    // ///////////////
    
    /*** For use with IActionQueue 
     * Add this event to put a Close action into another thread/actionqueue
     */
    public class CloseAction : IAction {
      public readonly Edge ToClose;
      public CloseAction(Edge e) {
        ToClose = e;
      }
      public void Start() {
        ToClose.Close();
      }
    }
    /*
     * Static code stuffs
     */
    private static long _edge_count;
    /*
     * We use a weakreference to make sure this doesn't keep items in scope
     * if there are unclosed edges.
     */
    private readonly static BCol.WeakValueTable<long, Edge> _num_to_edge;

    static Edge() {
      _edge_count = 0;
      _num_to_edge = new BCol.WeakValueTable<long, Edge>();
      SenderFactory.Register("edge", CreateInstance);
    }
    protected static long AllocEdgeNum(Edge e) {
      long result;
      lock( _num_to_edge ) {
        result = ++_edge_count;
        _num_to_edge.Add(result, e);
      }
      return result;
    }
    /** Get the edge with the given edge number.
     * @throws Exception if there is no (non-closed) edge with this number
     */
    public static Edge GetEdgeNum(long num) {
      Edge e = _num_to_edge.GetValue(num);
      if( e == null ) {
        throw new Exception(String.Format("No Edge with number: {0}", num));
      }
      return e;
    }
    /** Return the edge specified in the given URI
     * this matches the SenderFactory
     */
    public static Edge CreateInstance(object n, string uri) {
      string scheme;
      IDictionary<string, string> args = SenderFactory.DecodeUri(uri, out scheme);
      return GetEdgeNum( Int64.Parse( args["num"] ) );
    }

    protected static void ReleaseEdgeNum(long num) {
      lock( _num_to_edge ) {
        _num_to_edge.Remove(num);
      }
    }
    //Non-statics...

    protected Edge()
    {
      _sync = new object();
      _close_event = new FireOnceEvent();
      _edge_no = AllocEdgeNum(this);
      _is_closed = 0;
      _create_dt = DateTime.UtcNow;
      _last_out_packet_datetime = _create_dt.Ticks;
      _last_in_packet_datetime = _last_out_packet_datetime;
    }

    protected Edge(IEdgeSendHandler esh, bool is_in) : this()
    {
      _send_cb = esh;
      IsInbound = is_in;
    }

    /**
     * Closes the Edge, further Sends are not allowed
     * @return true if this is the first time Close is called
     */
    public virtual bool Close()
    {
      if( Interlocked.Exchange(ref _is_closed, 1) == 0 ) {
        //Make sure we don't keep a reference around to this edge
        ReleaseEdgeNum(_edge_no);
      }
#if POB_DEBUG
      Console.Error.WriteLine("EdgeClose: edge: {0}", this);
#endif
      return _close_event.Fire(this, null);
    }

    protected readonly long _edge_no;
    /**
     * Each time an Edge is created on a node, it is
     * assigned a unique number.  This is that number
     */
    public long Number { get { return _edge_no; } }

    public abstract TransportAddress LocalTA
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
    
    private FireOnceEvent _close_event;
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
        try {
          _close_event.Add(value);
        }
        catch {
          // We've already fired the close event!!
          throw new EdgeException(
            String.Format("Edge: {0} already fired CloseEvent",this));
        }
      }

      remove {
        _close_event.Remove(value);
      }
    }

    public abstract TransportAddress RemoteTA
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

    /*
     * May be null, but many Edge types delegate sending to the
     * EdgeListener, in those cases the Send method is always the
     * same.  We have this here for that case.
     */
    protected readonly IEdgeSendHandler _send_cb;

    public abstract TransportAddress.TAType TAType
    {
      get;
    }


    protected readonly DateTime _create_dt;
    /**
     * This is the time (in UTC) when the edge
     * was created.
     */
    public DateTime CreatedDateTime { get { return _create_dt; } }

    protected long _last_out_packet_datetime;
    /**
     * The DateTime of the last received packet (in UTC)
     */
    public DateTime LastOutPacketDateTime {
      get { return new DateTime(Interlocked.Read(ref _last_out_packet_datetime)); }
    }

    /*
     * We use Interlocked and convert the DateTime to a long
     */
    protected long _last_in_packet_datetime;
    /**
     * The DateTime (UTC) of the last received packet
     */
    public DateTime LastInPacketDateTime {
      get { return new DateTime(Interlocked.Read(ref _last_in_packet_datetime)); }
    }

    protected int _is_closed;
    public bool IsClosed
    {
      get { return 1 == _is_closed; }
    }

   /**
    * @return true if the edge is an in-degree
    */
    public readonly bool IsInbound;

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
    public void ReceivedPacketEvent(BU.MemBlock b)
    {
      if( 1 == _is_closed ) {
        //We should not be receiving packets on closed edges:
        throw new EdgeClosedException(
                      String.Format("Trying to Receive on a Closed Edge: {0}",
                                    this) );
      }
      try {
        _sub.Handle(b, this);
        Interlocked.Exchange(ref _last_in_packet_datetime, DateTime.UtcNow.Ticks);
      }
      catch(System.NullReferenceException) {
        //this can happen if _sub is null
        //We don't record the time of this packet.  We don't
        //want unhandled packets to keep edges open.
        //
        //This packet is going into the trash:
//        Console.Error.WriteLine("{0} lost packet {1}",this,b.ToBase16String());
      }
    }
    /**
     * @param p a Packet to send to the host on the other
     * side of the Edge.
     * @throw EdgeException if any problem happens
     */
    public virtual void Send(BU.ICopyable p) {
      if( 1 == _is_closed ) {
        throw new EdgeClosedException(
                    String.Format("Tried to send on a closed edge: {0}", this));
      }
      _send_cb.HandleEdgeSend(this, p);
      Interlocked.Exchange(ref _last_out_packet_datetime, DateTime.UtcNow.Ticks);
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
    
    public string ToUri() {
      return String.Format("sender:edge?num={0}", _edge_no);
    }
    
  }
  
  /**
   * Interfaces are much faster than delegates.
   * Since every packet must be handled, we use
   * an interface to represent handlers
   */
  public interface IEdgeSendHandler {
    void HandleEdgeSend(Edge from, BU.ICopyable data);
  }
}
