/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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

using System;
using System.Collections;
using System.Net;
using System.Text;
using System.Threading;

#if BRUNET_NUNIT
using System.Collections.Specialized;
using NUnit.Framework;
#endif


namespace Brunet
{

  /**
   * Represents the addresses used to transport the Brunet
   * protocol over lower layers (such as IP).  The transport
   * address is used when one host wants to connect to another
   * host in order to route Brunet packets.
   */

  public class TransportAddressFactory {
    //adding some kind of factory methods
    public static TransportAddress CreateInstance(string s) {
      Cache ta_cache = Interlocked.Exchange<Cache>(ref _ta_cache, null);
      TransportAddress result = null;
      if( ta_cache != null ) {
        try {
          result = (TransportAddress)ta_cache[s];
          if( result == null ) {
            result = NoCacheCreateInstance(s);
            string r_ts = result.ToString();
            if( r_ts.Equals(s) ) {
              //Keep the internal reference which is being saved already
              s = r_ts;
            }
            ta_cache[ s ] = result;
          }
        }
        finally {
          Interlocked.Exchange<Cache>(ref _ta_cache, ta_cache);
        }
      }
      else {
        result = NoCacheCreateInstance(s);
      }
      return result;
    }
    protected static TransportAddress NoCacheCreateInstance(string s) {
      string scheme = s.Substring(0, s.IndexOf(":"));
      string t = scheme.Substring(scheme.IndexOf('.') + 1);
      //Console.Error.WriteLine(t);
      
      TransportAddress result = null;
      TransportAddress.TAType ta_type = StringToType(t);
      
      switch(ta_type) {
        case TransportAddress.TAType.Tcp:
          result = new IPTransportAddress(s);
          break;
        case TransportAddress.TAType.Udp:
          result = new IPTransportAddress(s);
          break;
        case TransportAddress.TAType.Function:
          result = new IPTransportAddress(s);
          break;
        case TransportAddress.TAType.Tls:
          result = new IPTransportAddress(s);
          break;
        case TransportAddress.TAType.TlsTest:
          result = new IPTransportAddress(s);
          break;
        case TransportAddress.TAType.Tunnel:
          result = new TunnelTransportAddress(s);
          break;
      }

      return result;
    }
    

    protected static Hashtable _string_to_type;
    /*
     * Parsing strings into TransportAddress objects is pretty 
     * expensive (according to the profiler).  Since both
     * strings and TransportAddress objects are immutable,
     * we can keep a cache of them so we don't have to waste
     * time doing multiple TAs over and over again.
     */
    protected static Cache _ta_cache;
    protected const int CACHE_SIZE = 1024;
    
    static TransportAddressFactory() {
      _string_to_type = new Hashtable();
      _ta_cache = new Cache(CACHE_SIZE);
    }

    public static TransportAddress.TAType StringToType(string s) {
      lock( _string_to_type ) {
        object t = _string_to_type[s];
        if( t == null ) {
          t = System.Enum.Parse(typeof(TransportAddress.TAType), s, true);
          _string_to_type[ String.Intern(s) ] = t;
        }
        return (TransportAddress.TAType)t;
      }
    }

    public static TransportAddress CreateInstance(TransportAddress.TAType t,
              string host, int port) {  
      Cache ta_cache = Interlocked.Exchange<Cache>(ref _ta_cache, null);
      if( ta_cache != null ) {
        TransportAddress ta = null;
        try {
          CacheKey key = new CacheKey(host, port, t);
          ta = (TransportAddress) ta_cache[key];
          if( ta == null ) {
            ta = new IPTransportAddress(t, host, port);
            ta_cache[key] = ta; 
           }
        }
        finally {
          Interlocked.Exchange<Cache>(ref _ta_cache, ta_cache);
        }
        return ta;
      }
      else {
        return new IPTransportAddress(t, host, port);
      }
    }

