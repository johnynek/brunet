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

using Brunet;
using Brunet.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Brunet.Tunnel {
  /// <summary>Tunnels provide the ability for disconnected peers to form
  /// via existing overlay connections.  For example, two nodes behind asymmetric
  /// NATs can form a tunnel over a common peer forming a virtual direct link
  /// between each other.  Tunnels provide a mechanism for forming a complete
  /// ring.</summary>
  public class TunnelEdgeListener : EdgeListener, IEdgeSendHandler, IDataHandler {
    protected Node _node;
    protected int _running;
    protected int _started;
    Dictionary<int, TunnelEdge> _id_to_tunnel;
    object _sync;
    protected IList _local_tas;

    public override IEnumerable LocalTAs { get { return _local_tas; } }
    public override int Count { get { return _id_to_tunnel.Count; } }
    protected ConnectionList _connections;
    protected readonly ITunnelOverlap _ito;
    protected readonly OverlapConnectionOverlord _oco;
    protected readonly IAddressSelectorFactory _iasf;

    public override TransportAddress.TAType TAType {
      get {
        return TransportAddress.TAType.Tunnel;
      }
    }

    public override bool IsStarted {
      get {
        return _started == 1;
      }
    }



    public TunnelEdgeListener(Node node) :
      this(node, new SimpleTunnelOverlap(node), new SimpleAddressSelectorFactory())
    {
    }

    public TunnelEdgeListener(Node node, ITunnelOverlap ito, IAddressSelectorFactory iasf)
    {
      _ito = ito;
      _iasf = iasf;
      _oco = new OverlapConnectionOverlord(node);
      _node = node;
      _running = 0;
      _started = 0;
      _id_to_tunnel = new Dictionary<int, TunnelEdge>();
      _sync = new object();

      TransportAddress ta = new TunnelTransportAddress(node.Address, new List<Address>());
      ArrayList local_tas = new ArrayList(1);
      local_tas.Add(ta);
      _local_tas = local_tas;

      _node.DemuxHandler.GetTypeSource(PType.Protocol.Tunneling).Subscribe(this, null);
      _node.ConnectionTable.StatusChangedEvent += UpdateNeighborIntersection;
      _node.ConnectionTable.ConnectionEvent += ConnectionHandler;
      _node.ConnectionTable.DisconnectionEvent += DisconnectionHandler;
    }

    /// <summary>When a TunnelEdge closes, we must remove it from our
    /// hashtable.</summary>
    protected void CloseHandler(object o, EventArgs ea)
    {
      TunnelEdge te = o as TunnelEdge;
      lock(_sync) {
        _id_to_tunnel.Remove(te.LocalID);
      }
    }

    /// <summary>Does not immediately create an edge to a remote node, instead,
    /// it tells a timer to wait 5 seconds prior to attempting an edge.</summary>
    public override void CreateEdgeTo(TransportAddress ta, EdgeCreationCallback ecb)
    {
      TunnelTransportAddress tta = ta as TunnelTransportAddress;
      if(tta == null) {
        ecb(false, null, new Exception("TA Type is not Tunnel!"));
      } else {
        TunnelEdgeCallbackAction teca = new TunnelEdgeCallbackAction(tta, ecb);
        SimpleTimer timer = new SimpleTimer(CreateEdgeTo, teca, 5000, 0);
        timer.Start();
      }
    }

    /// <summary>The delayed callback for CreateEdgeTo, we create an edge if
    /// there is a potential non-tunnel overlap and allow the Linker to do the
    /// rest.</summary>
    protected void CreateEdgeTo(object o)
    {
      TunnelEdgeCallbackAction teca = o as TunnelEdgeCallbackAction;
      ConnectionList cons = _connections;
      List<Address> overlap = FindOverlap(teca.TunnelTA, cons);
      if(overlap.Count == 0) {
        if(_ito == null) {
          FailedEdgeCreate(teca);
        } else {
          AttemptToCreateOverlap(teca);
        }
        return;
      }

      CreateEdge(teca, overlap);
    }

    /// <summary>First we try to find a third party we can connect with for
    /// overlap, if that is successful, we attempt to connect to him, if that
    /// is successful, we create a new tunnel edge.</summary>
    protected void AttemptToCreateOverlap(TunnelEdgeCallbackAction teca)
    {
       WaitCallback create_connection = delegate(object o) {
        Address target = o as Address;
        if(o == null) {
          FailedEdgeCreate(teca);
          return;
        }

        if(!_node.ConnectionTable.Contains(OverlapConnectionOverlord.MAIN_TYPE, target)) {
          FailedEdgeCreate(teca);
          return;
        }

        List<Address> overlap = new List<Address>(1);
        overlap.Add(target);
        CreateEdge(teca, overlap);
      };

      WaitCallback found_overlap = delegate(object o) {
        Address target = o as Address;
        if(o == null) {
          FailedEdgeCreate(teca);
        } else {
          _oco.ConnectTo(target, create_connection);
        }
      };
      
      _ito.FindOverlap(teca.TunnelTA, found_overlap);
    }

    protected void CreateEdge(TunnelEdgeCallbackAction teca, List<Address> overlap)
    {
      TunnelEdge te = new TunnelEdge(this, (TransportAddress) _local_tas[0],
          teca.TunnelTA, _iasf.GetAddressSelector(), overlap);
      lock(_sync) {
        _id_to_tunnel[te.LocalID] = te;
      }

      te.CloseEvent += CloseHandler;
      teca.Success.Value = true;
      teca.Exception.Value = null;
      teca.Edge.Value = te;
      _node.EnqueueAction(teca);
    }

    protected void FailedEdgeCreate(TunnelEdgeCallbackAction teca)
    {
      teca.Success.Value = false;
      teca.Exception.Value = new Exception("Not enough forwarders!");
      teca.Edge.Value = null;
      _node.EnqueueAction(teca);
    }

    /// <summary>Whenever the node receives a new StatusMessage from a tunnel,
    /// we use this to build a consisting of the intersection of our peers
    /// creating a table of potential tunneling options.  We close the edge if
    /// it is empty.</summary>
    protected void UpdateNeighborIntersection(object o, EventArgs ea)
    {
      StatusMessage sm = o as StatusMessage;
      ConnectionEventArgs cea = ea as ConnectionEventArgs;
      TunnelEdge te = cea.Edge as TunnelEdge;
      if(te == null) {
        return;
      }

      ConnectionList cur_cons = _connections;
      List<Address> overlap = new List<Address>();
      // iterate through all remote peers information
      foreach(NodeInfo ni in sm.Neighbors) {
        bool is_tunnel = false;
        if(ni.Transports == null || ni.Transports.Count == 0) {
          continue;
        }

        if(ni.FirstTA is TunnelTransportAddress) {
          is_tunnel = true;
        }

        int index = cur_cons.IndexOf(ni.Address);
        if(index < 0) {
          continue;
        }

        Connection con = cur_cons[index];
        // Since there are no guarantees about routing over two tunnels, we do
        // not consider cases where we are connected to the overlapping tunnels
        // peers via tunnels
        if(con.Edge.TAType.Equals(TransportAddress.TAType.Tunnel) && is_tunnel) {
          continue;
        }
        overlap.Add(con.Address);
      }

      te.UpdateNeighborIntersection(overlap);
    }

    /// <summary>We need to keep track of a current ConnectionList, so we
    /// listen to incoming connections.  We also use this opportunity to
    /// rebuild our LocalTA.</summary>
    protected void ConnectionHandler(object o, EventArgs ea)
    {
      ConnectionEventArgs cea = ea as ConnectionEventArgs;
      if(cea.ConnectionType != ConnectionType.Structured) {
        return;
      }

      ConnectionList cons = cea.CList;
      Interlocked.Exchange(ref _connections, cons);

      IList addresses = GetNearest(_node.Address, cons);
      TransportAddress ta = new TunnelTransportAddress(_node.Address, addresses);

      ArrayList tas = new ArrayList(1);
      tas.Add(ta);

      Interlocked.Exchange(ref _local_tas, tas);
    }

    /// <summary>When a disconnection occurs, we must make sure that none of
    /// our tunnels use that faulty edge / connection any more.</summary>
    protected void DisconnectionHandler(object o, EventArgs ea)
    {
      ConnectionEventArgs cea = ea as ConnectionEventArgs;
      if(cea.ConnectionType != ConnectionType.Structured) {
        return;
      }

      ConnectionList cons = cea.CList;
      Interlocked.Exchange(ref _connections, cons);;

      IList addresses = GetNearest(_node.Address, cons);
      TransportAddress ta = new TunnelTransportAddress(_node.Address, addresses);

      ArrayList tas = new ArrayList(1);
      tas.Add(ta);

      Interlocked.Exchange(ref _local_tas, tas);

      List<TunnelEdge> tmp = null;
      lock(_sync) {
        tmp = new List<TunnelEdge>(_id_to_tunnel.Values);
      }

      foreach(TunnelEdge edge in tmp) {
        edge.DisconnectionHandler(cea.Connection.Address);
      }
    }

    /// <summary>Returns our nearest neighbors to the specified address, which
    /// is in turn used to help communicate with tunnel peer.</summary>
    public static List<Address> GetNearest(Address addr, ConnectionList cons)
    {
      ConnectionList cons_near = cons.GetNearestTo(addr, 8);
      List<Address> addrs = new List<Address>();
      foreach(Connection con in cons_near) {
        addrs.Add(con.Address);
      }
      return addrs;
    }

    /// <summary>Attempt to find the overlap in a remote TunnelTransportAddress
    /// and our local node.  This will be used to help communicate with a new
    /// tunneled peer.</summary>
    public static List<Address> FindOverlap(TunnelTransportAddress ta, ConnectionList cons)
    {
      List<Address> overlap = new List<Address>();
      foreach(Connection con in cons) {
        if(con.Edge.TAType == TransportAddress.TAType.Tunnel) {
          continue;
        }

        Address addr = con.Address;
        if(ta.ContainsForwarder(addr)) {
          overlap.Add(addr);
        }
      }
      return overlap;
    }

    /// <summary>Where data packets prepended with a tunnel come.  Here we
    /// receive data as well as create new TunnelEdges.</summary>
    public void HandleData(MemBlock data, ISender return_path, object state)
    {
      ForwardingSender from = return_path as ForwardingSender;
      if(from == null) {
        return;
      }

      AHAddress target = (AHAddress) from.Destination;

      int remote_id = NumberSerializer.ReadInt(data, 0);
      int local_id = NumberSerializer.ReadInt(data, 4);

      TunnelEdge te = null;
      // No locally assigned ID, so we'll create a new TunnelEdge and assign it one.
      // This could be hit many times by the same RemoteID, but it is safe since
      // we'll attempt Linkers on all of them and he'll only respond to the first
      // one he receives back.
      if(local_id == -1) {
        ConnectionList cons = _connections;
        int index = cons.IndexOf(from.Forwarder);
        if(index < 0) {
          return;
        }

        List<Address> overlap_addrs = new List<Address>();
        overlap_addrs.Add(cons[index].Address);

        te = new TunnelEdge(this, (TransportAddress) _local_tas[0],
            new TunnelTransportAddress(target, overlap_addrs),
            _iasf.GetAddressSelector(), overlap_addrs, remote_id);
        lock(_sync) {
          _id_to_tunnel[te.LocalID] = te;
        }
        local_id = te.LocalID;

        te.CloseEvent += CloseHandler;
        SendEdgeEvent(te);
      }

      if(!_id_to_tunnel.TryGetValue(local_id, out te)) {
        // Maybe we closed this edg
        throw new Exception("No such edge");
      } else if(te.RemoteID == -1) {
        // We created this, but we haven't received a packet yet
        te.RemoteID = remote_id;
      } else if(te.RemoteID != remote_id) {
        // Either we closed this edge and it was reallocated or something is up!
        throw new Exception("Receiving imposter packet...");
      }

      // Chop off the Ids
      data = data.Slice(8);
      te.ReceivedPacketEvent(data);
    }

    /// <summary>Used to send data over the tunnel via forwarding senders
    /// using a randomly selected peer from our overlap list.</summary>
    public void HandleEdgeSend(Edge from, ICopyable data)
    {
      TunnelEdge te = from as TunnelEdge;
      Address forwarder  = te.NextAddress;
      Address target = (te.RemoteTA as TunnelTransportAddress).Target;

      ForwardingSender s = new ForwardingSender(_node, forwarder,
          AHHeader.Options.Exact, target, _node.DefaultTTLFor(target),
          AHHeader.Options.Exact);
      s.Send(new CopyList(PType.Protocol.Tunneling, te.MId, data));
    }

    public override void Start()
    {
      if(Interlocked.Exchange(ref _started, 1) == 1) {
        throw new Exception("TunnelEdgeListener cannot be started twice.");
      }

      _oco.IsActive = true;
      Interlocked.Exchange(ref _running, 1);
    }

    public override void Stop()
    {
      _oco.IsActive = false;
      Interlocked.Exchange(ref _running, 0);
      base.Stop();
    }

    public class TunnelEdgeCallbackAction : IAction {
      public readonly TunnelTransportAddress TunnelTA;
      public readonly EdgeCreationCallback Ecb;
      public readonly WriteOnce<Exception> Exception;
      public readonly WriteOnce<bool> Success;
      public readonly WriteOnce<Edge> Edge;

      public TunnelEdgeCallbackAction(TunnelTransportAddress tta, EdgeCreationCallback ecb)
      {
        TunnelTA = tta;
        Ecb = ecb;
        Exception = new WriteOnce<Exception>();
        Success = new WriteOnce<bool>();
        Edge = new WriteOnce<Edge>();
      }

      public void Start()
      {
        Ecb(Success.Value, Edge.Value, Exception.Value);
      }
    }
  }
}
