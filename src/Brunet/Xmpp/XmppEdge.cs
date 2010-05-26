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

using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using Brunet.Messaging;
using Brunet.Transport;
using Brunet.Util;
using jabber;

namespace Brunet.Xmpp {
  /// <summary>Holds the state information for Xmpp communication.</summary>
  public class XmppEdge : Brunet.Transport.Edge, IIdentifierPair {
    public MemBlock Header { get { return _ip.Header; } }
    /// <summary>JID for the remote entity of this Edge.</summary>
    public readonly JID To;
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
        return TransportAddress.TAType.Xmpp;
      }
    }

    protected readonly IdentifierPair _ip;
    protected readonly TransportAddress _local_ta;
    protected readonly TransportAddress _remote_ta;

    /// <summary>Create a XmppEdge.</summary>
    public XmppEdge(IEdgeSendHandler send_handler, XmppTransportAddress local_ta,
        XmppTransportAddress remote_ta, bool inbound) :
      base(send_handler, inbound)
    {
      _ip = new IdentifierPair();
      _local_ta = local_ta;
      _remote_ta = remote_ta;
      To = remote_ta.JID;
    }
  }
}
