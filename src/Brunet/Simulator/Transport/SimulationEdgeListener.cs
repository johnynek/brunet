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

using Brunet.Transport;
using Brunet.Util;

namespace Brunet.Simulator.Transport {
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
    static protected Dictionary<TransportAddress.TAType, Dictionary<int, SimulationEdgeListener>> _el_map;
    ///<summary>Performance enhancement to reduce pressure on GC.</summary>
    static protected BufferAllocator _ba;

    /// <summary>ID of this EL.</summary>
    readonly public int LocalID;
    static readonly protected Random _rand;
    readonly protected Dictionary<Edge, Edge> _edges;
    /// <summary> uri's for this type look like: brunet.s://[listener_id] </summary>
    override public IEnumerable LocalTAs { get { return _local_tas; } }
    readonly protected IEnumerable _local_tas;

    protected double _ploss_prob;
    public override TransportAddress.TAType TAType { get { return _ta_type; } }
    protected TransportAddress.TAType _ta_type;

    static SimulationEdgeListener()
    {
      _el_map = new Dictionary<TransportAddress.TAType, Dictionary<int, SimulationEdgeListener>>();
      _ba = new BufferAllocator(Int16.MaxValue);
      _rand = new Random();
    }

    /// <summary>Retrieve a given EL Dictionary for the TA Type.  This could leak,
    /// though that would take the creation of many different EL types and in normal
    /// usage there will only be 1 or 2 types.</summary>
    static protected Dictionary<int, SimulationEdgeListener> GetEdgeListenerList(TransportAddress.TAType type)
    {
      if(!_el_map.ContainsKey(type)) {
        _el_map[type] = new Dictionary<int, SimulationEdgeListener>();
      }
      return _el_map[type];
    }

    public SimulationEdgeListener(int id):this(id, 0.05, null)
    {
    }

    public SimulationEdgeListener(int id, double loss_prob, TAAuthorizer ta_auth) :
      this(id, loss_prob, ta_auth, true)
    {
    }

    public SimulationEdgeListener(int id, double loss_prob, TAAuthorizer ta_auth,
        bool use_delay) : this(id, loss_prob, ta_auth, use_delay, TransportAddress.TAType.S)
    {
    }

    public SimulationEdgeListener(int id, double loss_prob, TAAuthorizer ta_auth,
        bool use_delay, TransportAddress.TAType type)
    {
      _edges = new Dictionary<Edge, Edge>();
      _use_delay = use_delay;
      LocalID = id;
      _ploss_prob = loss_prob;
      if (ta_auth == null) {
        _ta_auth = new ConstantAuthorizer(TAAuthorizer.Decision.Allow);
      } else {
        _ta_auth = ta_auth;
      }
      _ta_type = type;

      ArrayList tas = new ArrayList();
      string sta = String.Format("b.{0}://{1}",
          TransportAddress.TATypeToString(TAType), LocalID);
      tas.Add(TransportAddressFactory.CreateInstance(sta));
      _local_tas = ArrayList.ReadOnly(tas);

      _is_started = false;
    }

    static public void Clear()
    {
      Clear(TransportAddress.TAType.S);
    }

    static public void Clear(TransportAddress.TAType type)
    {
      if(!_el_map.ContainsKey(type)) {
        return;
      }
      var el_map = _el_map[type];
      el_map.Clear();
      _el_map.Remove(type);
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
          int local = LocalID % LatencyMap.Count;
          int remote = remote_id % LatencyMap.Count;
          delay = LatencyMap[local][remote] / 1000;
        } else {
          delay = 100;
        }
      }

      SimulationEdge se_l = new SimulationEdge(this, LocalID, remote_id, false, delay, _ta_type);
      var el_map = GetEdgeListenerList(TAType);
      if(el_map.ContainsKey(remote_id)) {
        var remote = el_map[remote_id];
        // Make sure that the remote listener does not deny our TAs.
        foreach (TransportAddress ta_local in LocalTAs) {
          if (remote.TAAuth.Authorize(ta_local) == TAAuthorizer.Decision.Deny ) {
            ecb(false, null, new EdgeException( ta_local.ToString() + " is not authorized by remote node.") );
            return;
          }
        }

        SimulationEdge se_r = new SimulationEdge(remote, remote_id, LocalID, true, delay, _ta_type);
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

      var el_map = GetEdgeListenerList(_ta_type);
      if(el_map.ContainsKey(LocalID)) {
        throw new Exception("SimulationEdgeListener already exists: " + LocalID);
      }
      _is_started = true;
      el_map[LocalID] = this;
    }

    public override void Stop()
    {
      if(!_is_started) {
        return;
      }
      _is_started = false;
      // If two simulations exist in the same space, this could have been overwritten
      var el_map = GetEdgeListenerList(_ta_type);
      if(el_map.ContainsKey(LocalID)) {
        if(el_map[LocalID] == this) {
          el_map.Remove(LocalID);
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
