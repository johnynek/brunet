/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida
                   Arijit Ganguly <aganguly@acis.ufl.edu> University of Florida

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
#define VTS_DEBUG
using Brunet;
using System;
using System.Collections;
using System.Threading;
using System.IO;

namespace Brunet.Coordinate {
  /**
   * This class implements an optimizer for the selection of shortcut connections
   * by the structured connection overlord. The goal is to minimize the total latency
   * incurred by routing of keys, by incorporating latencies to pick shortcuts. 
   */
  public class VivaldiTargetSelector: TargetSelector {
#if VTS_DEBUG
    protected static readonly int CHECKPOINT_INTERVAL = 120;//120 seconds
    protected static readonly object _class_lock = new object();
    protected static readonly string _checkpoint_file =   Path.Combine("/tmp", "vts_stats");
    //new outcomes are added to this list
    protected static ArrayList _query_list = new ArrayList();
    //outcomes in this list are written to file
    protected static ArrayList _action_list = new ArrayList();
    protected static Thread _checkpoint_thread = null;
#endif

    //lock variable
    protected object _sync;
    
    //local node
    protected Node _node;

    //keeps track of all the ongoing requests.
    protected Hashtable _channel_to_state;

    //keep track of number of outgoing requests.
    protected int _num_requests;

    //automatically controls the rate at which connections will be created
    protected static readonly int MAX_REQUESTS = 5;

    /**
     * Represents a state of a request to optimize a shortcut around a target address.
     */
    protected class RequestState {
      public readonly Hashtable ResultTable;
      public readonly Address Start;
      public readonly int Range;
      public readonly TargetSelectorDelegate Callback; 
      public readonly Address Current;
      public RequestState(Address start, int range, TargetSelectorDelegate cb, Address current) {
        Start = start;
        Range = range;
        Callback = cb;
        Current = current;
        ResultTable = new Hashtable();
      }
    }
    

    protected NCService _nc_service;
    public VivaldiTargetSelector(Node n, NCService service) {
      _sync = new object();
      _channel_to_state = new Hashtable();
      _node = n;
      _nc_service = service;
      _num_requests = 0;
#if VTS_DEBUG
      lock(_class_lock) {
        if (_checkpoint_thread == null) {
          _checkpoint_thread = new Thread(CheckpointThread);
          Console.Error.WriteLine("Starting the VTS checkpoint thread. ");
          _checkpoint_thread.Start();
        }
      }
#endif
    }
    
    /**
     * Compute candidate scores for a shortcut connection.
     * @param start address computed by the SCO.
     * @param range nunber of candidate nodes.
     * @param cb callback function when candidate scores are available. 
     * @param current current selection of the optimal in the provided range.
     */
    public override void ComputeCandidates(Address start, int range, TargetSelectorDelegate cb, Address current) {
      lock(_sync) {
        Console.Error.WriteLine("VTS local: {0}, start: {1}, range: {2}, count: {3}", _node.Address, start, range, _num_requests);
        if (_num_requests == MAX_REQUESTS) {
          return; //do nothing and return;
        }
        _num_requests++;
      }
      //create a new request state
      RequestState rs = new RequestState(start, range, cb, current);
      ISender s = new ForwardingSender(_node, 
                                       start, 
                                       AHPacket.AHOptions.Greedy, 
                                       new DirectionalAddress(DirectionalAddress.Direction.Left),
                                       (short) range,
                                       AHPacket.AHOptions.Path
                                       );
      Channel q = new Channel();

      lock(_sync) {
        //record the request state.
        _channel_to_state[q] = rs;
      }
      
      q.EnqueueEvent += new EventHandler(EnqueueHandler);
      q.CloseEvent += new EventHandler(CloseHandler);
      RpcManager rpc = RpcManager.GetInstance(_node);
      rpc.Invoke(s, q, "ncserver.EchoVivaldiState", new object[]{});
    }
    