    public static TransportAddress CreateInstance(TransportAddress.TAType t,
                            IPAddress host, int port) {
      Cache ta_cache = Interlocked.Exchange<Cache>(ref _ta_cache, null);
      if( ta_cache != null ) {
        TransportAddress ta = null;
        try {
          CacheKey key = new CacheKey(host, port, t);
          ta = (TransportAddress) ta_cache[key];
          if( ta == null ) {
            ta = new IPTransportAddress(t, host, port);
            ta_cache[key] = ta; 
           }
        }
        finally {
          Interlocked.Exchange<Cache>(ref _ta_cache, ta_cache);
        }
        return ta;
      }
      else {
        return new IPTransportAddress(t, host, port);
      }
    }

    public static TransportAddress CreateInstance(TransportAddress.TAType t,
           IPEndPoint ep){
      return CreateInstance(t, ep.Address, ep.Port);
    }

    protected class IPTransportEnum : IEnumerable {
      TransportAddress.TAType _tat;
      int _port;
      IEnumerable _ips;

      public IPTransportEnum(TransportAddress.TAType tat, int port, IEnumerable ips) {
        _tat = tat;
        _port = port;
        _ips = ips;
      }

      public IEnumerator GetEnumerator() {
        foreach(IPAddress ip in _ips) {  
          yield return CreateInstance(_tat, ip, _port);  
        }
      }
    }

    /**
     * Creates an IEnumerable of TransportAddresses for a fixed type and port,
     * over a list of IPAddress objects.
     * Each time this the result is enumerated, ips.GetEnumerator is called,
     * so, if it changes, that is okay, (this is like a map() over a list, and
     * the original list can change).
     */
    public static IEnumerable Create(TransportAddress.TAType tat, int port, IEnumerable ips)
    {
      return new IPTransportEnum(tat, port, ips);
    }
    
    /**
     * This gets the name of the local machine, then does a DNS lookup on that
     * name, and finally does the same as TransportAddress.Create for that
     * list of IPAddress objects.
     *
     * If the DNS hostname is not correctly configured, it will return the
     * loopback address.
     */
    public static IEnumerable CreateForLocalHost(TransportAddress.TAType tat, int port) {
      try {
        string StrLocalHost = Dns.GetHostName();
        IPHostEntry IPEntry = Dns.GetHostEntry(StrLocalHost);
        return Create(tat, port, IPEntry.AddressList);
      }
      catch(Exception) {
        //Oh, well, that didn't work.
        ArrayList tas = new ArrayList(1);
        //Just put the loopback address, it might help us talk to some other
        //local node.
        tas.Add( CreateInstance(tat, new IPEndPoint(IPAddress.Loopback, port) ) );
        return tas;
      }
    }    
  }

  public abstract class TransportAddress:IComparable
  {
    public enum TAType
    {
      Unknown,
      Tcp,
      Udp,
      Function,
      Tls,
      TlsTest,
      Tunnel,
    }

   protected static readonly string _UDP_S = "udp";
   protected static readonly string _TCP_S = "tcp";
   protected static readonly string _FUNCTION_S = "function";
   protected static readonly string _TUNNEL_S = "tunnel";
    /**
     * .Net methods are not always so fast here
     */
    public static string TATypeToString(TAType t) {
      switch(t) {
        case TAType.Udp:
          return _UDP_S;
        case TAType.Tunnel:
          return _TUNNEL_S;
        case TAType.Tcp:
          return _TCP_S;
        case TAType.Function:
          return _FUNCTION_S;
        default:
          return t.ToString().ToLower();
      }
    }
    protected readonly string _string_rep;
    /**
     * URI objects are pretty expensive, don't keep this around
     * since we won't often use it
     */
    protected System.Uri _parsed_uri;
    public System.Uri Uri {
      get {
        if( _parsed_uri == null ) {
          _parsed_uri = new Uri(_string_rep);
        }
        return _parsed_uri;
      }
    }

    protected TransportAddress(string s) {
      _string_rep = s;
    }
    
    public override string ToString() {
      return _string_rep;
    }
    
    public abstract TAType TransportAddressType { get;}

    public int CompareTo(object ta)
    {
      if (ta is TransportAddress) {
        ///@todo it would be nice to do a comparison that is not string based:
        return this.ToString().CompareTo(ta.ToString());
      }
      else {
        return -1;
      }
    }
  }

