/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
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

using Brunet.Tunnel;
using Brunet.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

#if NC_NUNIT
using NUnit.Framework;
using System.Security.Cryptography;
#endif

namespace Brunet.Coordinate {
  /// <summary>Implements an ITunnelOverlap that uses NCService to make
  /// intelligent decisions about selecting tunnel overlap.</summary>
  public class NCTunnelOverlap : SimpleTunnelOverlap {
    protected NCService _ncservice;

    public NCTunnelOverlap(NCService ncservice)
    {
      _ncservice = ncservice;
    }

    /// <summary>Returns the 4 oldest connections.</summary>
    protected List<Connection> GetClosest(ConnectionList cons)
    {
      List<Connection> lcons = new List<Connection>(cons.Count);
      foreach(Connection con in cons) {
        lcons.Add(con);
      }

      return GetClosest(lcons);
    }

    /// <summary>Returns at most the 4 closest addresses in order.</summary>
    protected List<Connection> GetClosest(List<Connection> cons)
    {
      List<Connection> closest = new List<Connection>(cons);

      // Since MeasuredLatency could change the duration of our sorting we make
      // a copy as necessary
      Dictionary<Connection, double> latencies = new Dictionary<Connection, double>();

      Comparison<Connection> comparer = delegate(Connection x, Connection y) {
        double lat_x, lat_y;
        if(!latencies.TryGetValue(x, out lat_x)) {
          lat_x = _ncservice.GetMeasuredLatency(x.Address);
          latencies[x] = lat_x;
        }
        if(!latencies.TryGetValue(y, out lat_y)) {
          lat_y = _ncservice.GetMeasuredLatency(y.Address);
          latencies[y] = lat_y;
        }

        // Remember that smaller is better but -1 is bad...
        // If either is smaller than 0 invert the comparison..
        if(lat_x < 0 || lat_y < 0) {
          return lat_y.CompareTo(lat_x);
        } else {
          return lat_x.CompareTo(lat_y);
        }
      };

      closest.Sort(comparer);
      if(closest.Count > 4) {
        closest.RemoveRange(4, closest.Count - 4);
      }
      return closest;
    }

    /// <summary>Always returns the fastest non-tunnel, overlapping address.</summary>
    public override Address EvaluatePotentialOverlap(IDictionary msg)
    {
      Address best_addr = null;
      double best_latency = double.MaxValue;
      Address their_best_addr = null;
      double their_best_latency = double.MaxValue;

      foreach(DictionaryEntry de in msg) {
        MemBlock key = de.Key as MemBlock;
        if(key == null) {
          key = MemBlock.Reference((byte[]) de.Key);
        }
        Address addr = new AHAddress(key);

        IDictionary info = de.Value as IDictionary;
        TransportAddress.TAType tatype =
          TransportAddressFactory.StringToType(info["ta"] as string);

        if(tatype.Equals(TransportAddress.TAType.Tunnel)) {
          continue;
        }

        double latency = _ncservice.GetMeasuredLatency(addr);
        if(latency > 0 && latency < best_latency) {
          best_addr = addr;
          best_latency = latency;
        }

        if(!info.Contains("lat")) {
          continue;
        }

        latency = (double) info["lat"];
        if(latency > 0 && latency < their_best_latency) {
          their_best_addr = addr;
          their_best_latency = latency;
        }
      }

      best_addr = their_best_latency < best_latency ? their_best_addr : best_addr;
      return best_addr == null ? base.EvaluatePotentialOverlap(msg) : best_addr;
    }

    /// <summary>Returns the four fastest in the overlap.</summary>
    public override List<Connection> EvaluateOverlap(ConnectionList cons, IDictionary msg)
    {
      List<Connection> overlap = new List<Connection>();

      foreach(DictionaryEntry de in msg) {
        MemBlock key = de.Key as MemBlock;
        if(key == null) {
          key = MemBlock.Reference((byte[]) de.Key);
        }
        Address addr = new AHAddress(key);

        int index = cons.IndexOf(addr);
        if(index < 0) {
          continue;
        }

        Connection con = cons[index];

        // Since there are no guarantees about routing over two tunnels, we do
        // not consider cases where we are connected to the overlapping tunnels
        // peers via tunnels
        if(con.Edge.TAType.Equals(TransportAddress.TAType.Tunnel)) {
          Hashtable values = de.Value as Hashtable;
          TransportAddress.TAType tatype =
            TransportAddressFactory.StringToType(values["ta"] as string);
          if(tatype.Equals(TransportAddress.TAType.Tunnel)) {
            continue;
          }
        }
        overlap.Add(con);
      }

      return GetClosest(overlap);
    }

