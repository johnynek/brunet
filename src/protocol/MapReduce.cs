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
   * 
   * By default, all methods throw NotImplemented, so we can
   * write tasks that just implement Reduce, Map, or GenerateTree
   * which can individually be accessed by Rpc calls.
   */
  public abstract class MapReduceTask {
//    private static int _log_enabled = -1;
    public static bool LogEnabled {
      get {
        return ProtocolLog.MapReduce.Enabled;
        /*
        int val = _log_enabled;
        if (val == -1) {
          val = ProtocolLog.MapReduce.Enabled ? 1 : 0;
          Interlocked.Exchange(ref _log_enabled, val);
        }
        return val == 1;
        */
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
    protected readonly WriteOnce<string> _task_name;
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
    public virtual void Map(Channel q, object map_arg) {
      throw new NotImplementedException();
    }
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
    public virtual void Reduce(Channel q, object reduce_arg, object current_result, RpcResult child_rpc) {
      throw new NotImplementedException();
    }
    /** tree generator function.
     * @param q The channel into which to put exactly one IList<MapReduceInfo>
     * @param args the MapReduceArgs for this call
     */
    public virtual void GenerateTree(Channel q, MapReduceArgs args) {
      throw new NotImplementedException();
    }
  }

  /** A class to handle dispatching RPC-based MapReduce
   * This class handles calling RPC-based MapReduce.  There
   * is one issue: to support XML-RPC, avoid using
   * any types not supported by XML-RPC, such as Null.
   */
  public class RpcMapReduceTask : MapReduceTask {
    protected readonly Pair<ISender, string> _map;
    protected readonly Pair<ISender, string> _tree;
    protected readonly Pair<ISender, string> _reduce;

    /**
     * @param node
     * @param targets keys should contain map, tree, reduce.  Values should be
     * pairs sender uris and method in that order
     */
    public RpcMapReduceTask(Node n, IDictionary targets) : base(n) {
      _task_name.Value = (string)targets["task_name"];
      _map = ParseTarget((IList)targets["map"]);
      _tree = ParseTarget((IList)targets["tree"]);
      _reduce = ParseTarget((IList)targets["reduce"]);
    }

    protected Brunet.Util.Pair<ISender, string> ParseTarget(IList targ) {
      var send = SenderFactory.CreateInstance(_node, (string)targ[0]);
      var meth = (string)targ[1];
      return new Brunet.Util.Pair<ISender, string>(send, meth);
    }

    public override void Map(Channel q, object map_arg) {
      Channel result = new Channel(1, q);
      result.CloseEvent += this.MapHandler;
      _node.Rpc.Invoke(_map.First, result, _map.Second, map_arg);
    }
    protected void MapHandler(object o, EventArgs args) {
      Channel result = (Channel)o;
      Channel q = (Channel)result.State;
      try {
        RpcResult r = (RpcResult)result.Dequeue();
        q.Enqueue(r.Result);  
      }
      catch(Exception x) {
        //Some kind of problem:
        q.Enqueue(x);
      }
    }
    public override void GenerateTree(Channel q, MapReduceArgs args) {
      Channel result = new Channel(1, q);
      result.CloseEvent += this.TreeHandler;
      _node.Rpc.Invoke(_tree.First, result, _tree.Second, args.ToHashtable());
    }
    protected void TreeHandler(object o, EventArgs eargs) {
      Channel result = (Channel)o;
      Channel q = (Channel)result.State;
      try {
        RpcResult r = (RpcResult)result.Dequeue();
        var mris = new List<MapReduceInfo>();
        foreach(IDictionary d in (IList)r.Result) {
          var uri = (string)d["sender"];
          var sender = SenderFactory.CreateInstance(_node, uri);
          var args = new MapReduceArgs((IDictionary)d["args"]);
          mris.Add(new MapReduceInfo(sender, args));
        }
        q.Enqueue(mris.ToArray());
      }
      catch(Exception x) {
        //Some kind of problem:
        q.Enqueue(x);
      }
    }
    public override void Reduce(Channel q, object reduce_arg, object current_result, RpcResult child_rpc) {
      Channel result = new Channel(1, q);
      result.CloseEvent += this.ReduceHandler;
      var childrpc_ht = new Hashtable();
      ISender rsend = child_rpc.ResultSender;
      childrpc_ht["sender"] = rsend != null ? rsend.ToUri() : "sender:localnode";
      try {
        //If this is an exception, this will throw
        childrpc_ht["result"] = child_rpc.Result; 
      }
      catch(Exception x) {
        childrpc_ht["result"] = x;
      }
      _node.Rpc.Invoke(_reduce.First, result, _reduce.Second, reduce_arg, current_result, childrpc_ht);
    }
    protected void ReduceHandler(object o, EventArgs args) {
      Channel result = (Channel)o;
      Channel q = (Channel)result.State;
      try {
        RpcResult r = (RpcResult)result.Dequeue();
        //The result should be a list:
        IList l = (IList)r.Result;
        q.Enqueue(new Pair<object, bool>(l[0], (bool)l[1]));
      }
      catch(Exception x) {
        q.Enqueue(x);
      }
    }
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
      //Set up some basic tasks:
      var basetasks = new MapReduceTask[]{
        new MapReduceBoundedBroadcast(_node),
        new MapReduceGreedy(_node),
        new MapReduceListConcat(_node),
      };
      foreach(MapReduceTask mrt in basetasks) {
        SubscribeTask(mrt);
      }
    }
    
    /**
     * This dispatches the particular methods this class provides.
     * Currently, the only invokable method is:
     * "Start". 
     */
    public void HandleRpc(ISender caller, string method, IList args, object req_state) {
      int part_idx = method.IndexOf(':');
      if( part_idx == -1 ) {
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
        else if( method == "AddHandler" ) {
          //Make sure this is local:
          ISender tmp_call = caller;
          bool islocal = tmp_call is Node;
          while(!islocal && tmp_call is IWrappingSender) {
            tmp_call =  ((IWrappingSender)tmp_call).WrappedSender;
            islocal = tmp_call is Node;
          }
          if( !islocal ) {
            throw new AdrException(-32601, "AddHandler only valid for local callers");
          }
          SubscribeTask(new RpcMapReduceTask(_node, (IDictionary)args[0]));
          _rpc.SendResult(req_state, null);
        }
        else {
          throw new AdrException(-32601, "No Handler for method: " + method);
        }
      }
      else {
        //This is a reference to a specific part of a task:
        string part = method.Substring(0, part_idx);
        string task_name = method.Substring(part_idx + 1);
        MapReduceTask task;
        if(false == _name_to_task.TryGetValue(task_name, out task)) {
          throw new AdrException(-32608, "No mapreduce task with name: " + task_name);          
        }
        if( part == "tree" ) {
          var mra = new MapReduceArgs((IDictionary)args[0]);

          var tree_res = new Channel(1, req_state);
          tree_res.CloseEvent += this.HandleTree;
          task.GenerateTree(tree_res, mra);
        }
        else if( part == "reduce" ) {
          //Prepare the RpcResult:
          var rres_d = (IDictionary)args[2];
          ISender send = SenderFactory.CreateInstance(_node, (string)rres_d["sender"]);
          var rres = new RpcResult(send, rres_d["result"]);
          
          Channel reduce_res = new Channel(1, req_state);
          reduce_res.CloseEvent += this.HandleReduce;
          task.Reduce(reduce_res, args[0], args[1], rres);
        }
        else if( part == "map" ) {
          Channel map_res = new Channel(1, req_state);
          map_res.CloseEvent += this.HandleMap;
          task.Map(map_res, args[0]);
        }
        else {
          throw new AdrException(-32608,
              String.Format("No mapreduce task({0}) part with name: {1}", task_name, part));          
        }
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

    protected void HandleMap(object q, EventArgs eargs) {
      object result;
      Channel map_res = (Channel)q;
      try {
        result = map_res.Dequeue();
      }
      catch(Exception x) {
        result = x;
      }
      _rpc.SendResult(map_res.State, result);
    }
    protected void HandleReduce(object q, EventArgs args) {
      object result;
      Channel red_res = (Channel)q;
      try {
        object red_o = red_res.Dequeue();
        if( red_o is Exception ) {
          result = red_o;
        }
        else {
          var pres = (Pair<object, bool>)red_o;
          result = new object[]{pres.First, pres.Second};
        }
      }
      catch(Exception x) {
        result = x;
      }
      _rpc.SendResult(red_res.State, result);
    }
    protected void HandleTree(object q, EventArgs args) {
      object result;
      Channel resq = (Channel)q;
      try {
        var tres = (IEnumerable)resq.Dequeue();
        var ld = new List<IDictionary>();
        foreach(MapReduceInfo mri in tres) {
          var mrid = new ListDictionary();
          mrid["sender"] = mri.Sender.ToUri();
          mrid["args"] = mri.Args.ToHashtable();
          ld.Add(mrid); 
        }
        result = ld;
      }
      catch(Exception x) {
        result = x;
      }
      _rpc.SendResult(resq.State, result);
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
//    private static int _log_enabled = -1;
    public static bool LogEnabled {
      get {
        return ProtocolLog.MapReduce.Enabled;
        /*
        int val = _log_enabled;
        if (val == -1) {
          val = ProtocolLog.MapReduce.Enabled ? 1 : 0;
          Interlocked.Exchange(ref _log_enabled, val);
        }
        return val == 1;
        */
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
        if( MapResult != DEFAULT_OBJ ) {
          throw new Exception("MapReduce: MapResult already set");
        }
        return new State(m, Tree, ChildReductions, ReduceResult, Done, Pending, true);
      }
      /*
       * This cannot change our Reducing state.
       */
      public State UpdateTree(MapReduceInfo[] tree) {
        if( Tree != null ) {
          throw new Exception("MapReduce: Tree already set");
        }
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
        bool done = Done || val.Second;
        int child_reds = ChildReductions;
        if( child_reduction ) {
          child_reds++;
        }
        if( false == Pending.IsEmpty && false == done ) {
          //More to reduce:
          pend = Pending.Tail;
          reduce = true;
        }
        else {
          pend = ImmutableList<RpcResult>.Empty;
          reduce = false;
          if( false == done ) {
            /*
             * Check to see if we have hit all our children
             * and we have already completed the map
             */
            if( Tree != null ) {
              done = (child_reds == Tree.Length);
            }
            else {
              //We haven't yet gotten the Tree result back, done = false
            }
          }
        }
        return new State(MapResult, Tree, child_reds, val.First, done, pend, reduce);
      }
      /*
       * We always reduce our map call first, then children.
       */
      public State AddChildResult(RpcResult child) {
        if( Reducing ) {
          //We need to add this to pending
          var pend = new ImmutableList<RpcResult>(child, Pending);
          return new State(MapResult, Tree, ChildReductions, ReduceResult, Done, pend, true); 
        }
        else if( MapResult == DEFAULT_OBJ ) {
          /*
           * We are not reducing AND we have not finished the first
           * Reduce, that means we are waiting for the Map result.
           */
          var pend = new ImmutableList<RpcResult>(child, Pending);
          return new State(MapResult, Tree, ChildReductions, ReduceResult, Done, pend, false); 
        }
        else {
          /*
           * In this case, we are not already reducing, and we have reduced
           * the MapResult
           */
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
        HandleException(null, x);
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
        HandleException(null, x);
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
      TryNextReduce(new_state, old_state, new RpcResult(null, map_res), false);
    }

    /**
     * @return true if we successfully started the next reduce
     */
    protected bool TryNextReduce(State new_s, State old_s, RpcResult v, bool cont) {
      if( new_s.Done ) {
        SendResult( new_s.ReduceResult );
        return false;
      }
      bool start_red = new_s.Reducing && (cont || (false == old_s.Reducing));
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, TryNextReduce: {1}.",
                          _node.Address, start_red));
      }
      if( start_red ) {
        Channel r_chan = new Channel(1, v);
        r_chan.CloseEvent += this.ReduceHandler;
        object startval = new_s.ReduceResult == State.DEFAULT_OBJ ? null : new_s.ReduceResult;
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0} abt to Reduce({1},{2},{3})",
                            _node.Address, _mr_args.ReduceArg, startval, v));
        }
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
      Channel reduce_chan = (Channel)ro;
      RpcResult value_reduced = (RpcResult)reduce_chan.State;
      try { 
        object r_o = reduce_chan.Dequeue();
        Exception r_x = r_o as Exception;
        if( r_x != null ) {
          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, resulted in: {1}.",
                          _node.Address, r_x));
          }
          SendResult(r_x);
          return;
        }
        retval = (Brunet.Util.Pair<object, bool>)r_o;
      }
      catch(Exception x) {
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, resulted in: {1}.", _node.Address, x));
        }
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
      
      TryNextReduce(new_state, old_state, old_state.Pending.Head, true);
    }

    protected void GenTreeHandler(object chano, EventArgs args) {
      if (LogEnabled) {
              ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, GenTreeHandler", _node.Address));
      }
      Channel gtchan = (Channel)chano;
      MapReduceInfo[] children;
      object gtchan_result = null;
      try {
        gtchan_result = gtchan.Dequeue();
        children = (MapReduceInfo[])gtchan_result;
      }
      catch(Exception x) {
        //We could fail to return (queue is empty), or get a bad result
        Exception rx = gtchan_result as Exception;
        if( rx != null) {
          x = rx; //x should have been a bad cast
        }
        if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.MapReduce,        
                          String.Format("MapReduce: {0}, GenTreeHandler exception ({1})", _node.Address, x));
        }
        HandleException(null, x);
        return;
      }
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
      foreach(MapReduceInfo mri in children) {
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
          HandleException(mri.Sender, x);
        }
      }
    }

    protected void HandleException(ISender from, Exception x) {
      ///@todo perhaps we can get Reduce to handle errors?
      SendResult(x);
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
      TryNextReduce(new_state, old_state, child_r, false);
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
  
