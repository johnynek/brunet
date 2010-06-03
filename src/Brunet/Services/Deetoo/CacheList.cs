/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2008 Taewoong Choi <twchoi@ufl.edu> University of Florida  

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
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

using Brunet.Messaging;
using Brunet.Util;
using Brunet.Services.MapReduce;
using Brunet.Symphony;

#if BRUNET_NUNIT
using NUnit.Framework;
using Brunet.Applications;
#endif

namespace Brunet.Services.Deetoo
{
  /**
   <summary>A hashtable contains the data for key:value pair.
   key = content, value = Entry
   */
  public class CacheList : IEnumerable {
    //LiskedList<MemBlock> list_of_contents = new LinkedList<MemBlock>();
    /// <summary>The log enabler for the dht.</summary>
    public static BooleanSwitch DeetooLog = new BooleanSwitch("DeetooLog", "Log for Deetoo!");
    protected Hashtable _data = new Hashtable();
    protected Node _node;
    protected RpcManager _rpc;
    /// <summary>number of string objects in the table.</summary>
    public int Count { get { return _data.Count; } }
    /// <summary>The node of this hashtable.</summary>
    public Address Owner { get { return _node.Address; } }
    public Hashtable Data { get { return _data; } }
    /*
     <summary>Create a new set of chached data(For now, data is strings).</summary>
     * 
     */
    public CacheList(Node node) { 
      _node = node;
      //_rpc = RpcManager.GetInstance(node);
      _rpc = _node.Rpc;
      ///add handler for deetoo data insertion and search.
      _rpc.AddHandler("Deetoo", new DeetooHandler(node,this));
    }
    /// <summary>need this for iteration</summary>
    public IEnumerator GetEnumerator() {
      IDictionaryEnumerator en = _data.GetEnumerator();
      while(en.MoveNext() ) {
        yield return en.Current;
      }
    }

