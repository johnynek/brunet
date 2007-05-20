/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
//#define ARI_CTM_DEBUG
using System;
using System.Collections;

namespace Brunet
{

  /**
   * sends ConnectToMessage objects out onto the network.
   * This sends the request, and then waits for the response.
   * When it gets the reponse, it creates a linker to link the
   * two nodes.  Once it has completed its job, it sends a FinishEvent.
   *
   * This should *ONLY* be used by ConnectionOverlord subclasses.  This
   * is a very low-level class that has to do with bootstrapping and
   * making sure the nodes have the proper neighbors.
   * 
   * @see CtmRequestHandler
   * @see StructuredConnectionOverlord
   */

  public class Connector : TaskWorker
  {

    /*private static readonly log4net.ILog _log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/

    protected bool _is_finished;
    override public bool IsFinished {
      get {
        lock( _sync ) {
          return _is_finished;
        }
      }
    }

    protected Node _local_node;
    /**
     * The node who is making the Connection request
     */
    public Node Node { get { return _local_node; } }

    protected ArrayList _got_ctms;
    /**
     * Each received CTM is put on this array.  This is
     * so when the finish event is fired, we can see what
     * the received CTMs were
     */
    public ArrayList ReceivedCTMs { get { return _got_ctms; } }

    protected ConnectToMessage _ctm;
    /** Holds the ConnectToMessage whose response we are looking for */
    public ConnectToMessage Ctm { get { return _ctm; } }

    protected ConnectionOverlord _co;

    /**
     * Either a Node or an Edge to use to send the
     * ConnectToMessage packet
     */
    protected ISender _sender;
    /**
     * Is false until we get a response
     */
    protected bool _got_ctm;
    /**
     * We lock this when we need thread safety
     */
    protected object _sync;
    public Object SyncRoot { get { return _sync; } }
    
    /**
     * Represents the Task this connector works on for the TaskWorker
     */
    protected class ConnectorTask {
      protected ISender _ips;
      public ConnectorTask(ISender ps) {
        _ips = ps;
      }

      override public int GetHashCode() {
        return _ips.GetHashCode();
      }
      override public bool Equals(object o) {
        ConnectorTask ct = o as ConnectorTask;
        bool eq = false;
        if( ct != null ) {
          eq = ct._ips.Equals( _ips );
        }
        return eq;
      }
    }
    protected object _task;
    override public object Task { get { return _task; } }

    /**
     * Before a Connector goes to work, it optionally calls
     * this method to see if it is still needed.
     */
    public delegate bool AbortCheck(Connector c);
    protected AbortCheck _abort;
    public AbortCheck AbortIf {
      get {
        return _abort;
      }
      set {
        _abort = value;
      }
    }

    /**
     * @param local the local Node to connect to the remote node
     * @param eh EventHandler to call when we are finished.
     * @param ISender Use this specific edge.  This is used when we want to
     * connecto to a neighbor of a neighbor
     * @param ctm the ConnectToMessage which is serialized in the packet
     */
    public Connector(Node local, ISender ps, ConnectToMessage ctm, ConnectionOverlord co)
    {
      _sync = new Object();
      _local_node = local;
      _is_finished = false;

      _got_ctms = new ArrayList();
      _got_ctm = false;
      _sender = ps;
      _ctm = ctm;
      _co = co;
      _task = new ConnectorTask(ps);
    }

    override public void Start() {
      bool fire_finished = false;
      lock( _sync ) {
        if( _abort != null ) {
          if( _abort(this) ) {
            //We are no longer needed:
            _is_finished = true;
            fire_finished = true;
          }
        }
      }
      if( fire_finished ) {
        FireFinished();
        return;
      }
      RpcManager rpc = RpcManager.GetInstance(_local_node);

      BlockingQueue results = rpc.Invoke(_sender, "sys:ctm.ConnectTo", _ctm.ToHashtable() );
      results.EnqueueEvent += this.EnqueueHandler;
      results.CloseEvent += this.QueueCloseHandler;
      if( results.Count > 0 ) {
        //Make sure we didn't miss an enqueue between creating and registering
        //the handler:
        EnqueueHandler(results, EventArgs.Empty);
      }
      //This does nothing if the queue is not actually closed yet
      QueueCloseHandler(results, EventArgs.Empty);
    }

    /**
     * Try to get an RpcResult out and handle it
     */
    protected void EnqueueHandler(object queue, EventArgs arg) {
      BlockingQueue q = (BlockingQueue)queue;
      /*
       * Try for 10 ms to something out, there should be something
       * in there if this method is being called
       */
      bool timedout = true;
      RpcResult rpc_res = null;
      try {
        rpc_res = (RpcResult)q.Dequeue(10, out timedout);
        if( !timedout ) {
          ConnectToMessage new_ctm = new ConnectToMessage( (Hashtable)rpc_res.Result );
          _got_ctm = true;
          /**
           * It is the responsibilty of the ConnectionOverlord
           * to deal with this ctm
           */
          _got_ctms.Add(new_ctm);
          bool close_queue = _co.HandleCtmResponse(this, rpc_res.ResultSender, new_ctm);
          if( close_queue ) {
            q.Close();
          }
        }
      }
      catch(Exception) {
        //This can happen if the queue is empty and closed.  Don't do
        //anything.
        timedout = true;
      }
    }
    /**
     * When the RPC is finished, the BlockingQueue is closed, and we handle
     * it here
     */
    protected void QueueCloseHandler(object queue, EventArgs arg) {
      BlockingQueue bq = (BlockingQueue)queue;
      if( bq.Closed ) {
        /*
         * We're done
         */
        bool fire = false;
        lock( _sync ) {
          if( !_is_finished ) {
            _is_finished = true;
            fire = true;
          }
        }
        if(fire) {
          FireFinished();
        }
      }
    }
  }
}



