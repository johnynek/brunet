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
using Brunet.Connections;
using Brunet.Services.Coordinate;
using Brunet.Symphony;
using Brunet.Transport;
using Brunet.Tunnel;
using Brunet.Util;

namespace Brunet.Simulator {
  public class TunnelOverlapEvaluator {
    public static void Main(string []args)
    {
      TunnelOverlapSimulator sim = new TunnelOverlapSimulator();
      bool complete;
      sim.NCEnable = true;
      Runner.ParseCommandLine(args, out complete, sim);
      sim.Complete();

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

    public static void Run(Simulator sim, Address addr1, Address addr2)
    {
      Console.WriteLine("Beginning");
      sim.Complete();

      SimpleTimer.RunSteps(1000000, false);
      StructuredNode node1 = (sim.Nodes[addr1] as NodeMapping).Node as StructuredNode;
      StructuredNode node2 = (sim.Nodes[addr2] as NodeMapping).Node as StructuredNode;
      sim.Complete();
      node1.ManagedCO.AddAddress(addr2);

      Connection con1 = node1.ConnectionTable.GetConnection(ConnectionType.Structured, addr2);
      while(con1 == null) {
        SimpleTimer.RunStep();
        con1 = node1.ConnectionTable.GetConnection(ConnectionType.Structured, addr2);
      }

      Hashtable ht = new Hashtable();
      foreach(Connection con in node1.ConnectionTable.GetConnections(Tunnel.OverlapConnectionOverlord.STRUC_OVERLAP)) {
        ht[con.Address] = (con.Edge as SimulationEdge).Delay;
        con.Edge.Close();
      }

      ConnectionList cl2 = node2.ConnectionTable.GetConnections(ConnectionType.Structured);
      foreach(DictionaryEntry de in ht) {
        Address addr = de.Key as Address;
        int delay = (int) de.Value;
        int index = cl2.IndexOf(addr);
        if(index < 0) {
          Console.WriteLine("No matching pair for overlap...");
          continue;
        }
        Connection con = cl2[index];
        delay += (con.Edge as SimulationEdge).Delay;
        Console.WriteLine("Delay: " + delay);
      }
      ht.Clear();

      foreach(Connection con in node2.ConnectionTable.GetConnections(Tunnel.OverlapConnectionOverlord.STRUC_OVERLAP)) {
        ht[con.Address] = (con.Edge as SimulationEdge).Delay;
        con.Edge.Close();
      }

      ConnectionList cl1 = node1.ConnectionTable.GetConnections(ConnectionType.Structured);
      foreach(DictionaryEntry de in ht) {
        Address addr = de.Key as Address;
        int delay = (int) de.Value;
        int index = cl1.IndexOf(addr);
        if(index < 0) {
          Console.WriteLine("No matching pair for overlap...");
          continue;
        }
        Connection con = cl1[index];
        delay += (con.Edge as SimulationEdge).Delay;
        Console.WriteLine("Delay: " + delay);
      }

      node1.Disconnect();
      node2.Disconnect();
      SimpleTimer.RunSteps(100000);
      Console.WriteLine("End");
    }
  }
}
