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
using System.Collections.Specialized;
using Brunet.Services.MapReduce;
using Brunet.Messaging;
using Brunet.Concurrent;
using Brunet.Symphony;

namespace Brunet.Services.Deetoo {
  /**
   * This class implements a map-reduce task that allows Deetoo 
   * object caching.
   * BoundedBroadcast is used for tree-generation.
   */ 
  public class MapReduceCache: MapReduceBoundedBroadcast {
    //list of Deetoo object in a node, 
    //[content, start_range, end_range, replication_factor]
    private readonly CacheList _cl;
    private static readonly object UNSET = new object();
    //private Node _node;
    /*
     * @param cl CacheList object for caching.
     */
    public MapReduceCache(Node n, CacheList cl): base(n) {
      //_node = n;
      _cl = cl;
    }
    /*
     * Map method to add CachEntry to CacheList
     * @param map_arg [content,alpha,start,end]
     */
    public override void Map(Channel q, object map_arg) {
      IList arg = map_arg as IList;	    
      object input = arg[0];
      double alpha = (double)arg[1]; //replication factor
      string st = arg[2] as string;  // start brunet address of range
      string ed = arg[3] as string;  // end brunet address of range 
      AHAddress a = (AHAddress)AddressParser.Parse(st);
      AHAddress b = (AHAddress)AddressParser.Parse(ed);
      Entry ce = new Entry(input, alpha, a, b);
      int result = 0;   // if caching is successful, result will set to 1.
      int previous_count = _cl.Count;
      try {
        if( _cl.Insert(ce) ) {
          result = 1;
        }
        else {
          result = 0;
        }
      }
      catch {
        result = 0;
      }
      if (_cl.Count > previous_count) { result = 1; }
      IDictionary my_entry = new ListDictionary();
      my_entry["count"]=1;
      my_entry["height"]=1;
      my_entry["success"]=result;
      q.Enqueue(my_entry);
    }
    
    /**
     * Reduce method
     * @param reduce_arg argument for reduce
     * @param current_result result of current map 
     * @param child_rpc results from children 
     * @param done if done is true, stop reducing and return result
     * return table of hop count, number of success, tree depth
     */
    public override void Reduce(Channel q, object reduce_arg, 
                                  object current_result, RpcResult child_rpc) {

      bool done = false;
      //ISender child_sender = child_rpc.ResultSender;
      //the following can throw an exception, will be handled by the framework
      object child_result = UNSET;
      try {
        child_result = child_rpc.Result;
      }
      catch(Exception x) {
        /*
        if (x.Equals("Child did not return") ) {
          //there might be at least one timeout from children.
	  //Start boundedcasting again
          IDictionary ht;
          string task_name = "Brunet.Services.Deetoo.MapReduceCache;
	  ht["task_name"] = task_name 
	  IDictionary map_arg;
	  IDictionary gen_arg;

	  = (IDictionary) args[0];
          MapReduceArgs mr_args = new MapReduceArgs(ht);
          //string task_name = mr_args.TaskName;
          MapReduceTask task;
          if (_name_to_task.TryGetValue(task_name, out task)) {
            MapReduceComputation mr = new MapReduceComputation(_node, req_state, task, mr_args);
            mr.Start();
          }
	}
        */
      } 
      if( child_result == UNSET ) {
        //The child threw an exception
        q.Enqueue(new Brunet.Collections.Pair<object, bool> (current_result, done));
        return;
      } 
      //child result is a valid result
      if (current_result == null) {
        q.Enqueue(new Brunet.Collections.Pair<object, bool> (child_result, done));
        return;
      }
      //else combine them: 
      IDictionary my_entry = (IDictionary)current_result;
      IDictionary value = (IDictionary)child_result;
      int max_height = (int) my_entry["height"];
      int count = (int) my_entry["count"];
      my_entry["success"] = (int) my_entry["success"] + (int) value["success"];
      int y = (int) value["count"];
      my_entry["count"] = count + y;
      int z = (int) value["height"] + 1;
      if (z > max_height) {
        my_entry["height"] = z; 
      }
      q.Enqueue(new Brunet.Collections.Pair<object, bool>(my_entry,done));
    }
  }
}
