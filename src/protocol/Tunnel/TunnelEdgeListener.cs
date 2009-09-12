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
  public class TunnelEdgeListener : EdgeListener, IEdgeSendHandler, IDataHandler, IRpcHandler {
    protected Node _node;
    protected int _running;
    protected int _started;
    Dictionary<int, TunnelEdge> _id_to_tunnel;
    /// <summary>Thread safe access to all our tunnel edges.</summary>
    List<TunnelEdge> _tunnels {
      get {
        List<TunnelEdge> tunnels = null;
        lock(_sync) {
          tunnels = new List<TunnelEdge>(_id_to_tunnel.Values);
        }
        return tunnels;
      }
    }

    object _sync;
    protected IList _local_tas;

    public override IEnumerable LocalTAs { get { return _local_tas; } }
    public override int Count { get { return _id_to_tunnel.Count; } }
    protected ConnectionList _connections;
    protected readonly ITunnelOverlap _ito;
    protected readonly OverlapConnectionOverlord _oco;
    protected readonly IForwarderSelectorFactory _iasf;
    protected readonly SimpleTimer _oco_trim_timer;
    protected readonly int _oco_trim_timeout = 300000; // 5 minutes

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
      this(node, new SimpleTunnelOverlap(), new SimpleForwarderSelectorFactory())
    {
    }

    public TunnelEdgeListener(Node node, ITunnelOverlap ito) :
      this(node, ito, new SimpleForwarderSelectorFactory())
    {
    }

    public TunnelEdgeListener(Node node, ITunnelOverlap ito, IForwarderSelectorFactory iasf)
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
      _node.ConnectionTable.ConnectionEvent += ConnectionHandler;
      _node.ConnectionTable.DisconnectionEvent += DisconnectionHandler;

      ConnectionList cons = _node.ConnectionTable.GetConnections(ConnectionType.Structured);
      Interlocked.Exchange(ref _connections, cons);
      _node.Rpc.AddHandler("tunnel", this);
      _oco_trim_timer = new SimpleTimer(OcoTrim, null, _oco_trim_timeout, _oco_trim_timeout);
    }

    /// <summary>A callback to trim Overlapped Connections.  We do this here,
    /// since we control Oco and he is essentially headless.</summary>
    protected void OcoTrim(object o)
    {
      Hashtable used_addrs = new Hashtable();
      foreach(TunnelEdge te in _tunnels) {
        foreach(Connection con in te.Overlap) {
          used_addrs[con.Address] = true;
        }
      }

      ConnectionList cons = _connections;
      DateTime timeout = DateTime.UtcNow.AddMilliseconds(-1 * _oco_trim_timeout);
      foreach(Connection con in cons) {
        if(!con.ConType.Equals(OverlapConnectionOverlord.STRUC_OVERLAP)) {
          continue;
        }
        // If we don't use it or it is still young we'll spare it for now
        if(used_addrs.Contains(con.Address) || con.CreationTime > timeout) {
          continue;
        }

        int left_pos = _connections.LeftInclusiveCount(_node.Address, con.Address);
        int right_pos = _connections.RightInclusiveCount(_node.Address, con.Address);
        if( left_pos >= StructuredNearConnectionOverlord.DESIRED_NEIGHBORS &&
            right_pos >= StructuredNearConnectionOverlord.DESIRED_NEIGHBORS )
        {
          _node.GracefullyClose(con.Edge, "OCO, unused overlapped connection");
        }
      }
    }

    public void HandleRpc(ISender caller, string method, IList args, object rs)
    {
      if(method.Equals("Sync")) {
        TunnelEdge te = (caller as ReqrepManager.ReplyState).ReturnPath as TunnelEdge;
        if(te == null) {
          throw new Exception(String.Format(
                "{0} must be called from a TunnelEdge.", method));
        }

        IDictionary dict = args[0] as IDictionary;
        if(dict == null) {
          throw new Exception(method + "\'s parameter is an IDictionary!");
        }

        UpdateNeighborIntersection(te, dict);
        _node.Rpc.SendResult(rs, true);
      } else if(method.Equals("RequestSync")) {
        _node.Rpc.SendResult(rs, _ito.GetSyncMessage(null, _node.Address, _connections));
      } else {
        throw new Exception(String.Format("No such method: {0}.", method));
      }
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

      List<Connection> overlap = _ito.FindOverlap(teca.TunnelTA, cons);
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

        ConnectionList cons = _connections;
        int index = cons.IndexOf(target);
        if(index < 0) {
          FailedEdgeCreate(teca);
          return;
        }

        List<Connection> overlap = new List<Connection>(1);
        overlap.Add(cons[index]);
        CreateEdge(teca, overlap);
      };

      Channel chan = new Channel(1);
      chan.CloseEvent += delegate(object o, EventArgs ea) {
        Address target = null;
        try {
          IDictionary msg = (chan.Dequeue() as RpcResult).Result as IDictionary;
          target = _ito.EvaluatePotentialOverlap(msg);
        } catch {
        }

        if(target == null) {
          FailedEdgeCreate(teca);
        } else {
          _oco.ConnectTo(target, create_connection);
        }
      };

      ISender s = new AHExactSender(_node, teca.TunnelTA.Target);
      _node.Rpc.Invoke(s, chan, "tunnel.RequestSync");
    }

    /// <summary>Common code to Create an outgoing edge.</summary>
    protected void CreateEdge(TunnelEdgeCallbackAction teca, List<Connection> overlap)
    {
      if(_connections.Contains(teca.TunnelTA.Target)) {
        FailedEdgeCreate(teca);
        return;
      }

      TunnelEdge te = new TunnelEdge(this, (TunnelTransportAddress) _local_tas[0],
          teca.TunnelTA, _iasf.GetForwarderSelector(), overlap);
      lock(_sync) {
        _id_to_tunnel[te.LocalID] = te;
      }
      te.CloseEvent += CloseHandler;

      teca.Success.Value = true;
      teca.Exception.Value = null;
      teca.Edge.Value = te;

      _node.EnqueueAction(teca);
    }

    /// <summary>Common code to signify the failure of edge creation.</summary>
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
    protected void UpdateNeighborIntersection(TunnelEdge from, IDictionary msg)
    {
      List<Connection> overlap = _ito.EvaluateOverlap(_connections, msg);
      from.UpdateNeighborIntersection(overlap);
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

      foreach(TunnelEdge te in _tunnels) {
        IDictionary sync_message = _ito.GetSyncMessage(te.Overlap, _node.Address, cons);
        Channel chan = new Channel(1);
        _node.Rpc.Invoke(te, chan, "tunnel.Sync", sync_message);
      }
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


      foreach(TunnelEdge te in _tunnels) {
        te.DisconnectionHandler(cea.Connection);
        if(te.IsClosed) {
          continue;
        }

        IDictionary sync_message = _ito.GetSyncMessage(te.Overlap, _node.Address, cons);
        Channel chan = new Channel(1);
        _node.Rpc.Invoke(te, chan, "tunnel.Sync", sync_message);
      }
    }

    /// <summary>Returns our nearest neighbors to the specified address, which
    /// is in turn used to help communicate with tunnel peer.</summary>
    public static List<Address> GetNearest(Address addr, ConnectionList cons)
    {
      ConnectionList cons_near = cons.GetNearestTo(addr, 16);
      List<Address> addrs = new List<Address>();
      foreach(Connection con in cons_near) {
        addrs.Add(con.Address);
      }
      return addrs;
    }

    /// <summary>Where data packets prepended with a tunnel come.  Here we
    /// receive data as well as create new TunnelEdges.</summary>
    public void HandleData(MemBlock data, ISender return_path, object state)
    {
      AHSender ah_from = return_path as AHSender;
      ForwardingSender fs_from = return_path as ForwardingSender;
      AHAddress target = null;

      if(ah_from == null) {
        if(fs_from == null) {
          return;
        }
        target = (AHAddress) fs_from.Destination;
      } else {
        target = (AHAddress) ah_from.Destination;
      }

      int remote_id = NumberSerializer.ReadInt(data, 0);
      int local_id = NumberSerializer.ReadInt(data, 4);

      TunnelEdge te = null;
      // No locally assigned ID, so we'll create a new TunnelEdge and assign it one.
      // This could be hit many times by the same RemoteID, but it is safe since
      // we'll attempt Linkers on all of them and he'll only respond to the first
      // one he receives back.
      if(local_id == -1) {
        if(fs_from == null) {
          throw new Exception("No LocalID assigned but not from a useful sender!");
        }

        ConnectionList cons = _connections;
        int index = cons.IndexOf(fs_from.Forwarder);
        if(index < 0) {
          return;
        }

        List<Connection> overlap_addrs = new List<Connection>();
        overlap_addrs.Add(cons[index]);

        te = new TunnelEdge(this, (TunnelTransportAddress) _local_tas[0],
            new TunnelTransportAddress(target, overlap_addrs),
            _iasf.GetForwarderSelector(), overlap_addrs, remote_id);
        lock(_sync) {
          _id_to_tunnel[te.LocalID] = te;
        }
        local_id = te.LocalID;

        te.CloseEvent += CloseHandler;
        SendEdgeEvent(te);
      }

      if(!_id_to_tunnel.TryGetValue(local_id, out te)) {
        // Maybe we closed this edge
        // throw new Exception("No such edge");
        // Old behavior would ignore these packets...
        return;
      } else if(te.RemoteID == -1) {
        // We created this, but we haven't received a packet yet
        te.RemoteID = remote_id;
      } else if(te.RemoteID != remote_id) {
        // Either we closed this edge and it was reallocated or something is up!
        // throw new Exception("Receiving imposter packet...");
        // Old behavior would ignore these packets...
        return;
      }

      if(te.IsClosed) {
        throw new Exception("Edge is closed...");
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
      Connection forwarder = te.NextForwarder;

      if(te.RemoteID == -1) {
        Address target = (te.RemoteTA as TunnelTransportAddress).Target;
        ISender sender = new ForwardingSender(_node, forwarder.Address, target);
        sender.Send(new CopyList(PType.Protocol.Tunneling, te.MId, data));
      } else {
        try {
          forwarder.Edge.Send(new CopyList(te.Header, te.MId, data));
        } catch {
          // We could be sending aon a closed edge... we could deal with this
          // better, but let's just let the system take its natural course.
        }
      }
    }

    public override void Start()
    {
      if(Interlocked.Exchange(ref _started, 1) == 1) {
        throw new Exception("TunnelEdgeListener cannot be started twice.");
      }

      _oco.IsActive = true;
      Interlocked.Exchange(ref _running, 1);
      _oco_trim_timer.Start();
    }

    public override void Stop()
    {
      _oco.IsActive = false;
      Interlocked.Exchange(ref _running, 0);
      _oco_trim_timer.Stop();
      base.Stop();

      List<TunnelEdge> tunnels = _tunnels;

      foreach(Edge e in tunnels) {
        try {
          e.Close();
        } catch { }
      }
    }

    /// <summary>Used to bundle a TunnelTA, an ECB, and an IAction
    /// altogether.</summary>
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
        //We failed at some point but created an edge.
        if(!Success.Value && Edge.Value != null) {
          Edge.Value.Close();
        }

        Ecb(Success.Value, Edge.Value, Exception.Value);
      }
    }
  }
}