    /*
     <summary></summay>Object insertion method.</summary>
     <param name="ce">A Entry which is inserted to this node.</param>
     */
    public void Insert(Entry ce) {
      string Key = ce.Content;
      if (!_data.ContainsKey(Key)  ) {
        _data.Add(Key,ce);
        if(CacheList.DeetooLog.Enabled) {
          ProtocolLog.Write(CacheList.DeetooLog, String.Format(
            "data {0} added to node {1}", Key, _node.Address));
        }
      }
      else {
        if(CacheList.DeetooLog.Enabled) {
          ProtocolLog.Write(CacheList.DeetooLog, String.Format(
            "data {0} already exists in node {1}", Key, _node.Address));
        }
      }
    }
    /**
     <summary>Overrided method for insertion, create new Entry with inputs, then, insert Entry to the CacheList.</summary>
     <param name="str">Content, this is key of hashtable.<param>
     <param name="alpha">replication factor.<param>
     <param name="a">start address of range.<param>
     <param name="b">end address of range.<param>
     */
    public void Insert(string str, double alpha, Address a, Address b) {
      Entry ce = new Entry(str, alpha, a, b);
      Insert(ce);
    }
    /**
     <summary>Regular Expression search method.</summary>
     <return>An array of matching strings from CacheList.</return>
     <param name = "pattern">A pattern of string one wish to find.</param>
    */
    public ArrayList RegExMatch(string pattern) { 
      ArrayList result = new ArrayList();
      Regex match_pattern = new Regex(pattern);
      foreach(DictionaryEntry de in _data)
      {
        string this_key = (string)(de.Key);
        if (match_pattern.IsMatch(this_key)) {
          result.Add(this_key);
        }
      }
      return result;
    }
    /**
     <summary>Perform an exact match.</summary>
     <return>A matching string in the CacheList.</return>
     <param name = "key">A string one wish to find.<param>
     <remarks>returns null if no matching string with a given key.</remarks>
     */
    public string ExactMatch(string key) {
      string result = null;
      if (_data.ContainsKey(key) )
      {
        result = key;
      }
      return result;
    } 
     /**
      <summary>Recalculate and replace range info for each Entry. 
      Remove Entries whose range is not in the  
      recalculated bounded broadcasting range.</summary>
     */
    public void RemoveEntries(int size) {
      if(CacheList.DeetooLog.Enabled) {
        ProtocolLog.Write(CacheList.DeetooLog, String.Format(
          "In node {0}, stabilization is called",_node.Address));
      }
      List<string> to_be_removed = new List<string>();
      MapReduceBoundedBroadcast mrbb = new MapReduceBoundedBroadcast(_node);
      foreach (DictionaryEntry dic in _data) {
        string this_key = (string)dic.Key;
        Entry ce = (Entry)dic.Value;
        /// Recalculate size of range.
        BigInteger rg_size = GetRangeSize(ce.Alpha,size);
        /// reassign range info based on recalculated range.
        if(CacheList.DeetooLog.Enabled) {
          ProtocolLog.Write(CacheList.DeetooLog, String.Format(
          "---range before reassignment, start: {0}, end: {1}", ce.Start, ce.End));
        }
        ce.ReAssignRange(rg_size);
        if(CacheList.DeetooLog.Enabled) {
          ProtocolLog.Write(CacheList.DeetooLog, String.Format(
          "+++range after reassignment, start: {0}, end: {1}", ce.Start, ce.End));
        }
        AHAddress addr = (AHAddress)_node.Address;
	AHAddress start = ce.Start as AHAddress;
	AHAddress end = ce.End as AHAddress;	
        if (!mrbb.InRange(addr, start, end)) {
          //This node is not in this entry's range. 
          //Remove this entry.
          if(CacheList.DeetooLog.Enabled) {
            ProtocolLog.Write(CacheList.DeetooLog, String.Format(
            "entry {0} needs to be removed from node {1}", ce.Content, _node.Address));
          }
          to_be_removed.Add(this_key);
        }
      }
      for (int i = 0; i < to_be_removed.Count; i++) {
        _data.Remove(to_be_removed[i]);
      } 
    }
    /*
    <summary>Determine size of bounded broadcasting range based on estimated network size.</summary>
    <returns>The range size as a biginteger.</returns>
    */    
    public BigInteger GetRangeSize(double alpha, int size) {
      //double alpha = this.Alpha;
      double a_n = alpha / (double)size;
      double sqrt_an = Math.Sqrt(a_n);
      double log_san = Math.Log(sqrt_an,2.0);
      //int exponent = (int)(log_san + 160);
      double exponent = log_san + 160;
      int exponent_i = (int)(exponent) - 63;
      double exponent_f = exponent - exponent_i;
      ulong twof = (ulong)Math.Pow(2,exponent_f); //exponent_f should be less than 64
      BigInteger bi_one = new BigInteger(1);
      BigInteger result = (bi_one << exponent_i)*twof;  
      if (result % 2 == 1) { result += 1; } // make this even number.
      //Console.WriteLine("a/n: {0}, sqrt(a/n): {1}, log_san: {2}, exponent: {3}, exponent_i: {4}, exp_f: {5}, twof: {6}, bi_one: {7}, result: {8}", a_n, sqrt_an, log_san, exponent, exponent_i, exponent_f, twof, bi_one, result);
      if(CacheList.DeetooLog.Enabled) {
        ProtocolLog.Write(CacheList.DeetooLog, String.Format(
          "network size estimation: {0}, new range size: {1}", size, result));
      }
      return result;
    }

  }
#if BRUNET_NUNIT
  [TestFixture]
  public class CacheListTester {  
    [Test]
    public void RangeTest() {
      AHAddress a, b, start, end;
      Entry ce;
      BigInteger distance;
      for (int i = 100; i < 1000000; i+=20000) {
        for (double alpha = 0.5; alpha < 5.0; alpha +=0.5) {
          a = Utils.GenerateAHAddress();
          b = Utils.GenerateAHAddress();
          ce = new Entry(alpha.ToString(), alpha, a,b );
          ce.Alpha = alpha;
          CacheList cl = new CacheList(new StructuredNode(a) );
          BigInteger rg_size = cl.GetRangeSize(alpha,i);
          ce.ReAssignRange(rg_size);
          start = (AHAddress)ce.Start;
          end = (AHAddress)ce.End;
          distance = start.LeftDistanceTo(end);
          Assert.AreEqual(distance, rg_size, "Test of equality of ranges fails");
        }
      }
    }
    public void WrapAroudTest() {
      AHAddress start, end;
      Entry ce;
      BigInteger distance;
      for (int i = 100; i < 1000000; i+=20000) {
        for (double alpha = 0.5; alpha < 10; alpha += 0.5) {
          AHAddress node_addr_first = new AHAddress(2);
          AHAddress node_addr_last = new AHAddress(Address.Full-2);
          ce = new Entry(alpha.ToString(), alpha, node_addr_first, node_addr_last);
          CacheList  cl_first = new CacheList(new StructuredNode(node_addr_first) );
          CacheList cl_last = new CacheList(new StructuredNode(node_addr_last) );
          BigInteger rg_size_first = cl_first.GetRangeSize(alpha,i);
          BigInteger rg_size_last = cl_last.GetRangeSize(alpha,i);

          ce.ReAssignRange(rg_size_first);
          start = (AHAddress)ce.Start;
          end = (AHAddress)ce.End;
          distance = start.LeftDistanceTo(end);
          Assert.AreEqual(distance, rg_size_first, "Test of equality of ranges fails");
                 
          ce.ReAssignRange(rg_size_last);
          start = (AHAddress)ce.Start;
          end = (AHAddress)ce.End;
          distance = start.LeftDistanceTo(end);
          Assert.AreEqual(distance, rg_size_last, "Test of equality of ranges fails");
          }
        
      }
    }
  }
#endif  

}
