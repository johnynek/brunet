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

using System;
using Brunet.Transport;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif 

namespace Brunet.Simulator.Transport {
  /// <summary>TransportAddress class for Simulations.  TAs of the form:
  /// b.s://ID.</summary>
  public class SimulationTransportAddress: TransportAddress {
    public readonly int ID;
    public override TAType TransportAddressType { get { return TAType.S; } }

    static SimulationTransportAddress() {
      TransportAddressFactory.AddFactoryMethod("s", Create);
    }

    /// <summary> Force the static constructor to be called.</summary.
    static public void Enable() {
    }

    private static TransportAddress Create(string s) {
      return new SimulationTransportAddress(s);
    }

    public override bool Equals(object o) {
      if ( o == this ) { return true; }
      SimulationTransportAddress other = o as SimulationTransportAddress;
      return other != null ? ID == other.ID : false;
    }

    public override int GetHashCode() {
      return base.GetHashCode();
    }

    public SimulationTransportAddress(string s) : base(s)
    {
      int pos = s.IndexOf(":") + 3;
      int end = s.IndexOf("/", pos);
      end = end > 0 ? end : s.Length;
      ID = Int32.Parse(s.Substring(pos, end - pos));
    }

    public SimulationTransportAddress(int id) : this(id, TAType.S)
    {
    }

    public SimulationTransportAddress Invert()
    {
      var ta = TransportAddressFactory.CreateInstance(GetString(TransportAddressType, ~ID));
      return ta as SimulationTransportAddress;
    }

    public static string GetString(TransportAddress.TAType ta_type, int id)
    {
      return String.Format("b.{0}://{1}", TransportAddress.TATypeToString(ta_type), id);
    }

    protected SimulationTransportAddress(int id, TransportAddress.TAType type) :
      this(GetString(type, id))
    {
      ID = id;
    }
  }

  /// <summary>This is a dummy class so that we can support multiple SimELs in
  /// the same address space, in the same simulation without them
  /// overlapping.  TAs of the form b:so://ID.</summary>
  public class SimulationTransportAddressOther: SimulationTransportAddress {
    public override TAType TransportAddressType { get { return TAType.SO; } }

    static SimulationTransportAddressOther() {
      TransportAddressFactory.AddFactoryMethod("so", Create);
    }

    /// <summary> Force the static constructor to be called.</summary.
    new static public void Enable() {
    }

    private static TransportAddress Create(string s) {
      return new SimulationTransportAddressOther(s);
    }

    public override bool Equals(object o) {
      if ( o == this ) { return true; }
      var other = o as SimulationTransportAddressOther;
      return other != null ? ID == other.ID : false;
    }

    public override int GetHashCode() {
      return base.GetHashCode();
    }

    public SimulationTransportAddressOther(string s) : base(s)
    {
    }

    public SimulationTransportAddressOther(int id) : base(id, TAType.SO)
    {
    }
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class SimulationTATest {
    [Test]
    public void Test()
    {
      SimulationTransportAddress.Enable();
      SimulationTransportAddressOther.Enable();
      TransportAddress tas = TransportAddressFactory.CreateInstance("b.s://234580");
      Assert.AreEqual(tas.ToString(), "b.s://234580", "Simulation string");
      Assert.AreEqual((tas as SimulationTransportAddress).ID, 234580, "Simulation id");
      Assert.AreEqual(TransportAddress.TAType.S, tas.TransportAddressType, "Simulation ta type");

      TransportAddress taso = TransportAddressFactory.CreateInstance("b.so://234580");
      Assert.AreEqual(taso.ToString(), "b.so://234580", "Simulation string");
      Assert.AreEqual((taso as SimulationTransportAddressOther).ID, 234580, "Simulation id");
      Assert.AreEqual(TransportAddress.TAType.SO, taso.TransportAddressType, "Simulation ta type");

      Assert.AreNotEqual(taso, tas, "TAs not equal");
      Assert.AreNotEqual(taso.TransportAddressType, tas.TransportAddressType, "Type not equal");

      var tas1 = TransportAddressFactory.CreateInstance("b.s://-234581");
      Assert.AreEqual(-234581, (tas1 as SimulationTransportAddress).ID);
      var tas2 = (tas as SimulationTransportAddress).Invert();
      Assert.AreEqual(tas1, tas2, "Invert equal");
    }
  }
#endif
}
