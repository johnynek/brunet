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

using Brunet;
using Brunet.Concurrent;
using Brunet.Connections;
using Brunet.Messaging;
using Brunet.Transport;
using Brunet.Util;

using jabber;
using jabber.protocol;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Xml;

namespace Brunet.Xmpp {
  /// <summary>Xmpp provides a means for discovery and as a relay.</summary>
  public class XmppEdgeListener : EdgeListener, IEdgeSendHandler {
    protected readonly IdentifierTable _it;
    protected XmppTransportAddress _local_ta;
    protected ArrayList _local_tas;
    protected int _ready;
    protected int _running;
    protected int _started;
    protected readonly XmppService _xmpp;

    public override IEnumerable LocalTAs { get { return _local_tas; } }
    public override int Count { get { return _it.Count; } }

    public override TransportAddress.TAType TAType {
      get {
        return TransportAddress.TAType.Xmpp;
      }
    }

    public override bool IsStarted {
      get {
        return _started == 1;
      }
    }

    /// <summary>Create a XmppEL.</summary>
    public XmppEdgeListener(XmppService xmpp)
    {
      _it = new IdentifierTable();
      _running = 0;
      _started = 0;
      _ready = 0;

      _xmpp = xmpp;
      xmpp.OnStreamInit += XmppRelayFactory.HandleStreamInit;
      // After we've authenticated we setup the rest of our paths
      xmpp.OnAuthenticate += HandleAuthenticate;
      if(xmpp.IsAuthenticated) {
        HandleAuthenticate(null);
      }
    }

    protected void HandleAuthenticate(object sender)
    {
      if(Interlocked.Exchange(ref _ready, 1) == 1) {
        return;
      }

      _local_ta = new XmppTransportAddress(_xmpp.JID);
      _local_tas = new ArrayList(1);
      _local_tas.Add(_local_ta as TransportAddress);
      _xmpp.Register(typeof(XmppRelay), HandleData);
    }

    /// <summary>Remove closed XmppEdges from the IdentifierTable.</summary>
    protected void CloseHandler(object o, EventArgs ea)
    {
      XmppEdge xe = o as XmppEdge;
      if(xe != null) {
        _it.Remove(xe.LocalID);
      }
    }

    /// <summary>Creates an XmppEdge.</summary>
    public override void CreateEdgeTo(TransportAddress ta, EdgeCreationCallback ecb)
    {
      if(_ready == 0) {
        ecb(false, null, new Exception("Xmpp is not authenticated"));
      }

      XmppTransportAddress xta = ta as XmppTransportAddress;
      if(xta == null) {
        ecb(false, null, new Exception("TA Type is not Xmpp!"));
        return;
      } else if(!_xmpp.IsUserOnline(xta.JID)) {
        ecb(false, null, new Exception("Xmpp user, " + xta.JID + ", is not online."));
        return;
      }

      XmppEdge xe = new XmppEdge(this, _local_ta, xta, false);
      _it.Add(xe);
      xe.CloseEvent += CloseHandler;
      ecb(true, xe, null);
    }

    /// <summary>Got a packet from Xmpp.</summary>
    protected void HandleData(Element msg, JID from)
    {
      // Speed in this method isn't key as we are going through a relay
      XmppRelay xr = msg as XmppRelay;
      if(xr == null) {
        return;
      }

      MemBlock payload;
      int local_id, remote_id;
      _it.Parse(xr.Data, out payload, out local_id , out remote_id);

      IIdentifierPair ip;
      XmppEdge xe;

      if(_it.TryGet(local_id, remote_id, out ip)) {
        xe = ip as XmppEdge;
      } else if(local_id == 0) {
        xe = new XmppEdge(this, _local_ta, new XmppTransportAddress(from), true);
        _it.Add(xe);
        xe.CloseEvent += CloseHandler;
        xe.RemoteID = remote_id;
        SendEdgeEvent(xe);
      } else {
        // Probably an edge closed earlier...
        return;
      }

      xe.ReceivedPacketEvent(payload);
    }

    /// <summary>Used to send data over Xmpp using the specified XmppEdge.</summary>
    public void HandleEdgeSend(Edge from, ICopyable data)
    {
      XmppEdge xe = from as XmppEdge;
      byte[] msg = new byte[xe.Header.Length + data.Length];
      xe.Header.CopyTo(msg, 0);
      data.CopyTo(msg, xe.Header.Length);
      _xmpp.SendTo(new XmppRelay(new XmlDocument(), msg), xe.To);
    }

    public override void Start()
    {
      if(Interlocked.Exchange(ref _started, 1) == 1) {
        throw new Exception("XmppEdgeListener cannot be started twice.");
      }
      Interlocked.Exchange(ref _running, 1);
    }

    public override void Stop()
    {
      Interlocked.Exchange(ref _running, 0);
      base.Stop();

      foreach(Edge e in _it) {
        try {
          e.Close();
        } catch {
        }
      }
    }
  }
}
