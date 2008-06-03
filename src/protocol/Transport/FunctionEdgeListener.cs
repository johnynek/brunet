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
//#define FUNCTION_DEBUG
using Brunet;
using System;
using System.IO;
using System.Collections;
using System.Threading;

namespace Brunet
{

  /**
  * A EdgeListener that allows local nodes to communicate
  * with one another.  No system interfaces are used,
  * the packets are simply passed by method calls.
  *
  * FunctionEdges are for debugging with several nodes  * within one process
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
    protected Brunet.Util.LFBlockingQueue _queue;
    protected Thread _queue_thread;
    protected double _ploss_prob;

    public override TransportAddress.TAType TAType
    {
      get
      {
        return TransportAddress.TAType.Function;
      }
    }


    public FunctionEdgeListener(int id):this(id, 0.05, null) {}

    public FunctionEdgeListener(int id, double loss_prob, TAAuthorizer ta_auth)
    {
      _listener_id = id;
      _ploss_prob = loss_prob;
      if (ta_auth == null) {
        _ta_auth = new ConstantAuthorizer(TAAuthorizer.Decision.Allow);
      } else {
	_ta_auth = ta_auth;
      }
      _tas = new ArrayList();
      _tas.Add(TransportAddressFactory.CreateInstance("brunet.function://localhost:" +
                                     _listener_id.ToString()) );
      _queue = new Brunet.Util.LFBlockingQueue();
      _queue_thread = new Thread(new ThreadStart(StartQueueProcessing));
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

#if FUNCTION_DEBUG
      foreach (TransportAddress local_ta in LocalTAs) {
	Console.Error.WriteLine("Create edge local: {0} <-> remote {1}.", local_ta, ta);
      }
#endif

      if( ta.TransportAddressType != this.TAType ) {
        //Can't make an edge of this type
#if FUNCTION_DEBUG
	Console.Error.WriteLine("Can't make edge of this type.");
#endif
        ecb(false, null, new EdgeException("Can't make edge of this type"));
	return;
      }
      
      if( _ta_auth.Authorize(ta) == TAAuthorizer.Decision.Deny ) {
        //Not authorized.  Can't make this edge:
#if FUNCTION_DEBUG
	Console.Error.WriteLine("Can't make edge. Remote TA {0} is not authorized locally.", ta);
#endif
        ecb(false, null,
            new EdgeException( ta.ToString() + " is not authorized") );
	return;
      }
      
      int remote_id = ((IPTransportAddress) ta).Port;
      //Get the edgelistener: 

      //Outbound edge:
      FunctionEdge fe_l = new FunctionEdge(this, _listener_id,
					   remote_id, false);
      lock( _listener_map ) { 
	FunctionEdgeListener remote
	  = (FunctionEdgeListener) _listener_map[remote_id];
	if( remote != null ) {
	  //
	  // Make sure that the remote listener does not deny 
	  // our TAs.
	  //

	  foreach (TransportAddress ta_local in LocalTAs) {
	    if (remote.TAAuth.Authorize(ta_local) == TAAuthorizer.Decision.Deny ) {
	      //Not authorized.  Can't make this edge:
#if FUNCTION_DEBUG
	      Console.Error.WriteLine("Can't make edge. local TA {0} is not authorized remotely by {1}.", ta_local, ta);
#endif
	      ecb(false, null,
		  new EdgeException( ta_local.ToString() + " is not authorized by remote node.") );
	      return;
	    }
	  }

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
        ecb(true, fe_l, null);
      }
    }

    public override void Start()
    {
      if( 1 == Interlocked.Exchange(ref _is_started, 1) ) {
        throw new Exception("Can only call FunctionEdgeListener.Start() once!"); 
      }
      lock( _listener_map ) {
        _listener_map[ _listener_id ] = this;
      }
      _queue_thread.Start();
    }

    public override void Stop()
    {
      Interlocked.Exchange(ref _is_started, 0);
      //Make sure to wake up the queue thread
      _queue.Enqueue(null);
      if( Thread.CurrentThread != _queue_thread ) {
        _queue_thread.Join();
      }
    }

    protected void StartQueueProcessing() {
      /*
       * Simulate packet loss
       */
      Random r = new Random();
      
      try {
        bool timedout;
        while( 1 == _is_started ) {
          FQEntry ent = (FQEntry)_queue.Dequeue(-1, out timedout);
          if( ent == null ) { return; }
          if( r.NextDouble() > _ploss_prob ) {
            FunctionEdge fe = ent.Edge;
            if( ent.P is MemBlock ) {
              fe.Push( (MemBlock) ent.P );
            }
            else {
              fe.Push( MemBlock.Copy(ent.P) );
            }
          }
        }
      }
      catch(InvalidOperationException) {
        // The queue has been closed
      }
    }

    public void HandleEdgeSend(Edge from, ICopyable p) {
      FunctionEdgeListener el = null;
      FunctionEdge fe_from = (FunctionEdge)from;
      FunctionEdge fe_to = fe_from.Partner;
      if( fe_to != null ) {
        el = (FunctionEdgeListener)_listener_map[ fe_to.ListenerId ];
        try {
	  el._queue.Enqueue(new FQEntry(fe_to, p));
        }
        catch(System.InvalidOperationException) {
          //The queue other queue is closed:
          //Just throw the packet away.  This simulates UDP, which is giving less information
          //to the local node.
        }
      }
    }
  }
}
