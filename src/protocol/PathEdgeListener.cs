/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2008 P. Oscar Boykin <boykin@pobox.com>,  University of Florida

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

namespace Brunet {

  /** Manages the PathEdgeListener objects for multiple Nodes
   *
   * Here's how to use this class:
   *  //Do this once for all the nodes you want to share the EL for:
   *   EdgeListener el = new UdpEdgeListener(port);
   *   PType path_p = PType.Protocol.Pathing;
   *   PathELManager pem = new PathELManager(el);
   *   pem.Start();
   *
   *   //For each node, do this to create a new PathEdgeListener
   *   //In this example, we used "/tmp_node_path" as the path, you can use any string.
   *   tmp_node.DemuxHandler.GetTypeSource(path_p).Subscribe(pem, path_p);
   *   tmp_node.AddEdgeListener(pem.CreatePath("/tmp_node_path"));
   */
  public class PathELManager : IDataHandler, IRpcHandler {
    
    //Properties and Variables
    readonly object _sync;
    readonly EdgeListener _el;
    readonly List<Edge> _edges;
    readonly Dictionary<string, PathEdgeListener> _pel_map;
    readonly Dictionary<Edge, PathEdge> _unannounced;

    //Here's how we handle the protocol:
    readonly ReqrepManager _rrm;
    readonly Thread _timer_thread;
    public readonly RpcManager Rpc;
    volatile bool _running;

    //Methods:

    /** Multiplex an EdgeListener using Pathing
     * @param el the EdgeListener to multiplex
     */
    public PathELManager(EdgeListener el) {
      _el = el;
      _sync = new object();
      _edges = new List<Edge>();
      _unannounced = new Dictionary<Edge, PathEdge>();
      _pel_map = new Dictionary<string, PathEdgeListener>();
      //Use the reqrep protocol with a special prefix:
      _rrm = new ReqrepManager("PathELManager", PType.Protocol.Pathing);
      _rrm.Subscribe(this, null);
      Rpc = new RpcManager(_rrm);
      Rpc.AddHandler("sys:pathing", this);
      _el.EdgeEvent += HandleEdge;
      _running = true;
      _timer_thread = new Thread(
        delegate() {
          while(_running) {
            Thread.Sleep(1000);
            _rrm.TimeoutChecker(null, null);
          }
        }
      );
      _timer_thread.IsBackground = true;
      _timer_thread.Start();
    }

    /** create a new PathEdgeListener
     */
    public PathEdgeListener CreatePath(string path) {
      PathEdgeListener new_pel = null;
      if( path[0] != '/') {
        path = String.Format("/{0}",path);
      }
      lock( _sync ) {
        //Make sure the path doesn't already exist,
        if( _pel_map.ContainsKey(path) ) {
          throw new Exception("Path already exists");
        }
        else {
          new_pel = new PathEdgeListener(this, path, _el);
          _pel_map[path] = new_pel;
        }
      }
      return new_pel;
    }

    /** Handle incoming data on an Edge 
     */
    public void HandleData(MemBlock data, ISender retpath, object state) {
      MemBlock rest_of_data;
      PType p;
      if( state == null ) {
        p = PType.Parse(data, out rest_of_data);
      }
      else {
        //a demux has already happened:
        p = (PType)state;
        rest_of_data = data;
      }
      if( PType.Protocol.Pathing.Equals(p) ) {
        /*
         * We use a special PType to denote this transaction so
         * we don't confuse it with other RepRep communication
         */
        _rrm.HandleData(rest_of_data, retpath, null);
      }
      else if( PType.Protocol.Rpc.Equals(p) ) {
       /*
        * Send this to the RpcHandler
        */
       Rpc.HandleData(rest_of_data, retpath, null);
      }
      else {
        /*
         * This is some other data
         * It is either:
         * 1) Time to announce an already created edge.
         * 2) Assume this is a "default path" edge creation, to be backwards
         * compatible
         */
        Edge e = null;
        PathEdge pe = null;
        try {
          e = (Edge)retpath;
          PathEdgeListener pel = null;
          lock( _sync ) {
            if( _unannounced.TryGetValue(e, out pe) ) {
              //
              _unannounced.Remove(e);
              pel = _pel_map[pe.LocalPath];
            }
          }
          if( pe == null ) {
            /*
             * This must be a "default path" incoming connection
             */
            pel = _pel_map["/"];
            pe = new PathEdge(e, "/", "/");
          }
          pel.SendPathEdgeEvent(pe);
          pe.Subscribe();
          pe.ReceivedPacketEvent(data);
        }
        catch(Exception x) {
          if( pe != null ) {
            //This closes both edges:
            pe.Close();  
          }
          else if( e != null ) {
            Console.WriteLine("Closing ({0}) due to: {1}", e, x);
            e.Close();  
          }
        }
      }
    }

