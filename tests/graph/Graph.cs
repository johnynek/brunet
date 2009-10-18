/*
This program is part of Brunet, a library for autonomic overlay networks.
Copyright (C) 2008 David Wolinsky davidiw@ufl.edu, Unversity of Florida

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

using Brunet.Util;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Brunet.Graph {
  ///<summary>Graph provides the ability to test routing algorithms for
  ///ring-based structured p2p networks</summary>
  ///<remarks>Key features provided by Graph include support for Tunnels,
  ///latency, hop count, user specified latency, tweaking of network size,
  ///near neighbor count, shortcut count.</remarks>
  public class Graph {
    protected Dictionary<AHAddress, GraphNode> _addr_to_node;
    protected List<AHAddress> _addrs;
    protected Random _rand;
    protected Dictionary<AHAddress, int>  _addr_to_index;

    public Graph(int count, int near, int shortcuts) :
      this(count, near, shortcuts, (new Random()).Next())
    {
    }

    ///</summary>Creates a new Graph for simulate routing algorithms.</summary>
    ///<param name="count">The network size not including the clusters.</param>
    ///<param name="near">The amount of connections on the left or right of a 
    ///node.</param>
    ///<param name="shortcuts">The amount of far connections had per node.</param>
    ///<param name="latency">(optional)count x count matrix containing the
    ///latency between ///two points.</param>
    ///<param name="cluster_count">A cluster is a 100 node network operating on
    ///a single point in the network.  A cluster cannot communicate directly
    ///with another cluster.</param>
    public Graph(int count, int near, int shortcuts, int random_seed)
    {
      _rand = new Random(random_seed);
      _addr_to_node = new Dictionary<AHAddress, GraphNode>(count);
      _addrs = new List<AHAddress>(count);
      _addr_to_index = new Dictionary<AHAddress, int>(count);

      // first we create our regular network
      while(_addrs.Count < count) {
        byte[] baddr = new byte[Address.MemSize];
        _rand.NextBytes(baddr);
        Address.SetClass(baddr, AHAddress.ClassValue);
        AHAddress addr = new AHAddress(MemBlock.Reference(baddr));
        if(_addr_to_node.ContainsKey(addr)) {
          continue;
        }
        GraphNode node = new GraphNode(addr);
        _addr_to_node[addr] = node;
        _addrs.Add(addr);
      }

      _addrs.Sort();

      for(int i = 0; i < count; i++) {
        _addr_to_index[_addrs[i]] = i;
      }

      for(int i = 0; i < count; i++) {
        GraphNode cnode = _addr_to_node[_addrs[i]];
        ConnectionList cons = cnode.ConnectionTable.GetConnections(ConnectionType.Structured);
        // We select our left and right neighbors up to near out (so we get 2*near connections)
        // Then we check to make sure we don't already have this connection, since the other guy
        // may have added it, if we don't we create one and add it.
        for(int j = 1; j <= near; j++) {
          int left = i - j;
          if(left < 0) {
            left += count;
          }
          GraphNode lnode = _addr_to_node[_addrs[left]];
          if(!cons.Contains(lnode.Address)) {
            int delay = CalculateDelay(cnode, lnode);
            AddConnection(cnode, lnode, delay);
            AddConnection(lnode, cnode, delay);
          }

          int right = i+j;
          if(right >= count) {
            right -= count;
          }
          GraphNode rnode = _addr_to_node[_addrs[right]];
          // No one has this connection, let's add it to both sides.
          if(!cons.Contains(rnode.Address)) {
            int delay = CalculateDelay(cnode, rnode);
            AddConnection(cnode, rnode, delay);
            AddConnection(rnode, cnode, delay);
          }
        }
        
        // Let's add shortcuts so that we have at least the minimum number of shortcuts
        while(cnode.Shortcuts < shortcuts) {
          cons = cnode.ConnectionTable.GetConnections(ConnectionType.Structured);
          AHAddress addr = ComputeShortcutTarget(cnode.Address);
          addr = FindNodeNearestToAddress(addr);
          if(cons.Contains(addr) || addr.Equals(cnode.Address)) {
            continue;
          }
          GraphNode snode = _addr_to_node[addr];
          cons = snode.ConnectionTable.GetConnections(ConnectionType.Structured);
          int delay = CalculateDelay(cnode, snode);
          if(delay == -1) {
            continue;
          }
          AddConnection(cnode, snode, delay);
          AddConnection(snode, cnode, delay);
          cnode.Shortcuts++;
          snode.Shortcuts++;
        }
      }

      foreach(GraphNode gn in _addr_to_node.Values) {
        gn.UpdateSystem();
      }
    }

    ///<summary>Calculates the delay between two nodes.</summary>
    protected virtual int CalculateDelay(GraphNode node1, GraphNode node2)
    {
      return _rand.Next(10, 240);
    }

    ///<summary>Creates an edge and a connection from node2 to node1 including
    ///the edge.  Note:  this is unidirectional, this must be called twice,
    ///swapping node1 with node2 for a connection to be complete.</summary>
    protected void AddConnection(GraphNode node1, GraphNode node2, int delay)
    {
      Edge edge = new GraphEdge(delay);
      Connection con = new Connection(edge, node2.Address, ConnectionType.Structured.ToString(), null, null);
      node1.ConnectionTable.Add(con);
    }

    /// <summary>Calculates a shortcut using a harmonic distribution as in a
    /// Symphony-lke shortcut.</summary>
    protected virtual AHAddress ComputeShortcutTarget(AHAddress addr)
    {
      int network_size = _addrs.Count;
      double logN = (double)(Brunet.Address.MemSize * 8);
      double logk = Math.Log( (double) network_size, 2.0 );
      double p = _rand.NextDouble();
      double ex = logN - (1.0 - p)*logk;
      int ex_i = (int)Math.Floor(ex);
      double ex_f = ex - Math.Floor(ex);
      //Make sure 2^(ex_long+1)  will fit in a long:
      int ex_long = ex_i % 63;
      int ex_big = ex_i - ex_long;
      ulong dist_long = (ulong)Math.Pow(2.0, ex_long + ex_f);
      //This is 2^(ex_big):
      BigInteger big_one = 1;
      BigInteger dist_big = big_one << ex_big;
      BigInteger rand_dist = dist_big * dist_long;

      // Add or subtract random distance to the current address
      BigInteger t_add = addr.ToBigInteger();

      // Random number that is 0 or 1
      if( _rand.Next(2) == 0 ) {
        t_add += rand_dist;
      }
      else {
        t_add -= rand_dist;
      }

      BigInteger target_int = new BigInteger(t_add % Address.Full);
      if((target_int & 1) == 1) {
        target_int -= 1;
      }

      byte[]buf = Address.ConvertToAddressBuffer(target_int);

      Address.SetClass(buf, AHAddress.ClassValue);
      return new AHAddress(buf);
    }

    protected AHAddress FindNodeNearestToAddress(AHAddress addr)
    {
      int index = _addrs.BinarySearch(addr);
      AHAddress to_use = addr;

      if(index < 0) { 
        index = ~index;
        if(index == _addrs.Count) {
          index = 0;
        }
        AHAddress right = _addrs[index];
        if(index == 0) {
          index = _addrs.Count - 1;
        }
        AHAddress left = _addrs[index - 1];
        if(right.DistanceTo(addr) < left.DistanceTo(addr)) {
          to_use = right;
        } else {
          to_use = left;
        }
      }

      return to_use;
    }

    /// <summary>Sends a packet from A to B returning the delay and hop count.</summary>
    public Pair<int, int> SendPacket(AHAddress from, AHAddress to)
    {
      AHHeader ah = new AHHeader(0, 100, from, to, AHHeader.Options.Greedy);

      GraphNode cnode = _addr_to_node[from];
      Edge cedge = null;
      AHAddress last_addr = from;
      Pair<Connection, bool> next = new Pair<Connection, bool>(null, false);

      int delay = 0;
      int hops = 0;

      while(!next.Second) {
        next = cnode.Router.NextConnection(cedge, ah);
        if(next.First == null && !next.Second) {
          break;
        } else if(next.First != null && next.Second) {
          break;
        } else if(next.First != null) {
          AHAddress caddress = next.First.Address as AHAddress;
          cnode = _addr_to_node[caddress];
          cedge = cnode.ConnectionTable.GetConnection(ConnectionType.Structured, last_addr).Edge;
          last_addr = caddress;
          delay += (cedge as GraphEdge).Delay;
          hops++;
        }
      }

      return new Pair<int, int>(delay, hops);
    }

    /// <summary>Crawls the network using a random address in the network.</summary>
    public void Crawl()
    {
      Crawl(_addrs[_rand.Next(0, _addrs.Count)]);
    }

    /// <summary>Crawls the network using the given starting address.</summary>
    public void Crawl(AHAddress start)
    {
      long total_delay = 0;
      List<int> delays = new List<int>(_addrs.Count - 1);
      long total_hops = 0;
      List<int> hops = new List<int>(_addrs.Count - 1);

      int network_size = _addrs.Count;
      int start_pos = _addr_to_index[start];
      int pos = (start_pos + 1) % network_size;

      while(pos != start_pos) {
        AHAddress current = _addrs[pos];
        Pair<int, int> delay_hops = SendPacket(start, current);

        total_delay += delay_hops.First;
        delays.Add(delay_hops.First);
        total_hops += delay_hops.Second;
        hops.Add(delay_hops.Second);

        pos++;
        if(pos >= network_size) {
          pos -= network_size;
        }
      }

      Console.WriteLine("Crawl results:");
      double average = Average(hops);
      Console.WriteLine("\tHops: Total: {0}, Average: {1}, Stdev: {2}",
          total_hops, average, StandardDeviation(hops, average));
      average = Average(delays);
      Console.WriteLine("\tDelay: Total: {0}, Average: {1}, Stdev: {2}",
          total_delay, average, StandardDeviation(delays, average));
    }

    /// <summary>Calculates all to all latency for A -> Band B -> A for all A
    /// and B in the network.
    public void AllToAll()
    {
      int network_size = _addrs.Count;
      int total = (network_size - 1) * (network_size - 1);
      long total_delay = 0;
      List<int> delays = new List<int>(total);
      long total_hops = 0;
      List<int> hops = new List<int>(total);

      for(int i = 0; i < network_size; i++) {
        for(int j = 0; j < network_size; j++) {
          if(i == j) {
            continue;
          }
          AHAddress from = _addrs[i];
          AHAddress to = _addrs[j];
          Pair<int, int> delay_hops = SendPacket(from, to);

          total_delay += delay_hops.First;
          delays.Add(delay_hops.First);
          total_hops += delay_hops.Second;
          hops.Add(delay_hops.Second);
        }
      }

      Console.WriteLine("AllToAll results:");
      double average = Average(hops);
      Console.WriteLine("\tHops: Total: {0}, Average: {1}, Stdev: {2}",
          total_hops, average, StandardDeviation(hops, average));
      average = Average(delays);
      Console.WriteLine("\tDelay: Total: {0}, Average: {1}, Stdev: {2}",
          total_delay, average, StandardDeviation(delays, average));
    }

    /// <summary>Calculates the average of a data set.</summary>
    public static double Average(List<int> data)
    {
      long total = 0;
      foreach(int point in data) {
        total += point;
      }

      return (double) total / data.Count;
    }

    /// <summary>Calculates the standard deviation given a data set and the
    /// average.</summary>
    public static double StandardDeviation(List<int> data, double avg)
    {
      double variance = 0;
      foreach(int point in data) {
        variance += Math.Pow(point  - avg, 2.0);
      }

      return Math.Sqrt(variance / (data.Count - 1));
    }

    ///<summary> Creates a Dot file which can generate an image using either
    ///neato with using the -n parameter or circo.</summary>
    public void WriteGraphFile(string outfile)
    {
      double nodesize = .5;
      int canvassize = _addrs.Count * 25;
      double r = (double) canvassize / 2.0 - 1.0 - 36.0 * nodesize;
      int c = canvassize / 2;
      double phi = Math.PI / (2 * _addrs.Count);

      using(StreamWriter sw = File.CreateText(outfile)) {
        sw.WriteLine("graph brunet {");
        for(int i = 0; i < _addrs.Count; i++) {
          double theta = (4 * i) * phi;
          int x = c + (int)(r * Math.Sin(theta));
          int y = c - (int)(r * Math.Cos(theta));
          sw.WriteLine("  {0} [pos = \"{1}, {2}\", width = \"{3}\", height = \"{3}\"];", i, x, y, nodesize);
        }
        for(int i = 0; i < _addrs.Count; i++) {
          GraphNode node = _addr_to_node[_addrs[i]];
          ConnectionList cl = node.ConnectionTable.GetConnections(ConnectionType.Structured);
          foreach(Connection con in cl) {
            AHAddress caddr = con.Address as AHAddress;
            int caddr_index = _addr_to_index[caddr];
            // we've already visited this connection no need to have it in
            // there twice
            if(caddr_index < i) {
              continue;
            }
            sw.WriteLine("  {0} -- {1};", i, _addr_to_index[caddr]);
          }
        }
        sw.WriteLine("}");
      }
    }

    public static void Main(string[] args)
    {
      int size = 100;
      int shortcuts = 1;
      int near = 3;
      int seed = (new Random()).Next();
      string outfile = string.Empty;

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
            default:
              throw new Exception("Invalid parameter");
          }
        } catch {
          Console.WriteLine("oops...");
        }
      }

      Console.WriteLine("Creating a graph with base size: {0}, near connections: {1}, shortcuts {2}",
          size, near, shortcuts);

      Graph graph = new Graph(size, near, shortcuts, seed);
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
