using System.Collections;
using System.Collections.Generic;
using Brunet;
using Brunet.Collections;
using Brunet.Util;
using Brunet.Transport;
using Brunet.Symphony;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Relay {
  public class RelayTransportAddress : TransportAddress {
    protected readonly Address _target;
    public Address Target { get { return _target; } }
    //in this new implementation, we have more than one packer forwarders
    protected readonly List<MemBlock> _forwarders;
    
    static RelayTransportAddress() {
      //Add support to create brunet.tunnel addresses:
      TransportAddressFactory.AddFactoryMethod("tunnel", Create);
    }

    public RelayTransportAddress(string s) : base(s) {
      /** String representing the tunnel TA is as follows: brunet.tunnel://A/X1+X2+X3
       *  A: target address
       *  X1, X2, X3: forwarders, each X1, X2 and X3 is actually a slice of the initial few bytes of the address.
       */
      int k = s.IndexOf(":") + 3;
      int k1 = s.IndexOf("/", k);
      byte []addr_t  = Base32.Decode(s.Substring(k, k1 - k)); 
      _target = AddressParser.Parse( MemBlock.Reference(addr_t) );
      k = k1 + 1;
      _forwarders = new List<MemBlock>();
      while (k < s.Length) {
        byte [] addr_prefix = Base32.Decode(s.Substring(k, 8));
        _forwarders.Add(MemBlock.Reference(addr_prefix));
        //jump over the 8 characters and the + sign
        k = k + 9;
      }
      _forwarders.Sort();
    }

    public RelayTransportAddress(Address target, IEnumerable forwarders): 
      this(GetString(target, forwarders)) 
    {
    }

    public static TransportAddress Create(string s) {
      return new RelayTransportAddress(s);
    }

    private static string GetString(Address target, IEnumerable forwarders) {
      var sb = new System.Text.StringBuilder();
      sb.Append("brunet.tunnel://");
      sb.Append(target.ToString().Substring(12));
      sb.Append("/");
      foreach(object forwarder in forwarders) {
        Address addr = forwarder as Address;
        if(addr == null) {
          addr = (forwarder as Brunet.Connections.Connection).Address;
        }
        sb.Append(addr.ToString().Substring(12,8));
        sb.Append("+");
      }
      if(sb[sb.Length - 1] == '+') {
        sb.Remove(sb.Length - 1, 1);
      }
      return sb.ToString();
    }

    public override TAType TransportAddressType { 
      get {
        return TransportAddress.TAType.Relay;
      }
    }

    public override bool Equals(object o) {
      if ( o == this ) { return true; }
      RelayTransportAddress other = o as RelayTransportAddress;
      if ( other == null ) { return false; }

      bool same = _target.Equals(other._target);
      same &= (_forwarders.Count == other._forwarders.Count);
      if( !same ) { return false; }
      for(int i = 0; i < _forwarders.Count; i++) {
        same = _forwarders[i].Equals( other._forwarders[i] );
        if( !same ) { return false; }
      }
      return true;
    }

    public bool ContainsForwarder(Address addr) {
      var test_mem = MemBlock.Reference(Base32.Decode(addr.ToString().Substring(12, 8)));
      return _forwarders.BinarySearch(test_mem) >= 0;
    }

    public override int GetHashCode() {
      return _target.GetHashCode();
    }
  }
#if BRUNET_NUNIT

  [TestFixture]
  public class TunTATester {
    [Test]
    public void Test() {
      string ta_string = "brunet.tunnel://UBU72YLHU5C3SY7JMYMJRTKK4D5BGW22/FE4QWASN+FE4QWASM";
      TransportAddress ta = TransportAddressFactory.CreateInstance("brunet.tunnel://UBU72YLHU5C3SY7JMYMJRTKK4D5BGW22/FE4QWASN+FE4QWASM");
      Assert.AreEqual(ta.ToString(), ta_string, "testing tunnel TA parsing");
      //Console.WriteLine(ta);

      RelayTransportAddress tun_ta = (RelayTransportAddress) TransportAddressFactory.CreateInstance("brunet.tunnel://OIHZCNNUAXTLLARQIOBNCUWXYNAS62LO/CADSL6GV+CADSL6GU");

      ArrayList fwd = new ArrayList();
      fwd.Add(new AHAddress(Base32.Decode("CADSL6GVVBM6V442CETP4JTEAWACLC5A")));
      fwd.Add(new AHAddress(Base32.Decode("CADSL6GUVBM6V442CETP4JTEAWACLC5A")));
      
      RelayTransportAddress test_ta = new RelayTransportAddress(tun_ta.Target, fwd);
      Assert.AreEqual(tun_ta, test_ta, "testing tunnel TA compression enhancements");
      //Console.WriteLine(tun_ta.ToString());
      //Console.WriteLine(test_ta.ToString());
      Assert.AreEqual(tun_ta.ToString(), test_ta.ToString(), "testing tunnel TA compression enhancements (toString)");

      Assert.AreEqual(tun_ta.ContainsForwarder(new AHAddress(Base32.Decode("CADSL6GVVBM6V442CETP4JTEAWACLC5A"))), true, 
          "testing tunnel TA contains forwarder (1)");

      Assert.AreEqual(tun_ta.ContainsForwarder(new AHAddress(Base32.Decode("CADSL6GUVBM6V442CETP4JTEAWACLC5A"))), true, 
          "testing tunnel TA contains forwarder (2)");
    }

  }
#endif
}
