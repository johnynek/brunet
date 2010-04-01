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
