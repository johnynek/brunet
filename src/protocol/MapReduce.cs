/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007  Arijit Ganguly <aganguly@gmail.com>, University of Florida
                    P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System.Collections.Specialized;

namespace Brunet {
  /** 
   * This class is the base class for a map-reduce task.
   */
  public abstract class MapReduceTask {
    /** unique name of the task. */
    public abstract string Name {
      get;
    }
    /** map function. */
    public abstract object Map(object map_arg);
    /** 
     * reduce function. 
     * @param map_result local map result
     * @param child_results hashtable containing results from each child
     */
    public abstract object Reduce(object map_result, Hashtable child_results);
    /** tree generator function. */
    public abstract IList GenerateTree(object map_result, object gen_arg);
  }
  
  /** 
   * This class provides an RPC interface into the map reduce functionality. 
   */  
  public class MapReduceHandler: IRpcHandler {
    protected readonly object _sync;
    protected readonly Node _node;
    protected readonly RpcManager _rpc;
    /** mapping of map-reduce task names to task objects. */
    protected readonly Hashtable _name_to_task;
    
    /**
     * Constructor
     * @param n local node
     */
    public MapReduceHandler(Node n) {
      _node = n;
      _rpc = RpcManager.GetInstance(n);
      _name_to_task = new Hashtable();
      _sync = new object();
    }
    
    /**
     * Allows subscribing new map-reduce tasks to the handler.
     * @param task an object representing the map-reduce task.
     */
    public void SubscribeTask(MapReduceTask task) {
      lock(_sync) {
        if (_name_to_task.ContainsKey(task.Name)) {
          throw new Exception(String.Format("Map reduce task name: {0} already registered.", task.Name));
        }
        _name_to_task[task.Name] = task;
      }
    }

    /**
     * This dispatches the particular methods this class provides
     */
    public void HandleRpc(ISender caller, string method, IList args, object req_state) {
      if (method == "StartComputation") {
        Hashtable ht = (Hashtable) args[0];
        MapReduceArgs mr_args = new MapReduceArgs(ht);
        string task_name = mr_args.TaskName;
        MapReduceTask task = null;
        lock(_sync) {
          task = (MapReduceTask) _name_to_task[task_name];
        }
        if (task != null) {
          StartComputation(task, mr_args, req_state);
        } 
        else {
          throw new AdrException(-32608, "No mapreduce task with name: " + task_name);          
        }
      }
      else {
        throw new AdrException(-32601, "No Handler for method: " + method);
      }
    }
    
    /**
     * Starts a map-reduce computation. 
     * @param task map reduce task to start.
     * @param args arguments for the map-reduce task. 
     * @param req_state RPC related state for the invocation.
     */
    protected void StartComputation(MapReduceTask task, MapReduceArgs args, object req_state) {
      MapReduceComputation mr = new MapReduceComputation(this, _node, req_state, 
                                                         task, args); 
      mr.Start();
    }
  }
    
  /** 
   * This class represents the arguments for a map-reduce computation.
   */
  public class MapReduceArgs {
    /** name of the map-reduce task. */
    public readonly string TaskName;
    /** argument to the map function. */
    public readonly object MapArg;
    /** argument to the tree generating function. */
    public readonly object GenArg;

    /**
     * Constructor
     */
    public MapReduceArgs(string task_name,
                         object map_arg,
                         object gen_arg)
    {
      TaskName = task_name;
      MapArg = map_arg;
      GenArg = gen_arg;
    }
    
    /**
     * Constructor
     */
    public MapReduceArgs(Hashtable ht) {
      TaskName = (string) ht["task_name"];
      MapArg =  ht["map_arg"];
      GenArg = ht["gen_arg"];
    }
    
    /**
     * Converts the arguments into a serializable hashtable
     */
    public Hashtable ToHashtable() {
      Hashtable ht = new Hashtable();
      ht["task_name"] = TaskName;
      ht["map_arg"] = MapArg;
      ht["gen_arg"] = GenArg;
      return ht;
    }
  }

  /**
   * This class encapsulates information about a map-reduce invocation.
   */
  public class MapReduceInfo {
    /** next sender. */
    public readonly ISender Sender;
    /** map reduce arguments. */
    public readonly MapReduceArgs Args;
    public MapReduceInfo(ISender sender, MapReduceArgs args) {
      Sender = sender;
      Args = args;
    }
  }


