/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using Brunet.Util;

namespace Brunet.Simulator {
  public class Runner {
    public static void Main(string []args)
    {
      Simulator sim = new Simulator();
      bool complete = false;
      ParseCommandLine(args, out complete, sim);

      if(complete) {
        sim.Complete();
      } else {
        Commands(sim);
      }
    }

    public static void ParseCommandLine(string []args, out bool complete, Simulator sim)
    {
      complete = false;
      string dataset_filename = String.Empty;
      int carg = 0;

      sim.StartingNetworkSize = 10;
      while(carg < args.Length) {
        String[] parts = args[carg++].Split('=');
        try {
          switch(parts[0]) {
            case "--n":
              sim.StartingNetworkSize = Int32.Parse(parts[1]);
              break;
            case "--broken":
              sim.Broken = Double.Parse(parts[1]);
              break;
            case "--complete":
              complete = true;
              break;
            case "--seed":
              sim.Seed = Int32.Parse(parts[1]);
              break;
            case "--se":
              sim.SecureEdges = true;
              sim.SecureStartup();
              break;
            case "--ss":
              sim.SecureSenders = true;
              sim.SecureStartup();
              break;
            case "--dataset":
              dataset_filename = parts[1];
              break;
            case "--help":
            default:
              PrintHelp();
              break;
          }
        }
        catch {
          PrintHelp();
        }
      }

      Console.WriteLine("Initializing...");

      if(dataset_filename != String.Empty) {
        List<List<int>> latency = new List<List<int>>();
        using(StreamReader fs = new StreamReader(new FileStream(dataset_filename, FileMode.Open))) {
          string line = null;
          while((line = fs.ReadLine()) != null) {
            string[] points = line.Split(' ');
            List<int> current = new List<int>(points.Length);
            foreach(string point in points) {
              int val;
              if(!Int32.TryParse(point, out val)) {
                continue;
              }
              current.Add(Int32.Parse(point));
            }
            latency.Add(current);
          }
        }
        if(sim.StartingNetworkSize == 10) {
          sim.StartingNetworkSize = latency.Count;
        }
        SimulationEdgeListener.LatencyMap = latency;
      }

      for(int i = 0; i < sim.StartingNetworkSize; i++) {
        Console.WriteLine("Setting up node: " + i);
        sim.AddNode();
      }

      Console.WriteLine("Initialization complete...\n");
    }

    public static void Commands(Simulator sim)
    {
      string command = String.Empty;
      Console.WriteLine("Type HELP for a list of commands.\n");
      while (command != "Q") {
        bool secure = false;
        Console.Write("#: ");
        // Commands can have parameters separated by spaces
        string[] parts = Console.ReadLine().Split(' ');
        command = parts[0];

        try {
          if(command.Equals("S")) {
            secure = true;
            command = parts[1];
          }

          switch(command) {
            case "C":
              sim.CheckRing(true);
              break;
            case "P":
              sim.PrintConnections();
              break;
            case "M":
              Console.WriteLine("Memory Usage: " + GC.GetTotalMemory(true));
              break;
            case "CR":
              sim.Crawl(true, secure);
              break;
            case "A2A":
              sim.AllToAll(secure);
              break;
            case "A":
              sim.AddNode();
              break;
            case "D":
              sim.RemoveNode(true, true);
              break;
            case "R":
              sim.RemoveNode(true, false);
              break;
            case "RUN":
              int steps = (parts.Length >= 2) ? Int32.Parse(parts[1]) : 0;
              if(steps > 0) {
                SimpleTimer.RunSteps(steps);
              } else {
                SimpleTimer.RunStep();
              }
              break;
            case "Q":
              break;
            case "CONSTATE":
              sim.PrintConnectionState();
              break;
            case "H":
              Console.WriteLine("Commands: \n");
              Console.WriteLine("A - add a node");
              Console.WriteLine("D - remove a node");
              Console.WriteLine("R - abort a node");
              Console.WriteLine("C - check the ring using ConnectionTables");
              Console.WriteLine("P - Print connections for each node to the screen");
              Console.WriteLine("M - Current memory usage according to the garbage collector");
              Console.WriteLine("[S] CR - Perform a (secure) crawl of the network using RPC");
              Console.WriteLine("[S] A2A - Perform all-to-all measurement of the network using RPC");
              Console.WriteLine("Q - Quit");
              break;
            default:
              Console.WriteLine("Invalid command");
              break;
          }
        } catch(Exception e) {
          Console.WriteLine("Error: " + e);
        }
        Console.WriteLine();
      }
    }

    public static void PrintHelp() {
      Console.WriteLine("Usage: SystemTest.exe --option[=value]...\n");
      Console.WriteLine("Options:");
      Console.WriteLine("--n=int - network size");
      Console.WriteLine("--broken - broken system test");
      Console.WriteLine("--complete -- run until fully connected network");
      Console.WriteLine("--help - this menu");
      Console.WriteLine();
      Environment.Exit(0);
    }
  }
}
