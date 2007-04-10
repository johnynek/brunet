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

  public abstract class Edge:IComparable, IPacketSender
  {

    public Edge()
    {
      _callbacks = new IPacketHandler[ Byte.MaxValue ];
      _have_fired_close = false;
    }
    /**
     * Adding logger
     */
    /*private static readonly log4net.ILog log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/

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
      for(int i = 0; i < _callbacks.Length; i++) {
        _callbacks[i] = null;
      }
      if (! _have_fired_close ) {
        //log.Warn("EdgeClose: edge: " + ToString());
#if POB_DEBUG
        Console.Error.WriteLine("EdgeClose: edge: {0}", this);
#endif
        /*
         * Set to true *BEFORE* firing the event since some of
         * the EventHandler objects may call close on the Edge.
         * They shouldn't, but who knows if people follow the rules.
         */
        _have_fired_close = true;
        if (CloseEvent != null) {
          CloseEvent(this, null);
        }
      }
      ///@todo it would be nice to clear the events to clear the references.
    }

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
      /**
       * For each Packet.ProtType, there may be a callback set
       * for it.  This list holds that mapping.
       */
    protected IPacketHandler[] _callbacks;
    /**
     * When an edge is closed (either due to the Close method
     * being called or due to some error during the receive loop)
     * this event is fired.
     */
    public event EventHandler CloseEvent;

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
    public void Send(Packet p) { Send( (ICopyable)p); }
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
      get { return _last_in_packet_datetime; }
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

        public virtual void ClearCallback(Packet.ProtType t)
      {
        _callbacks[(byte)t] = null;
      }

    public int CompareTo(object e)
    {
      if (e is Edge) {
        Edge edge = (Edge) e;
        int local_cmp = this.LocalTA.CompareTo(edge.LocalTA);
        if (local_cmp == 0) {
          return this.RemoteTA.CompareTo(edge.RemoteTA);
        }
        else {
          return local_cmp;
        }
      }
      else {
        return -1;
      }
    }

    /**
     * This method is used by subclasses.
     * @param p the packet to send a ReceivedPacket event for
     */
    protected void ReceivedPacketEvent(Brunet.Packet p)
    {
      if( IsClosed ) {
        //We should not be receiving packets on closed edges:
        // Comment this out for now until we are debugging with
        // FunctionEdge.
        //throw new EdgeException("Trying to Receive on a Closed Edge");
      }
      /**
       * logging of incoming packets
       */
      //string GeneratedLog = " a new packet was recieved on this edge ";
#if LOG_PACKET
      string base64String;
      try {
        base64String =
          System.Convert.ToBase64String(p.Buffer,p.Offset,p.Length);
        string GeneratedLog = "InPacket: edge: " + ToString() + ", packet: "
                              + base64String;
        //log.Info(GeneratedLog);
        // logging finished
      }
      catch (System.ArgumentNullException){
        //log.Error("Error: Packet is Null");
      }
#endif
      IPacketHandler cb = _callbacks[(byte)p.type];
      if ( cb != null ) {
        _last_in_packet_datetime = DateTime.UtcNow;
        cb.HandlePacket(p, this);
      }
      else {
        //We don't record the time of this packet.  We don't
        //want unhandled packets to keep edges open.
        //
        //This packet is going into the trash:
        //log.Error("Packet LOST: " + p.ToString());
#if DEBUG
        Console.Error.WriteLine("{0} lost packet {1}",this,p);
#endif

      }
    }

    /**
     * This sets a callback
     */
    public virtual void SetCallback(Packet.ProtType t, IPacketHandler cb)
    {
      _callbacks[(byte)t] = cb;
    }
    /**
     * Prints the local address, the direction and the remote address
     */
    public override string ToString()
    {
      string direction;
      if( IsInbound ) {
        direction = " <- ";
      }
      else {
        direction = " -> ";
      }
      return "local: " + LocalTA.ToString() +
             direction + "remote: " + RemoteTA.ToString();
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
