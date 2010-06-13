/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet.Messaging;
using Brunet.Util;

namespace Brunet.Transport {
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
