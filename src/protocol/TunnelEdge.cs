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
   * This class represents a tunnel edge. A tunnel edge represents an overlay
   * edge that is tunnelled over connections to common neighbors, and is useful to 
   * correct the overlay structure when adjacent nodes cannot form TCP ot UDP
   * (or any other transport) connections.  
   */

  public class TunnelEdge: Edge
  {
    protected readonly Node _node;

    //TODO: It would be nice to make the following items immutable lists. 
    /**
     * Keep track of list of forewarders associated with the tunnel edge. 
     */
    protected ArrayList _forwarders;
    /**
     * Packet senders associated with the forwarders. 
     */
    protected ArrayList _packet_senders;
    public ArrayList PacketSenders {
      get {
        lock(_sync) {
          return (ArrayList) _packet_senders.Clone();
        }
      }
    }

    protected int _last_sender_idx;
    
    /**
     * Target address for the tunnel edge. 
     */
    protected readonly Address _target;
    public Address Target {
      get {
	return _target;
      }
    }
    
    /**
     * Local id for the tunnel edge. 
     */
    protected readonly int _id;
    public int ID {
      get {
	return _id;
      }
    }

    /**
     * Remote id for the tunnel edge. 
     */
    protected int _remote_id;
    public int RemoteID {
      get {
        //Interlocked does a memory barrier.
        //Since we only change remote_id
        //in Interlocked, it is okay.
        return _remote_id;
      }
      set {
        int old_v = Interlocked.CompareExchange(ref _remote_id, value, 0);
        if( old_v != 0 ) {
          //We didn't really exchange above, and we should throw an exception
          throw new EdgeException(
                      String.Format("RemoteID already set: {0} cannot set to: {1}",
                                    old_v, value));	
        }
        // We need to create a new header.
        ICopyable new_th = GetTunnelHeader(_id, RemoteID,
                                           _node.Address, _target);
        lock(_sync ) {
          //We need a memory barrier (either explicit, or use a lock)
          //to make sure all threads will see the most recent data.
          _tun_header = new_th;
        }
      }
    }
    /**
     * Has a connection been formed over the tunnel edge. 
     */
    protected bool _is_connected;

    protected static readonly TimeSpan SyncInterval = new TimeSpan(0, 0, 30);//30 seconds.
    /**
     * Last time when we synchronized with the tunnel edge target,
     * on the list of forwarders. 
     */
    protected DateTime _last_sync_dt;
    protected readonly TunnelEdgeListener _tel;

    /**
     * Local URI representation for the edge. 
     */
    protected TransportAddress _localta;
    public override TransportAddress LocalTA
    {
      get { return _localta; }
    }    
    /**
     * Remote URI representation for the edge. 
     */
    protected TransportAddress _remoteta;
    public override TransportAddress RemoteTA
    {
      get { return _remoteta; }
    }    

    /**
     * List of connection (addresses) acquired between edge creation
     * and connnection setup on this edge. 
     */
    public ArrayList _acquire_summary;
    /**
     * List of connection (addresses) lost between edge creation
     * and connnection setup on this edge. 
     */
    public ArrayList _lost_summary;
    
    /**
     * This is the header we prepend to any Packet
     * we send.
     */
    protected ICopyable _tun_header;

    /**
     * Constructor for the tunnel edge.
     * @param cb reference to the tunnel edge listener.
     * @param is_in is the edge inbound.
     * @param n local node.
     * @param target address of the tunnel edge target node.
     * @param forwarders current list of forwarders (addresses) for the tunnel edge. 
     * @param id local id.
     * @param remoteid remote id.     
     */
    public TunnelEdge(TunnelEdgeListener cb, bool is_in, Node n, 
		      Address target, ArrayList forwarders, int id, int remoteid)
               : base(null, is_in)
    {
      _tel = cb;
      _is_connected = false;

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
      //create a tunnel header.
      _tun_header = GetTunnelHeader(ID, RemoteID, _node.Address, _target);

      //This doesn't require us to hold the lock on the connection table
      IEnumerable struct_cons = n.ConnectionTable.GetConnections(ConnectionType.Structured);
      foreach(Connection c in struct_cons) {
        if( forwarders.Contains(c.Address) && (false == (c.Edge is TunnelEdge) )) {
          //This is a connection we can use:
          _forwarders.Add(c.Address);
          _packet_senders.Add(c.Edge);
        }
      }
      _localta = new TunnelTransportAddress(_node.Address, _forwarders);
      _remoteta = new TunnelTransportAddress(target, _forwarders);
      
      lock(_sync) {
        _last_sync_dt = DateTime.UtcNow; //we just synchronized now.
        _node.ConnectionTable.DisconnectionEvent += new EventHandler(DisconnectHandler);
        _node.ConnectionTable.ConnectionEvent += new EventHandler(ConnectHandler);
        _node.HeartBeatEvent += new EventHandler(SynchronizeEdge);
      }
      
#if TUNNEL_DEBUG 
      Console.Error.WriteLine("Constructed: {0}", this);
#endif
    }
    /**
     * Closes the Edge, further Sends are not allowed.
     */
    public override bool Close()
    {
      bool act = base.Close();
      if( act ) {
        //unsubscribe the disconnecthandler
        _node.ConnectionTable.ConnectionEvent -= new EventHandler(ConnectHandler);      
        _node.ConnectionTable.DisconnectionEvent -= new EventHandler(DisconnectHandler); 
        _node.HeartBeatEvent -= new EventHandler(SynchronizeEdge);
      }
#if TUNNEL_DEBUG 
      Console.Error.WriteLine("Closing: {0}", this);
#endif
      return act;
    }
    /**
     * Type of the edge (tunnel in this case).
     */ 
    public override TransportAddress.TAType TAType
    {
      get {
	return TransportAddress.TAType.Tunnel;
      }
    }
    /**
     * Creates the tunnel header attached in front of packet sent on the tunnnel edge. 
     * We have to make this everytime _remote_id changes.
     * @param local_id local id of the edge. 
     * @param remote_id remote id of the edge. 
     * @param source local node address.
     * @param target target node address.
     */
    protected static ICopyable GetTunnelHeader(int local_id, int remote_id, Address source, Address target) {
      //The header always stays fixed for this TunnelEdge
      byte[] ids = new byte[9];
      ids[0] = (byte) TunnelEdgeListener.MessageType.EdgeData;
      //Write the IDs of the edge:
      //[edge data][local id 4 bytes][remote id 4 bytes][packet]
      NumberSerializer.WriteInt(local_id, ids, 1);
      NumberSerializer.WriteInt(remote_id, ids, 5);
      MemBlock ids_mb = MemBlock.Reference( ids );
      ICopyable header = new CopyList( PType.Protocol.AH,
                                       new AHHeader(1, 2, source, target, AHPacket.AHOptions.Exact),
                                       PType.Protocol.Tunneling,
                                       ids_mb );
      //Now we have assembled the full header to prepend to any data, but
      //it is a waste to do all the copying each time, so we copy it into
      //one buffer now:
      return MemBlock.Copy( (ICopyable) header );
    }
    /**
     * Send a packet on this edge. 
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
          throw new EdgeClosedException(String.Format("Edge closed: {0}", this));
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
          Interlocked.Exchange(ref _last_out_packet_datetime, DateTime.UtcNow.Ticks);
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
                throw new EdgeClosedException(String.Format("Edge closed: {0}", this));
              }
            }
            //Try to disconnect and go again.
            _node.ConnectionTable.Disconnect(s_edge);
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
            throw new EdgeClosedException(String.Format("Edge closed: {0}", this));
          }
        }
      }
    }

    /**
     * Handle event associated with a disconnection.
     * @param o some object 
     * @param args arguments representing the disconnection event.
     * (encapsulates the connection object).
     */
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
        _tel.HandleControlSend(this, new ArrayList(), lost);
      }
    }

    /**
     * Handle event associated with a new connection.
     * @param o some object 
     * @param args arguments representing the connection event
     * (encapsulates the connection object).
     */
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
        _tel.HandleControlSend(this, added, lost);
      }
    }
    
    /**
     * Handle periodic synchronization about forwarders with the
     * edge target.
     * @param o some object.
     * @param args event arguments.

     */
    protected void SynchronizeEdge(object o, EventArgs args) {
      
      // Send a message about my local connections.
      DateTime now = DateTime.UtcNow;
      lock(_sync) {
        if (now - _last_sync_dt < SyncInterval) {
          return;
        } 
        _last_sync_dt = now;
      }

#if TUNNEL_DEBUG
      Console.Error.WriteLine("Sending synchronize for edge: {0}.", this);
#endif
      ArrayList nearest = _node.ConnectionTable.GetNearestTo( (AHAddress) _node.Address, 6);
      ArrayList forwarders = new ArrayList();
      foreach(Connection cons in nearest) {
        if (cons.Edge.TAType != TransportAddress.TAType.Tunnel) {
          forwarders.Add(cons.Address);
        }
      }
      _tel.HandleSyncSend(this, forwarders);
#if TUNNEL_DEBUG
      Console.Error.WriteLine("Sent synchronize for edge: {0}.", this);
#endif
    }
    
    /**
     * Handle an edge control packet from the tunnel edge target
     * (change in his connections).
     * @param acquired list of connections (addresses) acquired. 
     * @param lost list of connections (addresses) lost. 
     */
    public void HandleControlPacket(ArrayList acquired, ArrayList lost) {
      //This does not require a lock, and stuct_cons can't change after this call
      IEnumerable struct_cons =
          _node.ConnectionTable.GetConnections(ConnectionType.Structured);
      
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
        //Add all the forwarders we just acquired:
        foreach(Connection c in struct_cons) {
          if(acquired.Contains(c.Address)) {
            if(false == (c.Edge is TunnelEdge)) {
              if( !_forwarders.Contains(c.Address) ) {  
                _forwarders.Add(c.Address);
              }
            }
          }
        }
        //Update PacketSenders:
        _packet_senders.Clear();
        foreach(Connection c in struct_cons) {
          if( _forwarders.Contains( c.Address ) ) {
            _packet_senders.Add(c.Edge);
          }
        }
        _localta = new TunnelTransportAddress(_node.Address, _forwarders);
        _remoteta = new TunnelTransportAddress(_target, _forwarders);	  
      }//Drop the lock before sending
