/*
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet.Messaging;
using Brunet.Transport;
using Brunet.Util;

using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

namespace Brunet.Symphony {
  /// <summary>Holds the state information for a Subrings.</summary>
  public class SubringEdge : Edge, IIdentifierPair {
    public MemBlock Header { get { return _ip.Header; } }
    public int LocalID {
      get {
        return _ip.LocalID;
      }
      set {
        _ip.LocalID = value;
      }
    }

    public override TransportAddress LocalTA {
      get {
        return _local_ta;
      }
    }

    public int RemoteID {
      get {
        return _ip.RemoteID;
      }
      set {
        _ip.RemoteID = value;
      }
    }

    public override TransportAddress RemoteTA {
      get {
        return _remote_ta;
      }
    }

    public override TransportAddress.TAType TAType {
      get {
        return TransportAddress.TAType.Subring;
      }
    }

    protected readonly IdentifierPair _ip;
    protected readonly TransportAddress _local_ta;
    protected readonly PType _ptype;
    protected readonly TransportAddress _remote_ta;
    protected readonly ISender _overlay_sender;

    /// <summary>Constructor for an outgoing edge, since we don't know the remote
    /// id yet, it must be outgoing!</summary>
    public SubringEdge(TransportAddress local_ta, TransportAddress remote_ta,
        bool inbound, ISender sender, PType ptype) :
        base(null, inbound)
    {
      _ip = new IdentifierPair();
      _local_ta = local_ta;
      _remote_ta = remote_ta;
      _ptype = ptype;
      _overlay_sender = sender;
    }

    override public void Send(ICopyable data) {
      _overlay_sender.Send(new CopyList(_ptype, Header, data));
      Interlocked.Exchange(ref _last_out_packet_datetime, DateTime.UtcNow.Ticks);
    }
  }
}
