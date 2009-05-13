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
//#define VTS_DEBUG
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
   * The algorithm works as follows:
   * 1. The SCO picks a random target x from Kleinberg's distribution and invokes the
   *    Vivaldi Target Selector (VTS) to query possible candidate nodes. 
   * 2. The VTS queries coordinates of log(n) nodes, starting at (to the left of) x.
   * 3. These candidate node addresses are returned back to the SCO, sorted 
   *    on distances to the current node in network coordinate space.
   */
  public class VivaldiTargetSelector: TargetSelector {
#if VTS_DEBUG
    protected static readonly int CHECKPOINT_INTERVAL = 120;//120 seconds
    protected static readonly object _class_lock = new object();
    protected static readonly string _checkpoint_file =   Path.Combine("/tmp", "vts_stats");
    //new outcomes are added to this list
    protected static ArrayList _query_list = new ArrayList();
    //keep track of all the nodes.
    protected static Hashtable _vts_nodes = new Hashtable();
    //outcomes in this list are written to file
    protected static Thread _checkpoint_thread = null;
    protected static int _checkpoint_thread_finished = 0;
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

    //local network coordinate service
    protected NCService _nc_service;

    /** 
     * Constructor. 
     * @param n local node
     * @param service local network coordinate service
     */
    public VivaldiTargetSelector(Node n, NCService service) {
      _sync = new object();
      _channel_to_state = new Hashtable();
      _node = n;
      _nc_service = service;
      _num_requests = 0;
#if VTS_DEBUG
      lock(_sync) {
        _node.StateChangeEvent += delegate(Node node, Node.ConnectionState s) {
          if( s == Node.ConnectionState.Joining ) {
            lock(_class_lock) {
              _vts_nodes[node] = null;
              if (_vts_nodes.Keys.Count == 1) { //first node
                Console.Error.WriteLine("Starting the VTS checkpoint thread. ");
                _checkpoint_thread = new Thread(CheckpointThread);
                _checkpoint_thread_finished = 0;
                _checkpoint_thread.Start();
              }
            }
          }
        };
        _node.StateChangeEvent += delegate(Node node, Node.ConnectionState s) {
          if( s == Node.ConnectionState.Disconnected ) {
            lock(_class_lock) {
              _vts_nodes.Remove(node);
              if (_vts_nodes.Keys.Count == 0) { //last node to leave
                Console.Error.WriteLine("Interrupting the VTS checkpoint thread. ");
                Interlocked.Exchange(ref _checkpoint_thread_finished, 1);
                _checkpoint_thread.Interrupt();
                _checkpoint_thread.Join();
                Console.Error.WriteLine("Join with the VTS checkpoint thread (finished).");
              }
            }
          }
        };
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
      Channel q = null;
      RequestState rs = null;
      lock(_sync) {
#if VTS_DEBUG
        Console.Error.WriteLine("VTS local: {0}, start: {1}, range: {2}, count: {3}", _node.Address, start, range, _num_requests);
#endif
        if (_num_requests == MAX_REQUESTS) {
          return; //do nothing and return;
        }
        _num_requests++;
        q = new Channel();
        rs = new RequestState(start, range, cb, current);
        _channel_to_state[q] = rs;        
      }
      
      //create a new request state
      ISender s = new ForwardingSender(_node, 
                                       start, 
                                       AHHeader.Options.Greedy, 
                                       new DirectionalAddress(DirectionalAddress.Direction.Left),
                                       (short) range,
                                       AHHeader.Options.Path
                                       );

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
      bool close_channel = false;
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
#if VTS_DEBUG
              Console.Error.WriteLine("VTS local: {0}, start: {1}, dest: {2}", _node.Address, request.Start, fs.Destination);
#endif
              request.ResultTable[fs.Destination] = new object[] {vs, (string) ht["hostname"]};
            }
            if (request.ResultTable.Keys.Count >= (int) request.Range*0.75) {
              close_channel = true;
            }
          }
        } catch(Exception x) {}
      }
      if (close_channel) {
        q.Close();
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
#if VTS_DEBUG
        Console.Error.WriteLine("VTS local: {0}, start: {1} channel closed.", _node.Address, request.Start);
#endif
        _channel_to_state.Remove(o);
        _num_requests--;
      }

      /** 
       * The request object has been removed the shared data structure.
       *  Since no one alse has access to it, it can be accessed outside the lock.
       */

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
        sorted_result[d] = target;
#if VTS_DEBUG
        Console.Error.WriteLine("VTS local: {0}, start: {1}, dest: {2}, distance: {3}", 
                                _node.Address, request.Start, target, d);
        sorted_stat[d] = new object[] {d, host};
#endif
      }

#if VTS_DEBUG
      lock(_sync) {
        _query_list.Add(sorted_stat);
      }
#endif
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
      ArrayList action_list = new ArrayList();
      do {
        try {
          System.Threading.Thread.Sleep(CHECKPOINT_INTERVAL*1000);
        } catch(System.Threading.ThreadInterruptedException) {
          break;
        }
        action_list = Interlocked.Exchange(ref _query_list, action_list);
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
          
          foreach(SortedList sorted_stat in action_list) {
            foreach(object[] curr_result in sorted_stat.Values) {
              //also write the checkpoint time
              double d = (double) curr_result[0];
              string host = (string) curr_result[1];
              tw.Write("{0} {1} ", host, d);
            }
            tw.WriteLine();
          }
          tw.Close();
        } catch (Exception x) {
          Console.Error.WriteLine(x);
        } finally {
          action_list.Clear();
        }
      } while (_checkpoint_thread_finished == 0);
    }
#endif
  }
}
