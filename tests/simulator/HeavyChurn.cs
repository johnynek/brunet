/*
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Brunet.Coordinate;
using Brunet.Tunnel;
using Brunet.Util;

namespace Brunet.Simulator {
  /// <summary>Tests a system with heavy churn for a "day".</summary>
  public class HeavyChurnSimulator {
    public static void Main(string []args)
    {
      Simulator sim = new Simulator();
      bool complete;
      Runner.ParseCommandLine(args, out complete, sim);
      DateTime start = DateTime.UtcNow;
      sim.Complete();

      Dictionary<Node, Node> volatile_nodes = new Dictionary<Node, Node>();
      int fifteen_mins = (int) ((new TimeSpan(0, 15, 0)).Ticks / TimeSpan.TicksPerMillisecond);
      int max = sim.StartingNetworkSize * 10;
      Random rand = new Random();
      while(start.AddHours(24) > DateTime.UtcNow) {
        SimpleTimer.RunSteps(fifteen_mins);
        List<Node> to_remove = new List<Node>();
        foreach(Node node in volatile_nodes.Keys) {
          double prob = rand.NextDouble();
          if(prob <= .7) {
            continue;
          }

//          sim.RemoveNode(node, prob > .9);
          sim.RemoveNode(node, true);
          to_remove.Add(node);
        }

        foreach(Node node in to_remove) {
          volatile_nodes.Remove(node);
        }

        Console.WriteLine("Removed: {0} Nodes" , to_remove.Count);
        while(volatile_nodes.Count < max) {
          Node node = sim.AddNode();
          volatile_nodes.Add(node, node);
        }
      }
    }
  }
}

