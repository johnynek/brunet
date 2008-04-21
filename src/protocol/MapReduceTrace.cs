/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
    protected readonly Node _node;
    public MapReduceTrace(Node n) {
      _node = n;
    }
    public override string Name {
      get {
        return "traceroute";
      }
    }
    
    public override object Map(object map_arg) {
      return _node.Address.ToString();
    }
    
    public override IList GenerateTree(object map_result, object gen_arg) {
      Console.Error.WriteLine("MapReduceTrace: {0}, greedy generator called, arg: {1}.", _node.Address, gen_arg);
      string address = gen_arg as string;
      AHAddress a =  (AHAddress) AddressParser.Parse(address);
      ArrayList retval = new ArrayList();
      ConnectionTable tab = _node.ConnectionTable;
      ConnectionList structs = tab.GetConnections(ConnectionType.Structured);
      Connection next_closest = structs.GetNearestTo((AHAddress) _node.Address, a);
      if (next_closest != null) {
        MapReduceInfo mr_info = new MapReduceInfo( (ISender) next_closest.Edge, 
                                                   new MapReduceArgs(this.Name, null, address));
        retval.Add(mr_info);
      }
      
      Console.Error.WriteLine("MapReduceTrace: {0}, greedy generator returning: {1} senders.", _node.Address, retval.Count);
      return retval;
    }
    
    public override object Reduce(object map_result, Hashtable child_results) {
      ArrayList retval = new ArrayList();
      ListDictionary my_entry = new ListDictionary();
      my_entry["node"] = map_result;
      int count = 0;
      ISender edge = null;
      IList value = null;
      foreach(DictionaryEntry  e in child_results) {
        if (count > 0) {
          break;
        }
        count++;
        edge = e.Key as Edge;
        value = e.Value as IList;
      }
      
      retval.Add(my_entry);
      if (edge != null) {
        my_entry["next_con"] = edge.ToString();
        if (value != null) {
          retval.AddRange(value);
        }
      }
      
      Console.Error.WriteLine("MapReduceTrace: {0}, reduce list count: {1}.", _node.Address, retval.Count);
      return retval;
    }
  }
}
