/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System.Threading;
using System.Collections;
using System.Collections.Specialized;
using System.IO;
using Brunet.Collections;
using Brunet.Transport;
using Brunet.Util;

#if BRUNET_NUNIT 
using Brunet.Symphony;
using System.Security.Cryptography;
using NUnit.Framework;
#endif

namespace Brunet.Connections
{

  /**
   * Represents information about a node.  May be exchanged with
   * neighbors or serialized for later usage.
   */
  public class NodeInfo 
  {
    /**
     * @param a The Address of the node we are refering to
     * @param transports a list of TransportAddress objects
     */
    protected NodeInfo(Address a, IList transports)
    {
      _address = a;
      _tas = transports;
    }

    /**
     * We will often create NodeInfo objects with only one
     * TransportAddress.  This constructor makes that easy.
     */
    protected NodeInfo(Address a, TransportAddress ta) :
      this(a, new TransportAddress[] { ta })
    {
    }

    /*
     * This constructor is only used for the _cache_key object
     * so we only have to have one of them
     */
    protected NodeInfo() {
    }

    static NodeInfo()
    {
      _cache = new WeakValueTable<int, NodeInfo>();
      _cache_key = new NodeInfo();
      _ta_list = new TransportAddress[1];
    }

    //A ni_cache of currently in use NodeInfo objects
    protected static WeakValueTable<int, NodeInfo> _cache;
    protected static NodeInfo _cache_key = new NodeInfo();
    //For _cache_key when there is only one ta:
    protected static TransportAddress[] _ta_list = new TransportAddress[1];

    /**
     * Factory method to reduce memory allocations by caching
     * commonly used NodeInfo objects
     */
    public static NodeInfo CreateInstance(Address a) {
      return CreateInstance(a, EmptyTas, null);
    }

    public static NodeInfo CreateInstance(Address a, TransportAddress ta) {
      return CreateInstance(a, null, ta);
    }

    public static NodeInfo CreateInstance(Address a, IList tas) {
      return CreateInstance(a, tas, null);
    }

    protected static NodeInfo CreateInstance(Address a, IList tas, TransportAddress ta) {
      NodeInfo result = null;
      var ni_cache = Interlocked.Exchange<WeakValueTable<int, NodeInfo>>(ref _cache, null);
      if(ni_cache != null) {
        try {
          //Set up the key:
          _cache_key._done_hash = false;
          _cache_key._address = a;
          if(tas == null) {
           if(ta == null) {
              _cache_key._tas = EmptyTas;
            } else {
              _ta_list[0] = ta;
              _cache_key._tas = _ta_list;
            }
          } else {
            _cache_key._tas = tas;
          }
          
          result = ni_cache.GetValue(_cache_key.GetHashCode());
          if( !_cache_key.Equals(result) ) {
            //This may look weird, but we are using a NodeInfo as a key
            //to lookup NodeInfos, this will allow us to only keep one
            //identical NodeInfo in scope at a time.
            if(ta == null) {
              result = new NodeInfo(a, tas);
            } else {
              result = new NodeInfo(a, ta);
            }
            ni_cache.Replace(result.GetHashCode(), result);
          }
        } finally {
          Interlocked.Exchange<WeakValueTable<int, NodeInfo>>(ref _cache, ni_cache);
        }
      }
      else if(ta == null) {
        result = new NodeInfo(a, tas);
      } else {
        result = new NodeInfo(a, ta);
      }
      return result;
    }

    public static NodeInfo CreateInstance(IDictionary d) {
      Address address = null;
      IList tas;
      object addr_str = d["address"];
      if( addr_str != null ) {
        address = AddressParser.Parse((string)addr_str);
      }
      IList trans = d["transports"] as IList;
      if( trans != null ) {
        int count = trans.Count;
        tas = new TransportAddress[count];
        for(int i = 0; i < count; i++) {
          tas[i] = TransportAddressFactory.CreateInstance((string)trans[i]);
        }
        NodeInfo ni = CreateInstance(address, tas);
        return ni;
      }
      else {
        NodeInfo ni = CreateInstance(address);
        return ni;
      }
    }

    protected Address _address;
    /**
     * The address of the node (may be null)
     */
    public Address Address {
      get { return _address; }
    }

    /**
     * The first TransportAddress in the list is used often.
     * This attribute makes it easy to get it.
     * Note that it will also appear as the first position
     * in the Transports list.
     */
    public TransportAddress FirstTA {
      get { return (TransportAddress)_tas[0]; }
    }
    protected IList _tas;
    protected static readonly IList EmptyTas = new TransportAddress[0];
    /**
     * a List of the TransportAddresses associated with this node
     */
    public IList Transports {
      get { return _tas; }
    }

