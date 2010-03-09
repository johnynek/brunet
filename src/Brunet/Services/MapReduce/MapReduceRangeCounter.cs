/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2008 Arijit Ganguly <aganguly@acis.ufl.edu> University of Florida  
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
using Brunet.Concurrent;

using Brunet.Messaging;
namespace Brunet.Services.MapReduce {
  /**
   * This class implements a map-reduce task that allows counting number of 
   * nodes in a range and also depth of the resulting trees. 
   */ 
  public class MapReduceRangeCounter: MapReduceBoundedBroadcast {
    public MapReduceRangeCounter(Node n): base(n) {}
    public override void Map(Channel q, object map_arg) {
      IDictionary my_entry = new ListDictionary();
      my_entry["count"] = 1;
      my_entry["height"] = 1;
      q.Enqueue( my_entry );
    }
    
    public override void Reduce(Channel q, object reduce_arg, 
                                  object current_result, RpcResult child_rpc) {

      bool done = false;
      //ISender child_sender = child_rpc.ResultSender;
      //the following can throw an exception, will be handled by the framework
      object child_result = child_rpc.Result;
      
      //child result is a valid result
      if (current_result == null) {
        q.Enqueue(new Brunet.Collections.Pair<object, bool>(child_result, done));
        return;
      }
      
      IDictionary my_entry = current_result as IDictionary;
      IDictionary value = child_result as IDictionary;
      int max_height = (int) my_entry["height"];
      int count = (int) my_entry["count"];

      int y = (int) value["count"];
      my_entry["count"] = count + y;
      int z = (int) value["height"] + 1;
      if (z > max_height) {
        my_entry["height"] = z; 
      }
      q.Enqueue(new Brunet.Collections.Pair<object, bool>(my_entry, done));
    }
  }
}
