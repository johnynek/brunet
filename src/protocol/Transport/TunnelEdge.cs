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

//#define TUNNEL_DEBUG

using System;
using System.Collections;
using System.Threading;

namespace Brunet
{
  /**
   * Abstract base class used for all Edges.  Manages
   * the sending of Packet objects and sends and event
   * when a Packet arrives.
   */

  public class TunnelEdge: Edge
  {
    protected readonly Node _node;
    protected readonly TunnelEdgeListener _send_cb;

    protected ArrayList _forwarders;
    protected ArrayList _packet_senders;
    public ArrayList PacketSenders {
      get {
	return _packet_senders;
      }
    }

    protected int _last_sender_idx;
    
    protected readonly Address _target;
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
    protected volatile int _remote_id;
    public int RemoteID {
      get {
	return _remote_id;
      }
      set {
        lock( _sync ) {
	  _remote_id = value;
          MakeTunnelHeader();
        }
      }
    }
    
    protected int _is_closed;
    public override bool IsClosed
    {
      get { return (Thread.VolatileRead( ref _is_closed) == 1); }
    }
    protected bool _is_connected;
    protected bool _inbound;
    public override bool IsInbound
    {
      get
      {
        return _inbound;
      }
    }

    protected readonly DateTime _create_dt;
    public override DateTime CreatedDateTime {
      get { return _create_dt; }
    }
    protected DateTime _last_out_packet_datetime;
    public override DateTime LastOutPacketDateTime {
      get { lock( _sync ) { return _last_out_packet_datetime; } }
    }
    
    protected volatile TransportAddress _localta;
    public override TransportAddress LocalTA
    {
      get { return _localta; }
    }    
    protected volatile TransportAddress _remoteta;
    public override TransportAddress RemoteTA
    {
      get { return _remoteta; }
    }    

    public ArrayList _acquire_summary;
    public ArrayList _lost_summary;
    
    /**
     * This is the header we prepend to any Packet
     * we send
     */
    protected ICopyable _tun_header;

    public TunnelEdge(TunnelEdgeListener cb, bool is_in, Node n, 
		      Address target, ArrayList forwarders, int id, int remoteid)
    {
      _send_cb = cb;
      _inbound = is_in;

      _is_connected = false;
      _is_closed = 0;
      _sync = new object();
      _node = n;
      _target = target;
      
#if TUNNEL_DEBUG 
      Console.Error.WriteLine("Constructing: {0}", this);
#endif
      
      //track forwarding addresses
      _forwarders = new ArrayList();
      _acquire_summary  = new ArrayList();
      _lost_summary = new ArrayList();

      //track forwarding edges
      _packet_senders = new ArrayList();
      _id = id;
      _remote_id = remoteid;
      _last_sender_idx = 0; 
      MakeTunnelHeader();

      _node.ConnectionTable.DisconnectionEvent += DisconnectHandler;
      _node.ConnectionTable.ConnectionEvent += ConnectHandler;
      
      lock(_node.ConnectionTable.SyncRoot) {
	  foreach(Address addr in forwarders) {
	    Connection cons = n.ConnectionTable.GetConnection(ConnectionType.Structured, addr);
	    if (cons != null && (false == (cons.Edge is TunnelEdge) )) {
	      _packet_senders.Add(cons.Edge);
	      _forwarders.Add(addr);
	    }
	  }
      }
      _localta = new TunnelTransportAddress(_node.Address, _forwarders);
      _remoteta = new TunnelTransportAddress(target, _forwarders);
#if TUNNEL_DEBUG 
      Console.Error.WriteLine("Constructed: {0}", this);
#endif
    }
    /**
     * Closes the Edge, further Sends are not allowed
     */
    public override void Close()
    {
      int was_closed = Interlocked.Exchange(ref _is_closed, 1);
      if( was_closed == 0 ) {
        //unsubscribe the disconnecthandler
        _node.ConnectionTable.ConnectionEvent -= new EventHandler(ConnectHandler);      
        _node.ConnectionTable.DisconnectionEvent -= new EventHandler(DisconnectHandler);      
        base.Close();
      }
#if TUNNEL_DEBUG 
      Console.Error.WriteLine("Closing: {0}", this);
#endif
    }

