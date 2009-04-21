/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2008  Arijit Ganguly <aganguly@gmail.com>, University of Florida
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
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

using Brunet.Util;

namespace Brunet {
  /** 
   * This class is the base class for a map-reduce task.
   * These tasks are completely stateless. All the state related 
   * to the progress of the computation is stored inside
   * a MapReduceComputation object.
   */
  public abstract class MapReduceTask {
    protected static int _log_enabled = -1;
    public static bool LogEnabled {
      get {
        int val = _log_enabled;
        if (val == -1) {
          val = ProtocolLog.MapReduce.Enabled ? 1 : 0;
          Interlocked.Exchange(ref _log_enabled, val);
        }
        return val == 1;
      }
    }

    protected void Log(string format_string, params object[] format_args) {
      if (LogEnabled) {
        string s = String.Format(format_string, format_args);
        ProtocolLog.Write(ProtocolLog.MapReduce, 
                          String.Format("{0}: {1}, {2}", _node.Address, this.GetType(), s));
      }
    }

    protected readonly Node _node;
    private readonly WriteOnce<string> _task_name;
    /** unique type of the task. */
    public string TaskName {
      get {
        string val;
        if( !_task_name.TryGet(out val) ) {
          val = this.GetType().ToString();
          if( !_task_name.TrySet( val ) ) { val = _task_name.Value; }
        }
        return val;
      }
    }

    /**
     * Constructor.
     * @param n local node
     */
    protected MapReduceTask(Node n) {
      _node = n;
      _task_name = new WriteOnce<string>();
    }
    
    
    /** map function.
     * @param q the Channel into which the Map result is Enqueue 'd
     * @param map_arg the argument for the map function
     * */
    public abstract void Map(Channel q, object map_arg);
    /** 
     * reduce function.  This reduces the local and children results into one.
     * This is also the error handling function.  Any exceptions must be
     * handled by this method, if not, the computation stops immediately and
     * sends the exception back up the tree.
     * 
     * @param q the Channel into which a Brunet.Util.Pair<object, bool> is enqueued,
     * if the second item is true, we stop querying nodes
     * @param reduce_arg arguments for the reduce
     * @param current_result accumulated result of reductions
     * @param child_rpc result from child computation
     */
    public abstract void Reduce(Channel q, object reduce_arg, object current_result, RpcResult child_rpc);
    /** tree generator function.
     * @param q The channel into which to put exactly one MapReduceInfo[]
     * @param args the MapReduceArgs for this call
     * */
    public abstract void GenerateTree(Channel q, MapReduceArgs args);
  }
  
  /** 
   * This class provides an RPC interface into the map reduce functionality. 
   * To invoke a map-reduce task, we make an RPC call to 
   * "mapreduce.Start". The argument to this call is a
   * Hashtable describing the arguments to the call.
   * Later, it might be possible to add new methods that would allow 
   * inquiring state of a map-reduce task while it is running. 
   */  
  
  public class MapReduceHandler: IRpcHandler {
    protected readonly object _sync;
    protected readonly Node _node;
    protected readonly RpcManager _rpc;
    /** mapping of map-reduce task names to task objects. */
    protected readonly Dictionary<string, MapReduceTask> _name_to_task;
    
    /**
     * Constructor
     * @param n local node
     */
    public MapReduceHandler(Node n) {
      _node = n;
      _rpc = n.Rpc;
      _name_to_task = new Dictionary<string, MapReduceTask>();
      _sync = new object();
    }
    
