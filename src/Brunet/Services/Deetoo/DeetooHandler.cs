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
//using System.Threading;
using Brunet.Messaging;
using Brunet.Connections;
using Brunet.Services.MapReduce;
using Brunet.Symphony;
using Brunet.Concurrent;
using Brunet.Util;

/**
\brief Provides Deetoo caching and querying services using the Brunet P2P infrastructure
 */
namespace Brunet.Services.Deetoo
{
  /**
   * <summary>This class provides a client interface to CacheList class.<\summary>
   */
  public class DeetooHandler : IRpcHandler  {
    /// <summary>The node to provide services for.<\summary>
    protected Node _node;
    /// <summary>The RpcManager to perform data transfer.<\summary>
    protected readonly RpcManager _rpc;
    /// <summary>The collection of cached data.(Hashtable)<\summary>
    protected CacheList _cl;
    /// <summary> The left neighbor of this node.<\summary>
    protected Address _left_addr = null;
    /// <summary> The right neighbor of this node.<\summary>
    protected Address _right_addr = null;
    /// <summary>The total amount of cached data.</summary>
    public int Count { get { return _cl.Count; } }
    protected int _local_network_size;
    protected int _median_network_size;
    //protected int _network_size;
    //public int NetworkSize { 
    //  get { return _network_size; } 
    //  set { _network_size = value; }
    //} 
    /**
    <summary>This provides translation for Rpc methods.<\summary> 
    <param name="caller">The ISender who made the request.<\param>
    <param name="method">The method requested.</param>
    <param name="args">A list of arguments to pass to the method.</param>
    <param name="req_state">The return state sent back to the RpcManager so that it
    knows who to return the result to.</param>
    <exception>Thrown when there the method is not pre-defined</exception>
    <remark>This handler is registered in CacheList constructor.<\remark>
    */
    public void HandleRpc(ISender caller, string method, IList args, object req_state) {
      object result = null;
      try {
        if (method == "InsertHandler") {
	  string content = (string)args[0];
	  double alpha = (double)args[1];
	  AHAddress start = (AHAddress)AddressParser.Parse((string)args[2]);
	  AHAddress end = (AHAddress)AddressParser.Parse((string)args[3]);
          result = InsertHandler(content, alpha, start, end);
          //_rpc.SendResult(req_state,result);
        }
        else if (method == "count") {
          result = _cl.Count;
          //_rpc.SendResult(req_state,result);
        }
        else if (method == "getestimatedsize") {
          result = _local_network_size;
          //_rpc.SendResult(req_state,result);
        }
        else if (method == "medianestimatedsize") {
          result = _median_network_size;
          //_rpc.SendResult(req_state,result);
        }
	/*
        else if (method == "updatenetworksize") {
          NetworkSize = (int)(args[0]);
        }
        */
        else {
          throw new Exception("DeetooHandler.Exception: No Handler for method: " + method);
	}
      }
      catch (Exception e) {
        result = new AdrException(-32602, e);
      }
      _rpc.SendResult(req_state,result);
    }
    /**
    <summary>This handles only connection event for now.<\summary>
    <param name="node">The node the deetoo is to serve from.<\param>
    <param name="cl">The CacheList belongs to this node.<\param>
    */
    public DeetooHandler(Node node, CacheList cl) {
      _node = node;
      _rpc = _node.Rpc;
      _cl = cl;
      _local_network_size = _node.NetworkSize;
      node.ConnectionTable.ConnectionEvent += this.GetEstimatedSize;
      node.ConnectionTable.DisconnectionEvent += this.GetEstimatedSize;
      //node.ConnectionTable.ConnectionEvent += this.GetMedianEstimation;
      //node.ConnectionTable.DisconnectionEvent += this.GetMedianEstimation;
      //node.ConnectionTable.ConnectionEvent += this.ConnectionHandler;
      //node.ConnectionTable.DisconnectionEvent += this.ConnectionHandler;
    }
    /**
     <summary>This is called when ConnectionEvent occurs.
     First, recalculate ranges of entries(stabilize).
     Then, if target node is in the object's range,
     call InsertHandler on the remote node which 
     insert entry to the remote node's CacheList.<\summary>
     <param name="con">The Connection which copies CacheEntries from this node.<\param>
     */
    public void Put(Connection con) {
      if( _cl.Count > 0 ) {
        // Before data are transferred, recalculate each object's range
        // If the node is out of new range, entry will be removed from local list.
        //_cl.Stabilize(_median_network_size);
        _cl.RemoveEntries(_median_network_size);
        foreach(DictionaryEntry de in _cl) {
          Entry ce = (Entry)de.Value;
          Channel queue = new Channel(1);
          /*
            queue.CloseEvent += delegate(object o, EventArgs args) {
            Channel q = (Channel)o;
            if (q.Count != 0) {
              RpcResult rres = (RpcResult)queue.Dequeue();
              bool res = false;
              try {
                res = (bool)rres.Result;
              }
              catch (Exception e) {
                Console.WriteLine("{0} Exception caught. Insertion failed.",e);
              }
              //_rpc.SendResult(req_state,res);
            }
          };
          */
	  MapReduceBoundedBroadcast mrbb = new MapReduceBoundedBroadcast(_node);
          AHAddress addr = (AHAddress)con.Address;
	  AHAddress start = ce.Start as AHAddress;
	  AHAddress end = ce.End as AHAddress;
          if (mrbb.InRange(addr, start, end) ) {
            //If new connection is within the range, ask to insert this object.
            try {
              _rpc.Invoke(con.Edge,queue,"Deetoo.InsertHandler",ce.Content, ce.Alpha, ce.Start.ToString(), ce.End.ToString());
              if(CacheList.DeetooLog.Enabled) {
                ProtocolLog.Write(CacheList.DeetooLog, String.Format(
                  "node {0} asks node {1} to insert content {2}, start: {3}, end: {4}", _node.Address, addr, ce.Content, ce.Start, ce.End));
             }
            }
            catch (Exception e){
              Console.WriteLine("{0} Exception caught.",e);
            }
          }
        }
      }
    }
    /**
     <summary>When this is called from remote ndoe, this node tries to insert an object to CacheList.<\summary>
     <param name="o">The Entry about to be inserted<\param>
     */
    public bool InsertHandler(string content, double alpha, Address start, Address end) {
      Entry ce = new Entry(content, alpha, start, end);
      bool result = false;
      try {
        _cl.Insert(ce);
        result = true;
        if(CacheList.DeetooLog.Enabled) {
          ProtocolLog.Write(CacheList.DeetooLog, String.Format(
          "content {0} is inserted at {1}", ce.Content, _node.Address));
        }
      }
      catch {
        throw new Exception("ENTRY_ALREADY_EXISTS");
      }
      return result;
    }
    /**
    <summary>This is called whenever there is a disconnect or a connect, the
    idea is to determine if there is a new left or right node, if there is and
    here is a pre-existing transfer, we must interupt it, and start a new
    transfer.</summary>
    <remarks>The possible scenarios where this would be active:
     - no change on left
     - new left node with no previous node (from disc or new node)
     - left disconnect and new left ready
     - left disconnect and no one ready
     - no change on right
     - new right node with no previous node (from disc or new node)
     - right disconnect and new right ready
     - right disconnect and no one ready
    </remarks>
    <param name="o">Unimportant</param>
    <param name="eargs">Contains the ConnectionEventArgs, which lets us know
    if this was a Structured Connection change and if it is, we should check
    the state of the system to see if we have a new left or right neighbor.
    </param>
    */
    protected void ConnectionHandler(object o, EventArgs eargs) {
      ConnectionTable tab = _node.ConnectionTable;
      Connection lc = null, rc = null;
      try {
        lc = tab.GetLeftStructuredNeighborOf((AHAddress) _node.Address);
    }
      catch(Exception) {}
      try {
        rc = tab.GetRightStructuredNeighborOf((AHAddress) _node.Address);
      }
      catch(Exception) {}
      if(lc != null) {
        if(!lc.Address.Equals(_left_addr)) {
          _left_addr = lc.Address;
        }
        if(Count > 0) {
          Put(lc); 
        }
      }
      if(rc != null) {
        if(!rc.Address.Equals(_right_addr)) {
          _right_addr = rc.Address;
        }
        if(Count > 0) {
          Put(rc); 
        }
      }
    }
    protected void GetEstimatedSize(object obj, EventArgs eargs) {
      try {
        short logN0 = (short)(Math.Log(_local_network_size) ); 
        if (logN0 < 1) { logN0 = 1;}
        Address target = new DirectionalAddress(DirectionalAddress.Direction.Left); 
        ISender send = new AHSender(_node, target, logN0, AHHeader.Options.Last); ///log N0-hop away node
        Channel queue = new Channel(1);
        _rpc.Invoke(send, queue, "sys:link.Ping",0);
        queue.CloseEvent += delegate(object o, EventArgs args) {
          Channel q = (Channel)o;
          if (q.Count > 0) {
            RpcResult rres = (RpcResult)q.Dequeue();
            AHSender res_sender = (AHSender)rres.ResultSender;
            AHAddress remote = (AHAddress)res_sender.Destination; ///this is logN0-hop away node's address
            AHAddress me = (AHAddress)_node.Address;
            BigInteger width = me.LeftDistanceTo(remote); ///distance between me and remote node
            BigInteger inv_density = width / (logN0); ///inverse density
            BigInteger total = Address.Full / inv_density;  ///new estimation
            int total_int = total.IntValue();
            _local_network_size = total_int;
          }
        };
	GetMedianEstimation(obj, eargs);
      }
      catch(Exception x) {
        if(CacheList.DeetooLog.Enabled) {
          ProtocolLog.Write(CacheList.DeetooLog, x.ToString());
        }
      }
    }
    protected void GetMedianEstimation(object obj, EventArgs eargs) {
      //_median_network_size = _local_network_size;
      ConnectionTable tab = _node.ConnectionTable;
      IEnumerable structs = tab.GetConnections("structured.shortcut");
      List<Connection> cons = new List<Connection>();
      foreach (Connection c in structs) {
        cons.Add(c);
      }
      int q_size = cons.Count;
      if (q_size > 0) {
        Channel queue = new Channel(q_size);
        foreach(Connection c in structs) {
          _rpc.Invoke(c.Edge, queue, "Deetoo.getestimatedsize",0);
        }
        queue.CloseEvent += delegate(object o, EventArgs args) {
          Channel q = (Channel)o;
          List<int> size_list = new List<int>();
          size_list.Add(_local_network_size);
          int q_cnt = q.Count;
          for(int i = 0; i < q_cnt; i++) {
            RpcResult rres = (RpcResult)queue.Dequeue();
            try {
              int res = (int)rres.Result;
              size_list.Add(res);
            }
            catch (Exception e) {
              Console.WriteLine("{0} Exception caught. couldn't retrieve neighbor's estimation.",e);
            }
          }
          int median_size = GetMedian(size_list);
          _median_network_size = median_size;
          //_rpc.SendResult(req_state,mean_size);
        };
	ConnectionHandler(obj,eargs);
      }
    }
    protected int GetMedian(List<int> list) {
      int idx;
      int median;
      int size = list.Count;
      list.Sort();
      if (size % 2 == 1) {
        idx = size / 2;
        median = list[idx];
      }
      else {
        idx = (int)(size / 2);
        int first = list[idx-1];
        int second = list[idx];
        median = (int)( (first + second) / 2);
      }
      return median;
    }
  }
/*
#if BRUNET_NUNIT
  [TestFixture]
  public class DeetooHandlerTest
  {
    public DeetooHandlerTest() {}
  }
*/
}
