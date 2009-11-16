/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com> University of Florida
Copyright (C) 2008 David Wolinsky <davidiw@ufl.edu> University of Florida

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
using System.Threading;

namespace Brunet
{

  /**
  * A EdgeListener that allows local nodes to communicate
  * with one another.  No system interfaces are used,
  * the packets are simply passed by method calls.
  *
  * SimulationEdges are for debugging with several nodes  * within one process
  *
  */

  public class SimulationEdgeListener : EdgeListener, IEdgeSendHandler
  {
    /// A mapping of delays to use based upon id (port) number.
    public static List<List<int>> LatencyMap;
    /// whether or not to use delays, set in the constructor
    protected bool _use_delay;

    /**
     * Each listener has an integer associated with it.
     * This map allows us to look up a listener
     * based on the id.
     */
    static protected Hashtable _listener_map = new Hashtable();

    /**
     * The id of this listener
     */
    protected int _listener_id;
    protected Random _rand;
    protected Hashtable _edges;

    protected ArrayList _tas;
    /**
     * The uri's for this type look like:
     * brunet.function:[edge_id]
     */
    public override IEnumerable LocalTAs
    {
      get
      {
        return ArrayList.ReadOnly(_tas);
      }
    }

    protected double _ploss_prob;
    protected BufferAllocator _ba;
    protected object _sync;
    public override TransportAddress.TAType TAType { get { return TransportAddress.TAType.S; } }


    public SimulationEdgeListener(int id):this(id, 0.05, null) {}
    public SimulationEdgeListener(int id, double loss_prob, TAAuthorizer ta_auth) :
      this(id, loss_prob, ta_auth, false) {}

    public SimulationEdgeListener(int id, double loss_prob, TAAuthorizer ta_auth, bool use_delay)
    {
      _edges = new Hashtable();
      _use_delay = use_delay;
      _sync = new object();
      _ba = new BufferAllocator(Int16.MaxValue);
      _listener_id = id;
      _ploss_prob = loss_prob;
      if (ta_auth == null) {
        _ta_auth = new ConstantAuthorizer(TAAuthorizer.Decision.Allow);
      } else {
        _ta_auth = ta_auth;
      }
      _tas = new ArrayList();
      _tas.Add(TransportAddressFactory.CreateInstance("b.s://" + _listener_id));
      _rand = new Random();
    }

    protected int _is_started = 0;
    public override bool IsStarted
    {
      get { return 1 == _is_started; }
    }

    /*
     * Implements the EdgeListener function to 
     * create edges of this type.
     */
    public override void CreateEdgeTo(TransportAddress ta,
                                      EdgeCreationCallback ecb)
    {
      if( !IsStarted )
      {
        // it should return null and not throw an exception
        // for graceful disconnect and preventing others to
        // connect to us after we've disconnected.
        ecb(false, null, new EdgeException("Not started"));
        return;
      }

      if( ta.TransportAddressType != this.TAType ) {
        //Can't make an edge of this type
        ecb(false, null, new EdgeException("Can't make edge of this type"));
        return;
      }
      
      if( _ta_auth.Authorize(ta) == TAAuthorizer.Decision.Deny ) {
        //Not authorized.  Can't make this edge:
        ecb(false, null, new EdgeException( ta.ToString() + " is not authorized") );
        return;
      }
      
      int remote_id = ((SimulationTransportAddress) ta).ID;
      //Get the edgelistener: 

      //Outbound edge:
      int delay = 0;
      if(_use_delay) {
        if(LatencyMap != null) {
          // id != 0, so we reduce all by 1
          delay = LatencyMap[_listener_id][remote_id] / 1000;
        } else {
          lock(_sync) {
            delay = _rand.Next(10, 250);
          }
        }
      }

      SimulationEdge se_l = new SimulationEdge(this, _listener_id, remote_id, false, delay);
      AddEdge(se_l);

      SimulationEdgeListener remote = (SimulationEdgeListener) _listener_map[remote_id];
      if( remote != null ) {
        //
        // Make sure that the remote listener does not deny 
        // our TAs.
        //

        foreach (TransportAddress ta_local in LocalTAs) {
          if (remote.TAAuth.Authorize(ta_local) == TAAuthorizer.Decision.Deny ) {
          //Not authorized.  Can't make this edge:
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
      lock(_sync) {
        _edges.Add(edge, edge);
      }
    }

    protected void CloseHandler(object edge, EventArgs ea)
    {
      (edge as SimulationEdge).Partner = null;
      lock(_sync) {
        _edges.Remove(edge);
      }
    }

    public override void Start()
    {
      if( 1 == Interlocked.Exchange(ref _is_started, 1) ) {
        throw new Exception("Can only call SimulationEdgeListener.Start() once!"); 
      }
      lock( _listener_map.SyncRoot ) {
        _listener_map[ _listener_id ] = this;
      }
    }

    public override void Stop()
    {
      Interlocked.Exchange(ref _is_started, 0);
      lock ( _listener_map.SyncRoot ) {
        _listener_map.Remove(_listener_id);
      }

      ArrayList list = null;
      lock(_sync) {
        list = new ArrayList(_edges.Values);
      }

      foreach(Edge e in list) {
        try {
          e.Close();
        } catch { }
      }
    }

    public void HandleEdgeSend(Edge from, ICopyable p) {
      if(_ploss_prob > 0) {
        lock(_rand) {
          if(_rand.NextDouble() < _ploss_prob) {
            return;
          }
        }
      }

      MemBlock mb = p as MemBlock;
      if(mb == null) {
        lock(_sync) {
          int offset = p.CopyTo(_ba.Buffer, _ba.Offset);
          mb = MemBlock.Reference(_ba.Buffer, _ba.Offset, offset);
          _ba.AdvanceBuffer(offset);
        }
      }

      SimulationEdge se_from = (SimulationEdge)from;
      SimulationEdge se_to = se_from.Partner;
      if(se_to != null) {
        se_to.Push(mb);
      }
    }
  }
}
