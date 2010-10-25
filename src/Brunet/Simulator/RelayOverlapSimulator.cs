/*
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Security.Cryptography;
using Brunet.Connections;
using Brunet.Services.Coordinate;
using Brunet.Symphony;
using Brunet.Relay;
using Brunet.Transport;
using Brunet.Util;

namespace Brunet.Simulator {
  public class RelayOverlapSimulator : Simulator {
    public RelayOverlapSimulator(Parameters p) : base(p)
    {
    }

    public void CloseOverlap(Node node)
    {
      foreach(Connection con in node.ConnectionTable.GetConnections(Relay.OverlapConnectionOverlord.STRUC_OVERLAP)) {
        Console.WriteLine("Closing: " + con);
        con.State.Edge.Close();
      }
    }

    public void FindOverlap(Node node1, Node node2)
    {
      FindOverlapWorker(node1, node2);
      FindOverlapWorker(node2, node1);
    }

    protected void FindOverlapWorker(Node node1, Node node2)
    {
      ConnectionList cl = node1.ConnectionTable.GetConnections(ConnectionType.Structured);
      IEnumerable ov = node1.ConnectionTable.GetConnections(Relay.OverlapConnectionOverlord.STRUC_OVERLAP);
      foreach(Connection ov_con in ov) {

        int index = cl.IndexOf(ov_con.Address);
        if(index < 0) {
          Console.WriteLine("No matching pair for overlap...");
          continue;
        }
        Connection con = cl[index];
        int delay = (ov_con.State.Edge as SimulationEdge).Delay + (con.State.Edge as SimulationEdge).Delay;
        Console.WriteLine("Delay: " + delay);
      }
    }

    // adds a disconnected pair to the pool
    public void AddDisconnectedPair(out Address address1, out Address address2, bool nctunnel)
    {
      address1 = new AHAddress(new RNGCryptoServiceProvider());
      byte[] addrbuff = Address.ConvertToAddressBuffer(address1.ToBigInteger() + (Address.Full / 2));
      Address.SetClass(addrbuff, AHAddress._class);
      address2 = new AHAddress(addrbuff);

      AddDisconnectedPair(address1, address2, nctunnel);
    }

    public void AddDisconnectedPair(Address address1, Address address2, bool nctunnel)
    {
      NodeMapping nm1 = new NodeMapping();
      nm1.ID = TakeID();
      TakenIDs[nm1.ID] = nm1;
      NodeMapping nm2 = new NodeMapping();
      nm2.ID = TakeID();
      TakenIDs[nm2.ID] = nm2;

      AddBrokenNode(ref nm1, address1, nm2.ID, nctunnel);
      Nodes[address1] = nm1;

      AddBrokenNode(ref nm2, address2, nm1.ID, nctunnel);
      Nodes[address2] = nm2;
    }

    protected void AddBrokenNode(ref NodeMapping nm, Address addr, int broken_port, bool nctunnel)
    {
      nm.Node = new StructuredNode(addr as AHAddress, BrunetNamespace);

      TAAuthorizer auth = new IDTAAuthorizer(broken_port);
      nm.Node.AddEdgeListener(new SimulationEdgeListener(nm.ID, 0, auth, true));

      IRelayOverlap ito = null;
      if(NCEnable) {
        nm.NCService = new NCService(nm.Node, new Point());
// Until we figure out what's going on with VivaldiTargetSelector its not quite useful for these purposes
//        (nm.Node as StructuredNode).Sco.TargetSelector = new VivaldiTargetSelector(nm.Node, ncservice);
      }
      if(nctunnel && NCEnable) {
        ito = new NCRelayOverlap(nm.NCService);
      } else {
        ito = new SimpleRelayOverlap();
      }

      nm.Node.AddEdgeListener(new Relay.RelayEdgeListener(nm.Node, ito));
      nm.Node.RemoteTAs = GetRemoteTAs();
      nm.Node.Connect();
      CurrentNetworkSize++;
    }

    // Static Members

    public static void Simulator(RelayOverlapSimulator sim)
    {
      Address addr1 = null, addr2 = null;
      sim.AddDisconnectedPair(out addr1, out addr2, sim.NCEnable);
      sim.Complete(false);
      SimpleTimer.RunSteps(1000000, false);

      StructuredNode node1 = (sim.Nodes[addr1] as NodeMapping).Node as StructuredNode;
      StructuredNode node2 = (sim.Nodes[addr2] as NodeMapping).Node as StructuredNode;

      ManagedConnectionOverlord mco = new ManagedConnectionOverlord(node1);
      mco.Start();
      node1.AddConnectionOverlord(mco);
      mco.Set(addr2);
      SimpleTimer.RunSteps(100000, false);

      Console.WriteLine(addr1 + "<=>" + addr2 + ":");
      Console.WriteLine("\t" + node1.ConnectionTable.GetConnection(ConnectionType.Structured, addr2) + "\n");
      sim.PrintConnections(node1);
      Console.WriteLine();
      sim.PrintConnections(node2);

      Console.WriteLine("\nPhase 2 -- Disconnect...");
      sim.FindOverlap(node1, node2);
      sim.CloseOverlap(node1);
      sim.CloseOverlap(node2);

      SimpleTimer.RunSteps(100000, false);

      Console.WriteLine(addr1 + "<=>" + addr2 + ":");
      Console.WriteLine("\t" + node1.ConnectionTable.GetConnection(ConnectionType.Structured, addr2) + "\n");
      sim.PrintConnections(node1);
      Console.WriteLine();
      sim.PrintConnections(node2);

      sim.Disconnect();
    }

    public static void Evaluator(RelayOverlapSimulator sim)
    {
      Address addr1 = null, addr2 = null;
      sim.AddDisconnectedPair(out addr1, out addr2, true);
      Run(sim, addr1, addr2);
      sim.AddDisconnectedPair(addr1, addr2, false);
      Run(sim, addr1, addr2);
      sim.AddDisconnectedPair(addr1, addr2, false);
      Run(sim, addr1, addr2);
      sim.AddDisconnectedPair(addr1, addr2, false);
      Run(sim, addr1, addr2);
      sim.AddDisconnectedPair(addr1, addr2, false);
      Run(sim, addr1, addr2);
      Console.WriteLine("NC Tests");
      sim.AddDisconnectedPair(addr1, addr2, true);
      Run(sim, addr1, addr2);
      sim.AddDisconnectedPair(addr1, addr2, true);
      Run(sim, addr1, addr2);
      sim.AddDisconnectedPair(addr1, addr2, true);
      Run(sim, addr1, addr2);
      sim.AddDisconnectedPair(addr1, addr2, true);
      Run(sim, addr1, addr2);
      sim.AddDisconnectedPair(addr1, addr2, true);
      Run(sim, addr1, addr2);
    }

    public static void Run(RelayOverlapSimulator sim, Address addr1, Address addr2)
    {
      Console.WriteLine("Beginning");
      sim.Complete(false);

      SimpleTimer.RunSteps(1000000, false);
      StructuredNode node1 = (sim.Nodes[addr1] as NodeMapping).Node as StructuredNode;
      StructuredNode node2 = (sim.Nodes[addr2] as NodeMapping).Node as StructuredNode;
      sim.Complete(true);

      ManagedConnectionOverlord mco = new ManagedConnectionOverlord(node1);
      mco.Start();
      node1.AddConnectionOverlord(mco);
      mco.Set(addr2);

      Connection con1 = node1.ConnectionTable.GetConnection(ConnectionType.Structured, addr2);
      while(con1 == null) {
        SimpleTimer.RunStep();
        con1 = node1.ConnectionTable.GetConnection(ConnectionType.Structured, addr2);
      }

      Console.WriteLine(addr1 + "<=>" + addr2 + ":");
      Console.WriteLine("\t" + node1.ConnectionTable.GetConnection(ConnectionType.Structured, addr2) + "\n");
      sim.FindOverlap(node1, node2);
      node1.Disconnect();
      node2.Disconnect();
      SimpleTimer.RunSteps(100000);
      Console.WriteLine("End");
    }

    public static int Main(string []args)
    {
      Parameters p = new Parameters("RelayOverlapSimulator", "Brunet Time Based Simulator for Relays");
      if(p.Parse(args) != 0) {
        Console.WriteLine(p.ErrorMessage);
        p.ShowHelp();
        return -1;
      } else if(p.Help) {
        p.ShowHelp();
        return -1;
      }

      RelayOverlapSimulator sim = new RelayOverlapSimulator(p);
      sim.Complete(false);
      if(p.Evaluation) {
        Evaluator(sim);
      } else {
        Simulator(sim);
      }

      return 0;
    }
  }
}