    /**
     * This dispatches the particular methods this class provides.
     * Currently, the only invokable method is:
     * "Start". 
     */
    public void HandleRpc(ISender caller, string method, IList args, object req_state) {
      if (method == "Start") {
        IDictionary ht = (IDictionary) args[0];
        MapReduceArgs mr_args = new MapReduceArgs(ht);
        string task_name = mr_args.TaskName;
        MapReduceTask task;
        if (_name_to_task.TryGetValue(task_name, out task)) {
          MapReduceComputation mr = new MapReduceComputation(_node, req_state, task, mr_args);
          mr.Start();
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
     * Allows subscribing new map-reduce tasks to the handler.
     * @param task an object representing the map-reduce task.
     */
    public void SubscribeTask(MapReduceTask task) {
      lock(_sync) {
        if (_name_to_task.ContainsKey(task.TaskName)) {
          throw new Exception(String.Format("Map reduce task name: {0} already registered.", task.TaskName));
        }
        _name_to_task[task.TaskName] = task;
      }
    }
  }
    
  /** 
   * This class represents the arguments for a map-reduce computation.
   */
  public class MapReduceArgs {
    /** name of the task. */
    public readonly string TaskName;
    /** argument to the map function. */
    public readonly object MapArg;
    /** argument to the tree generating function. */
    public readonly object GenArg;
    /** argument to the reduce function. */
    public readonly object ReduceArg;

    /**
     * Constructor
     */
    public MapReduceArgs(string task_name, 
                         object map_arg,
                         object gen_arg,
                         object reduce_arg)
    {
      TaskName = task_name;
      MapArg = map_arg;
      GenArg = gen_arg;
      ReduceArg = reduce_arg;
    }
    
    /**
     * Constructor
     */
    public MapReduceArgs(IDictionary ht) {
      TaskName =  (string) ht["task_name"];
      MapArg =  ht["map_arg"];
      GenArg = ht["gen_arg"];
      ReduceArg = ht["reduce_arg"];
    }
    
    /**
     * Converts the arguments into a serializable hashtable
     */
    public Hashtable ToHashtable() {
      Hashtable ht = new Hashtable();
      ht["task_name"] = TaskName;
      ht["map_arg"] = MapArg;
      ht["gen_arg"] = GenArg;
      ht["reduce_arg"] = ReduceArg;
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
    protected static int _log_enabled = -1;
    public static bool LogEnabled {
      get {
        int val = _log_enabled;
        if (val == -1) {
          val = ProtocolLog.MapReduce.Enabled ? 1 : 0;
          Interlocked.Exchange(ref _log_enabled, val);
        }
        return val == 1;
      }
    }
    
    protected readonly Node _node;
    protected readonly RpcManager _rpc;
    protected readonly object _sync;
    
    // task executed in this computation instance
    protected readonly MapReduceTask _mr_task;

    // arguments to the task
    protected readonly MapReduceArgs _mr_args;

    // Rpc related state
    protected readonly object _mr_request_state;

    /*
     * Represents the state of the MapReduceComputation
     * This is an immutable object.
     */
    protected class State {
      public readonly object MapResult;
      public readonly MapReduceInfo[] Tree;
      public readonly object ReduceResult;
      public readonly bool Done;
      public readonly int ChildReductions;
      public readonly ImmutableList<RpcResult> Pending;
      public readonly bool Reducing;
      
      public static readonly object DEFAULT_OBJ = new object();
      
      public State() {
        MapResult = DEFAULT_OBJ;
        Tree = null;
        ChildReductions = 0;
        ReduceResult = DEFAULT_OBJ;
        Done = false;
        Pending = ImmutableList<RpcResult>.Empty;
        Reducing = false;
      }

      protected State(object map, MapReduceInfo[] tree, int cred, object reduce, bool done, ImmutableList<RpcResult> pend, bool reducing) {
        MapResult = map;
        Tree = tree;
        ChildReductions = cred;
        ReduceResult = reduce;
        Done = done;
        Pending = pend;
        Reducing = reducing;
      }

      /*
       * After we have the map result, we immediately start to reduce it
       */
      public State UpdateMap(object m) {
        return new State(m, Tree, ChildReductions, ReduceResult, Done, Pending, true);
      }
      /*
       * This cannot change our Reducing state.
       */
      public State UpdateTree(MapReduceInfo[] tree) {
        bool done = Done;
        if( ReduceResult != DEFAULT_OBJ ) {
          /*
           * We have already reduced the map result, we are done, ONLY if
           * the tree is empty:
           */
          done = done || (tree.Length == 0);
        }
        else {
          //We haven't yet reduced the map result
          //Done should be false
        }
        return new State(MapResult, tree, ChildReductions, ReduceResult, done, Pending, Reducing);
      }
      
      public State UpdateReduce(Brunet.Util.Pair<object,bool> val, bool child_reduction) {
        //If we can, pop off a result which we need to reduce:
        ImmutableList<RpcResult> pend;
        bool reduce;
        bool done = val.Second;
        int child_reds = ChildReductions;
        if( child_reduction ) {
          child_reds++;
        }
        if( false == Pending.IsEmpty ) {
          //More to reduce:
          pend = Pending.Tail;
          reduce = true;
        }
        else {
          pend = ImmutableList<RpcResult>.Empty;
          reduce = false;
          if( false == done ) {
            //Check to see if we have hit all our children:
            if( Tree != null ) {
              done = child_reds == Tree.Length;
            }
          }
        }
        return new State(MapResult, Tree, child_reds, val.First, done, pend, reduce);
      }
      /*
       * If we were not already reducing, we will be after this call
       */
      public State AddChildResult(RpcResult child) {
        if( Reducing ) {
          //We need to add this to pending
          var pend = new ImmutableList<RpcResult>(child, Pending);
          return new State(MapResult, Tree, ChildReductions, ReduceResult, Done, pend, true); 
        }
        else {
          //We can go ahead and reduce this new value
          return new State(MapResult, Tree, ChildReductions, ReduceResult, Done, ImmutableList<RpcResult>.Empty, true); 
        }
      }
    }

    protected State _state;
    protected object _result; 
    /** 
     * Constructor
     * @param node local node
     * @param state RPC related state.
     * @param task map-reduce task.
     * @param args arguments to the map reduce task.
     */
    public MapReduceComputation(Node node, object state, 
                                MapReduceTask task,
                                MapReduceArgs args)
    {
      _node = node;
      _rpc = node.Rpc;
      _mr_request_state = state;
      _mr_task = task;
      _mr_args = args;
      //Here is our state variable:
      _state = new State();
      _result = State.DEFAULT_OBJ;
    }
    
    /** Starts the computation. */
    public void Start() {
      //invoke map
      try {
        Channel map_res = new Channel(1);
        map_res.CloseEvent += this.MapHandler;
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, about to call Map", _node.Address));
        }
        _mr_task.Map(map_res, _mr_args.MapArg);
      } 
      catch(Exception x) {
        //Simulate the above except with the Exception enqueued
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, Exception in Map: {1}", _node.Address, x));
        }
        Channel map_res2 = new Channel(1);
        map_res2.CloseEvent += this.MapHandler;
        map_res2.Enqueue(x);
      }
      if( _state.Done ) { return; }
      /* Our local Map was not enough
       * to finish the computation, look
       * for children
       */
      try {
        Channel gentree_res = new Channel(1);
        gentree_res.CloseEvent += this.GenTreeHandler;
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, about to call GenerateTree", _node.Address));
        }
        _mr_task.GenerateTree(gentree_res, _mr_args);
      }
      catch(Exception x) {
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, Exception in GenerateTree: {1}", _node.Address, x));
        }
        //Simulate the above except with the Exception enqueued
        Channel gentree_res = new Channel(1);
        gentree_res.CloseEvent += this.GenTreeHandler;
        gentree_res.Enqueue(x);
      }
    }

