/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System.Threading;
using System.Collections;
using System.Collections.Specialized;
using System.IO;
using Brunet.Collections;
using Brunet.Transport;
using Brunet.Util;

#if BRUNET_NUNIT 
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
    protected NodeInfo(Address a, TransportAddress ta)
    {
      _address = a;
      _tas = new TransportAddress[]{ ta };
    }
    /**
     * Here is a NodeInfo with no TransportAddress
     */
    protected NodeInfo(Address a)
    {
      _address = a;
      _tas = EmptyTas;
    }
    /*
     * This constructor is only used for the _cache_key object
     * so we only have to have one of them
     */
    protected NodeInfo() {

    }
    //A cache of commonly used NodeInfo objects
    protected static Cache _cache = new Cache(512);
    protected static NodeInfo _cache_key = new NodeInfo();
    //For _cache_key when there is only one ta:
    protected static TransportAddress[] _ta_list = new TransportAddress[1];

    //Here's the cache for the Address -> NodeInfo case
    protected static readonly NodeInfo[] _mb_cache = new NodeInfo[UInt16.MaxValue + 1];
    /**
     * Factory method to reduce memory allocations by caching
     * commonly used NodeInfo objects
     */
    public static NodeInfo CreateInstance(Address a) {
      //Read some of the least significant bytes out,
      //AHAddress all have last bit 0, so we skip the last byte which
      //will have less entropy
      MemBlock mb = a.ToMemBlock();
      ushort idx = (ushort)NumberSerializer.ReadShort(mb, Address.MemSize - 3);
      NodeInfo ni = _mb_cache[idx];
      if( ni != null ) {
        if (a.Equals(ni._address)) {
          return ni;
        }
      }
      ni = new NodeInfo(a);
      _mb_cache[idx] = ni;
      return ni;
    }
    public static NodeInfo CreateInstance(Address a, TransportAddress ta) {
      NodeInfo result = null;
      Cache ni_cache = Interlocked.Exchange<Cache>(ref _cache, null);
      if( ni_cache != null ) {
        //Only one thread at the time can be in here:
        try {
          //Set up the key:
          _cache_key._done_hash = false;
          _cache_key._address = a;
          _ta_list[0] = ta;
          _cache_key._tas = _ta_list;

          result = (NodeInfo)ni_cache[_cache_key];
          if( result == null ) {
            //This may look weird, but we are using a NodeInfo as a key
            //to lookup NodeInfos, this will allow us to only keep one
            //identical NodeInfo in scope at a time.
            result = new NodeInfo(a, ta);
            ni_cache[result] = result;
          }
        }
        finally {
          Interlocked.Exchange<Cache>(ref _cache, ni_cache);
        }
      }
      else {
        result = new NodeInfo(a, ta);
      }
      return result;
    }
    public static NodeInfo CreateInstance(Address a, IList ta) {
      NodeInfo result = null;
      Cache ni_cache = Interlocked.Exchange<Cache>(ref _cache, null);
      if( ni_cache != null ) {
        try {
          //Set up the key:
          _cache_key._done_hash = false;
          _cache_key._address = a;
          _cache_key._tas = ta;
          
          result = (NodeInfo)ni_cache[_cache_key];
          if( result == null ) {
            //This may look weird, but we are using a NodeInfo as a key
            //to lookup NodeInfos, this will allow us to only keep one
            //identical NodeInfo in scope at a time.
            result = new NodeInfo(a, ta);
            ni_cache[result] = result;
          }
        }
        finally {
          Interlocked.Exchange<Cache>(ref _cache, ni_cache);
        }
      }
      else {
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
      Address a = new DirectionalAddress(DirectionalAddress.Direction.Left);
      Address a2 = new DirectionalAddress(DirectionalAddress.Direction.Left);
      TransportAddress ta = TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:5000");
      TransportAddress ta2 = TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:5000");
      NodeInfo ni = NodeInfo.CreateInstance(a, ta);
      NodeInfo ni2 = NodeInfo.CreateInstance(a2, ta2);
      Assert.AreSame( ni, ni2, "Reference equality of NodeInfo objects");
    }
    [Test]
    public void TestWriteAndParse()
    {
      Address a = new DirectionalAddress(DirectionalAddress.Direction.Left);
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