    public override TransportAddress.TAType TAType
    {
      get {
	return TransportAddress.TAType.Tunnel;
      }
    }
    /**
     * We have to make this everytime _remote_id changes
     */
    protected void MakeTunnelHeader() {
      //The header always stays fixed for this TunnelEdge
      byte[] ids = new byte[9];
      ids[0] = (byte) TunnelEdgeListener.MessageType.EdgeData;
      //Write the IDs of the edge:
      //[edge data][local id 4 bytes][remote id 4 bytes][packet]
      NumberSerializer.WriteInt(_id, ids, 1);
      NumberSerializer.WriteInt(_remote_id, ids, 5);
      MemBlock ids_mb = MemBlock.Reference( ids );
      ICopyable header = new CopyList( PType.Protocol.AH,
                                       new AHHeader(1, 2, _node.Address, _target, AHPacket.AHOptions.Exact),
                                       PType.Protocol.Tunneling,
                                       ids_mb );
      //Now we have assembled the full header to prepend to any data, but
      //it is a waste to do all the copying each time, so we copy it into
      //one buffer now:
      _tun_header = MemBlock.Copy( (ICopyable) header );
    }
    /**
     * @param p a Packet to send to the host on the other
     * side of the Edge.
     * @throw EdgeException if any problem happens
     */
    public override void Send(ICopyable p) {
      if ( p == null ) {
        throw new System.NullReferenceException(String.Format("Packet null Edge: {0}", this));
      }
#if TUNNEL_DEBUG
      Console.Error.WriteLine("About to send on: {0}", this);
#endif
      ISender s = null;
      int p_s_c = -1;
      //Loop until success or failure:
      while(true) {
        if( IsClosed ) {
          throw new EdgeException(String.Format("Edge closed: {0}", this));
        }
        try {
          lock( _sync ) {
            _last_sender_idx++;
            p_s_c = _packet_senders.Count;
            if( _last_sender_idx >= p_s_c ) {
              _last_sender_idx = 0;
            }
            s = (ISender)_packet_senders[ _last_sender_idx ];
          }
          s.Send( new CopyList(_tun_header, p) );
          lock( _sync ) { _last_out_packet_datetime = DateTime.UtcNow; }
#if TUNNEL_DEBUG
          Console.Error.WriteLine("Sent on: {0} over {1}", this, s);
#endif
          return; //Looks good.
        }
        catch(EdgeException
#if TUNNEL_DEBUG
              x 
#endif
        ) {
          //Make sure this  most recent edge is removed from the
          //ConnectionTable:
#if TUNNEL_DEBUG
          Console.Error.WriteLine("Caught {0}, on {1} Disconnecting: {2}", x, this, s);
#endif
          Edge s_edge = s as Edge;
          if( s_edge != null && s_edge.IsClosed ) {
            //Make sure we handle the Disconnection in this thread before
            //continuing
            Connection c = _node.ConnectionTable.GetConnection(s_edge);
            if( c == null && _packet_senders.Contains(s_edge) ) {
              //A connection is gone, but somehow, we still have it in our
              //_packet_senders
              if(ProtocolLog.TunnelEdge.Enabled)
                ProtocolLog.Write(ProtocolLog.TunnelEdge, String.Format(
                  "{0} has {1} but it should be gone", this, s_edge));
              lock(_sync) {
                _packet_senders.Remove( s_edge );
                p_s_c = _packet_senders.Count;
              }
              if( p_s_c == 0 ) {
                Close();
                throw new EdgeException(String.Format("Edge closed: {0}", this));
              }
            }
            //Try to disconnect and go again.
            _node.ConnectionTable.Disconnect((Edge)s);
          }
        }
        catch (Exception
#if TUNNEL_DEBUG
              x 
#endif
        ) {
#if TUNNEL_DEBUG
          Console.Error.WriteLine("Caught {0}, on {1}, p_s_c == {2}", x, this, p_s_c);
#endif
          if( p_s_c == 0 ) {
            Close();
            throw new EdgeException(String.Format("Edge closed: {0}", this));
          }
        }
      }
    }
    
    public void Push(MemBlock p)
    {
#if TUNNEL_DEBUG
      Console.Error.WriteLine("Received on: {0}", this);
#endif
      ReceivedPacketEvent(p);
    }
    