    /**
     * Invoked when a result is enqueued into the channel.
     */
    protected void EnqueueHandler(object o, EventArgs args) {
      Channel q = (Channel) o;
      lock(_sync) {
        try {
          RpcResult result = q.Dequeue() as RpcResult;
          Hashtable ht = (Hashtable) result.Result;
          Hashtable ht_position = (Hashtable) ht["position"];
          NCService.VivaldiState vs = new NCService.VivaldiState();
          vs.Position = new Point((double[]) ((ArrayList) ht_position["side"]).ToArray(typeof(double)), (double) ht_position["height"]);
          vs.WeightedError = (double) ht["error"];
          ForwardingSender fs = result.ResultSender as ForwardingSender;
          
          //record this information in the request state.
          RequestState request = (RequestState) _channel_to_state[q];
          if (request != null) {
            //make sure this is not our own reply
            if (!fs.Destination.Equals(_node.Address)) {
              Console.Error.WriteLine("VTS local: {0}, start: {1}, dest: {2}", _node.Address, request.Start, fs.Destination);
              request.ResultTable[fs.Destination] = new object[] {vs, (string) ht["hostname"]};
            }
          }
          if (request.ResultTable.Keys.Count >= (int) request.Range*0.75) {
            q.Close();
          }
        } catch(Exception x) {}
      }
    }

    /**
     * Invoked when the request channel is closed.
     */
    protected void CloseHandler(object o, EventArgs args) {
      RequestState request = null;
      lock(_sync) {
        //retrieve the request state assiciated with the closed channel
        request = (RequestState) _channel_to_state[o];
        if (request == null) {
          //cannot happen
          Console.Error.WriteLine("VTS unable to retrieve request for a closed channel");
          return;
        }
        Console.Error.WriteLine("VTS local: {0}, start: {1} channel closed.", _node.Address, request.Start);
        _channel_to_state.Remove(o);
        _num_requests--;
      }

      SortedList sorted_result = new SortedList();
#if VTS_DEBUG
      SortedList sorted_stat = new SortedList();
#endif
      NCService.VivaldiState local_vs = _nc_service.State;
      foreach(Address target in request.ResultTable.Keys) {
        object[] curr_result = (object[]) request.ResultTable[target];
        NCService.VivaldiState vs = (NCService.VivaldiState) curr_result[0];
        string host = (string) curr_result[1];
        double d = local_vs.Position.GetEucledianDistance(vs.Position);
        Console.Error.WriteLine("VTS local: {0}, start: {1}, dest: {2}, distance: {3}", 
                                _node.Address, request.Start, target, d);
        sorted_result[d] = target;
#if VTS_DEBUG
        sorted_stat[d] = new object[] {d, host};
#endif
      }

      _query_list.Add(sorted_stat);
      request.Callback(request.Start, sorted_result, request.Current);
    }

#if VTS_DEBUG
    /**
     * The following method prints outcome of a shortcuts selection to a file.
     * The method is purely for debugging puposes and hopefully does not
     * come in way of performance.
     */
    protected static void CheckpointThread() {
      bool start = true;
      do {
        System.Threading.Thread.Sleep(CHECKPOINT_INTERVAL*1000);
        _action_list = Interlocked.Exchange(ref _query_list, _action_list);
        try {
          TextWriter tw = null; 
          if (start) {
            //truncate file
            tw = new StreamWriter(_checkpoint_file, false);
            start = false;
          } 
          else {
            //open in append mode
            tw = new StreamWriter(_checkpoint_file, true);
          }
          
          while (_action_list.Count > 0) {
            SortedList sorted_stat = (SortedList) _action_list[0];
            foreach(object[] curr_result in sorted_stat.Values) {
              //also write the checkpoint time
              double d = (double) curr_result[0];
              string host = (string) curr_result[1];
              tw.Write("{0} {1} ", host, d);
            }
            _action_list.RemoveAt(0);
            tw.WriteLine();
          }
          tw.Close();
        } catch (Exception x) {
          Console.Error.WriteLine(x);
          _action_list.Clear();
        }
      } while (true);
#endif
    }
  }
}
  
