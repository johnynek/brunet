/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2008 Arijit Ganguly <aganguly@acis.ufl.edu> University of Florida  
                   P. Oscar Boykin <boykin@pobox.com>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Specialized;

using Brunet.Concurrent;
using Brunet.Util;

using Brunet.Messaging;
namespace Brunet.Services.MapReduce {
  /**
   * The following class implements a map-reduce task that allows counting number of 
   * collection of important statistics along a greedy path to a destination. 
   */   
  public class MapReduceTrace: MapReduceGreedy {
    public MapReduceTrace(Node n):base(n) {}
    public override void Map(Channel q, object map_arg) {
      IList retval = new ArrayList();
      IDictionary my_entry = new ListDictionary();
      my_entry["node"] = _node.Address.ToString();
      retval.Add(my_entry);
      q.Enqueue(retval);
    }
    

    public override void Reduce(Channel q, object reduce_arg, 
                                  object current_result, RpcResult child_rpc) {

      bool done = false;
      ISender child_sender = child_rpc.ResultSender;
      //the following can throw an exception, will be handled by the framework
      object child_result = child_rpc.Result;
      

      //child result is a valid result
      if (current_result == null) {
        q.Enqueue(new Brunet.Collections.Pair<object, bool>(child_result, done));
        return;
      }
      

      ArrayList retval = current_result as ArrayList;
      IDictionary my_entry = (IDictionary) retval[0];
      my_entry["next_con"] = child_sender.ToUri();
      retval.AddRange((IList) child_result);
      
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.MapReduce, 
                          String.Format("{0}: {1}, reduce list count: {2}.", this.TaskName, _node.Address, retval.Count));
      }
      q.Enqueue(new Brunet.Collections.Pair<object, bool>(retval, done));
    }
  }
}