    /**
     * We don't only want to compute the Hash once:
     */
    protected bool _done_hash = false;
    protected int _code;

    /**
     * @return true if e is equivalent to this
     */
    public override bool Equals(object e)
    {
      if( e == this ) { return true; }
      NodeInfo ne = e as NodeInfo;
      if ( ne != null ) {
        bool same;
        if( _address != null ) {
          same = _address.Equals( ne.Address );
        }
        else {
          same = (ne.Address == null);
        }
        if( !same ) { return false; }
        //Now check the TransportAddresses:
	same = (_tas.Count == ne.Transports.Count);
	if( same ) {
	  for(int i = 0; i < _tas.Count; i++) {
            same &= _tas[i].Equals( ne.Transports[i] );
            if( !same ) { return false; }
	  }
        }
	return same;
      }
      else {
        return false;
      }
    }

    public override int GetHashCode() {
      if( !_done_hash ) {
        int code = 0;
        if( _address != null ) {
          code = _address.GetHashCode();
        }
        if( _tas.Count > 0 ) {
          code ^= _tas.Count;
          code ^= _tas[0].GetHashCode();
        }
        _code = code;
	_done_hash = true;
      }
      return _code;
    }

    protected IDictionary _as_dict;

    public IDictionary ToDictionary()
    {
      if( _as_dict != null ) {
        return _as_dict;
      }
      ListDictionary ht = new ListDictionary();
      if( _address != null ) {
        ht["address"] = _address.ToString();
      }
      if( _tas.Count > 0 ) {
        string[] trans = new string[ _tas.Count ];
        int count = _tas.Count;
        for(int i = 0; i < count; i++) {
          trans[i] = _tas[i].ToString();
        }
        ht["transports"] = trans;
      }
      Interlocked.Exchange<IDictionary>(ref _as_dict, ht);
      return ht;
    }
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class NodeInfoTest {
    public NodeInfoTest() {

    }
    public void RoundTripHT(NodeInfo ni) {
      NodeInfo ni_other = NodeInfo.CreateInstance( ni.ToDictionary() );
      Assert.AreEqual(ni, ni_other, "Hashtable roundtrip");
      Assert.AreEqual(ni.GetHashCode(), ni_other.GetHashCode(), "Hashtable GetHashCode roundtrip");
    }
    public void RoundTrip(NodeInfo ni) {
      NodeInfo ni_other = NodeInfo.CreateInstance(ni.Address, ni.Transports);
      Assert.AreEqual(ni, ni_other, "Hashtable roundtrip");
      Assert.AreEqual(ni.GetHashCode(), ni_other.GetHashCode(), "Hashtable GetHashCode roundtrip");
    }
    //Test methods:
    [Test]
    public void CacheTest()
    {
      RandomNumberGenerator rng = new RNGCryptoServiceProvider();
      Address a = new AHAddress(rng);
      Address a2 = new AHAddress(a.ToMemBlock());
      TransportAddress ta = TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:5000");
      TransportAddress ta2 = TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:5000");
      NodeInfo ni = NodeInfo.CreateInstance(a, ta);
      NodeInfo ni2 = NodeInfo.CreateInstance(a2, ta2);
      Assert.AreSame( ni, ni2, "Reference equality of NodeInfo objects");
    }
    [Test]
    public void TestWriteAndParse()
    {
      RandomNumberGenerator rng = new RNGCryptoServiceProvider();
      Address a = new AHAddress(rng);
      TransportAddress ta = TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:5000");
      NodeInfo ni = NodeInfo.CreateInstance(a, ta);
      RoundTripHT(ni);
      RoundTrip(ni);

      //Test multiple tas:
      ArrayList tas = new ArrayList();
      tas.Add(ta);
      for(int i = 5001; i < 5010; i++)
        tas.Add(TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:" + i.ToString()));
      NodeInfo ni3 = NodeInfo.CreateInstance(a, tas);
      RoundTripHT(ni3);
      RoundTrip(ni3);
      
      //Test null address:
      NodeInfo ni4 = NodeInfo.CreateInstance(null, ta);
      RoundTripHT(ni4);
      RoundTrip(ni4);
      
      //No TAs:
      NodeInfo ni5 = NodeInfo.CreateInstance( a );
      RoundTripHT(ni5);
      RoundTrip(ni5);
    }
  }
#endif
}
