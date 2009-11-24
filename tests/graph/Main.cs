/*
This program is part of Brunet, a library for autonomic overlay networks.
Copyright (C) 2009 David Wolinsky davidiw@ufl.edu, Unversity of Florida

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
using System.Collections.Generic;

namespace Brunet.Graph {
  public class Runner {
    public static void Main(string[] args)
    {
      int size = 100;
      int shortcuts = 1;
      int near = 3;
      int seed = (new Random()).Next();
      string outfile = string.Empty;
      string dataset = string.Empty;

      int carg = 0;
      while(carg < args.Length) {
        string[] parts = args[carg++].Split('=');
        try {
          switch(parts[0]) {
            case "--size":
              size = Int32.Parse(parts[1]);
              break;
            case "--shortcuts":
              shortcuts = Int32.Parse(parts[1]);
              break;
            case "--near":
              near = Int32.Parse(parts[1]);
              break;
            case "--seed":
              seed = Int32.Parse(parts[1]);
              break;
            case "--outfile":
              outfile = parts[1];
              break;
            case "--dataset":
              dataset = parts[1];
              break;
            default:
              throw new Exception("Invalid parameter");
          }
        } catch {
          Console.WriteLine("oops...");
        }
      }

      Console.WriteLine("Creating a graph with base size: {0}, near connections: {1}, shortcuts {2}",
          size, near, shortcuts);

      List<List<int>> latency_map = null;
      if(dataset != string.Empty) {
        latency_map = Graph.ReadLatencyDataSet(dataset);
      }

      Graph graph = new Graph(size, near, shortcuts, seed, latency_map);
      Console.WriteLine("Done populating graph...");
      graph.Crawl();
      graph.AllToAll();
      if(outfile != string.Empty) {
        Console.WriteLine("Saving dot file to: " + outfile);
        graph.WriteGraphFile(outfile);
      }
    }
  }
}
