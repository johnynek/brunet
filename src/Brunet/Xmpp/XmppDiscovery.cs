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

using Brunet.Transport;
using jabber;
using jabber.protocol;
using System;
using System.Collections;
using System.Xml;

namespace Brunet.Xmpp {
  /// <summary>Xmpp provides a means for discovery and as a relay.</summary>
  public class XmppDiscovery : Discovery {
    protected static readonly IList EMPTY_LIST = new ArrayList(0);
    protected readonly XmppService _xmpp;
    protected readonly string _realm;
    protected readonly string _local_ta;
    protected int _ready;

    /// <summary>A rendezvous service for finding remote TAs and sharing
    /// our TA, so that peers can become connected.</summary>
    public XmppDiscovery(ITAHandler ta_handler, XmppService xmpp, string realm) :
      base(ta_handler)
    {
      _realm = realm;
      _xmpp = xmpp;
      _ready = 0;
      _xmpp.Register(typeof(XmppTARequest), HandleRequest);
      _xmpp.Register(typeof(XmppTAReply), HandleReply);
      _xmpp.OnStreamInit += XmppTAFactory.HandleStreamInit;

      // Operations aren't valid until Xmpp has authenticated with the servers
      xmpp.OnAuthenticate += HandleAuthenticate;
      if(xmpp.IsAuthenticated) {
        HandleAuthenticate(null);
      }
    }

    /// <summary>Called once Xmpp has authenticated with the servers.  This
    /// generates our Xmpp server now that we have our complete JID.  username,
    /// server, and resource.</summary>
    protected void HandleAuthenticate(object sender)
    {
      System.Threading.Interlocked.Exchange(ref _ready, 1);
    }

    /// <summary>Some remote entity inquired for our TA, but we only reply if
    /// we are in the same realm.</summary>
    protected void HandleRequest(Element msg, JID from)
    {
      XmppTARequest xt = msg as XmppTARequest;
      if(xt == null) {
        return;
      }

      IList tas = EMPTY_LIST;
      if(xt.Realm.Equals(_realm)) {
        tas = LocalTAsToString(20);
      }
      XmppTAReply xtr = new XmppTAReply(new XmlDocument(), _realm, tas);
      _xmpp.SendTo(xtr, from);
    }

    /// <summary>We got a reply to one of our requests.  Let's send the result
    /// back to the TA listener.</summary>
    protected void HandleReply(Element msg, JID from)
    {
      XmppTAReply xt = msg as XmppTAReply;
      if(xt == null) {
        return;
      }

      if(!xt.Realm.Equals(_realm)) {
        return;
      }

      UpdateRemoteTAs(xt.TransportAddresses);
    }

    /// <summary>We need some TAs, let's query our friends to see if any of them
    /// can supply us with some.<summary>
    protected override void SeekTAs(DateTime now)
    {
      if(_ready == 0) {
        return;
      }

      XmppTARequest xtr = new XmppTARequest(new XmlDocument(), _realm);
      _xmpp.SendRandomMulticast(xtr);
    }
  }
}
