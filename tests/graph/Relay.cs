/*
This program is part of Brunet, a library for autonomic overlay networks.
Copyright (C) 2010 David Wolinsky davidiw@ufl.edu, Unversity of Florida

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

using NDesk.Options;
using System;
using System.Collections.Generic;

namespace Brunet.Graph {
  public class Relay : Graph {
    public Relay(int count, int near, int shortcuts, int random_seed,
        List<List<int>> dataset) :
      base(count, near, shortcuts, random_seed, dataset)
    {
    }

    public void CalculateTwoHopDelays()
    {
      int network_size = _addrs.Count;
      int total = (network_size - 1) * (network_size - 1);
      List<int> delays = new List<int>(total);
      List<int> direct_delays = new List<int>(total);
      List<int> overlay_delays = new List<int>(total);
      int ugh = 0;

      foreach(GraphNode src in _addr_to_node.Values) {
        foreach(GraphNode dst in _addr_to_node.Values) {
          if(src == dst) {
            continue;
          }

          if(src.ConnectionTable.IndexOf(ConnectionType.Structured, dst.Address) >= 0) {
            continue;
          }

          int direct_delay = CalculateDelay(src, dst);
          if(direct_delay == 0 || direct_delay > 500) {
            continue;
          }

          int delay = System.Math.Min(LowestDelay(src, dst), LowestDelay(dst, src));
          if(delay < direct_delay) {
            ugh++;
            continue;
          }
          delays.Add(delay);

          var result = SendPacket(src.Address, dst.Address);
          if(result.Count == 0) {
            throw new Exception("SendPacket failed!");
          }
          overlay_delays.Add(result[0].Delay);
          direct_delays.Add(direct_delay);
        }
      }

      Console.WriteLine(ugh + " " + direct_delays.Count);
      Console.WriteLine("TwoHops results:");
      double average = Average(direct_delays);
      Console.WriteLine("\tDirect Delay: Average: {0}, Stdev: {1}", average,
          StandardDeviation(direct_delays, average));
      average = Average(delays);
      Console.WriteLine("\tTwoHops Delay: Average: {0}, Stdev: {1}", average,
          StandardDeviation(delays, average));
      average = Average(overlay_delays);
      Console.WriteLine("\tOverlay Delay: Average: {0}, Stdev: {1}", average,
          StandardDeviation(overlay_delays, average));
    }

    protected int LowestDelay(GraphNode src, GraphNode dst)
    {
      ConnectionList cl = src.ConnectionTable.GetConnections(ConnectionType.Structured);
      Connection fastest_con = cl[0];
      int lowest_delay = Int32.MaxValue;
      int second_half = Int32.MaxValue;

      foreach(Connection con in cl) {
        GraphEdge edge = con.Edge as GraphEdge;
        int delay = edge.Delay;
        if(delay == 0) {
          continue;
        }

        GraphNode mid = _addr_to_node[fastest_con.Address as AHAddress];
        int delay2 = CalculateDelay(mid, dst);
        if(second_half == 0) {
          continue;
        }

        if(delay < lowest_delay) {
          fastest_con = con;
          lowest_delay = delay;
          second_half = delay2;
        }
      }

      return lowest_delay + second_half;
    }

    public static void Main(string[] args)
    {
      Parameters p = new Parameters("Relay", "Relay - Brunet Network Modeler for Relay.");
      p.Parse(args);

      if(p.Help) {
        p.ShowHelp();
        return;
      }
      if(p.ErrorMessage != string.Empty) {
        Console.WriteLine(p.ErrorMessage);
        p.ShowHelp();
        return;
      }

      Console.WriteLine("Creating a graph with base size: {0}, near " + 
          "connections: {1}, shortcuts {2}", p.Size, p.Near, p.Shortcuts);

      Relay graph = new Relay(p.Size, p.Near, p.Shortcuts, p.Seed, p.LatencyMap);
      Console.WriteLine("Done populating graph...");
      graph.CalculateTwoHopDelays();

      if(p.Outfile != string.Empty) {
        Console.WriteLine("Saving dot file to: " + p.Outfile);
        graph.WriteGraphFile(p.Outfile);
      }
    }
  }
}
