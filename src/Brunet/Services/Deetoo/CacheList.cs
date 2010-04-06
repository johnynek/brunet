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
//using Brunet.Applications;
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
      foreach (DictionaryEntry dic in _data) {
        string this_key = (string)dic.Key;
        Entry ce = (Entry)dic.Value;
        /// Recalculate size of range.
        BigInteger rg_size = ce.GetRangeSize(size);
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
	MapReduceBoundedBroadcast mrbb = new MapReduceBoundedBroadcast(_node);
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
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class CacheListTester {  
    [Test]
    public void RangeTest() {
      for (int i = 100; i < 10000000; i+=100) {
        for (double alpha = 0.5; alpha < 5.0; alpha +=0.5) {
          AHAddress a = Utils.GenerateAHAddress();
          AHAddress b = Utils.GenerateAHAddress();
          Entry ce = new Entry(alpha.ToString(), alpha, a,b );
          ce.Alpha = alpha;
          BigInteger rg_size = ce.GetRangeSize(i);
          ce.ReAssignRange(rg_size);
          AHAddress start = (AHAddress)ce.Start;
          AHAddress end = (AHAddress)ce.End;
          BigInteger distance = start.LeftDistanceTo(end);
          Assert.AreEqual(distance, rg_size, "Testing equality of ranges");
        }
      }
    }
  }
#endif  

}
