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

using jabber;
using jabber.protocol;

using System;
using System.Xml;

namespace Brunet.Xmpp {
  /// <summary> An IQ for XmppRelay, send binary data through an Xmpp server.</summary>
  public class XmppRelay : Element
  {
    public const string NAMESPACE = "Brunet:Transport:Xmpp";
    public const string DATA = "data";
    public static readonly byte[] EMPTY_DATA = new byte[0];
    /// <summary> For user generated IQs, sender side.</summary>
    public XmppRelay(XmlDocument doc, byte[] data) :
      base("query", NAMESPACE, doc)
    {
      SetElem(DATA, Convert.ToBase64String(data));
    }

    /// <summary> For application generated IQs, receiver side.</summary>
    public XmppRelay(string prefix, XmlQualifiedName qname, XmlDocument doc) :
      base(prefix, qname, doc)
    {
    }

    /// <summary>binary (standard 8-bit, base-256) encoding of the data.</summary>
    public byte[] Data {
      get {
        try {
          return Convert.FromBase64String(Base64);
        } catch {
          return EMPTY_DATA;
        }
      }
    }

    /// <summary>Base64 representation of the binary data.</summary>
    public string Base64 { get { return GetElem(DATA); } }
  }

  /// <summary>Enables parsing of the IQ.Query to these types.</summary>
  public class XmppRelayFactory : jabber.protocol.IPacketTypes
  {
    private static QnameType[] _s_qnt = new QnameType[] {
      new QnameType("query", XmppRelay.NAMESPACE, typeof(XmppRelay))
    };

    QnameType[] IPacketTypes.Types { get { return _s_qnt; } }

    /// <summary>Called by the underlying XmppStream during the OnStreamInit event.</summary>
    public static void HandleStreamInit(object sender, ElementStream stream)
    {
      stream.AddFactory(new XmppRelayFactory());
    }
  }
}
