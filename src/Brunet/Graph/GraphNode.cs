/*
This program is part of Brunet, a library for autonomic overlay networks.
Copyright (C) 2008 David Wolinsky davidiw@ufl.edu, Unversity of Florida

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

using Brunet.Collections;
using Brunet.Connections;
using Brunet.Symphony;
using Brunet.Transport;
using Brunet.Util;
using System;
using System.Collections.Generic;

namespace Brunet.Graph {
  public class GraphNode {
    public readonly ConnectionTable ConnectionTable;
    public readonly AHAddress Address;
    public int Shortcuts;
    protected AHHandler.AHState _ahstate;

    protected static Random _rand = new Random();
    protected static Dictionary<int, int> _unique_allocations = new Dictionary<int, int>();

    // UniqueIDs are like hashcodes, but we can use a ranom generator with
    // a preconfigured seed to generate them, so we can repeat experiments.
    // The UniqueID is useful when working with Latency datasets that are
    // smaller than the graph size.
    public readonly int UniqueID;

    public static void SetSeed(int seed)
    {
      _rand = new Random(seed);
    }

    public static void ClearAllocations()
    {
      _unique_allocations = new Dictionary<int, int>();
    }

    public GraphNode(AHAddress addr)
    {
      Shortcuts = 0;
      Address = addr;
      ConnectionTable = new ConnectionTable(addr);
      UpdateSystem();

      do {
        UniqueID = _rand.Next();
      } while(_unique_allocations.ContainsKey(UniqueID));
      _unique_allocations[UniqueID] = UniqueID;
    }

    public Pair<Connection, bool> NextConnection(Edge edge, AHHeader header)
    {
      Pair<Connection, bool> result = null;

      //Check to see if we can use a Leaf connection:
      int dest_idx = _ahstate.Leafs.IndexOf(header.Destination);
      if( dest_idx >= 0 ) {
        result = new Pair<Connection, bool>(_ahstate.Leafs[dest_idx], false);
      } else {
        var alg = _ahstate.GetRoutingAlgo(header);
        result = alg.NextConnection(edge, header);
      }

      return result;
    }

    public void UpdateSystem()
    {
      var stcons = ConnectionTable.GetConnections(ConnectionType.Structured);
      var lfcons = ConnectionTable.GetConnections(ConnectionType.Leaf);
      _ahstate = new AHHandler.AHState(Address, stcons, lfcons);
    }
  }
}
