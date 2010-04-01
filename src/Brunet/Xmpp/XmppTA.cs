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
using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Brunet.Xmpp {
  /// <summary> An IQ for XmppTARequest, we separate the XmppTA into separate
  /// queries and results to simplify handling.</summary>
  public class XmppTARequest : Element
  {
    public const string NAMESPACE = "Brunet:Transport:Realm:Request";
    public const string REALM = "realm";

    /// <summary> For user generated IQs, sender side.</summary>
    public XmppTARequest(XmlDocument doc, string realm) : base("query", NAMESPACE, doc)
    {
      SetElem(REALM, realm);
    }

    /// <summary> For application generated IQs, receiver side.</summary>
    public XmppTARequest(string prefix, XmlQualifiedName qname, XmlDocument doc) :
      base(prefix, qname, doc)
    {
    }

    /// <summary>The Brunet.Realm where the requesting node is located.</summary>
    public string Realm { get { return GetElem(REALM); } }
  }

  /// <summary> An IQ for XmppTAReply, we separate the XmppTA into separate
  /// queries and results to simplify handling.</summary>
  public class XmppTAReply : Element
  {
    public const string NAMESPACE = "Brunet:Transport:Realm:Reply";
    public const string REALM = "realm";
    public const string TRANSPORT_ADDRESS = "ta";

    /// <summary> For user generated IQs, sender side.</summary>
    public XmppTAReply(XmlDocument doc, string realm, IList tas) : base("query", NAMESPACE, doc)
    {
      SetElem(REALM, realm);

      string tas_xml = string.Empty;
      XmlSerializer serializer = new XmlSerializer(typeof(string[]));
      using(StringWriter sw = new StringWriter()) {
        serializer.Serialize(sw, (string[]) (new ArrayList(tas)).ToArray(typeof(string)));
        tas_xml = sw.ToString();
      }
      SetElem(TRANSPORT_ADDRESS, tas_xml);
    }

    /// <summary> For application generated IQs, receiver side.</summary>
    public XmppTAReply(string prefix, XmlQualifiedName qname, XmlDocument doc) :
      base(prefix, qname, doc)
    {
    }

    /// <summary>The Brunet.Realm where the requesting node is located.</summary>
    public string Realm { get { return GetElem(REALM); } }
    /// <summary>The Brunet.TransportAddress for the responding node.</summary>
    public IList TransportAddresses {
      get {
        string tas_xml = GetElem(TRANSPORT_ADDRESS);
        using(StringReader sr = new StringReader(tas_xml)) {
          XmlSerializer serializer = new XmlSerializer(typeof(string[]));
          return (IList) serializer.Deserialize(sr);
        }
      }
    }
  }

  /// <summary>Enables parsing of the IQ.Query to these types.</summary>
  public class XmppTAFactory : jabber.protocol.IPacketTypes
  {
    private static QnameType[] _s_qnt = new QnameType[] {
      new QnameType("query", XmppTARequest.NAMESPACE, typeof(XmppTARequest)),
      new QnameType("query", XmppTAReply.NAMESPACE, typeof(XmppTAReply))
    };

    QnameType[] IPacketTypes.Types { get { return _s_qnt; } }

    /// <summary>Called by the underlying XmppStream during the OnStreamInit event.</summary>
    public static void HandleStreamInit(object sender, ElementStream stream)
    {
      stream.AddFactory(new XmppTAFactory());
    }
  }
}