    protected void DisconnectHandler(object o, EventArgs args) {
      ConnectionEventArgs cargs = args as ConnectionEventArgs;
      Connection cons = cargs.Connection;

      //ignore leaf connections
      if (cons.MainType != ConnectionType.Structured) {
        return;
      }
      if( IsClosed ) { return; }
      //make sure we are not pointing to ourselves
      if (cons.Edge == this) {
        return;
      }
      IEnumerable lost = null;
      bool close_now = false;
#if TUNNEL_DEBUG
      bool updated = false;
#endif
      lock(_sync) {
	 //note we cannot test for connection address being present in the forwarders array, 
	 //this might be a leaf connection disconnect
	 if (_packet_senders.Contains(cons.Edge)) {
#if TUNNEL_DEBUG
           updated = true;
#endif
	   _forwarders.Remove(cons.Address);
	   _packet_senders.Remove(cons.Edge);
	   close_now = (_packet_senders.Count == 0);
	   _localta = new TunnelTransportAddress(_node.Address, _forwarders);
	   _remoteta = new TunnelTransportAddress(_target, _forwarders);
	 }
	 //in case there is a connection for this tunnel edge
	 if (!_is_connected) {
	   _lost_summary.Add(cons.Address);
	 } else {
	   ArrayList temp = new ArrayList();
	   temp.Add(cons.Address);
           lost = temp;
	 }
      }
#if TUNNEL_DEBUG
	   Console.Error.WriteLine("Edge {0} modified.", this); 
	   Console.Error.WriteLine("Because of base connection close: {0}", cons);
	   Console.Error.WriteLine("Forwarders.count has changed to: {1}", this, _forwarders.Count);
	   Console.Error.WriteLine("Updated (on disconnect) localTA: {0}", _localta);
	   Console.Error.WriteLine("Updated (on disconnect) remoteTA: {0}", _remoteta);
#endif
      if( close_now) {
        Close();
      }
      else if( lost != null ) {
        //now send a control packet
        _send_cb.HandleControlSend(this, new ArrayList(), lost);
      }
    }

    protected void ConnectHandler(object o, EventArgs args) {
      ConnectionEventArgs cargs = args as ConnectionEventArgs;
      Connection cons = cargs.Connection;

      //ignore leaf connections
      if (cons.MainType != ConnectionType.Structured) {
        return;
      }

      bool send_control = false;
      IEnumerable added = null;
      IEnumerable lost = null;
      lock(_sync) {
	 if (!_is_connected) {
	   if (cons.Edge == this) {
	     //this connection caused us to get connected
	     _is_connected = true;
             send_control = true;
             added = _acquire_summary;
             lost = _lost_summary;
	   } else {
	     //only add non-tunnel connections
	     if (cons.Edge.TAType != TransportAddress.TAType.Tunnel) {
	       _acquire_summary.Add(cons.Address);
	     }
	   }
	 } else { //we are activated already
	   if (cons.Edge.TAType != TransportAddress.TAType.Tunnel) {	   
	     ArrayList temp = new ArrayList();
	     temp.Add(cons.Address);
             added = temp;
             lost = new ArrayList();
             send_control = true;
	   }
	 }
      }
      if( send_control ) {
        TunnelEdgeListener tun_listener = _send_cb as TunnelEdgeListener;
        tun_listener.HandleControlSend(this, added, lost);
      }
    }
    
    
    public void HandleControlPacket(ArrayList acquired, ArrayList lost) {
      ArrayList to_add = new ArrayList();
      lock(_sync) {
#if TUNNEL_DEBUG
	Console.Error.WriteLine("Edge {0} modified (receiving control).", this); 
#endif
	//remove lost connections
	foreach(Address addr in lost) {
#if TUNNEL_DEBUG
	  Console.Error.WriteLine("Removed forwarder: {0}.", addr); 
#endif	  
	  _forwarders.Remove(addr);
	}
	lock(_node.ConnectionTable.SyncRoot) {
	  //update list of forwarders
	  foreach (Address addr in acquired) {
	    Connection cons = _node.ConnectionTable.GetConnection(ConnectionType.Structured, addr);
	    if (cons != null && (false == (cons.Edge is TunnelEdge)) ) {
	      if (!_forwarders.Contains(addr)) {
		_forwarders.Add(addr);
		to_add.Add(addr);
	      }
	    }
	  }

	  //update packet senders
	  _packet_senders.Clear();
	  foreach(Address addr in _forwarders) {
	    Connection cons = _node.ConnectionTable.GetConnection(ConnectionType.Structured, addr);
	    if (cons != null && (false == (cons.Edge is TunnelEdge)) ) {
	      _packet_senders.Add(cons.Edge);
            }
	  }

	} //Release the lock on the ConnectionTable
	_localta = new TunnelTransportAddress(_node.Address, _forwarders);
	_remoteta = new TunnelTransportAddress(_target, _forwarders);	  

      }//Drop the lock before sending
#if TUNNEL_DEBUG
      Console.Error.WriteLine("Updated (on control) localTA: {0}", _localta);
      Console.Error.WriteLine("Updated (on control) remoteTA: {0}", _remoteta);
#endif	  
      if (to_add.Count > 0) {
        TunnelEdgeListener tun_listener = _send_cb as TunnelEdgeListener;
        tun_listener.HandleControlSend(this, to_add, new ArrayList());	  
      }
    }
  }
}
