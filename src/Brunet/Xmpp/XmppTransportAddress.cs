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

using Brunet.Transport;
using jabber;

#if NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Xmpp {
  public class XmppTransportAddress : TransportAddress {
    public readonly JID JID;
    
    /// <summary>Add support to create brunet.xmpp TAs</summary>
    static XmppTransportAddress()
    {
      TransportAddressFactory.AddFactoryMethod("xmpp", Create);
    }

    /// <summary>Convert a XmppTA string to an XmppTA object.</summary>
    public XmppTransportAddress(string s) : base(s)
    {
      int k = s.IndexOf(":") + 3;
      if(k == 3) {
        throw new System.Exception("Expected a URL.");
      }
      JID = new JID(s.Substring(k).Trim('/'));
    }

    /// <summary>Create an XmppTA from a JID.</summary>
    public XmppTransportAddress(JID jid) : this(GetString(jid))
    {
    }

    /// <summary>Called by the TAFactory.</summary>
    public static TransportAddress Create(string s)
    {
      return new XmppTransportAddress(s);
    }

    /// <summary>Converts a JID into an XmppTA string.</summary>
    private static string GetString(JID jid)
    {
      return "brunet.xmpp://" + jid.ToString();
    }

    public override TAType TransportAddressType { 
      get {
        return TransportAddress.TAType.Xmpp;
      }
    }

    public override bool Equals(object o) {
      if(o == this) {
        return true;
      }

      XmppTransportAddress other = o as XmppTransportAddress;
      if(other == null) {
        return false;
      }

      return other.JID.Equals(JID);
    }

    public override int GetHashCode() {
      return JID.GetHashCode();
    }
  }

#if NUNIT
  [TestFixture]
  public class TATester {
    [Test]
    public void Test() {
      string sjid = "isaac.wolinsky@gmail.com/Brunet.367FDA47";
      JID jid = new JID(sjid);
      XmppTransportAddress ta0 = new XmppTransportAddress(jid);
      XmppTransportAddress ta1 = new XmppTransportAddress("brunet.xmpp://" + sjid);
      XmppTransportAddress ta2 = (XmppTransportAddress) XmppTransportAddress.Create("brunet.xmpp://" + sjid);
      Assert.AreEqual(ta0, ta1, "TA comparison");
      Assert.AreEqual(ta0, ta2, "TA comparison 2");
      Assert.AreEqual(jid, ta1.JID, "JID comparison");
    }
  }
#endif
}
