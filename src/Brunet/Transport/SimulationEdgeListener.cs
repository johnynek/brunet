/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com> University of Florida
Copyright (C) 2008 David Wolinsky <davidiw@ufl.edu> University of Florida

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
using System.Threading;

using Brunet.Util;

namespace Brunet.Transport
{

  /**
   * Allows local nodes to communicate directly without the use of the
   * networking stack.  This class is not thread-safe and meant for single
   * threaded simulation environments.
   */
  public class SimulationEdgeListener : EdgeListener, IEdgeSendHandler
  {
    /// A mapping of delays to use based upon id (port) number.
    public static List<List<int>> LatencyMap;
    /// whether or not to use delays, set in the constructor
    protected bool _use_delay;
    protected long _bytes = 0;
    public long BytesSent { get { return _bytes; } }

    ///<summary>Map EL id to EL.</summary>
    static protected Dictionary<int, SimulationEdgeListener> _listener_map;
    ///<summary>Performance enhancement to reduce pressure on GC.</summary>
    static protected BufferAllocator _ba;

    /// <summary>ID of this EL.</summary>
    readonly protected int _listener_id;
    static readonly protected Random _rand;
    readonly protected Dictionary<Edge, Edge> _edges;
    /// <summary> uri's for this type look like: brunet.s://[listener_id] </summary>
    override public IEnumerable LocalTAs { get { return _local_tas; } }
    readonly protected IEnumerable _local_tas;

    protected double _ploss_prob;
    public override TransportAddress.TAType TAType { get { return TransportAddress.TAType.S; } }

    static SimulationEdgeListener()
    {
      _listener_map = new Dictionary<int, SimulationEdgeListener>();
      _ba = new BufferAllocator(Int16.MaxValue);
      _rand = new Random();
    }

    public SimulationEdgeListener(int id):this(id, 0.05, null) {}
    public SimulationEdgeListener(int id, double loss_prob, TAAuthorizer ta_auth) :
      this(id, loss_prob, ta_auth, false) {}

    public SimulationEdgeListener(int id, double loss_prob, TAAuthorizer ta_auth, bool use_delay)
    {
      _edges = new Dictionary<Edge, Edge>();
      _use_delay = use_delay;
      _listener_id = id;
      _ploss_prob = loss_prob;
      if (ta_auth == null) {
        _ta_auth = new ConstantAuthorizer(TAAuthorizer.Decision.Allow);
      } else {
        _ta_auth = ta_auth;
      }

      ArrayList tas = new ArrayList();
      tas.Add(TransportAddressFactory.CreateInstance("b.s://" + _listener_id));
      _local_tas = ArrayList.ReadOnly(tas);

      _is_started = false;
    }

    static public void Clear()
    {
      _listener_map.Clear();
    }

    protected bool _is_started;
    public override bool IsStarted { get { return _is_started; } }

    /*
     * Implements the EdgeListener function to 
     * create edges of this type.
     */
    public override void CreateEdgeTo(TransportAddress ta, EdgeCreationCallback ecb)
    {
      if( !IsStarted ) {
        // it should return null and not throw an exception
        // for graceful disconnect and preventing others to
        // connect to us after we've disconnected.
        ecb(false, null, new EdgeException("Not started"));
        return;
      } else if( ta.TransportAddressType != this.TAType ) {
        //Can't make an edge of this type
        ecb(false, null, new EdgeException("Can't make edge of this type"));
        return;
      } else if( _ta_auth.Authorize(ta) == TAAuthorizer.Decision.Deny ) {
        //Not authorized.  Can't make this edge:
        ecb(false, null, new EdgeException( ta.ToString() + " is not authorized") );
        return;
      }
      
      int remote_id = ((SimulationTransportAddress) ta).ID;

      //Outbound edge:
      int delay = 0;
      if(_use_delay) {
        if(LatencyMap != null) {
          int local = _listener_id % LatencyMap.Count;
          int remote = remote_id % LatencyMap.Count;
          delay = LatencyMap[local][remote] / 1000;
        } else {
          delay = 100;
        }
      }

      SimulationEdge se_l = new SimulationEdge(this, _listener_id, remote_id, false, delay);
      AddEdge(se_l);

      if(_listener_map.ContainsKey(remote_id)) {
        var remote = _listener_map[remote_id];
        // Make sure that the remote listener does not deny our TAs.
        foreach (TransportAddress ta_local in LocalTAs) {
          if (remote.TAAuth.Authorize(ta_local) == TAAuthorizer.Decision.Deny ) {
            ecb(false, null, new EdgeException( ta_local.ToString() + " is not authorized by remote node.") );
            return;
          }
        }

        SimulationEdge se_r = new SimulationEdge(remote, remote_id, _listener_id, true, delay);
        remote.AddEdge(se_r);

        se_l.Partner = se_r;
        se_r.Partner = se_l;
        remote.SendEdgeEvent(se_r);
      } else {
          //There is no other edge, for now, we use "udp-like"
          //behavior of just making an edge that goes nowhere.
      }
      ecb(true, se_l, null);
    }


    protected void AddEdge(Edge edge)
    {
      edge.CloseEvent += CloseHandler;
      _edges.Add(edge, edge);
    }

    protected void CloseHandler(object edge, EventArgs ea)
    {
      SimulationEdge se = edge as SimulationEdge;
      // Speed up GC
      if(se.Partner != null) {
        se.Partner.Partner = null;
        se.Partner = null;
      }
      _edges.Remove(se);
    }

    public override void Start()
    {
      if(_is_started) {
        throw new Exception("Can only call SimulationEdgeListener.Start() once!"); 
      }

      if(_listener_map.ContainsKey(_listener_id)) {
        throw new Exception("SimulationEdgeListener already exists: " + _listener_id);
      }
      _is_started = true;
      _listener_map[_listener_id] = this;
    }

    public override void Stop()
    {
      if(!_is_started) {
        return;
      }
      _is_started = false;
      // If two simulations exist in the same space, this could have been overwritten
      if(_listener_map.ContainsKey(_listener_id)) {
        if(_listener_map[_listener_id] == this) {
          _listener_map.Remove(_listener_id);
        }
      }

      ArrayList list = new ArrayList(_edges.Values);

      foreach(Edge e in list) {
        try {
          e.Close();
        } catch { }
      }
    }

    public void HandleEdgeSend(Edge from, ICopyable p) {
      if(_ploss_prob > 0) {
        if(_rand.NextDouble() < _ploss_prob) {
          return;
        }
      }

      MemBlock mb = p as MemBlock;
      if(mb == null) {
        int offset = p.CopyTo(_ba.Buffer, _ba.Offset);
        mb = MemBlock.Reference(_ba.Buffer, _ba.Offset, offset);
        _ba.AdvanceBuffer(offset);
        _bytes += offset;
      }

      SimulationEdge se_from = (SimulationEdge)from;
      SimulationEdge se_to = se_from.Partner;
      if(se_to != null) {
        se_to.Push(mb);
      }
    }
  }
}
