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
using Brunet.Collections;
using Brunet.Connections;
using Brunet.Concurrent;

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
    public class CacheListState {
      public readonly ImmutableList<Entry> Data;
      public readonly ConnectionList Structs;
      public readonly AHAddress Local;
      public readonly Connection Left; 
      public readonly Connection Right;

      public CacheListState(ImmutableList<Entry> data,
                            ConnectionList structs,
                            AHAddress local) {
        Data = data;
        Structs = structs;
        Local = local;
        
	if (structs.Count > 0) {
          Left = Structs.GetLeftNeighborOf(Local);
          Right = Structs.GetRightNeighborOf(Local);
	}
	else {
          Left = null;
	  Right = null;
	}
      }
      public CacheListState SetData(ImmutableList<Entry> data) {
        return new CacheListState(data, Structs, Local);
      } 
      public CacheListState SetStructs(ConnectionList structs) {
        return new CacheListState(Data, structs, Local);
      }
    }
    public class AddEntry : Mutable<CacheListState>.Updater {
      private readonly Entry ToAdd;
      public AddEntry(Entry e) {
        ToAdd = e;
      }
      public CacheListState ComputeNewState(CacheListState old) {
        bool add_item = true;
        foreach(Entry e in old.Data) {
          /*
           * @todo we might want to be more careful, perhaps
           * we should potentially increase the range to the larger
           * of the two items
           */
          if(e.Content.Equals(ToAdd.Content)) {
            //It's already present
            add_item = false; 
          }
        }
        if(add_item) {
          var new_data = old.Data.PushIntoNew(ToAdd);
          return old.SetData(new_data);
        }
        else {
          return old;
        }
      }
    }
    public class Resize : Mutable<CacheListState>.Updater<Pair<IList<Entry>,
                                                                  IList<Entry>>> {
      private readonly int NewSize;
      public Resize(int newsize) {
        NewSize = newsize;
      }
      /*
       * the side result has the list of items to send to left and right
       */
      public Pair<CacheListState, Pair<IList<Entry>,IList<Entry>>>
          ComputeNewStateSide(CacheListState old)
      {
        var new_data = ImmutableList<Entry>.Empty;
        var send_left = new List<Entry>();
        var send_right = new List<Entry>();
        foreach(Entry e in old.Data) {
          // Recalculate size of range.
          BigInteger rg_size = CacheList.GetRangeSize(e.Alpha, NewSize);
          // reassign range info based on recalculated range.
          if(CacheList.DeetooLog.Enabled) {
            ProtocolLog.Write(CacheList.DeetooLog, String.Format(
            "---range before reassignment, start: {0}, end: {1}", e.Start, e.End));
          }
          Entry new_e = e.ReAssignRange(rg_size);
          if(CacheList.DeetooLog.Enabled) {
            ProtocolLog.Write(CacheList.DeetooLog, String.Format(
            "+++range after reassignment, start: {0}, end: {1}", new_e.Start, new_e.End));
          }
          if (!MapReduceBoundedBroadcast.InRange(old.Local, new_e.Start, new_e.End)) {
            //This node is not in this entry's range. 
            //Remove this entry.
            if(CacheList.DeetooLog.Enabled) {
              ProtocolLog.Write(CacheList.DeetooLog, String.Format(
              "entry {0} needs to be removed from node {1}", e.Content, old.Local));
            }
          }
          else {
            //This one is in the range, we keep it:
            new_data = new_data.PushIntoNew(new_e);
          }
          //Now check the ones to be sent left:
          AHAddress old_left = old.Left != null ? (AHAddress)old.Left.Address : null;
          bool s_left = (old_left != null) && 
            MapReduceBoundedBroadcast.InRange(old_left, new_e.Start, new_e.End) &&
            (!MapReduceBoundedBroadcast.InRange(old_left, e.Start, e.End) );
          if(s_left) { send_left.Add(new_e); }
          //Now check the ones to be sent right:
          AHAddress old_right = old.Left != null ? (AHAddress)old.Right.Address : null;
          bool s_right = (old_right != null) && 
            MapReduceBoundedBroadcast.InRange(old_right, new_e.Start, new_e.End) &&
            (!MapReduceBoundedBroadcast.InRange(old_right, e.Start, e.End) );
          if(s_right) { send_right.Add(new_e); }
        }
        var to_send = new Pair<IList<Entry>,IList<Entry>>(send_left, send_right);
        var new_state = old.SetData(new_data);
        return new Pair<CacheListState, Pair<IList<Entry>,IList<Entry>>>(new_state, to_send);
      }
      
    }
    public class HandleNewConnection : Mutable<CacheListState>.Updater {
      private readonly Connection NewCon;
      private readonly ConnectionList CList;
      public HandleNewConnection(Connection c, ConnectionList cl) {
        NewCon = c;
        CList = cl;
      }
      public CacheListState ComputeNewState(CacheListState old) {
        if(CList.MainType == ConnectionType.Structured) {
          return old.SetStructs(CList);
        }
        else {
          return old;
        }
      }
    }

    //LiskedList<MemBlock> list_of_contents = new LinkedList<MemBlock>();
    /// <summary>The log enabler for the dht.</summary>
    public static BooleanSwitch DeetooLog = new BooleanSwitch("DeetooLog", "Log for Deetoo!");
    protected readonly Node _node;
    protected readonly RpcManager _rpc;
    public readonly Mutable<CacheListState> MState;   

    /// <summary>number of string objects in the table.</summary>
    public int Count { get { return MState.State.Data.Count; } }
    /// <summary>The node of this hashtable.</summary>
    public Address Owner { get { return _node.Address; } }
    public Hashtable Data {
      get {
        var d_list = MState.State.Data;
        //Put it in a hashtable,
        //@todo, remove this
        Hashtable ht = new Hashtable(d_list.Count);
        foreach(Entry e in d_list) {
          ht[e.Content] = e;
        }
        return ht;
      }
    }
    /*
     <summary>Create a new set of chached data(For now, data is strings).</summary>
     * 
     */
    public CacheList(Node node) { 
      _node = node;
      //_rpc = RpcManager.GetInstance(node);
      _rpc = _node.Rpc;
      var structs = _node.ConnectionTable.GetConnections(ConnectionType.Structured);
      var state = new CacheListState(ImmutableList<Entry>.Empty, structs, (AHAddress)_node.Address);
      MState = new Mutable<CacheListState>(state);
      ///add handler for deetoo data insertion and search.
      _rpc.AddHandler("Deetoo", new DeetooHandler(node,this));
    }
    /// <summary>need this for iteration</summary>
    public IEnumerator GetEnumerator() {
      return Data.GetEnumerator();
    }

    /*
     <summary></summay>Object insertion method.</summary>
     <param name="ce">A Entry which is inserted to this node.</param>
     */
    public bool Insert(Entry ce) {
      var states = MState.Update(new AddEntry(ce));
      var old_s = states.First;
      var new_s = states.Second;
      bool added = old_s != new_s;
      if (added) {
        if(CacheList.DeetooLog.Enabled) {
          ProtocolLog.Write(CacheList.DeetooLog, String.Format(
            "data {0} added to node {1}", ce.Content, _node.Address));
        }
      }
      else {
        if(CacheList.DeetooLog.Enabled) {
          ProtocolLog.Write(CacheList.DeetooLog, String.Format(
            "data {0} already exists in node {1}", ce.Content, _node.Address));
        }
      }
      return added;
    }
    /**
     <summary>Regular Expression search method.</summary>
     <return>An array of matching strings from CacheList.</return>
     <param name = "pattern">A pattern of string one wish to find.</param>
    */
    //public ArrayList RegExMatch(string pattern) { 
    public IList<string> RegExMatch(string pattern) { 
      //var result = new ArrayList();
      var result = new List<string>();
      Regex match_pattern = new Regex(pattern);
      var data = MState.State.Data;
      foreach(Entry e in data) {
        string this_key = (string)e.Content;
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
      var data = MState.State.Data;
      foreach(Entry e in data) {
        result = e.Content as string;
        if(key.Equals(result)) {
          return result;
        }
      }
      return null;
    } 
    /*
    <summary>Determine size of bounded broadcasting range based on estimated network size.</summary>
    <returns>The range size as a biginteger.</returns>
    */    
    public static BigInteger GetRangeSize(double alpha, int size) {
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
          //BigInteger rg_size = cl.GetRangeSize(alpha,i);
          BigInteger rg_size = CacheList.GetRangeSize(alpha,i);
          Entry new_e = ce.ReAssignRange(rg_size);
          start = (AHAddress)new_e.Start;
          end = (AHAddress)new_e.End;
          distance = start.LeftDistanceTo(end);
	  //Console.WriteLine("distance: {0}, rg_size: {1}", distance, rg_size );
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
          //BigInteger rg_size_first = cl_first.GetRangeSize(alpha,i);
          //BigInteger rg_size_last = cl_last.GetRangeSize(alpha,i);
          BigInteger rg_size_first = CacheList.GetRangeSize(alpha,i);
          BigInteger rg_size_last = CacheList.GetRangeSize(alpha,i);

          Entry new_e = ce.ReAssignRange(rg_size_first);
          start = (AHAddress)new_e.Start;
          end = (AHAddress)new_e.End;
          distance = start.LeftDistanceTo(end);
	  //Console.WriteLine("distance: {0}, rg_size: {1}", distance, rg_size_first );
          Assert.AreEqual(distance, rg_size_first, "Test of equality of ranges fails");
                 
          new_e = ce.ReAssignRange(rg_size_last);
          start = (AHAddress)new_e.Start;
          end = (AHAddress)new_e.End;
          distance = start.LeftDistanceTo(end);
          Assert.AreEqual(distance, rg_size_last, "Test of equality of ranges fails");
          }
        
      }
    }
  }
#endif  

}
