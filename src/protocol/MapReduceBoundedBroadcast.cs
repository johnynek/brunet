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
  public class MapReduceBoundedBroadcast: MapReduceTask {
    public MapReduceBoundedBroadcast(Node n): base(n) {}
    public override object Map(object map_arg) {
      IDictionary my_entry = new ListDictionary();
      my_entry["count"] = 1;
      my_entry["height"] = 1;
      return my_entry;
    }
    
    public override object Reduce(object current_result, ISender child_sender, object child_result, ref bool done) {
      if (current_result == null) {
        return child_result;
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
      return my_entry;
    }

    /**
     * Generates tree for bounded broadcast. Algorithm works as follows:
     * The goal is to broadcast to all nodes in range [local_address, end).
     * Given a range [local_address, b), determine all connections that belong to this range.
     * Let the connections be b_1, b_2, ..... b_n.
     * To connection bi assign the range [b_i, b_{i+1}).
     * To the connection bn assign range [b_n, end).]
     */
    public override IList GenerateTree(object gen_arg) 
    {
      string end_range = gen_arg as string;
      AHAddress end_addr = (AHAddress) AddressParser.Parse(end_range);
      AHAddress start_addr = _node.Address as AHAddress;
      //we are at the start node, here we go:
      ConnectionTable tab = _node.ConnectionTable;
      ConnectionList structs = tab.GetConnections(ConnectionType.Structured);      
      ArrayList con_list = new ArrayList();
      foreach (Connection con in structs) {
        BigInteger start_int = start_addr.ToBigInteger();
        BigInteger end_int = end_addr.ToBigInteger();
        BigInteger con_int = con.Address.ToBigInteger();
        if (con_int < start_int) {
          //still not in reached the range
//           Console.Error.WriteLine("{0}: {1}, address: {2}, smaller than range start: {3}", 
//                                   this.TaskName, _node.Address, 
//                                   con.Address, start_addr);
          continue;
        }

        if (con_int >= end_int)  {
          //crossed the range
//           Console.Error.WriteLine("{0}: {1}, address: {2}, greater than or equal to range end: {3}", 
//                                   this.TaskName, _node.Address, 
//                                   con.Address, end_addr);
          break;
        }
        con_list.Add(con);
      }

      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.MapReduce, 
                          String.Format("{0}: {1}, number of child connections: {2}", 
                                        this.TaskName, _node.Address, con_list.Count));
      }
      IList retval = new ArrayList();
      for (int i = 0; i < con_list.Count; i++) {
        MapReduceInfo mr_info = null;
        ISender sender = null;
        Connection con = (Connection) con_list[i];
        sender = (ISender) con.Edge;
        //check if last connection
        if (i == con_list.Count - 1) {
          mr_info = new MapReduceInfo( (ISender) sender, 
                                       new MapReduceArgs(this.TaskName, null, end_range));

          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.MapReduce, 
                              String.Format("{0}: {1}, adding address: {2} to sender list, range end: {3}", 
                                            this.TaskName, _node.Address, 
                                            con.Address, end_range));
          }
          retval.Add(mr_info);
        }
        else {
          string child_end = ((Connection) con_list[i+1]).Address.ToString();
          mr_info = new MapReduceInfo( sender,
                                       new MapReduceArgs(this.TaskName, null, child_end));
          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.MapReduce, 
                              String.Format("{0}: {1}, adding address: {2} to sender list, range end: {3}", 
                                  this.TaskName, _node.Address, 
                                  con.Address, child_end));
          }
          retval.Add(mr_info);
        }
      }
      
      return retval;
    }
  }
}
