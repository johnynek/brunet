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
