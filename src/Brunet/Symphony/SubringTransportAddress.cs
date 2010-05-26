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

using Brunet.Util;
using Brunet.Transport;

using System;
using System.Collections;
using System.Collections.Generic;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Symphony {
  ///<summary>Stores SubringTransportAddress in the form of brunet.subring://$Address
  ///The Address should be the shared overlays Address.</summary>
  public class SubringTransportAddress : TransportAddress {
    public readonly AHAddress Target;
    public readonly string Namespace;

    static SubringTransportAddress()
    {
      TransportAddressFactory.AddFactoryMethod("subring", Create);
    }

    public SubringTransportAddress(string s) : base(s)
    {
      int addr_start = s.IndexOf(":") + 3;
      int addr_end = s.IndexOf(".", addr_start);
      int ns_start = addr_end + 1;
      int ns_end = Math.Max(s.Length, s.IndexOf("/", ns_start));

      byte[] addr = Base32.Decode(s.Substring(addr_start, addr_end - addr_start));
      Target = new AHAddress(MemBlock.Reference(addr));
      Namespace = s.Substring(ns_start, ns_end - ns_start);
    }

    public SubringTransportAddress(AHAddress target, string node_ns) :
      base(string.Format("brunet.{0}://{1}.{2}", TATypeToString(TAType.Subring),
            target.ToMemBlock().ToBase32String(), node_ns))
    {
      Target = target;
      Namespace = node_ns;
    }

    public static TransportAddress Create(string s)
    {
      return new SubringTransportAddress(s);
    }

    public override TAType TransportAddressType { 
      get {
        return TransportAddress.TAType.Subring;
      }
    }

    public override bool Equals(object o)
    {
      if(o == this) {
        return true;
      }

      SubringTransportAddress other = o as SubringTransportAddress;
      if(other == null) {
        return false;
      }

      return Target.Equals(other.Target);
    }

    public override int GetHashCode() {
      return Target.GetHashCode();
    }
  }
#if BRUNET_NUNIT

  [TestFixture]
  public class SubringTATester {
    [Test]
    public void Test() {
      SubringTransportAddress sta =new SubringTransportAddress(
          new AHAddress(Base32.Decode("CADSL6GVVBM6V442CETP4JTEAWACLC5A")),
          "ns.ns0.ns1");
      string ta_string = "brunet.subring://CADSL6GVVBM6V442CETP4JTEAWACLC5A.ns.ns0.ns1";
      TransportAddress ta = TransportAddressFactory.CreateInstance(ta_string);
      Assert.AreEqual(ta, sta, "TA == STA -- 1");

      ta_string = "brunet.subring://CADSL6GVVBM6V442CETP4JTEAWACLC5A.ns.ns0.ns1/";
      ta = TransportAddressFactory.CreateInstance(ta_string);
      Assert.AreEqual(ta, sta, "TA == STA -- 2");
    }
  }
#endif
}
