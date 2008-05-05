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
using System.Collections;
//using System.Collections.Specialized;

/** Base map-reduce tasks. */
namespace Brunet {
  /** 
   * The following class provides a base class for tasks utilizing a greedy tree
   *  for computation. 
   */
  public abstract class MapReduceGreedy: MapReduceTask {
    public MapReduceGreedy(Node n):base(n) {}
    public override MapReduceInfo[] GenerateTree(MapReduceArgs mr_args) {
      object gen_arg = mr_args.GenArg;
      Log("{0}: {1}, greedy generator called, arg: {2}.", 
          this.TaskName, _node.Address, gen_arg);
      string address = gen_arg as string;
      AHAddress a =  (AHAddress) AddressParser.Parse(address);
      ArrayList retval = new ArrayList();
      ConnectionTable tab = _node.ConnectionTable;
      ConnectionList structs = tab.GetConnections(ConnectionType.Structured);
      Connection next_closest = structs.GetNearestTo((AHAddress) _node.Address, a);
      if (next_closest != null) {
        MapReduceInfo mr_info = new MapReduceInfo( (ISender) next_closest.Edge,
                                                   mr_args); //arguments do not change at all
        retval.Add(mr_info);
      }
      
      Log("{0}: {1}, greedy generator returning: {2} senders.", 
          this.TaskName, _node.Address, retval.Count);
      return (MapReduceInfo[]) retval.ToArray(typeof(MapReduceInfo));
    }
  }
  
  /** The following class provides the base class for tasks utilizing the
   *  BoundedBroadcastTree generation.
   */
  public abstract class MapReduceBoundedBroadcast: MapReduceTask 
  {
    public MapReduceBoundedBroadcast(Node n):base(n) {}
    /**
     * Generates tree for bounded broadcast. Algorithm works as follows:
     * The goal is to broadcast to all nodes in range [local_address, end).
     * Given a range [local_address, b), determine all connections that belong to this range.
     * Let the connections be b_1, b_2, ..... b_n.
     * To connection bi assign the range [b_i, b_{i+1}).
     * To the connection bn assign range [b_n, end).]
     */
    public override MapReduceInfo[] GenerateTree(MapReduceArgs mr_args) 
    {
      Log("generating child tree.");      
      object gen_arg = mr_args.GenArg;
      string end_range = gen_arg as string;
      AHAddress end_addr = (AHAddress) AddressParser.Parse(end_range);
      AHAddress start_addr = _node.Address as AHAddress;
      //we are at the start node, here we go:
      ConnectionTable tab = _node.ConnectionTable;
      ConnectionList structs = tab.GetConnections(ConnectionType.Structured);
      ArrayList retval = new ArrayList();

      if (structs.Count > 0) {
        Connection curr_con = structs.GetLeftNeighborOf(_node.Address);
        int curr_idx = structs.IndexOf(curr_con.Address);
        //keep going until we leave the range
        int count = 0;
        ArrayList con_list = new ArrayList();
        while (count++ < structs.Count && ((AHAddress) curr_con.Address).IsBetweenFromLeft(start_addr, end_addr)) {
          con_list.Add(curr_con);
          //Log("adding connection: {0} to list.", curr_con.Address);
          curr_idx  = (curr_idx + 1)%structs.Count;
          curr_con = structs[curr_idx];
        }
        
        Log("{0}: {1}, number of child connections: {2}", 
            this.TaskName, _node.Address, con_list.Count);
        for (int i = 0; i < con_list.Count; i++) {
          MapReduceInfo mr_info = null;
          ISender sender = null;
          Connection con = (Connection) con_list[i];
          sender = (ISender) con.Edge;
          //check if last connection
          if (i == con_list.Count - 1) {
            mr_info = new MapReduceInfo( (ISender) sender, 
                                         new MapReduceArgs(this.TaskName, 
                                                           mr_args.MapArg, //map argument
                                                           end_range, //generate argument
                                                           mr_args.ReduceArg //reduce argument
                                                           ));
            
            Log("{0}: {1}, adding address: {2} to sender list, range end: {3}", 
                this.TaskName, _node.Address, 
                con.Address, end_range);
            retval.Add(mr_info);
          }
          else {
            string child_end = ((Connection) con_list[i+1]).Address.ToString();
            mr_info = new MapReduceInfo( sender,
                                         new MapReduceArgs(this.TaskName,
                                                           mr_args.MapArg, 
                                                           child_end,
                                                           mr_args.ReduceArg));
            Log("{0}: {1}, adding address: {2} to sender list, range end: {3}", 
                this.TaskName, _node.Address, 
                con.Address, child_end);
            retval.Add(mr_info);
          }
        }
      }
      return (MapReduceInfo[]) retval.ToArray(typeof(MapReduceInfo));
    }
  }   
}
