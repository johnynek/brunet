/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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
using System.Net;

namespace Brunet.Transport
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

    public override TransportAddress.TAType TAType
    {
      get
      {
        return TransportAddress.TAType.Udp;
      }
    }

    protected readonly TransportAddress _localta;
    public override TransportAddress LocalTA
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

    ///@return true if the id is note yet set, or set to rem
    public int TrySetRemoteID(int rem) {
      if( _remoteid == rem ) {
        return rem;
      }
      return Interlocked.CompareExchange(ref _remoteid, rem, 0);
    }
  }

}
