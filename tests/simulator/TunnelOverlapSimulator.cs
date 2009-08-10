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
using System.Security.Cryptography;
using Brunet.Util;

namespace Brunet.Simulator {
  public class TunnelOverlapSimulator : Simulator {
    public static void Main(string []args)
    {
      TunnelOverlapSimulator sim = new TunnelOverlapSimulator();
      string dataset_filename = String.Empty;
      int carg = 0;

      while(carg < args.Length) {
        String[] parts = args[carg++].Split('=');
        try {
          switch(parts[0]) {
            case "--n":
              sim.StartingNetworkSize = Int32.Parse(parts[1]);
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
        sim.StartingNetworkSize = latency.Count;
        SimulationEdgeListener.LatencyMap = latency;
      }

      for(int i = 0; i < sim.StartingNetworkSize; i++) {
        Console.WriteLine("Setting up node: " + i);
        sim.AddNode(false);
      }

      Console.WriteLine("Done setting up...\n");

      Complete(sim);
      Address addr1 = null, addr2 = null;
      sim.AddDisconnectedPair(out addr1, out addr2);
      Complete(sim);
      StructuredNode node = (sim.Nodes[addr1] as NodeMapping).Node as StructuredNode;
      StructuredNode node2 = (sim.Nodes[addr2] as NodeMapping).Node as StructuredNode;
      node.ManagedCO.AddAddress(addr2);
      int count = 0;
      while(count++ < 100000) {
        SimpleTimer.RunStep();
        if(node.ConnectionTable.Contains(ConnectionType.Structured, addr2)) {
          break;
        }
      }

      Console.WriteLine(addr1 + "<=>" + addr2 + ":");
      Console.WriteLine("\t" + node.ConnectionTable.GetConnection(ConnectionType.Structured, addr2) + "\n");
      sim.PrintConnections(node);
      Console.WriteLine();
      sim.PrintConnections(node2);

      Console.WriteLine("\nPhase 2 -- Disconnect...");
      foreach(Connection con in node.ConnectionTable.GetConnections(Tunnel.OverlapConnectionOverlord.STRUC_OVERLAP)) {
        Console.WriteLine("Closing: " + con);
        con.Edge.Close();
      }

      foreach(Connection con in node2.ConnectionTable.GetConnections(Tunnel.OverlapConnectionOverlord.STRUC_OVERLAP)) {
        Console.WriteLine("Closing: " + con);
        con.Edge.Close();
      }

      SimpleTimer.RunSteps(100000);
      count = 0;
      while(count++ < 100000) {
        SimpleTimer.RunStep();
        if(node.ConnectionTable.Contains(ConnectionType.Structured, addr2)) {
          break;
        }
      }

      Console.WriteLine(addr1 + "<=>" + addr2 + ":");
      Console.WriteLine("\t" + node.ConnectionTable.GetConnection(ConnectionType.Structured, addr2) + "\n");
      sim.PrintConnections(node);
      Console.WriteLine();
      sim.PrintConnections(node2);

      sim.Disconnect();
    }

    protected static void Complete(Simulator sim)
    {
      DateTime start = DateTime.UtcNow;
      while(!sim.Crawl(false, sim.SecureSenders)) {
        SimpleTimer.RunStep();
      }
      Console.WriteLine("It took {0} to complete the ring", DateTime.UtcNow - start);
    }

    protected static void Commands(Simulator sim)
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
          switch(command) {
            case "C":
              sim.CheckRing();
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
            case "SCR":
              secure = true;
              goto case "CR";
            case "A2A":
              sim.AllToAll(secure);
              break;
            case "SA2A":
              secure = true;
              goto case "A2A";
            case "A":
              sim.AddNode(true);
              break;
            case "D":
              sim.RemoveNode(true, false);
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
            case "H":
              Console.WriteLine("Commands: \n");
              Console.WriteLine("A - add a node");
              Console.WriteLine("R - remove a node");
              Console.WriteLine("C - check the ring using ConnectionTables");
              Console.WriteLine("P - Print connections for each node to the screen");
              Console.WriteLine("M - Current memory usage according to the garbage collector");
              Console.WriteLine("G - Retrieve total dht entries");
              Console.WriteLine("CR - Perform a crawl of the network using RPC");
              Console.WriteLine("ST - Speed test, parameter - integer - times to end data");
              Console.WriteLine("CM - ManagedCO test");
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
      Console.WriteLine("--help - this menu");
      Console.WriteLine();
      Environment.Exit(0);
    }

    // adds a disconnected pair to the pool
    public void AddDisconnectedPair(out Address address1, out Address address2)
    {
      address1 = new AHAddress(new RNGCryptoServiceProvider());
      byte[] addrbuff = Address.ConvertToAddressBuffer(address1.ToBigInteger() + (Address.Full / 2));
      Address.SetClass(addrbuff, AHAddress._class);
      address2 = new AHAddress(addrbuff);


      NodeMapping nm1 = new NodeMapping();
      nm1.Port = TakePort();
      TakenPorts[nm1.Port] = nm1.Port;
      NodeMapping nm2 = new NodeMapping();
      nm2.Port = TakePort();
      TakenPorts[nm2.Port] = nm2.Port;

      nm1.Node = AddBrokenNode(address1, nm1.Port, nm2.Port);
      Nodes[address1] = nm1;

      nm2.Node = AddBrokenNode(address2, nm2.Port, nm1.Port);
      Nodes[address2] = nm2;
    }

    protected Node AddBrokenNode(Address addr, int port, int broken_port)
    {
      Node node = new StructuredNode(addr as AHAddress, BrunetNamespace);

      TAAuthorizer auth = new PortTAAuthorizer(broken_port);
      node.AddEdgeListener(new SimulationEdgeListener(port, 0, auth, true));
      node.AddEdgeListener(new Tunnel.TunnelEdgeListener(node));

      ArrayList RemoteTAs = new ArrayList();
      for(int i = 0; i < 5 && i < TakenPorts.Count; i++) {
        int rport = (int) TakenPorts.GetByIndex(_rand.Next(0, TakenPorts.Count));
        RemoteTAs.Add(TransportAddressFactory.CreateInstance("brunet.function://127.0.0.1:" + rport));
      }
      node.RemoteTAs = RemoteTAs;

      node.Connect();
      return node;
    }
  }
}
