/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 Arijit Ganguly <aganguly@acis.ufl.edu> University of Florida  
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
  public class MapReduceTrace: MapReduceTask {
    public MapReduceTrace(Node n):base(n) {}
    public override object Map(object map_arg) {
      IList retval = new ArrayList();
      IDictionary my_entry = new ListDictionary();
      my_entry["node"] = _node.Address.ToString();
      retval.Add(my_entry);
      return retval;
    }
    
    public override IList GenerateTree(object gen_arg) {
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.MapReduce,
                          String.Format("{0}: {1}, greedy generator called, arg: {2}.", 
                                        this.TaskName, _node.Address, gen_arg));
      }
      string address = gen_arg as string;
      AHAddress a =  (AHAddress) AddressParser.Parse(address);
      ArrayList retval = new ArrayList();
      ConnectionTable tab = _node.ConnectionTable;
      ConnectionList structs = tab.GetConnections(ConnectionType.Structured);
      Connection next_closest = structs.GetNearestTo((AHAddress) _node.Address, a);
      if (next_closest != null) {
        MapReduceInfo mr_info = new MapReduceInfo( (ISender) next_closest.Edge,
                                                   new MapReduceArgs(TaskName, null, address));
        retval.Add(mr_info);
      }
      
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.MapReduce,
                          String.Format("{0}: {1}, greedy generator returning: {2} senders.", 
                                        this.TaskName, _node.Address, retval.Count));
      }
      return retval;
    }
    
    public override object Reduce(object current_result, ISender child_sender, object child_result, ref bool done) {
      if (current_result == null) {
        return child_result;
      }
      ArrayList retval = current_result as ArrayList;
      IDictionary my_entry = (IDictionary) retval[0];
      Edge e = child_sender as Edge;
      my_entry["next_con"] = e.ToString();
      retval.AddRange((IList) child_result);
      
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.MapReduce, 
                          String.Format("{0}: {1}, reduce list count: {2}.", this.TaskName, _node.Address, retval.Count));
      }
      return retval;
    }
  }
}
