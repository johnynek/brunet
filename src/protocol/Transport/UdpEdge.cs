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
using System.Threading;
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
    protected System.Net.EndPoint end;
    /**
     * This is the IPEndPoint for this UdpEdge.
     * No one other than the EdgeListeners that created
     * this edge should access this.
     */
    public System.Net.EndPoint End {
      get {
        return end;
      }
      set {
        TransportAddress new_ta = TransportAddressFactory.CreateInstance(TAType, (IPEndPoint) value);
        lock( _sync ) {
          end = value;
          _remoteta = new_ta;
        }
      }
    }

    protected readonly int _id;
    public int ID { get { return _id; } }

    protected int _remoteid;
    /**
     * This can only be set if it is currently 0.
     */
    public int RemoteID {
      get { return _remoteid; }
      set {
        //This does a memory barrier, so we don't need to on read
        int old_v = Interlocked.CompareExchange(ref _remoteid, value, 0);
        if( old_v != 0 ) {
          //We didn't really exchange above, and we should throw an exception
          throw new EdgeException(
                      String.Format("RemoteID already set: {0} cannot set to: {1}",
                                    old_v, value));
        }
      }
    }
    
    /**
     * The send_cb is the method which actually does the
     * sending (which is in UdpEdgeListener).
     */
    public UdpEdge(IEdgeSendHandler send_cb,
                   bool is_in,
                   System.Net.IPEndPoint remote_end_point,
                   System.Net.IPEndPoint local_end_point,
                   int id, int remoteid) : base(send_cb, is_in)
    {
      //This will update both the end point and the remote TA
      this.End = remote_end_point;
      _localta = TransportAddressFactory.CreateInstance(TAType, (IPEndPoint) local_end_point);
      _id = id;
      _remoteid = remoteid;
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
    
    protected TransportAddress _remoteta;
    public override TransportAddress RemoteTA
    {
      get { return _remoteta; }
    }
    
    //UDP ports are always bi-directional, and never ephemeral
    public override bool RemoteTANotEphemeral { get { return true; } }
  }

}