#if TUNNEL_DEBUG
      Console.Error.WriteLine("Updated (on control) localTA: {0}", _localta);
      Console.Error.WriteLine("Updated (on control) remoteTA: {0}", _remoteta);
#endif	  
      if (to_add.Count > 0) {
        _tel.HandleControlSend(this, to_add, new ArrayList());	  
      }
    }

    /**
     * Handle a synchornization packet from tunnel edge target.  
     * @param forwarders list of forwarding addresses the target is
     * using.
     */
    public void HandleSyncPacket(ArrayList forwarders) {
      //This does not require a lock, and stuct_cons can't change after this call
      IEnumerable struct_cons =
          _node.ConnectionTable.GetConnections(ConnectionType.Structured);
      ArrayList temp_forwarders = new ArrayList();
      ArrayList temp_senders = new ArrayList();
      foreach(Connection c in struct_cons) {
        if(forwarders.Contains(c.Address)) {
          if(false == (c.Edge is TunnelEdge)) {
            temp_forwarders.Add(c.Address);
            temp_senders.Add(c.Edge);
          }
        }
      }
      TransportAddress new_local 
        = new TunnelTransportAddress(_node.Address, temp_forwarders);
      TransportAddress new_remote
        = new TunnelTransportAddress(_target, temp_forwarders);
      /*
       * We are clearly only holding on lock in the below code
       * since we are only writing to memory and not calling any
       * functions
       */
      lock(_sync) {
        _forwarders = temp_forwarders;
        _packet_senders = temp_senders;
        _localta = new_local;
        _remoteta = new_remote;	  
      }
#if TUNNEL_DEBUG
      Console.Error.WriteLine("Synchronized edge: {0}.", this);
#endif 
      if (temp_forwarders.Count == 0) {
        //Now we have no forwarders, so close the edge
        Close();
      }
    }
  }
}