    protected void MapHandler(object chan, EventArgs args) {
      //Get the Map result:
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, in MapHandler", _node.Address));
      }
      object map_res;
      Channel map_chan = (Channel)chan;
      if( map_chan.Count == 0 ) {
        //We must have timed out trying to get the Map result
        map_res = new AdrException(-32000, "no map result");
      }
      else {
        map_res = map_chan.Dequeue();
      }
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, got map result: {1}.", _node.Address, map_res));
      }
      //The usual transactional bit:
      State state = _state;
      State old_state;
      State new_state;
      do {
        old_state = state;
        new_state = old_state.UpdateMap(map_res);
        state = Interlocked.CompareExchange<State>(ref _state, new_state, old_state);
      }
      while( state != old_state);
      //Do the first reduction:
      TryNextReduce(new_state, old_state, new RpcResult(null, map_res));
    }

    /**
     * @return true if we successfully started the next reduce
     */
    protected bool TryNextReduce(State new_s, State old_s, RpcResult v) {
      if( new_s.Done ) {
        SendResult( new_s.ReduceResult );
        return false;
      }
      bool start_red = new_s.Reducing && (false == old_s.Reducing);
      if( start_red ) {
        Channel r_chan = new Channel(1, v);
        r_chan.CloseEvent += this.ReduceHandler;
        object startval = new_s.ReduceResult == State.DEFAULT_OBJ ? null : new_s.ReduceResult;
        try {
          _mr_task.Reduce(r_chan, _mr_args.ReduceArg, startval, v);
        }
        catch(Exception x) {
          //Reduce is where we do error handling, if that doesn't work, oh well:
          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, reduce threw: {1}.", _node.Address, x));
          }
          SendResult(x);
          return false;
        }
      }
      return start_red;
    }

    protected void ReduceHandler(object ro, EventArgs args) {
      Brunet.Util.Pair<object, bool> retval;
      RpcResult value_reduced;
      try { 
        Channel reduce_chan = (Channel)ro;
        value_reduced = (RpcResult)reduce_chan.State;
        retval = (Brunet.Util.Pair<object, bool>)reduce_chan.Dequeue();
      }
      catch(Exception x) {
        SendResult(x);
        return;
      }
      //The usual transactional bit:
      State state = _state;
      State old_state;
      State new_state;
      do {
        old_state = state;
        /*
         * compute new_state here
         */
        new_state = old_state.UpdateReduce(retval, value_reduced.ResultSender != null);
        state = Interlocked.CompareExchange<State>(ref _state, new_state, old_state);
      }
      while( state != old_state);
      
      TryNextReduce(new_state, old_state, old_state.Pending.Head);
    }

    protected void GenTreeHandler(object chano, EventArgs args) {
      if (LogEnabled) {
              ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, GenTreeHandler", _node.Address));
      }
      try {
        Channel gtchan = (Channel)chano;
        MapReduceInfo[] children = (MapReduceInfo[])gtchan.Dequeue();
      
        //The usual transactional bit:
        State state = _state;
        State old_state;
        State new_state;
        do {
          old_state = state;
          new_state = old_state.UpdateTree(children);
          state = Interlocked.CompareExchange<State>(ref _state, new_state, old_state);
        }
        while( state != old_state);
        if( new_state.Done ) {
          //We don't need to start calling our children
          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, done on GenTreeHandler", _node.Address));
          }
          SendResult( new_state.ReduceResult );
          return;
        }
        //Now we need to start calling our children:
        foreach(MapReduceInfo mri in new_state.Tree) {
          Channel child_q = new Channel(1, mri);
          child_q.CloseEvent += this.ChildCallback;
          try {
            if (LogEnabled) {
              ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, calling child ({1})", _node.Address, mri.Sender.ToUri()));
            }
            _rpc.Invoke(mri.Sender, child_q,  "mapreduce.Start", mri.Args.ToHashtable());
          }
          catch(Exception x) {
            if (LogEnabled) {
              ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, child ({1}) call threw: {2}.", _node.Address, mri.Sender.ToUri(), x));
            }
            Reduce(new RpcResult(mri.Sender, x));
          }
        }
      }
      catch(Exception x) {
        //We reduce exceptions:
        if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, GenTreeHandler exception ({1})", _node.Address, x));
        }
        Reduce(new RpcResult(null, x));
      }
    }

    protected void Reduce(RpcResult child_r) {
      //The usual transactional bit:
      State state = _state;
      State old_state;
      State new_state;
      do {
        old_state = state;
        new_state = old_state.AddChildResult(child_r);
        state = Interlocked.CompareExchange<State>(ref _state, new_state, old_state);
      }
      while( state != old_state);
      //If we need to start a new reduce, it's the latest value:  
      TryNextReduce(new_state, old_state, child_r);
    }

    protected void ChildCallback(object cq, EventArgs arg) {
      RpcResult child_r;
      Channel child_q = (Channel)cq;
      MapReduceInfo mri = (MapReduceInfo)child_q.State;
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, handling child result from: {1}.", _node.Address, mri.Sender.ToUri()));
      }
      if( child_q.Count == 0 ) {
        child_r = new RpcResult(mri.Sender, new AdrException(-32000, "Child did not return"));
      }
      else {
        child_r = (RpcResult)child_q.Dequeue();
      }
      Reduce(child_r);
    }

    /**
     * Sends the result of the computation back.
     * @return true if this is the first time this has been called
     */ 
    protected bool SendResult(object result) {
      object old_res = Interlocked.CompareExchange(ref _result, result, State.DEFAULT_OBJ);
      if( old_res == State.DEFAULT_OBJ ) {
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, sending back result: {1}.", _node.Address, result));
        }
        _rpc.SendResult(_mr_request_state, result);
        return true;
      }
      else {
        return false;
      }
    }
  }
}
  