    /** Join a path to the end of a TransportAddress
     */
    public static TransportAddress JoinPath(TransportAddress ta, string path) {
      Uri orig_u = ta.Uri;
      string s = orig_u.ToString();
      if( s[s.Length - 1] == '/' ) {
        s = s.Substring(0, s.Length - 1);
      }
      if (path[0] == '/') {
        path = path.Substring(1);
      }
      return TransportAddressFactory.CreateInstance(String.Format("{0}/{1}", s, path));
    }

    /** return the base TransportAddress and the path associated with it
     */
    public static TransportAddress SplitPath(TransportAddress ta, out string path) {
      Uri orig_u = ta.Uri;
      path = orig_u.AbsolutePath;
      string base_uri = String.Format("{0}://{1}", orig_u.Scheme, orig_u.Authority);
      return TransportAddressFactory.CreateInstance(base_uri);
    }

    /** Start the underlying EdgeListener and start processing edges
     */
    public void Start() { _el.Start(); }
    /*
     * Stop the underlying EdgeListener.  This is important to stop any
     * thread and resources that might be allocated by that EdgeListener.
     */
    public void Stop() {
      _running = false;
      _el.Stop();
   }

    /** Watch this incoming Edge
     */
    protected void HandleEdge(object newedge, System.EventArgs args) {
      Edge e = (Edge)newedge;
      try {
        e.CloseEvent += this.HandleEdgeClose;
        e.Subscribe(this, null);
        lock( _sync ) { 
          _edges.Add(e);
        }
      }
      catch(Exception x) {
        //Didn't work out, make sure the edges is closed
        Console.WriteLine("Closing ({0}) due to: {1}", e, x);
        e.Close();
      }
    }

    protected void HandleEdgeClose(object closing_edge, System.EventArgs args) {
      lock( _sync ) {
        Edge e = (Edge)closing_edge;
        _edges.Remove(e);
      }
    }

    public void HandleRpc(ISender caller, string meth, IList args, object state) {
      if( meth == "create" ) {
        Edge calling_edge = (Edge)((ReqrepManager.ReplyState)caller).ReturnPath;
        string remote_path = (string)args[0];
        string local_path = (string)args[1];
        PathEdgeListener el = _pel_map[local_path];
        if( el.IsStarted ) {
          PathEdge npe = new PathEdge(calling_edge, local_path, remote_path);
          lock( _sync ) {
            //We don't announce yet, wait till we get some data, which
            //verifies that the other side has seen it.
            _unannounced[calling_edge] = npe;
          }
          //So the new Edge has been announced.
          Rpc.SendResult(state, true);
        }
        else {
          throw new Exception(
             String.Format("PathEdgeListener({0}) not started", local_path));
        }
      }
      else {
        throw new AdrException(-32601, "No Handler for method: " + meth);
      }
    }

  }

  /** Class to wrap underlying EdgeListeners with the Pathing protocol
   */
  public class PathEdgeListener : EdgeListener {

    readonly string _path;
    readonly EdgeListener _el;
    readonly PathELManager _pem;
    int _is_started;
    
    public PathEdgeListener(PathELManager pem, string path, EdgeListener el) {
      _path = path;
      _el = el;
      _pem = pem;
      _is_started = 0;
    }

    public override IEnumerable LocalTAs {
      get {
        List<TransportAddress> ltas = new List<TransportAddress>();
        foreach(TransportAddress ta in _el.LocalTAs) {
          ltas.Add( PathELManager.JoinPath(ta, _path) );
        }
        return ltas;
      }
    }

    public override TransportAddress.TAType TAType { get { return _el.TAType; } }

    public override bool IsStarted {
      get { return 1 == _is_started; }
    }

    protected class CreateState {
      public readonly string RemotePath;
      public readonly string LocalPath;
      public readonly EdgeListener.EdgeCreationCallback ECB;  

