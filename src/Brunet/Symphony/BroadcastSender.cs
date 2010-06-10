/*
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet.Connections;
using Brunet.Messaging;
using Brunet.Transport;
using Brunet.Util;

using System;
using System.Collections.Generic;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Symphony {
  /// <summary>Broadcasts data to a region of or the entire overlay.  The
  /// initial sender will probably use the single parameter constructor,
  /// and each forwarding node will recursively send it using the multiple
  /// parameter constructor.</summary>
  public class BroadcastSender : ISender {
    /// <summary>All broadcast senders are embedded with this PType.</summary>
    public static readonly PType PType = new PType("broadcast");
    /// <summary>The local node.</summary>
    public readonly StructuredNode Node;
    /// <summary>The beginning (inclusive) of the range.</summary>
    public readonly AHAddress From;
    /// <summary>The originator of the broadcast.</summary>
    public readonly AHAddress Source;
    /// <summary>The ending (inclusive) of the range.</summary>
    public readonly AHAddress To;
    public readonly int Hops;
    protected readonly MemBlock _hops;
    protected readonly bool _wrap;

    /// <summary>The count of nodes this node sent to in the last broadcast.</summary>
    public int SentTo { get { return _sent_to; } }
    protected int _sent_to;

    public const int DEFAULT_FORWARDERS = -1;
    public static readonly MemBlock DEFAULT_FORWARDERS_MB;
    public readonly int Forwarders;
    protected readonly MemBlock _forwarders;
    protected readonly IComparer<Connection> _distance_sorter;
    protected readonly IComparer<Connection> _address_sorter;

    static BroadcastSender()
    {
      byte[] def = new byte[4];
      NumberSerializer.WriteInt(DEFAULT_FORWARDERS, def, 0);
      DEFAULT_FORWARDERS_MB = MemBlock.Reference(def);
    }

    /// <summary>Broadcasts to the entire overlay.</summary>
    public BroadcastSender(StructuredNode node) : this(node, DEFAULT_FORWARDERS)
    {
    }

    public BroadcastSender(StructuredNode node, int forwarders) :
      this(node, node.Address as AHAddress, forwarders)
    {
    }

    protected BroadcastSender(StructuredNode node, AHAddress source, int forwarders) :
      this(node, source, GetRightNearTarget(source), GetLeftNearTarget(source),
          forwarders, 0)
    {
    }

    /// <summary>Continues a broadcast to the overlay.</summary>
    public BroadcastSender(StructuredNode node, AHAddress source,
        AHAddress from, AHAddress to, int forwarders, int hops)
    {
      Node = node;
      Source = source;
      From = from;
      To = to;
      _distance_sorter = new AbsoluteDistanceComparer(node.Address as AHAddress);
      _address_sorter = new LeftDistanceComparer(node.Address as AHAddress);
      Forwarders = forwarders;
      Hops = hops;

      byte[] hops_data = new byte[4];
      NumberSerializer.WriteInt(hops, hops_data, 0);
      _hops = MemBlock.Reference(hops_data);

      if(forwarders == DEFAULT_FORWARDERS) {
        _forwarders =DEFAULT_FORWARDERS_MB;
      } else {
        byte[] def = new byte[4];
        NumberSerializer.WriteInt(forwarders, def, 0);
        _forwarders = MemBlock.Reference(def);
      }
    }

    /// <summary>Creates a new BroadcastSender based upon a packet sent from
    /// one.  This makes the recursion step easier.</summary>
    public static BroadcastSender Parse(StructuredNode node, MemBlock packet,
        out MemBlock data)
    {
      int pos = 0;
      AHAddress source = new AHAddress(packet.Slice(pos, Address.MemSize));
      pos += Address.MemSize;
      AHAddress from = new AHAddress(packet.Slice(pos, Address.MemSize));
      pos += Address.MemSize;
      AHAddress to = new AHAddress(packet.Slice(pos, Address.MemSize));
      pos += Address.MemSize;
      int forwarders = NumberSerializer.ReadInt(packet, pos);
      pos += 4;
      int hops = NumberSerializer.ReadInt(packet, pos) + 1;
      pos += 4;
      data = packet.Slice(pos);
      return new BroadcastSender(node, source, from, to, forwarders, hops);
    }

    public void Send(ICopyable data)
    {
      ConnectionList cl = Node.ConnectionTable.GetConnections(ConnectionType.Structured);
      // We start with the node immediately after the starting index
      int start = cl.IndexOf(From);
      if(start < 0) {
        start = ~start;
      }

      // We end with the node immediately before the end index
      int end = cl.IndexOf(To);
      if(end < 0) {
        end = ~end;
      }

      // If start >= end, because From < To or because this is a terminal
      // node and there are no other entities to forward it to.  So the
      // second test ensures that the first entity is actually inside the
      // range to forward it to.
      AHAddress start_addr = cl[start].Address as AHAddress;
      if(start >= end && start_addr.IsBetweenFromLeft(From, To)) {
        end += cl.Count;
      }

      List<Connection> cons = SortByDistance(cl, start, end, Forwarders);
      for(int i = 0; i < cons.Count; i++) {
        Connection con = cons[i];
        int next = i + 1;
        AHAddress nfrom = con.Address as AHAddress;
        Address nto = To;
        if(next < cons.Count) {
          nto = GetLeftNearTarget(cons[next].Address as AHAddress);
        }
        con.Edge.Send(new CopyList(PType, Source.ToMemBlock(),
              nfrom.ToMemBlock(), nto.ToMemBlock(), _forwarders, _hops, data));
      }

      _sent_to = cons.Count;
    }

    /// <summary>Returns a list of sorted connections to send on starting with
    /// the node closest on the right to the peer.</summary>
    public List<Connection> SortByDistance(ConnectionList cl, int start, int end, int take)
    {
      List<Connection> cons = new List<Connection>(cl.Count);
      for(int idx = start; idx < end; idx++) {
        cons.Add(cl[idx]);
      }

      if(take == -1 || take >= cons.Count || cons.Count == 0) {
        return cons;
      }

      cons.Sort(_distance_sorter);
      List<Connection> to_send = new List<Connection>(take);
      to_send.Add(cl[start]);
      for(int idx = cons.Count - take + 1; idx < cons.Count; idx++) {
        if(cl[start].Address.Equals(cons[idx].Address)) {
          continue;
        }
        to_send.Add(cons[idx]);
      }

      to_send.Sort(_address_sorter);
      return to_send;
    }

    /// <summary>Sorts a set of connections from the perspective of start
    /// going right to left. </summary>
    public class LeftDistanceComparer : IComparer<Connection> {
      protected readonly AHAddress _start;
      public LeftDistanceComparer(AHAddress start)
      {
        _start = start;
      }

      public int Compare(Connection x, Connection y)
      {
        AHAddress xaddr = x.Address as AHAddress;
        AHAddress yaddr = y.Address as AHAddress;
        if(xaddr == null || y == null) {
          throw new Exception("Invalid comparison");
        }
        BigInteger xdist = _start.LeftDistanceTo(xaddr);
        BigInteger ydist = _start.LeftDistanceTo(yaddr);
        if(xdist == ydist) {
          return 0;
        } else if(xdist < ydist) {
          return -1;
        } else {
          return 1;
        }
      }
    }

    /// <summary>Sorts a set of connections based upon their distance to a
    /// common location.</summary>
    public class AbsoluteDistanceComparer : IComparer<Connection> {
      protected readonly AHAddress _local;
      public AbsoluteDistanceComparer(AHAddress local)
      {
        _local = local;
      }

      public int Compare(Connection x, Connection y)
      {
        AHAddress xaddr = x.Address as AHAddress;
        AHAddress yaddr = y.Address as AHAddress;
        if(xaddr == null || y == null) {
          throw new Exception("Invalid comparison");
        }

        BigInteger xdist = _local.DistanceTo(xaddr).abs();
        BigInteger ydist = _local.DistanceTo(yaddr).abs();
        if(xdist < ydist) {
          return -1;
        } else if(ydist < xdist) {
          return 1;
        } else {
          return 0;
        }
      }
    }

    public string ToUri()
    {
      throw new NotImplementedException();
    }

    /// <summary>Calculate the Address immediately to the left.</summary>
    public static AHAddress GetLeftNearTarget(AHAddress address)
    {
      BigInteger local_int_add = address.ToBigInteger();
      //must have even addresses so increment twice
      local_int_add -= 2;
      //Make sure we don't overflow:
      BigInteger tbi = new BigInteger(local_int_add % Address.Full);
      return new AHAddress(tbi);
    }

    /// <summary>Calculate the Address immediately to the right.</summary>
    public static AHAddress GetRightNearTarget(AHAddress address)
    {
      BigInteger local_int_add = address.ToBigInteger();
      //must have even addresses so increment twice
      local_int_add += 2;
      //Make sure we don't overflow:
      BigInteger tbi = new BigInteger(local_int_add % Address.Full);
      return new AHAddress(tbi);
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class BroadcastTester {
    [Test]
    public void Test()
    {
      TransportAddress ta =
        TransportAddressFactory.CreateInstance("brunet.tcp://169.0.5.1:5000");
      FakeEdge fe = new FakeEdge(ta, ta);
      var rng = new System.Security.Cryptography.RNGCryptoServiceProvider();
      var cons = new List<Connection>();
      for(int i = 0; i < 50; i++) {
        var addr = new AHAddress(rng);
        cons.Add(new Connection(fe, addr, "structured", null, null));
      }

      var start_addr = new AHAddress(rng);
      IComparer<Connection> distance_comparer =
        new BroadcastSender.AbsoluteDistanceComparer(start_addr);
      cons.Sort(distance_comparer);
      BigInteger current_distance = new BigInteger(0);
      foreach(Connection con in cons) {
        AHAddress addr = con.Address as AHAddress;
        BigInteger next_distance = start_addr.DistanceTo(addr).abs();
        Assert.IsTrue(current_distance < next_distance, "DistanceComparer");
        current_distance = next_distance;
      }

      IComparer<Connection> address_comparer =
        new BroadcastSender.LeftDistanceComparer(start_addr);
      cons.Sort(address_comparer);
      current_distance = new BigInteger(0);
      foreach(Connection con in cons) {
        AHAddress addr = con.Address as AHAddress;
        BigInteger next_distance = start_addr.LeftDistanceTo(addr).abs();
        Assert.IsTrue(current_distance < next_distance, "AddressComparer");
        current_distance = next_distance;
      }
    }
  }
#endif
}
