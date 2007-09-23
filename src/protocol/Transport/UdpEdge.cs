/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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

using System;
using System.Collections;
using System.Net;

namespace Brunet
{

  /**
  * A Edge which does its transport over the Udp protocol.
  * The UDP protocol is really better for Brunet.
  */

  public class UdpEdge : Edge
  {
    protected readonly bool inbound;
    protected volatile bool _is_closed;
    protected IEdgeSendHandler _send_cb;

    protected System.Net.EndPoint end;
    /**
     * This is the IPEndPoint for this UdpEdge.
     * No one other than the EdgeListeners that created
     * this edge should access this.
     */
    public System.Net.EndPoint End {
      get {
        lock( _sync ) {
          return end;
        }
      }
      set {
        lock( _sync ) {
          end = value;
          _remoteta = TransportAddressFactory.CreateInstance(TAType, (IPEndPoint) end);
        }
      }
    }

    protected readonly int _id;
    public int ID { get { return _id; } }

    protected int _remoteid;
    public int RemoteID {
      get { lock( _sync ) { return _remoteid; } }
      set { lock( _sync ) { _remoteid = value; } }
    }
    
    /**
     * The send_cb is the method which actually does the
     * sending (which is in UdpEdgeListener).
     */
    public UdpEdge(IEdgeSendHandler send_cb,
                   bool is_in,
                   System.Net.IPEndPoint remote_end_point,
                   System.Net.IPEndPoint local_end_point,
                   int id, int remoteid)
    {
      _send_cb = send_cb;
      inbound = is_in;
      _is_closed = false;
      _create_dt = DateTime.UtcNow;
      _last_out_packet_datetime = _create_dt;
      _last_in_packet_datetime = _last_out_packet_datetime;
      //This will update both the end point and the remote TA
      this.End = remote_end_point;
      _localta = TransportAddressFactory.CreateInstance(TAType, (IPEndPoint) local_end_point);
      _id = id;
      _remoteid = remoteid;
    }

    public override void Close()
    {
      _is_closed = true;
      base.Close();
    }

    public override bool IsClosed
    {
      get { return _is_closed; }
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

    public override void Send(ICopyable p)
    {
      if( _is_closed ) {
        throw new EdgeException("Tried to send on a closed UdpEdge"); 
      }
      if( p == null ) {
        ProtocolLog.WriteIf(ProtocolLog.UdpEdge, String.Format(
          "{0} tried to send a null packet", this));
        throw new System.NullReferenceException(
	                 "UdpEdge.Send: argument can't be null");
      }

      _send_cb.HandleEdgeSend(this, p);
      lock( _sync ) {
        _last_out_packet_datetime = DateTime.UtcNow;
      }
#if UDP_DEBUG
      /**
         * logging of outgoing packets
         */
      //string GeneratedLog = " a new packet was recieved on this edge ";
      string base64String;
      byte[] packet_buf = new byte[ p.Length ];
      p.CopyTo(packet_buf, 0);
      base64String = Convert.ToBase64String(packet_buf);
      string GeneratedLog = "OutPacket: " + LocalTA.ToString()+", "+RemoteTA.ToString()+ ", " + base64String;
      //log.Info(GeneratedLog);
      // logging finished
#endif
    }

    public override Brunet.TransportAddress.TAType TAType
    {
      get
      {
        return Brunet.TransportAddress.TAType.Udp;
      }
    }

    protected readonly TransportAddress _localta;
    public override Brunet.TransportAddress LocalTA
    {
      get { return _localta; }
    }
    protected TransportAddress _pvlocal;
    /**
     * the other end (due to NAT) may see our LocalTA differently than
     * we do.  This is the view of the LocalTA from the point of
     * view of our Peer
     */
    public TransportAddress PeerViewOfLocalTA {
      get { lock( _sync ) { return _pvlocal; } }
      set { lock( _sync ) { _pvlocal = value; } }
    }
    //UDP ports are always bi-directional, and never ephemeral
    public override bool LocalTANotEphemeral { get { return true; } }
    
    protected volatile TransportAddress _remoteta;
    public override TransportAddress RemoteTA
    {
      get { return _remoteta; }
    }
    
    //UDP ports are always bi-directional, and never ephemeral
    public override bool RemoteTANotEphemeral { get { return true; } }
    
    public void Push(MemBlock b)
    {
      ReceivedPacketEvent(b);
    }
  }

}