  public class IPTransportAddress: TransportAddress {
    protected ArrayList _ips = null;
    public string Host { get { return Uri.Host; } }
    public int Port { get { return Uri.Port; } }
    protected TAType _type = TAType.Unknown;

    public override TAType TransportAddressType
    {
      get {
        if( _type == TAType.Unknown ) {
          string t = Uri.Scheme.Substring(Uri.Scheme.IndexOf('.') + 1);
          _type = TransportAddressFactory.StringToType(t);
        }
        return _type;
      }
    }

    public override bool Equals(object o) {
      if ( o == this ) { return true; }
      IPTransportAddress other = o as IPTransportAddress;
      if ( other == null ) { return false; }
      return Uri.Equals( other.Uri );  
    }

    public override int GetHashCode() {
      return Uri.GetHashCode();
    }

    public IPTransportAddress(string uri_s) : base(uri_s) { 
      _ips = null;
    }

    public IPTransportAddress(TransportAddress.TAType t, string host, int port) :
      this(String.Format("brunet.{0}://{1}:{2}", TATypeToString(t), host, port))
    {
      _type = t;
      _ips = null;
    }

    public IPTransportAddress(TransportAddress.TAType t, IPAddress addr, int port):
      this(String.Format("brunet.{0}://{1}:{2}", TATypeToString(t), addr, port))
    {
      _type = t;
      _ips = new ArrayList(1);
      _ips.Add( addr );
    }

    public IPAddress GetIPAddress()
    {
      if ( _ips != null && _ips.Count > 0) {
        return (IPAddress) _ips[0];
      }

      IPAddress a = IPAddress.Parse(Uri.Host);
      _ips = new ArrayList(1);
      _ips.Add(a);
      return a;
    }
  }

  public class TunnelTransportAddress: TransportAddress {
    protected Address _target;
    public Address Target { get { return _target; } }
    //in this new implementation, we have more than one packer forwarders
    protected ArrayList _forwarders;

    public TunnelTransportAddress(string s) : base(s) {
      /** String representing the tunnel TA is as follows: brunet.tunnel://A/X1+X2+X3
       *  A: target address
       *  X1, X2, X3: forwarders, each X1, X2 and X3 is actually a slice of the initial few bytes of the address.
       */
      int k = s.IndexOf(":") + 3;
      int k1 = s.IndexOf("/", k);
      byte []addr_t  = Base32.Decode(s.Substring(k, k1 - k)); 
      _target = AddressParser.Parse( MemBlock.Reference(addr_t) );
      k = k1 + 1;
      _forwarders = new ArrayList();
      while (k < s.Length) {
        byte [] addr_prefix = Base32.Decode(s.Substring(k, 8));
        _forwarders.Add(MemBlock.Reference(addr_prefix));
        //jump over the 8 characters and the + sign
        k = k + 9;
      }
      _forwarders.Sort();
    }

    public TunnelTransportAddress(Address target, IEnumerable forwarders): 
      this(GetString(target, forwarders)) 
    {
    }

    private static string GetString(Address target, IEnumerable forwarders) {
      StringBuilder sb = new StringBuilder();
      sb.Append("brunet.tunnel://");
      sb.Append(target.ToString().Substring(12));
      sb.Append("/");
      foreach(object forwarder in forwarders) {
        Address addr = forwarder as Address;
        if(addr == null) {
          addr = (forwarder as Connection).Address;
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
        return TransportAddress.TAType.Tunnel;
      }
    }

    public override bool Equals(object o) {
      if ( o == this ) { return true; }
      TunnelTransportAddress other = o as TunnelTransportAddress;
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
      MemBlock test_mem = MemBlock.Reference(Base32.Decode(addr.ToString().Substring(12, 8)));
      return _forwarders.Contains(test_mem);
    }

    public override int GetHashCode() {
      return _target.GetHashCode();
    }
  }
#if BRUNET_NUNIT

