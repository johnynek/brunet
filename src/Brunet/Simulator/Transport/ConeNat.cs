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
  public class ConeNat : INat {
    protected TimeBasedCache<TransportAddress, bool> _allowed;
    protected TransportAddress[] _external_ta;
    protected TransportAddress[] _internal_ta;
    protected TransportAddress[] _known_tas;
    protected bool _allow_inbound;

    public ConeNat(TransportAddress ta, int timeout)
    {
      _external_ta = new TransportAddress[1] { ta };
      _internal_ta = new TransportAddress[1] { ((SimulationTransportAddress) ta).Invert() };
      _known_tas = _internal_ta;
      // TBC uses a staged GC, so values are still in after one timeout
      _allowed = new TimeBasedCache<TransportAddress, bool>(timeout / 2);
      _allowed.EvictionHandler += HandleEviction;
      _allow_inbound = false;
    }

    /// <summary>If there are no more entries in the cache, our mapping has, we
    /// will no longer allow inbound connections.</summary>
    private void HandleEviction(object sender,
        TimeBasedCache<TransportAddress, bool>.EvictionArgs ea)
    {
      _allow_inbound = _allowed.Count > 0;
    }

    public bool AllowingIncomingConnections { get { return _allow_inbound; } }
    public IEnumerable ExternalTransportAddresses { get { return _external_ta; } }
    public IEnumerable InternalTransportAddresses { get { return _internal_ta; } }
    public IEnumerable KnownTransportAddresses { get { return _known_tas; } }
    
    virtual public bool Incoming(TransportAddress ta)
    {
      return _allowed.Count > 0;
    }

    public bool Outgoing(TransportAddress ta)
    {
      _allowed.Update(ta, true);
      return true;
    }

    public void UpdateTAs(TransportAddress remote_ta, TransportAddress local_ta)
    {
      if(_known_tas.Length == 2) {
        return;
      }
      _known_tas = new TransportAddress[2] { _internal_ta[0], local_ta };
      _allow_inbound = true;
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class ConeNatTest {
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
      ConeNat nat = new ConeNat(ta, 30000);
      Assert.IsFalse(nat.Incoming(ta_oth), "No outbound yet...");
      Assert.IsTrue(nat.Outgoing(ta_oth), "outbound...");
      Assert.IsFalse(nat.AllowingIncomingConnections, "Have not received external ta.");
      Assert.AreEqual(nat.InternalTransportAddresses, nat.KnownTransportAddresses, "ITA and KTA match");

      nat.UpdateTAs(ta_oth, ta);
      Assert.IsTrue(nat.Incoming(ta_oth), "Allowed incoming");
      Assert.IsTrue(nat.Incoming(ta_oth0), "Allowed incoming 0");
      Assert.IsTrue(nat.AllowingIncomingConnections, "Have received external ta.");
      Assert.AreEqual(tas, nat.KnownTransportAddresses, "Two TAs!");

      Brunet.Util.SimpleTimer.RunSteps(7500);
      Assert.IsTrue(nat.Incoming(ta_oth0), "Allowed incoming 0");
      Brunet.Util.SimpleTimer.RunSteps(7500);
      Assert.IsTrue(nat.Incoming(ta_oth0), "Allowed incoming 0");
      Brunet.Util.SimpleTimer.RunSteps(7500);
      Assert.IsTrue(nat.Incoming(ta_oth0), "Allowed incoming 0");
      Brunet.Util.SimpleTimer.RunSteps(7500);
      Assert.IsTrue(nat.Incoming(ta_oth0), "Allowed incoming 0");
      Assert.IsTrue(nat.AllowingIncomingConnections, "Have received external ta.");
      Assert.AreEqual(tas, nat.KnownTransportAddresses, "Two TAs!");

      Brunet.Util.SimpleTimer.RunSteps(60000);
      Assert.IsFalse(nat.AllowingIncomingConnections, "AllowIC:  Timed out...");
      Assert.IsFalse(nat.Incoming(ta_oth), "Incoming:  Timed out....");
    }
  }
#endif
}
