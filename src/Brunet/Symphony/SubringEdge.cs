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