  [TestFixture]
  public class TATester {
    [Test]
    public void TestTATypeToString() {
      foreach(TransportAddress.TAType t in
              Enum.GetValues(typeof(TransportAddress.TAType))) {
        string s = t.ToString().ToLower();
        Assert.AreEqual(s, TransportAddress.TATypeToString(t), "TATypeToString");
      }
    }

    [Test]
    public void Test() {
      TransportAddress ta1 = TransportAddressFactory.CreateInstance("brunet.udp://10.5.144.69:5000");
      Assert.AreEqual(ta1.ToString(), "brunet.udp://10.5.144.69:5000", "Testing TA parsing");
      
      TransportAddress ta2 = TransportAddressFactory.CreateInstance("brunet.udp://10.5.144.69:5000"); 
      Assert.AreEqual(ta1, ta2, "Testing TA Equals");
      
      string ta_string = "brunet.tunnel://UBU72YLHU5C3SY7JMYMJRTKK4D5BGW22/FE4QWASN+FE4QWASM";
      TransportAddress ta = TransportAddressFactory.CreateInstance("brunet.tunnel://UBU72YLHU5C3SY7JMYMJRTKK4D5BGW22/FE4QWASN+FE4QWASM");
      Assert.AreEqual(ta.ToString(), ta_string, "testing tunnel TA parsing");
      //Console.WriteLine(ta);

      TunnelTransportAddress tun_ta = (TunnelTransportAddress) TransportAddressFactory.CreateInstance("brunet.tunnel://OIHZCNNUAXTLLARQIOBNCUWXYNAS62LO/CADSL6GV+CADSL6GU");

      ArrayList fwd = new ArrayList();
      fwd.Add(new AHAddress(Base32.Decode("CADSL6GVVBM6V442CETP4JTEAWACLC5A")));
      fwd.Add(new AHAddress(Base32.Decode("CADSL6GUVBM6V442CETP4JTEAWACLC5A")));
      
      TunnelTransportAddress test_ta = new TunnelTransportAddress(tun_ta.Target, fwd);
      Assert.AreEqual(tun_ta, test_ta, "testing tunnel TA compression enhancements");
      //Console.WriteLine(tun_ta.ToString());
      //Console.WriteLine(test_ta.ToString());
      Assert.AreEqual(tun_ta.ToString(), test_ta.ToString(), "testing tunnel TA compression enhancements (toString)");

      Assert.AreEqual(tun_ta.ContainsForwarder(new AHAddress(Base32.Decode("CADSL6GVVBM6V442CETP4JTEAWACLC5A"))), true, 
          "testing tunnel TA contains forwarder (1)");

      Assert.AreEqual(tun_ta.ContainsForwarder(new AHAddress(Base32.Decode("CADSL6GUVBM6V442CETP4JTEAWACLC5A"))), true, 
          "testing tunnel TA contains forwarder (2)");

      
      
      string StrLocalHost = Dns.GetHostName();
      IPHostEntry IPEntry = Dns.GetHostEntry(StrLocalHost);
      TransportAddress local_ta = TransportAddressFactory.CreateInstance("brunet.udp://" +  IPEntry.AddressList[0].ToString() + 
                   ":" + 5000);
      IEnumerable locals = TransportAddressFactory.CreateForLocalHost(TransportAddress.TAType.Udp, 5000);

      bool match = false;
      foreach (TransportAddress test_ta1 in locals) {
        //Console.WriteLine("test_ta: {0}", test_ta1);
        if (test_ta1.Equals(local_ta)) {
          match = true;
        }
      }
      Assert.AreEqual(match, true, "testing local TA matches");
      //testing function TA
      TransportAddress func_ta = TransportAddressFactory.CreateInstance("brunet.function://localhost:3000");
      TransportAddress func_ta2 = TransportAddressFactory.CreateInstance("brunet.function://localhost:3000");
      Assert.AreEqual(func_ta, func_ta2, "equality of instances");
      Assert.IsTrue(func_ta == func_ta2, "reference equality, test of caching");
      Assert.AreEqual(func_ta.ToString(), "brunet.function://localhost:3000", "Testing function TA parsing");
      
    }
  }
#endif
}
