/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com> University of Florida

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
using System;
using System.Collections;
using System.Threading;

namespace Brunet
{

  /**
  * A EdgeListener that allows local nodes to communicate
  * with one another.  No system interfaces are used,
  * the packets are simply passed by method calls.
  *
  * FunctionEdges are for debugging with several nodes
  * within one process
  *
  */

  public class FunctionEdgeListener : EdgeListener, IEdgeSendHandler
  {

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

    protected class FQEntry {
      public FQEntry(FunctionEdge e, ICopyable p) { Edge = e; P = p; }
      public FunctionEdge Edge;
      public ICopyable P;
    }
    protected BlockingQueue _queue;
    protected Thread _queue_thread;

    public override TransportAddress.TAType TAType
    {
      get
      {
        return TransportAddress.TAType.Function;
      }
    }


    public FunctionEdgeListener(int id)
    {
      _listener_id = id;
      _tas = new ArrayList();
      _tas.Add(TransportAddressFactory.CreateInstance("brunet.function://localhost:" +
                                     _listener_id.ToString()) );
      _queue = new BlockingQueue();
      _queue_thread = new Thread(new ThreadStart(StartQueueProcessing));
    }

    volatile protected bool _is_started = false;
    public override bool IsStarted
    {
      get { return _is_started; }
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
        ecb(false, null, null);
        return;
      }

      if( ta.TransportAddressType == this.TAType ) {
        int remote_id = ((IPTransportAddress) ta).Port;
        //Get the edgelistener:
        
        //Outbound edge:
        FunctionEdge fe_l = new FunctionEdge(this, _listener_id,
                                             remote_id, false);
        lock( _listener_map ) { 
          FunctionEdgeListener remote
                      = (FunctionEdgeListener) _listener_map[remote_id];
          if( remote != null ) {
            FunctionEdge fe_r = new FunctionEdge(remote, remote_id,
                                                 _listener_id, true);
            fe_l.Partner = fe_r;
            fe_r.Partner = fe_l;
            remote.SendEdgeEvent(fe_r);
          }
          else {
            //There is no other edge, for now, we use "udp-like"
            //behavior of just making an edge that goes nowhere.
          }
        }
        ecb(true, fe_l, null);
      }
      else {
        //Can't make an edge of this type
        ecb(false, null, null);
      }
    }

    public override void Start()
    {
      _is_started = true;
      lock( _listener_map ) {
        _listener_map[ _listener_id ] = this;
      }
      _queue_thread.Start();
    }

    public override void Stop()
    {
      _is_started = false;
    }

    protected void StartQueueProcessing() {
      bool timedout;
      while( _is_started ) {
        //Wait 100 ms for an a packet to be sent:
        FQEntry ent = (FQEntry)_queue.Dequeue(100, out timedout);
        if( !timedout ) {
          FunctionEdge fe = ent.Edge;
          fe.Push( (Packet)ent.P);
        }
      }
    }

    public void HandleEdgeSend(Edge from, ICopyable p) {
      FunctionEdgeListener el = null;
      FunctionEdge fe_from = (FunctionEdge)from;
      FunctionEdge fe_to = fe_from.Partner;
      if( fe_to != null ) {
        el = (FunctionEdgeListener)_listener_map[ fe_to.ListenerId ];
        el._queue.Enqueue( new FQEntry(fe_to, p) );
      }
    }

  }
}
