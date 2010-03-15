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

using Brunet.Util;
using Brunet.Concurrent;

using Brunet.Messaging;
namespace Brunet.Connections
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
    protected int _is_finished;
    override public bool IsFinished {
      get { return (_is_finished == 1); }
    }

    protected readonly Node _local_node;
    /**
     * The node who is making the Connection request
     */
    public Node Node { get { return _local_node; } }

    protected readonly ArrayList _got_ctms;
    /**
     * Each received CTM is put on this array.  This is
     * so when the finish event is fired, we can see what
     * the received CTMs were
     */
    public ArrayList ReceivedCTMs { get { return _got_ctms; } }

    protected readonly ConnectToMessage _ctm;
    /** Holds the ConnectToMessage whose response we are looking for */
    public ConnectToMessage Ctm { get { return _ctm; } }

    protected readonly ConnectionOverlord _co;

    /**
     * Either a Node or an Edge to use to send the
     * ConnectToMessage packet
     */
    protected readonly ISender _sender;
    protected readonly object _sync;
    public readonly object State;
    
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
    protected readonly object _task;
    override public object Task { get { return _task; } }

    /**
     * Before a Connector goes to work, it optionally calls
     * this method to see if it is still needed.
     */
    public delegate bool AbortCheck(Connector c);
    protected readonly WriteOnce<AbortCheck> _abort;
    public AbortCheck AbortIf {
      get {
        AbortCheck res;
        if( _abort.TryGet(out res) ) {
          return res;
        }
        else {
          return null;
        }
      }
      set {
        _abort.Value = value;
      }
    }

    /**
     * @param local the local Node to connect to the remote node
     * @param eh EventHandler to call when we are finished.
     * @param ISender Use this specific edge.  This is used when we want to
     * connecto to a neighbor of a neighbor
     * @param ctm the ConnectToMessage which is serialized in the packet
     */
    public Connector(Node local, ISender ps, ConnectToMessage ctm, ConnectionOverlord co):
      this(local, ps, ctm, co, null)
    {
    }

    public Connector(Node local, ISender ps, ConnectToMessage ctm, ConnectionOverlord co, object state)
    {
      _sync = new Object();
      _local_node = local;
      _is_finished = 0;
      _got_ctms = new ArrayList();
      _sender = ps;
      _ctm = ctm;
      _co = co;
      _task = new ConnectorTask(ps);
      _abort = new WriteOnce<AbortCheck>();
      State = state;
    }

    override public void Start() {
      ProtocolLog.WriteIf(ProtocolLog.LinkDebug,
          String.Format("{0}: Starting Connector: {1}, {2}",
            _local_node.Address, _sender, State));
      AbortCheck ac = _abort.Value;
      if( ac != null ) {
        if( ac(this) ) {
          //We are no longer needed:
          QueueCloseHandler(null, null);
          return;
        }
      }
      
      RpcManager rpc = RpcManager.GetInstance(_local_node);

      Channel results = new Channel();
      results.EnqueueEvent += this.EnqueueHandler;
      results.CloseEvent += this.QueueCloseHandler;
      rpc.Invoke(_sender, results, "sys:ctm.ConnectTo", _ctm.ToDictionary() );
    }

    /**
     * Try to get an RpcResult out and handle it
     */
    protected void EnqueueHandler(object queue, EventArgs arg) {
      Channel q = (Channel)queue;
      RpcResult rpc_res = null;
      try {
        rpc_res = (RpcResult)q.Dequeue();
        ConnectToMessage new_ctm = new ConnectToMessage( (IDictionary)rpc_res.Result );
        if(_local_node.Address.Equals(new_ctm.Target.Address)) {
          throw new Exception("Trying to connect to myself!");
        }
        lock( _sync ) {
        /**
         * It is the responsibilty of the ConnectionOverlord
         * to deal with this ctm
         */
          _got_ctms.Add(new_ctm);
	      }
        bool close_queue = _co.HandleCtmResponse(this, rpc_res.ResultSender, new_ctm);
        if( close_queue ) {
          q.Close();
        }
      }
      catch(Exception) {
        //This can happen if the queue is empty and closed.  Don't do
        //anything.
      }
    }
    /**
     * When the RPC is finished, the Channel is closed, and we handle
     * it here.  This method is only called once.
     */
    protected void QueueCloseHandler(object queue, EventArgs arg) {
      ProtocolLog.WriteIf(ProtocolLog.LinkDebug,
          String.Format("{0}: Connector Finished: {1}, {2}, results: {3}",
            _local_node.Address, _sender, State, _got_ctms.Count));
      System.Threading.Interlocked.Exchange(ref _is_finished, 1);
      FireFinished();
    }
  }
}



