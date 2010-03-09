/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using Brunet.Util;
using Brunet.Transport;

using Brunet.Messaging;
namespace Brunet.Security.Transport {
  ///<summary>Provides a Wrapper for edges, this allows us to control input,
  ///output, and state of the edge.  This class is thread-safe.</summary>
  ///<remarks>This could be an abstract class, but it was fully implemented for
  ///testing purposes</summary>
  public class WrapperEdge: Edge, IDataHandler {
    protected Edge _edge;
    protected int _weclosed;
    ///<summary>The underlying edge.</summary>
    public Edge WrappedEdge { get { return _edge; } }
    public WrapperEdge(Edge edge): this(edge, true)
    {
    }

    ///<summary>Creates a new WrapperEdge.<summary>
    ///<param name="edge">The edge to wrap.</param>
    ///<param name="SubscribeToEdge">Should this subscribe to the edge.</param>
    public WrapperEdge(Edge edge, bool SubscribeToEdge) {
      _weclosed = 0;
      _edge = edge;
      if(SubscribeToEdge) {
        _edge.Subscribe(this, null);
      }
    }

    ///<summary>We automatically push all data to the listener of this edge.</summary>
    public void HandleData(MemBlock b, ISender return_path, object state) {
      ReceivedPacketEvent(b);
    }

    public override bool Close() {
      if(Interlocked.Exchange(ref _weclosed, 1) == 1) {
        return false;
      }
      base.Close();
      _edge.Close();
      return true;
    }

    ///<summary>This is the underlying Edge's state.  By default, we do not
    ///change this state.</summary>
    public override TransportAddress LocalTA {
      get {
        return _edge.LocalTA;
      }
    }

    ///<summary>This is the underlying Edge's state.  By default, we do not
    ///change this state.</summary>
    public override bool LocalTANotEphemeral {
      get {
        return _edge.LocalTANotEphemeral;
      }
    }

    ///<summary>This is the underlying Edge's state.  By default, we do not
    ///change this state.</summary>
    public override TransportAddress RemoteTA {
      get {
        return _edge.RemoteTA;
      }
    }

    ///<summary>This is the underlying Edge's state.  By default, we do not
    ///change this state.</summary>
    public override bool RemoteTANotEphemeral {
      get {
        return _edge.RemoteTANotEphemeral;
      }
    }

    ///<summary>This is the underlying Edge's state.  By default, we do not
    ///change this state.</summary>
    public override TransportAddress.TAType TAType {
      get {
        return _edge.TAType;
      }
    }

    ///<summary>Sends the data over the underlying edge.</summary>
    public override void Send(ICopyable p) {
      _edge.Send(p);
    }

    public override string ToString() {
      return "WrappedEdge: " + _edge.ToString();
    }
  }
}
