/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using Brunet.Collections;

#if BRUNET_NUNIT
using System.Collections.Specialized;
using NUnit.Framework;
#endif

using Brunet.Util;

namespace Brunet.Transport
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
      string t = scheme.Substring(scheme.IndexOf('.') + 1).ToLower();

      Converter<string, TransportAddress> factory;
      if( _ta_factories.TryGetValue(t, out factory ) ) {
        return factory(s);
      }
      else {
        throw new ParseException("Cannot parse: " + s);
      }
    }
    

    private static readonly Dictionary<string,TransportAddress.TAType> _string_to_type;
    private static readonly Dictionary<string, Converter<string,TransportAddress>> _ta_factories;
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
      _string_to_type = new Dictionary<string,TransportAddress.TAType>();
      _ta_cache = new Cache(CACHE_SIZE);
      _ta_factories = new Dictionary<string,Converter<string,TransportAddress>>();
      AddFactoryMethod("tcp", IPTransportAddress.Create);
      AddFactoryMethod("udp", IPTransportAddress.Create);
      AddFactoryMethod("function", IPTransportAddress.Create);
      AddFactoryMethod("tls", IPTransportAddress.Create);
      AddFactoryMethod("tlstest", IPTransportAddress.Create);
      //Here's the odd ball:
      AddFactoryMethod("s", delegate(string s) {
        return new SimulationTransportAddress(s);
      });
    }
    public static void AddFactoryMethod(string s, Converter<string,TransportAddress> meth) {
      lock( _ta_factories ) {
        _ta_factories[s.ToLower()] = meth;
      }
    }

    public static TransportAddress.TAType StringToType(string s) {
      TransportAddress.TAType t;
      //reading is thread-safe:
      if(! _string_to_type.TryGetValue(s, out t) ) {
        lock( _string_to_type ) {
          //This is safe, because even if another thread does this again,
          //we are going to put the same cached value in:
          t = (TransportAddress.TAType)System.Enum.Parse(typeof(TransportAddress.TAType), s, true);
          _string_to_type[ String.Intern(s) ] = t;
        }
      }
      return t;
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
      S,
      Tls,
      TlsTest,
      Tunnel,
    }

   protected static readonly string _UDP_S = "udp";
   protected static readonly string _TCP_S = "tcp";
   protected static readonly string _FUNCTION_S = "function";
   protected static readonly string _TUNNEL_S = "tunnel";
   protected static readonly string _SIMULATION_S = "s";
    /**
     * .Net methods are not always so fast here
     */
    public static string TATypeToString(TAType t) {
      switch(t) {
        case TAType.S:
          return _SIMULATION_S;
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
    public static TransportAddress Create(string s) {
      return new IPTransportAddress(s);
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

  public class SimulationTransportAddress: TransportAddress {
    public readonly int ID;
    protected TAType _type = TAType.S;
    public override TAType TransportAddressType { get { return TAType.S; } }

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

    public SimulationTransportAddress(int id) : this("b.s://" + id)
    {
      ID = id;
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
      TransportAddress tas = TransportAddressFactory.CreateInstance("b.s://234580");
      Assert.AreEqual(tas.ToString(), "b.s://234580", "Simulation string");
      Assert.AreEqual((tas as SimulationTransportAddress).ID, 234580, "Simulation id");
      Assert.AreEqual(TransportAddress.TAType.S, tas.TransportAddressType, "Simulation ta type");
      TransportAddress ta1 = TransportAddressFactory.CreateInstance("brunet.udp://10.5.144.69:5000");
      Assert.AreEqual(ta1.ToString(), "brunet.udp://10.5.144.69:5000", "Testing TA parsing");
      
      TransportAddress ta2 = TransportAddressFactory.CreateInstance("brunet.udp://10.5.144.69:5000"); 
      Assert.AreEqual(ta1, ta2, "Testing TA Equals");
      
      
      
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
