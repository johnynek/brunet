/*
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida

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
