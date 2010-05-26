/*
This program is part of Brunet, a library for autonomic overlay networks.
Copyright (C) 2009 David Wolinsky davidiw@ufl.edu, Unversity of Florida

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
