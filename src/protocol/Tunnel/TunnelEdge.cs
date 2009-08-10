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

    /// <summary>These are the overlapping neighbors.</summary>
    public IList Tunnels { get { return _tunnels; } }
    /// <summary>A readonly list of tunnels.  Must be replaced to update.</summary>
    protected ArrayList _tunnels;

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

    /// <summary>Outgoing edge, since we don't know the RemoteID yet!</summary>
    public TunnelEdge(IEdgeSendHandler send_handler, TransportAddress local_ta,
        TransportAddress remote_ta, ArrayList neighbors) :
      this(send_handler, local_ta, remote_ta, neighbors, -1)
    {
    }

    /// <summary>Constructor for a TunnelEdge, RemoteID == -1 for out bound.</summary>
    public TunnelEdge(IEdgeSendHandler send_handler, TransportAddress local_ta,
        TransportAddress remote_ta, ArrayList neighbors, int remote_id) :
        base(send_handler, remote_id != -1)
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
      _tunnels = ArrayList.ReadOnly(new ArrayList(neighbors));
    }

    /// <summary>When our tunnel peer has some state change, he notifies us and
    /// use that information to update our overlap, here we set the overlap.</summary>
    public void UpdateNeighborIntersection(ArrayList neighbors)
    {
      bool close = false;
      lock(_sync) {
        _tunnels = ArrayList.ReadOnly(new ArrayList(neighbors));
        close = _tunnels.Count == 0;
      }

      if(close) {
        Close();
      }
    }

    /// <summary>We don't want to send on disconnected edges.  So we remove said
    /// connections and edges!</summary>
    public void DisconnectionHandler(Address addr)
    {
      bool close = false;
      lock(_sync) {
        int index = _tunnels.IndexOf(addr);
        if(_tunnels.IndexOf(addr) < 0) {
          return;
        }

        ArrayList tunnels = Functional.RemoveAt(_tunnels, index);
        _tunnels = ArrayList.ReadOnly(tunnels);
        close = _tunnels.Count == 0;
      }

      if(close) {
        Close();
      }
    }
  }
}