      readonly PathEdgeListener _pel;

      public CreateState(PathEdgeListener pel, string rem, string loc,
                         EdgeListener.EdgeCreationCallback ecb) {
        _pel = pel;
        RemotePath = rem;
        LocalPath = loc;
        ECB = ecb;
      }

      public void HandleEC(bool succ, Edge e, Exception x) {
        if( succ ) {
          /*
           * Got the underlying Edge, now do the path protocol
           */ 
          Channel results = new Channel(1);
          results.CloseEvent += delegate(object q, EventArgs args) {
            try {
              RpcResult res = (RpcResult)results.Dequeue();
              object o = res.Result;
              //If we get here, everything looks good:
              PathEdge pe = new PathEdge(e, LocalPath, RemotePath);
              //Start sending e's packets into pe
              pe.Subscribe();
              ECB(true, pe, null);
            }
            catch(Exception cx) {
              ECB(false, null, cx);
            }
          };
          //Make sure we hear the packets on this edge:
          e.Subscribe(_pel._pem, null);
          //Now make the rpc call:
          _pel._pem.Rpc.Invoke(e, results, "sys:pathing.create", LocalPath, RemotePath ); 
        }
        else {
          ECB(false, null, x);
        }
      }
    }
    /** creates a new outgoing Edge using the pathing protocol
     */
    public override void CreateEdgeTo(TransportAddress ta,
                                      EdgeListener.EdgeCreationCallback ecb) {
      if( !IsStarted ) {
        throw new EdgeException("PathEdgeListener is not started");
      }
      string rempath;
      TransportAddress base_ta = PathELManager.SplitPath(ta, out rempath);
      if( _path == "/" && rempath == "/" ) {
        /*
         * This is "normal" case, and we can skip all this stuff
         */
        _el.CreateEdgeTo(ta, ecb);
      }
      else {
        CreateState cs = new CreateState(this, rempath, _path, ecb);
        /*
         * Make the underlying Edge:
         */
        _el.CreateEdgeTo(base_ta, cs.HandleEC);
      }
    }

    public override void Start() {
      if( 0 == Interlocked.Exchange(ref _is_started, 1) ) {
      
      }
      else {
        throw new Exception("Can only call PathEdgeListener.Start() once!");
      }
    }

    public override void Stop() {
      if( 1 == Interlocked.Exchange(ref _is_started, 0) ) {
        //Actually stopped this time.
      }
    }

    /** try to create a new PathEdge and send the EdgeEvent
     */
    public void SendPathEdgeEvent(PathEdge pe) {
      if( 1 == _is_started ) {
        SendEdgeEvent(pe);
      }
      else {
        throw new Exception(
           String.Format("PathEdgeListener{0} not yet started", _path));
      }
    }
  }


  public class PathEdge : Edge, IDataHandler {
    readonly Edge _e;
    public readonly string LocalPath;
    public readonly string RemotePath;

    public PathEdge(Edge e, string local_path, string remote_path)
       : base(null, e.IsInbound) {
      _e = e;
      LocalPath = local_path;
      RemotePath = remote_path;
      //Make sure if the edge closes we also close
      _e.CloseEvent += this.HandleUnderClose;
    }

    public override bool Close() {
      _e.Close();
      return base.Close();
    }

    /*
     * Handle the data from our underlying edge
     */
    public void HandleData(MemBlock b, ISender ret, object state) {
      ReceivedPacketEvent(b);
    }

    public override TransportAddress LocalTA {
      get { return PathELManager.JoinPath(_e.LocalTA, LocalPath); }
    }
    public override bool LocalTANotEphemeral {
      get { return _e.LocalTANotEphemeral; }
    }

    public override TransportAddress RemoteTA {
      get { return PathELManager.JoinPath(_e.RemoteTA, RemotePath); }
    }
    
    public override bool RemoteTANotEphemeral {
      get { return _e.RemoteTANotEphemeral; }
    }
    
    public override TransportAddress.TAType TAType {
      get { return _e.TAType; } 
    }

    public override void Send(ICopyable p) {
      _e.Send(p);
      Interlocked.Exchange(ref _last_out_packet_datetime, DateTime.UtcNow.Ticks);
    }

    public void Subscribe() {
      _e.Subscribe(this, null);
    }

    protected void HandleUnderClose(object edge, EventArgs args) {
      this.Close();
    }
  }
}
