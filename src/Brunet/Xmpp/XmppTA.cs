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
