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
      Parameters p = new Parameters("Graph", "Graph - Brunet Network Modeler.");
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

      Console.WriteLine("Creating a graph with base size: {0}, near connections: {1}, shortcuts {2}",
          p.Size, p.Near, p.Shortcuts);
      Graph graph = new Graph(p.Size, p.Near, p.Shortcuts, p.Seed, p.LatencyMap);
      Console.WriteLine("Done populating graph...");
      graph.Crawl();
      graph.AllToAll();
      graph.BroadcastAverage();
      if(p.Outfile != string.Empty) {
        Console.WriteLine("Saving dot file to: " + p.Outfile);
        graph.WriteGraphFile(p.Outfile);
      }
    }
  }
}