  /**
   * This class represents an instance of a map-reduce computation. 
   */
  public class MapReduceComputation {
    protected readonly Node _node;
    protected readonly RpcManager _rpc;
    protected readonly object _sync;
    
    // local map-reduce handler
    protected readonly MapReduceHandler _mr_handler;
    // task executed in this computation instance
    protected readonly MapReduceTask _mr_task;
    // arguments to the task
    protected readonly MapReduceArgs _mr_args;
    // Rpc related state
    protected readonly object _mr_request_state;

    // result of the map function
    protected object _map_result;
    // hashtable containing <child_sender, result> pairs.
    protected Hashtable _child_results;
    // hashtable mapping a channel to a child sender
    protected Hashtable _queue_to_sender;
    // active child computations
    protected int _active;
    // result of reduce.
    protected object _reduce_result;
    
    /** 
     * Constructor
     * @param handler local handler for map-reduce computations
     * @param node local node
     * @param state RPC related state.
     * @param task map-reduce task.
     * @param args arguments to the map reduce task.
     */
    public MapReduceComputation(MapReduceHandler handler, Node node, object state, 
                                MapReduceTask task,
                                MapReduceArgs args)
    {
      _mr_handler = handler;
      _node = node;
      _rpc = RpcManager.GetInstance(node);
      _mr_request_state = state;
      _mr_task = task;
      _mr_args = args;
      _child_results = new Hashtable();
      _queue_to_sender = new Hashtable();
      _sync = new object();
    }
    
    /** Starts the computation. */
    public void Start() {
      //invoke map
      try {
        _map_result = _mr_task.Map(_mr_args.MapArg);
      } 
      catch(Exception x) {
        _map_result = x;
      }

      Console.Error.WriteLine("MapReduce: {0}, map result: {1}.", _node.Address, _map_result);
      //compute the list of targets
      ArrayList child_gen_info = new ArrayList();
      try {
        IList next_mr_info = _mr_task.GenerateTree(_map_result, _mr_args.GenArg);
        foreach (MapReduceInfo mr_info in next_mr_info) {
          child_gen_info.Add(mr_info);
        }
      } catch (Exception x) {
        Console.Error.WriteLine("MapReduce: {0}, generate tree exception: {1}.", _node.Address, x);        
      }
      
      lock(_sync) {
        _active = child_gen_info.Count;
      }
      Console.Error.WriteLine("MapReduce: {0}, child senders count: {1}.", _node.Address, child_gen_info.Count);
      if (child_gen_info.Count > 0) {
        foreach ( MapReduceInfo mr_info in child_gen_info) {
          Channel child_q = new Channel();
          //keep track of the sender
          lock(_sync) {
            _queue_to_sender[child_q] = mr_info.Sender;
          }
          child_q.CloseAfterEnqueue();
          child_q.EnqueueEvent += new EventHandler(ChildCallback);
          try {
            _rpc.Invoke(mr_info.Sender, child_q, "mapreduce.StartComputation", mr_info.Args.ToHashtable());
          } catch(Exception) {
            ChildCallback(child_q, null);
          }
        }
      } else {
        Channel empty_q = new Channel();
        empty_q.Close();
        ChildCallback(empty_q, null);
      }
    }
    
    /**
     * Invoked when a child map-reduce computation finishes. 
     */
    protected void ChildCallback(object child_o, EventArgs child_event_args) {
      Channel child_q = (Channel) child_o;
      object child_result = null;
      bool reduce = false;
      if (child_q.Count > 0) {
        try {
          RpcResult child_rres = (RpcResult) child_q.Dequeue();
          child_result = child_rres.Result;
        } catch (Exception x) {
          child_result = x;
        }
        Console.Error.WriteLine("MapReduce: {0}, got child result: {1}.", _node.Address, child_result);
      }
      
      lock(_sync) {
        ISender sender = null;
        if (_queue_to_sender.Contains(child_q)) {
          sender = (ISender) _queue_to_sender[child_q];
          _queue_to_sender.Remove(child_q);
          _active--;
        }
        if (sender != null ) {
          _child_results[sender] = child_result;
        }
        reduce = (_active == 0);
      }
      
      if (reduce) {
        //at most one thread gets here
        try {
          _reduce_result = _mr_task.Reduce(_map_result, _child_results);
        } catch (Exception x) {
          _reduce_result = x;
        }
        Console.Error.WriteLine("MapReduce: {0}, reduce result: {1}.", _node.Address, _reduce_result);
        _rpc.SendResult(_mr_request_state, _reduce_result);
      }
    }
  }
}
  
