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
    protected readonly CacheList _cl;
    protected class DeetooState {
      public readonly int LocalNetSize;
      public readonly int MedianNetSize;
      public DeetooState(int localns, int medns) {
        LocalNetSize = localns;
        MedianNetSize = medns;
      }
    }
    protected class SetSizes : Mutable<DeetooState>.Updater {
      public readonly int LocalSize;
      public readonly int MedSize;
      public SetSizes(int local, int med) { LocalSize = local; MedSize = med; }
      public DeetooState ComputeNewState(DeetooState old) {
        return new DeetooState(LocalSize, MedSize);
      }
    }
    protected readonly Mutable<DeetooState> _state;
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
          Entry ce = new Entry(content, alpha, start, end);
          result = _cl.Insert(ce);
        }
        else if (method == "count") {
          result = _cl.Count;
        }
        else if (method == "getestimatedsize") {
          result = _state.State.LocalNetSize;
        }
        else if (method == "medianestimatedsize") {
          result = _state.State.MedianNetSize;
        }
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
      _state = new Mutable<DeetooState>(new DeetooState(_node.NetworkSize, _node.NetworkSize));
      node.ConnectionTable.ConnectionEvent += this.ConnectionHandler;
      node.ConnectionTable.DisconnectionEvent += this.ConnectionHandler;
    }
    /**
     <summary>This is called when ConnectionEvent occurs.
     First, recalculate ranges of entries(stabilize).
     Then, if target node is in the object's range,
     call InsertHandler on the remote node which 
     insert entry to the remote node's CacheList.<\summary>
     <param name="con">The Connection which copies CacheEntries from this node.<\param>
     */
    public void Put(IEnumerable<Entry> data, Connection con) {
      if( con == null ) { return; }
      AHAddress addr = (AHAddress)con.Address;
      ISender edge = con.Edge;
      foreach(Entry ce in data) {
        Channel queue = new Channel(1);
        if(MapReduceBoundedBroadcast.InRange(addr, ce.Start, ce.End) ) {
          try {
            _rpc.Invoke(edge, queue, "Deetoo.InsertHandler", ce.Content,
                        ce.Alpha, ce.Start.ToString(), ce.End.ToString());
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
      //Throw an exception if we don't have ConnectionEventArgs here:
      ConnectionEventArgs cargs = (ConnectionEventArgs)eargs;
      var upd = new CacheList.HandleNewConnection(cargs.Connection, cargs.CList);
      var old_new = _cl.MState.Update(upd);
      var old_s = old_new.First;
      var new_s = old_new.Second;
      Connection new_left = new_s.Left;
      Address old_left = old_s.Left != null ? old_s.Left.Address : null;
      if( (new_left != null) && (!new_left.Address.Equals(old_left))) {
        Put(new_s.Data, new_left); 
      }
      Connection new_right = new_s.Right;
      if( new_right != new_left ) {
        Address old_right = old_s.Right != null ? old_s.Right.Address : null;
        if( (new_right != null) && (!new_right.Address.Equals(old_right))) {
          Put(new_s.Data, new_right); 
        }
      }
      //Now we re-estimate the size:
      GetEstimatedSize(new_s);
    }
    protected void GetEstimatedSize(CacheList.CacheListState new_state) {
      try {
        Channel queue = new Channel(1);
        short logN0 = (short)(Math.Log(_node.NetworkSize)); 
        if (logN0 < 1) { logN0 = 1;}
        Address target = new DirectionalAddress(DirectionalAddress.Direction.Left); 
        ISender send = new AHSender(_node, target, logN0, AHHeader.Options.Last); ///log N0-hop away node
        queue.CloseEvent += delegate(object o, EventArgs args) {
          Channel q = (Channel)o;
          int new_local = _state.State.LocalNetSize;
          if (q.Count > 0) {
            RpcResult rres = (RpcResult)q.Dequeue();
            AHSender res_sender = (AHSender)rres.ResultSender;
            AHAddress remote = (AHAddress)res_sender.Destination; ///this is logN0-hop away node's address
            AHAddress me = (AHAddress)_node.Address;
            BigInteger width = me.LeftDistanceTo(remote); ///distance between me and remote node
            BigInteger inv_density = width / (logN0); ///inverse density
            BigInteger total = Address.Full / inv_density;  ///new estimation
            new_local = total.IntValue();
          }
          GetMedianEstimation(new_local, new_state);
        };
        _rpc.Invoke(send, queue, "sys:link.Ping",0);
      }
      catch(Exception x) {
        if(CacheList.DeetooLog.Enabled) {
          ProtocolLog.Write(CacheList.DeetooLog, x.ToString());
        }
      }
    }
    protected void GetMedianEstimation(int local, CacheList.CacheListState new_s) {
      List<Connection> shorts = new List<Connection>();
      foreach (Connection c in new_s.Structs) {
        if( c.ConType == "structured.shortcut" ) {
          shorts.Add(c);
        }
      }
      int q_size = shorts.Count;
      //int q_size = structs.Count;
      if (q_size > 0) {
        Channel queue = new Channel(q_size);
        queue.CloseEvent += delegate(object o, EventArgs args) {
          Channel q = (Channel)o;
          List<int> size_list = new List<int>();
          size_list.Add(local);
          int q_cnt = q.Count;
          for(int i = 0; i < q_cnt; i++) {
            try {
              RpcResult rres = (RpcResult)queue.Dequeue();
              int res = (int)rres.Result;
              size_list.Add(res);
            }
            catch (Exception e) {
              Console.WriteLine("{0} Exception caught. couldn't retrieve neighbor's estimation.",e);
            }
          }
          int median_size = DeetooHandler.GetMedian(size_list);
          _state.Update(new SetSizes(local, median_size));
          UpdateSize(median_size);
        };
        foreach(Connection c in shorts) {
          _rpc.Invoke(c.Edge, queue, "Deetoo.getestimatedsize",0);
        }
      }
      else {
        //We have no shortcuts, just use local size to trim:
        _state.Update(new SetSizes(local, local));
        UpdateSize(local);
      }
    }
    protected static int GetMedian(List<int> list) {
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
    protected void UpdateSize(int size) {
      var upd = new CacheList.Resize(size);
      var old_new_tosend = _cl.MState.Update(upd);
      var new_s = old_new_tosend.Second;
      var tosendl = old_new_tosend.Third.First;
      var tosendr = old_new_tosend.Third.Second;
      Put(tosendl, new_s.Left); 
      Put(tosendr, new_s.Right); 
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
