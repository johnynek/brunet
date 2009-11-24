/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Threading;
using System.Collections;
using System.Collections.Generic;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Tunnel {
  /// <summary>Holds the state information for a Tunnels.</summary>
  public class TunnelEdge : Edge {
    protected static readonly Random _rand = new Random();
    public readonly int LocalID;
    protected int _remote_id;

    public int RemoteID {
      get {
        return _remote_id;
      }
      set {
        //When an outgoing edge first hears back, he doesn't know the
        //remote id, we set it ONCE and fail if it is attempted again!
        if(Interlocked.CompareExchange(ref _remote_id, value, -1) != -1) {
          throw new Exception("RemoteID already set!");
        }

        byte[] bid = new byte[8];
        NumberSerializer.WriteInt(LocalID, bid, 0);
        NumberSerializer.WriteInt(_remote_id, bid, 4);
        MemBlock mid = MemBlock.Reference(bid);
        Interlocked.Exchange(ref _mid, mid);
      }
    }


    protected MemBlock _mid;
    public MemBlock MId { get { return _mid; } }

    protected readonly TransportAddress _local_ta;
    protected readonly TransportAddress _remote_ta;

    /// <summary>A functional list of tunnels.  Replace to update.</summary>
    protected List<Connection> _tunnels;
    protected IForwarderSelector _ias;

    /// <summary>_tunnels is never modified, only replaced, this allows
    /// external code access with thread-safety without making an additional
    /// copy.</summary>
    public IList<Connection> Overlap { get { return _tunnels.AsReadOnly(); } }

    public Connection NextForwarder {
      get {
        return _ias.NextForwarder;
      }
    }

    public override Brunet.TransportAddress LocalTA {
      get {
        return _local_ta;
      }
    }

    public override Brunet.TransportAddress RemoteTA {
      get {
        return _remote_ta;
      }
    }

    public override Brunet.TransportAddress.TAType TAType {
      get {
        return TransportAddress.TAType.Tunnel;
      }
    }

    public readonly MemBlock Header;

    /// <summary>Outgoing edge, since we don't know the RemoteID yet!</summary>
    public TunnelEdge(IEdgeSendHandler send_handler, TunnelTransportAddress local_ta,
        TunnelTransportAddress remote_ta, IForwarderSelector ias, List<Connection> overlap) :
      this(send_handler, local_ta, remote_ta, ias, overlap, -1)
    {
    }

    /// <summary>Constructor for a TunnelEdge, RemoteID == -1 for out bound.</summary>
    public TunnelEdge(IEdgeSendHandler send_handler, TunnelTransportAddress local_ta,
        TunnelTransportAddress remote_ta, IForwarderSelector ias, List<Connection> overlap,
        int remote_id) : base(send_handler, remote_id != -1)
    {
      _remote_id = remote_id;
      lock(_rand) {
        LocalID = _rand.Next();
      }
      byte[] bid = new byte[8];
      NumberSerializer.WriteInt(LocalID, bid, 0);
      NumberSerializer.WriteInt(_remote_id, bid, 4);
      _mid = MemBlock.Reference(bid);
      _local_ta = local_ta;
      _remote_ta = remote_ta;
      _tunnels = new List<Connection>(overlap);
      _ias = ias;
      _ias.Update(_tunnels);

      AHHeader ahh = new AHHeader(1, 20, local_ta.Target, remote_ta.Target,
          AHHeader.Options.Exact);
      ICopyable header = new CopyList(PType.Protocol.AH, ahh,
          PType.Protocol.Tunneling);
      Header = MemBlock.Copy(header);
    }

    /// <summary>When our tunnel peer has some state change, he notifies us and
    /// use that information to update our overlap, here we set the overlap.</summary>
    public void UpdateNeighborIntersection(List<Connection> neighbors)
    {
      lock(_sync) {
        _tunnels = new List<Connection>(neighbors);
      }

      if(neighbors.Count == 0 || ShouldClose()) {
        Close();
      }
      _ias.Update(neighbors);
    }

    /// <summary>We don't want to send on disconnected edges.  So we remove said
    /// connections and edges!</summary>
    public void DisconnectionHandler(Connection con)
    {
      bool close = false;
      List<Connection> tunnels = null;
      lock(_sync) {
        tunnels = new List<Connection>(_tunnels.Count);
        foreach(Connection ccon in _tunnels) {
          if(con.Edge.Equals(ccon.Edge)){
            continue;
          }
          tunnels.Add(ccon);
        }

        if(tunnels.Count == _tunnels.Count) {
          return;
        }

        _tunnels = tunnels;
        close = _tunnels.Count == 0;
      }

      if(close || ShouldClose()) {
        Close();
      }

      //_tunnels is immutable and if we don't pass the latest _tunnels to _ias
      //he will go into an inconsistent state.
      _ias.Update(tunnels);
    }

    /// <summary>We need to make sure we still have a way out of the tunnel
    /// maze, otherwise we could get an endless loop resulting in a stack
    /// exception.  For now, we will simply ensure that one of our overlap
    /// or our overlaps overlap has at least one such connection.  Furthermore,
    /// if we have a long chain of tunnels relying on one overlap, they will
    /// eventually all close after the top most peer which happens to be in
    /// their overlap closes.</summary>
#if BRUNET_NUNIT
    public bool ShouldClose() {
#else
    protected bool ShouldClose() {
#endif
      Dictionary<Edge, bool> have_passed = new Dictionary<Edge, bool>();
      Stack stack = new Stack();
      stack.Push(_tunnels.GetEnumerator());
      while(stack.Count > 0) {
        IEnumerator<Connection> cons = stack.Pop() as IEnumerator<Connection>;
        while(cons.MoveNext()) {
          TunnelEdge te = cons.Current.Edge as TunnelEdge;
          if(te == null) {
            return false;
          }

          if(have_passed.ContainsKey(te)) {
            continue;
          }
          have_passed[te] = true;
          stack.Push(cons);
          cons = te.Overlap.GetEnumerator();
        }
      }
      return true;
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class TunnelEdgeTest {
    [Test]
    public void Test()
    {
      AHAddress addr = new AHAddress(new System.Security.Cryptography.RNGCryptoServiceProvider());
      TransportAddress ta = TransportAddressFactory.CreateInstance("brunet.tcp://169.0.5.1:5000");
      FakeEdge fe = new FakeEdge(ta, ta);
      Connection fcon = new Connection(fe, addr, "structured", null, null);

      List<Connection> overlap = new List<Connection>();
      overlap.Add(fcon);
      TunnelTransportAddress tta = new TunnelTransportAddress(addr, overlap);
      TunnelEdge te1 = new TunnelEdge(null, tta, tta, new SimpleForwarderSelector(), overlap);
      Connection t1con = new Connection(te1, addr, "structured", null, null);

      overlap = new List<Connection>();
      overlap.Add(t1con);
      TunnelEdge te2 = new TunnelEdge(null, tta, tta, new SimpleForwarderSelector(), overlap);
      Connection t2con = new Connection(te2, addr, "structured", null, null);

      overlap = new List<Connection>();
      overlap.Add(t2con);
      TunnelEdge te3 = new TunnelEdge(null, tta, tta, new SimpleForwarderSelector(), overlap);
      Connection t3con = new Connection(te3, addr, "structured", null, null);

      overlap = new List<Connection>();
      overlap.Add(t3con);
      TunnelEdge te4 = new TunnelEdge(null, tta, tta, new SimpleForwarderSelector(), overlap);
      Connection t4con = new Connection(te4, addr, "structured", null, null);

      overlap = new List<Connection>();
      overlap.Add(t4con);
      TunnelEdge te5 = new TunnelEdge(null, tta, tta, new SimpleForwarderSelector(), overlap);
      Connection t5con = new Connection(te5, addr, "structured", null, null);

      Assert.AreEqual(te5.ShouldClose(), false, "Shouldn't close yet...");
      te1.DisconnectionHandler(fcon);
      Assert.AreEqual(te5.ShouldClose(), true, "Should close...");

      overlap.Add(t5con);
      overlap.Add(t3con);
      overlap.Add(t1con);
      te2.UpdateNeighborIntersection(overlap);
      Assert.AreEqual(te5.ShouldClose(), true, "Should close... 2");
    }
  }
#endif
}
