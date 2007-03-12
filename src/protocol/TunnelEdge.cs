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

using System;
using System.Collections;

namespace Brunet
{
  /**
   * Abstract base class used for all Edges.  Manages
   * the sending of Packet objects and sends and event
   * when a Packet arrives.
   */

  public class TunnelEdge: Edge
  {
    protected Node _node;
    protected IPacketHandler _send_cb;

    protected ForwardingSender _forwarding_sender;
    public ForwardingSender PacketSender {
      get {
	return _forwarding_sender;
      }
    }

    protected Address _target;
    public Address Target {
      get {
	return _target;
      }
    }

    protected int _id;
    public int ID {
      get {
	return _id;
      }
    }
    protected int _remote_id;
    public int RemoteID {
      get {
	return _remote_id;
      }
      set {
	_remote_id = value;
      }
    }
    
    protected bool _is_closed;
    public override bool IsClosed
    {
      get
      {
        return _is_closed;
      }
    }
    protected bool _inbound;
    public override bool IsInbound
    {
      get
      {
        return _inbound;
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
    
    protected TransportAddress _localta;
    public override Brunet.TransportAddress LocalTA
    {
      get { return _localta; }
    }    
    protected TransportAddress _remoteta;
    public override Brunet.TransportAddress RemoteTA
    {
      get { return _remoteta; }
    }    

    protected byte[] _send_buffer;
    public byte[] SendBuffer {
      get {
	return _send_buffer;
      }
    }

    public TunnelEdge(IPacketHandler cb, bool is_in, Node n, 
		      Address target, Address forwarder, int id, int remoteid, 
		      byte[] buffer) 
    {
      _send_cb = cb;
      _inbound = is_in;

      _node = n;
      //we assume that forwarder is just 1 hop away
      _forwarding_sender = new ForwardingSender(n, forwarder, 1);
      _target = target;
      
      _localta = new TunnelTransportAddress(_node.Address, forwarder);
      _remoteta = new TunnelTransportAddress(target, forwarder);

      _id = id;
      _remote_id = remoteid;

      _send_buffer = buffer;
    }
    /**
     * Closes the Edge, further Sends are not allowed
     */
    public override void Close()
    {
      base.Close();
      _is_closed = true;
    }

    public override Brunet.TransportAddress.TAType TAType
    {
      get {
	return Brunet.TransportAddress.TAType.Tunnel;
      }
    }

    /**
     * @param p a Packet to send to the host on the other
     * side of the Edge.
     * @throw EdgeException if any problem happens
     */
    public override void Send(Brunet.Packet p) {
      _last_out_packet_datetime = DateTime.UtcNow;
      _send_cb.HandlePacket(p, this);
    }

    public void Push(Packet p)
    {
      ReceivedPacketEvent(p);
    }
  }
}
