/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Brunet.Security.PeerSec.Symphony;
using Brunet.Simulator.Tasks;
using Brunet.Transport;
using Brunet.Util;

namespace Brunet.Simulator {
  public class Runner {
    public static int Main(string []args)
    {
#if SUBRING
      SubringParameters p = new SubringParameters();
#else
      Parameters p = new Parameters("Simulator", "Simulator - Brunet Time Based Simulator");
#endif
      if(p.Parse(args) != 0) {
        Console.WriteLine(p.ErrorMessage);
        p.ShowHelp();
        return -1;
      } else if(p.Help) {
        p.ShowHelp();
        return -1;
      }

#if SUBRING
      SubringSimulator sim = new SubringSimulator(p);
#else
      Simulator sim = new Simulator(p);
#endif

      if(p.Complete) {
        sim.Complete(false);
      } else if(p.Broadcast > -2) {
        Broadcast(sim, p.Broadcast, p.Output);
      } else if(p.HeavyChurn > 0) {
        HeavyChurn(sim, p.HeavyChurn);
      } else if(p.Evaluation) {
        Evaluate(sim, p);
      } else {
        Commands(sim);
      }
      return 0;
    }

    protected static void Broadcast(Simulator sim, int forwarders, string filename)
    {
      DateTime now = DateTime.UtcNow;
      SimpleTimer.RunSteps(360000, false);
      sim.Complete(true);
      SimpleTimer.RunSteps(3600000, false);
      sim.Complete(true);
      Console.WriteLine("Time spent setting up: " + (DateTime.UtcNow - now).ToString());
      for(int i = 0; i < sim.Nodes.Count; i++) {
        Broadcast bcast = new Broadcast(sim.SimBroadcastHandler,
            sim.Nodes.Values[i].Node, forwarders, TaskFinished);
        bcast.Start();
        RunUntilTaskFinished();
        bcast.WriteResultsToDisk(filename);
      }
    }

    protected static void Evaluate(Simulator sim, Parameters p)
    {
//      DateTime now = DateTime.UtcNow;
      SimpleTimer.RunSteps(360000, false);
      sim.Complete(true);
      SimpleTimer.RunSteps(3600000, false);
      sim.Complete(true);
      sim.AddNode();
      sim.Complete(false);
    }

    public static void HeavyChurn(Simulator sim, int time)
    {
      sim.Complete(false);
      Dictionary<Node, Node> volatile_nodes = new Dictionary<Node, Node>();
      int fifteen_mins = (int) ((new TimeSpan(0, 15, 0)).Ticks / TimeSpan.TicksPerMillisecond);

      int max = sim.StartingNetworkSize * 2;
      Random rand = new Random();
      DateTime end = DateTime.UtcNow.AddSeconds(time);
      while(end > DateTime.UtcNow) {
        SimpleTimer.RunSteps(fifteen_mins);
        List<Node> to_remove = new List<Node>();
        foreach(Node node in volatile_nodes.Keys) {
          double prob = rand.NextDouble();
          if(prob <= .7) {
            continue;
          }

// This is due to bugs in Abort don't handle closing of ELs and maybe other stuff
//          sim.RemoveNode(node, prob > .9);
          sim.RemoveNode(node, true, false);
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

    public static void Commands(Simulator sim)
    {
      string command = String.Empty;
      Console.WriteLine("Type HELP for a list of commands.\n");
      while (command != "Q") {
        bool secure = false;
        Console.Write("#: ");
        // Commands can have parameters separated by spaces
        string[] parts = Console.ReadLine().Split(' ');
        command = parts[0].ToUpper();

        try {
          if(command.Equals("S")) {
            secure = true;
            command = parts[1].ToUpper();;
          }

          switch(command) {
            case "B":
              int forwarders = (parts.Length >= 2) ? Int32.Parse(parts[1]) : -1;
              Broadcast bcast = new Broadcast(sim.SimBroadcastHandler,
                  sim.RandomNode().Node, forwarders, TaskFinished);
              bcast.Start();
              RunUntilTaskFinished();
              break;
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
              NodeMapping nm = sim.Nodes.Values[0];
              SymphonySecurityOverlord bso = null;
              if(secure) {
                bso = nm.Sso;
              }
              Crawl c = new Crawl(nm.Node, sim.Nodes.Count, bso, TaskFinished);
              c.Start();
              RunUntilTaskFinished();
              break;
            case "A2A":
              AllToAll atoa = new AllToAll(sim.Nodes, secure, TaskFinished);
              atoa.Start();
              RunUntilTaskFinished();
              break;
            case "A":
              sim.AddNode();
              break;
            case "D":
              sim.RemoveNode(true, true);
              break;
            case "R":
              sim.RemoveNode(false, true);
              break;
            case "REVOKE":
              sim.Revoke(true);
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

    protected static bool _finished;
    protected static void RunUntilTaskFinished()
    {
      _finished = false;
      while(!_finished) {
        SimpleTimer.RunStep();
      }
    }

    protected static void TaskFinished(object sender, EventArgs ea)
    {
      _finished = true;
      Console.WriteLine(sender);
    }
  }
}
