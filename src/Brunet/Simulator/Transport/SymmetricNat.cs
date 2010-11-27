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

using System.Collections;
using Brunet.Transport;
using Brunet.Collections;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif 

namespace Brunet.Simulator.Transport {
  /// <summary> Supports a very simple Cone NAT.  Once the node has learned its
  /// own address and it has a stable connection to another peer, it allows
  /// inbound connections.  Incoming messages are allow so long as there is an
  /// existing NAT mapping.</summary>
  public class SymmetricNat : RestrictedConeNat {
    public SymmetricNat(TransportAddress ta, int timeout) : base(ta, timeout)
    {
    }

    override public bool AllowingIncomingConnections { get { return false; } }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class SymmetricNatTest {
    [Test]
    public void Test()
    {
      SimulationTransportAddress.Enable();
      SimulationTransportAddressOther.Enable();
      var ta = TransportAddressFactory.CreateInstance("b.s://234580") as SimulationTransportAddress;
      var tai = ta.Invert();
      TransportAddress[] tas = new TransportAddress[2] { tai, ta };
      var ta_oth = TransportAddressFactory.CreateInstance("b.s://234581");
      var ta_oth0 = TransportAddressFactory.CreateInstance("b.s://234582");
      var nat = new SymmetricNat(ta, 30000);
      Assert.IsFalse(nat.Incoming(ta_oth), "No outbound yet...");
      Assert.IsTrue(nat.Outgoing(ta_oth), "outbound...");
      Assert.IsFalse(nat.AllowingIncomingConnections, "SymmetricNat does not allow incoming cons");
      Assert.AreEqual(nat.InternalTransportAddresses, nat.KnownTransportAddresses, "ITA and KTA match");

      nat.UpdateTAs(ta_oth, ta);
      Assert.IsTrue(nat.Incoming(ta_oth), "Allowed incoming");
      Assert.IsFalse(nat.Incoming(ta_oth0), "Port mapped systems must send out a packet first...");
      Assert.IsFalse(nat.AllowingIncomingConnections, "SymmetricNat does not allow incoming cons");
      Assert.AreEqual(tas, nat.KnownTransportAddresses, "Two TAs!");
      Assert.IsTrue(nat.Outgoing(ta_oth0), "outbound...");

      Brunet.Util.SimpleTimer.RunSteps(7500);
      Assert.IsTrue(nat.Incoming(ta_oth0), "Allowed incoming 0");
      Brunet.Util.SimpleTimer.RunSteps(7500);
      Assert.IsTrue(nat.Incoming(ta_oth0), "Allowed incoming 0");
      Brunet.Util.SimpleTimer.RunSteps(7500);
      Assert.IsTrue(nat.Incoming(ta_oth0), "Allowed incoming 0");
      Brunet.Util.SimpleTimer.RunSteps(7500);
      Assert.IsTrue(nat.Incoming(ta_oth0), "Allowed incoming 0");
      Assert.IsFalse(nat.AllowingIncomingConnections, "SymmetricNat does not allow incoming cons");
      Assert.AreEqual(tas, nat.KnownTransportAddresses, "Two TAs!");

      Brunet.Util.SimpleTimer.RunSteps(60000);
      Assert.IsFalse(nat.AllowingIncomingConnections, "SymmetricNat does not allow incoming cons");
      Assert.IsFalse(nat.Incoming(ta_oth), "Incoming:  Timed out....");
    }
  }
#endif
}
