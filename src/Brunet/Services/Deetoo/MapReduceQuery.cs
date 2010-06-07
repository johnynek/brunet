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
using System.Collections.Specialized;
using System.Text.RegularExpressions;

using Brunet.Services.MapReduce; 
using Brunet.Messaging;
using Brunet.Concurrent;
using Brunet.Util;
namespace Brunet.Services.Deetoo {
  /**
   * This class implements a map-reduce task that allows regular 
   * expression search using Bounded Broadcasting. 
   */ 
  public class MapReduceQuery: MapReduceBoundedBroadcast {
    private CacheList _cl;
    public MapReduceQuery(Node n, CacheList cl): base(n) {
      _cl = cl;
    }
    /*
     * Map method to add CachEntry to CacheList
     * @param map_arg [pattern, query_type]
     */
    public override void Map(Channel q, object map_arg) {
      ArrayList map_args = map_arg as ArrayList;
      string pattern = (string)(map_args[0]);
      string query_type = (string)(map_args[1]);
      IDictionary my_entry = new ListDictionary();
      if (query_type == "regex") {
        my_entry["query_result"] = _cl.RegExMatch(pattern);
      }
      else if (query_type == "exact") {
        my_entry["query_result"] = _cl.ExactMatch(pattern);
      }
      else {
        q.Enqueue(new AdrException(-32608, "No Deetoo match option with this name: " +  query_type) );
        return;
      }
      my_entry["count"] = 1;
      my_entry["height"] = 1;
      q.Enqueue(my_entry);
    }
      
    /**
     * Reduce method
     * @param reduce_arg argument for reduce
     * @param current_result result of current map 
     * @param child_rpc results from children 
     * @param done if done is true, stop reducing and return result
     * return table of hop count, tree depth, and query result
     */  
    public override void Reduce(Channel q, object reduce_arg, 
                                  object current_result, RpcResult child_rpc
                                  ) {

      bool done = false;
      //ISender child_sender = child_rpc.ResultSender;
      string query_type = (string)reduce_arg;
      object child_result = null;
      child_result = child_rpc.Result;
      //child result is a valid result
      if (current_result == null) {
        q.Enqueue(new Brunet.Collections.Pair<object, bool>(child_result, done) );
        return;
      }
      IDictionary my_entry = current_result as IDictionary;
      IDictionary value = child_result as IDictionary;
      int max_height = (int) (my_entry["height"]);
      int count = (int) (my_entry["count"]);
      int y = (int) value["count"];
      my_entry["count"] = count + y;
      int z = (int) value["height"] + 1;
      if (z > max_height) {
        my_entry["height"] = z; 
      }
      if (query_type == "exact") {
        string m_result = (string)(my_entry["query_result"]); //current result
        string c_result = (string)(value["query_result"]); //child result
        if (m_result != null) {
          // if query type is exact matching and current result is not an empty string, 
          // stop searching and return the result immediately.
          done = true;
        }
        else {
          if (c_result != null) {
            done = true;
            my_entry["query_result"] = c_result;
          }
          else {
            //there is no valid result, return null for the entry
            my_entry["query_result"] = null;
          }
        }
        q.Enqueue(new Brunet.Collections.Pair<object, bool>(my_entry, done));
      }
      else if (query_type == "regex") {
        IList q_result = (IList)(my_entry["query_result"]);
        IList c_result = (IList)(value["query_result"]);
	ArrayList combined = new ArrayList();
        combined.AddRange(q_result); //concatenate current result with child result
        combined.AddRange(c_result); //concatenate current result with child result
        my_entry["query_result"] = combined;
        q.Enqueue(new Brunet.Collections.Pair<object, bool>(my_entry, done));
      }
      else {
        q.Enqueue(new AdrException(-32608, "This query type {0} is not supported." + query_type) );
      }
    }
  }
}