    /// <summary>Returns a Tunnel Sync message containing all overlap and then
    /// the four fastest (if not included in the overlap.</summary>
    public override IDictionary GetSyncMessage(IList<Connection> current_overlap,
        Address local_addr, ConnectionList cons)
    {
      Hashtable ht = new Hashtable(40);
      DateTime now = DateTime.UtcNow;

      if(current_overlap != null) {
        foreach(Connection con in current_overlap) {
          Hashtable info = new Hashtable(3);
          info["ta"] = TransportAddress.TATypeToString(con.Edge.TAType);
          info["lat"] = _ncservice.GetMeasuredLatency(con.Address);
          info["ct"] = (int) (now - con.CreationTime).TotalMilliseconds;
          ht[con.Address.ToMemBlock()] = info;
        }
      }

      foreach(Connection con in GetClosest(cons)) {
        MemBlock key = con.Address.ToMemBlock();
        if(ht.Contains(key)) {
          continue;
        }
        // No need to verify it is >= 0, since addr comes from cons in a
        // previous stage
        Hashtable info = new Hashtable(3);
        info["ta"] = TransportAddress.TATypeToString(con.Edge.TAType);
        info["lat"] = _ncservice.GetMeasuredLatency(con.Address);
        info["ct"] = (int) (now - con.CreationTime).TotalMilliseconds;
        ht[key] = info;
      }

      return ht;
    }
  }
#if NC_NUNIT
  [TestFixture]
  public class NCTunnelOverlapTest {
    // This tests some simple cases for the NCTunelOverlap
    [Test]
    public void Test()
    {
      Address addr_x = new AHAddress(new RNGCryptoServiceProvider());
      byte[] addrbuff = Address.ConvertToAddressBuffer(addr_x.ToBigInteger() + (Address.Full / 2));
      Address.SetClass(addrbuff, AHAddress._class);
      Address addr_y = new AHAddress(addrbuff);

      List<Connection> connections = new List<Connection>();
      ConnectionTable ct_x = new ConnectionTable();
      ConnectionTable ct_y = new ConnectionTable();
      ConnectionTable ct_empty = new ConnectionTable();
      NCService ncservice = new NCService();

      Connection fast_con = null;
      for(int i = 1; i <= 11; i++) {
        addrbuff = Address.ConvertToAddressBuffer(addr_x.ToBigInteger() + (i * Address.Full / 16));
        Address.SetClass(addrbuff, AHAddress._class);
        Address addr = new AHAddress(addrbuff);
        Connection con = null;

        TransportAddress ta = TransportAddressFactory.CreateInstance("brunet.tcp://158.7.0.1:5000");
        Edge fe = new FakeEdge(ta, ta, TransportAddress.TAType.Tcp);
        if(i <= 10) {
          con = new Connection(fe, addr, "structured", null, null);
          ct_x.Add(con);
          if(i % 2 == 0) {
            ncservice.ProcessSample(DateTime.UtcNow, String.Empty, addr,
                new Point(new double[] {0, 0}, 0), 0, i*10);
          }
        } else {
          fast_con = new Connection(fe, addr, "structured", null, null);
          ncservice.ProcessSample(DateTime.UtcNow, String.Empty, addr,
              new Point(new double[] {0, 0}, 0), 0, 5);
        }

        if(i == 10) {
          ct_y.Add(con);
        }
        connections.Add(con);
      }

      ITunnelOverlap sto = new SimpleTunnelOverlap();
      ITunnelOverlap nto = new NCTunnelOverlap(ncservice);

      ConnectionType con_type = ConnectionType.Structured;
      List<Connection> pre_cons = new List<Connection>();
      pre_cons.Add(connections[9]);
      IDictionary id = nto.GetSyncMessage(pre_cons, addr_x, ct_x.GetConnections(con_type));

      // We do have some pre-existing overlap
      Assert.AreEqual(nto.EvaluateOverlap(ct_y.GetConnections(con_type), id)[0], connections[9], "NC: Have an overlap!");
      Assert.AreEqual(sto.EvaluateOverlap(ct_y.GetConnections(con_type), id)[0], connections[9], "Simple: Have an overlap!");

      // We have no overlap with an empty connection table
      Assert.AreEqual(nto.EvaluateOverlap(ct_empty.GetConnections(con_type), id).Count, 0, "No overlap!");
      Assert.AreEqual(sto.EvaluateOverlap(ct_empty.GetConnections(con_type), id).Count, 0, "No overlap!");

      // latency[0] == -1
      Assert.AreEqual(connections[1].Address.Equals(nto.EvaluatePotentialOverlap(id)), true,
          "NC: EvaluatePotentialOverlap returns expected!");
      Assert.AreEqual(ct_x.Contains(con_type, sto.EvaluatePotentialOverlap(id)), true,
          "Simple: EvaluatePotentialOverlap returns valid!");

      ct_y.Add(fast_con);
      ct_x.Add(fast_con);
      id = nto.GetSyncMessage(pre_cons, addr_x, ct_x.GetConnections(con_type));
      Assert.AreEqual(fast_con.Address.Equals(nto.EvaluatePotentialOverlap(id)), true,
          "NC: EvaluatePotentialOverlap returns expected!");
      Assert.AreEqual(nto.EvaluateOverlap(ct_y.GetConnections(con_type), id)[0], fast_con, "NC: Have better overlap!");
    }
  }
#endif
}
