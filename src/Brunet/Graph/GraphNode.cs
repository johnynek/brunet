/*
This program is part of Brunet, a library for autonomic overlay networks.
Copyright (C) 2008 David Wolinsky davidiw@ufl.edu, Unversity of Florida

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
