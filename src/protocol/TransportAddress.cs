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
      string scheme = s.Substring(0, s.IndexOf(":"));
      string t = scheme.Substring(scheme.IndexOf('.') + 1);
      //Console.WriteLine(t);
      TransportAddress.TAType ta_type =  
	(TransportAddress.TAType) System.Enum.Parse(typeof(TransportAddress.TAType), t, true);
      
      
      if (ta_type ==  TransportAddress.TAType.Tcp) {
	return new IPTransportAddress(s);
      }
      if (ta_type ==  TransportAddress.TAType.Udp) {
	return new IPTransportAddress(s);
      }
      if (ta_type ==  TransportAddress.TAType.Function) {
	return new IPTransportAddress(s);
      }
      if (ta_type ==  TransportAddress.TAType.Tls) {
	return new IPTransportAddress(s);
      }
      if (ta_type ==  TransportAddress.TAType.TlsTest) {
	return new IPTransportAddress(s);
      }
      if (ta_type ==  TransportAddress.TAType.Tunnel) {
	return new TunnelTransportAddress(s);
      }
      return null;
    }
    public static TransportAddress CreateInstance(TransportAddress.TAType t,
						  string host, int port) {
      
      return new IPTransportAddress(t, host, port);
    }
    public static TransportAddress CreateInstance(TransportAddress.TAType t,
                            System.Net.IPAddress add, int port) {
      return new IPTransportAddress(t, add, port);
    }

    public static TransportAddress CreateInstance(TransportAddress.TAType t,
				   System.Net.IPEndPoint ep) {
      return new IPTransportAddress(t, ep);
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
          yield return new IPTransportAddress(_tat, new IPEndPoint(ip, _port) );  
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
        IPHostEntry IPEntry = Dns.GetHostByName(StrLocalHost);
        return Create(tat, port, IPEntry.AddressList);
      }
      catch(Exception) {
        //Oh, well, that didn't work.
        ArrayList tas = new ArrayList();
        //Just put the loopback address, it might help us talk to some other
        //local node.
        tas.Add( new IPTransportAddress(tat, new IPEndPoint(IPAddress.Loopback, port) ) );
        return tas;
      }
    }    
  }

  public abstract class TransportAddress:IComparable
  {
    
    protected string _scheme;

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
    protected TransportAddress(string s) {
      _scheme = s;
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
    protected System.Uri _uri = null;
    
    public string Host {
      get {
	return _uri.Host;
      }
    }
    public int Port {
      get {
	return _uri.Port;
      }
    }
    public override TAType TransportAddressType
    {
      get {
        string t = _uri.Scheme.Substring(_uri.Scheme.IndexOf('.') + 1);
        return (TAType) System.Enum.Parse(typeof(TAType), t, true);
      }
    }
    public override string ToString() {
      return _uri.ToString();
    }
    public override bool Equals(object o) {
      if ( o == this ) { return true; }
      IPTransportAddress other = o as IPTransportAddress;
      if ( other == null ) { return false; }
      return _uri.Equals( other._uri );  
    }
    public override int GetHashCode() {
      return _uri.GetHashCode();
    }
    public IPTransportAddress(string uri):base(uri) { 
      _uri = new Uri(uri);
      _ips = null;
    }
    
    public IPTransportAddress(TransportAddress.TAType t,
                            string host, int port):
      this("brunet." + t.ToString().ToLower() + "://"
	   + host + ":" + port.ToString())
    {
      _ips = null;
    }
    public IPTransportAddress(TransportAddress.TAType t,
                            System.Net.IPAddress add, int port):
          this("brunet." + t.ToString().ToLower() + "://"
         + add.ToString() + ":" + port.ToString())
    {
      _ips = new ArrayList();
      _ips.Add( add );
    }
    public IPTransportAddress(TransportAddress.TAType t,
                            System.Net.IPEndPoint ep) :
      this(t, ep.Address, ep.Port) {
    }

    public ArrayList GetIPAddresses()
    {
      if ( _ips != null ) {
        return _ips;
      }

      try {
        IPAddress a = IPAddress.Parse(_uri.Host);
        _ips = new ArrayList();
        _ips.Add(a);
        return _ips;
      }
      catch(Exception) {

      }

      try {
        IPHostEntry IPHost = Dns.Resolve(_uri.Host);
        _ips = new ArrayList(IPHost.AddressList);
      } catch(Exception e) {
        // log this exception!
	System.Console.Error.WriteLine("In GetIPAddress() Resolving {1}: {0}",
                                        e, _uri.Host);
      }
      return _ips;
    }

  }
  public class TunnelTransportAddress: TransportAddress {
    protected Address _target;
    public Address Target {
      get {
	return _target;
      }
    }
    

    protected Address _forwarder;
    public Address Forwarder {
      get {
	return _forwarder;
      }
    }
    public TunnelTransportAddress(string s):base(s) {
      int k = s.IndexOf(":") + 3;
      //k is at beginning of something like brunet:node:xxx/brunet:node:yyy
      k = k + 12;
      int len = 0;
      while(s[k + len] != '/') {
	len++;
      }
      byte []addr_t  = Base32.Decode(s.Substring(k, len)); 
      _target = new AHAddress(addr_t);
      

      k = k + len + 1;
      k = k + 12;

      byte [] addr_f = Base32.Decode(s.Substring(k));
      _forwarder = new AHAddress(addr_f);
    }

    public TunnelTransportAddress(Address target, Address forwarder):
      base("brunet.tunnel://" +  
	   target.ToString() + "/" + forwarder.ToString()) {
      _target = target;
      _forwarder = forwarder;
    }

    public override TAType TransportAddressType { 
      get {
	return TransportAddress.TAType.Tunnel;
      }
    }
    public override string ToString() {
      return "brunet.tunnel://" + _target.ToString() + "/" + _forwarder.ToString();
    }
    public override bool Equals(object o) {
      if ( o == this ) { return true; }
      TunnelTransportAddress other = o as TunnelTransportAddress;
      if ( other == null ) { return false; }
      return (TransportAddressType == other.TransportAddressType && 
	      Target.Equals(other.Target) && 
	      Forwarder.Equals(other.Forwarder));
    }
    public override int GetHashCode() {
      return base.GetHashCode();
    }
  }
#if BRUNET_NUNIT

  [TestFixture]
  public class TATester {
    [Test]
    public void Test() {
      TransportAddress ta1 = TransportAddressFactory.CreateInstance("brunet.udp://10.5.144.69:5000");
      Assert.AreEqual(ta1.ToString(), "brunet.udp://10.5.144.69:5000/", "Testing TA parsing");
      
      TransportAddress ta2 = TransportAddressFactory.CreateInstance("brunet.udp://10.5.144.69:5000"); 
      Assert.AreEqual(ta1, ta2, "Testing TA Equals");
      
      string ta_string = "brunet.tunnel://brunet:node:UBU72YLHU5C3SY7JMYMJRTKK4D5BGW22/brunet:node:FE4QWASNSYAR5RH5JHSHJECC7M3AAADE";
      TransportAddress ta = TransportAddressFactory.CreateInstance("brunet.tunnel://brunet:node:UBU72YLHU5C3SY7JMYMJRTKK4D5BGW22/brunet:node:FE4QWASNSYAR5RH5JHSHJECC7M3AAADE");
      Assert.AreEqual(ta.ToString(), ta_string, "testing tunnel TA parsing");
    }
  }
#endif
}
