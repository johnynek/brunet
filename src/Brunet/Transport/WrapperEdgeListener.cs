/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Collections;
using System.Collections.Generic;

namespace Brunet.Transport {
  ///<summary>Wraps the underlying EdgeListener by listening for new edges and
  ///wrapping those edges.  This class is thread-safe.</summary>
  ///<remarks>This could be an abstract class, but it was fully implemented for
  ///testing purposes</summary>
  public class WrapperEdgeListener: EdgeListener {
    protected EdgeListener _el;
    protected object _sync;
    protected Dictionary<Edge, WrapperEdge> _edge_to_wrapper_edge;
    protected Dictionary<Edge, EdgeCreationWrapper> _edge_to_ecw;

    ///<summary>This is the underlying EdgeListener's state.  By default, we do
    ///not change this state.</summary>
    override public TAAuthorizer TAAuth {
      get {
        return _el.TAAuth;
      }
      set {
        _el.TAAuth = value;
      }
    }

    ///<summary>This is the underlying EdgeListener's state.  By default, we do
    ///not change this state.</summary>
    public override IEnumerable LocalTAs {
      get {
        return _el.LocalTAs;
      }
    }

    ///<summary>This is the underlying EdgeListener's state.  By default, we do
    ///not change this state.</summary>
    public override TransportAddress.TAType TAType {
      get {
        return _el.TAType;
      }
    }

    ///<summary>This is the underlying EdgeListener's state.  By default, we do
    ///not change this state.</summary>
    public override bool IsStarted {
      get {
        return _el.IsStarted;
      }
    }

    public WrapperEdgeListener(EdgeListener el) {
      _sync = new object();
      lock(_sync) {
        _el = el;
        _el.EdgeEvent += HandleEdgeEvent;
        _el.EdgeCloseRequestEvent += HandleEdgeCloseRequestEvent;
        _edge_to_wrapper_edge = new Dictionary<Edge, WrapperEdge>(); 
        _edge_to_ecw = new  Dictionary<Edge, EdgeCreationWrapper>();
      }
    }

    ///<summary>Returns the underlying edge count.</summary>
    public int UnderlyingCount {
      get {
        return _el.Count;
      }
    }

    public override int Count {
      get {
        return _edge_to_wrapper_edge.Count;
      }
    }

    ///<summary>A new edge has been created!  This comes from the underlying
    ///EL's SendEdgeEvent.</summary>
    protected void HandleEdgeEvent(object edge, EventArgs ea) {
      Edge e = edge as Edge;
      if(e == null) {
        throw new Exception("Not an Edge!");
      }
      EdgeCreationWrapper ecw = new EdgeCreationWrapper(null, SendEdgeEventHelper, e, this);
      AddEdge(ecw);
    }

    ///<summary>This is a EdgeCallback used when to handle the cases for
    ///inbound edes.  This executes SendEdgeEvent for this WEL.</summary>
    protected void SendEdgeEventHelper(bool success, Edge e, Exception x) {
      if(e != null) {
        SendEdgeEvent(e);
      }
    }

    ///<summary>This handles the RequestClose of the underlying edge.</summary>
    ///<remarks>
    /// The convoluted model for handling edge closes is this...
    /// I) UEL calls RequestClose
    ///  1) Triggers HandleEdgeCloseRequestEvent
    ///  2) If there is a wrapper edge, call RequestClose on it
    ///  3) If there isn't a wrapper edge, we are forced to simply close the edge and send a failed ecw
    /// II) A wrapper edge is responsible for calling edge close on the wrapped edge on edge close
    /// III) if an wrapped edge calls close, we are notified via an event, that will send a failed ecw or close the wrapper edge
    /// </remarks>
    protected void HandleEdgeCloseRequestEvent(object el, EventArgs ea) {
      EdgeCloseRequestArgs ecra = ea as EdgeCloseRequestArgs;
      if(ecra == null) {
        throw new Exception("Not an EdgeCloseRequestArgs!");
      }
      EdgeCreationWrapper ecw = null;
      Edge e = null;
      lock(_sync) {
        if(_edge_to_ecw.ContainsKey(ecra.Edge)) {
          ecw = _edge_to_ecw[ecra.Edge];
          _edge_to_ecw.Remove(ecra.Edge);
        } else if(_edge_to_wrapper_edge.ContainsKey(ecra.Edge)) {
          e = _edge_to_wrapper_edge[ecra.Edge];
          _edge_to_wrapper_edge.Remove(ecra.Edge);
        }
      }

      if(e != null) {
        RequestClose(e);
      } else {
        ecra.Edge.Close();
        if(ecw != null) {
          ecw.CreationCallback(false, null, new EdgeException("Requested close on an unwrapped edge."));
        }
      }
    }

    ///<summary>This wraps the underlying CreateEdgeTo using EdgeCreationWrappers</summary>
    public override void CreateEdgeTo(TransportAddress ta, EdgeCreationCallback ecb) {
      EdgeCreationWrapper ecw = new EdgeCreationWrapper(ta, ecb, null, this);
      _el.CreateEdgeTo(ta, ecw.Callback);
    }

    ///<summary>This is passed to the underlying EdgeListener.  By default we do
    ///nothing to this.</summary>
    public override void Start() {
      _el.Start();
    }

    ///<summary>This is passed to the underlying EdgeListener.  By default we do
    ///nothing to this.</summary>
    public override void Stop() {
      _el.Stop();
    }

    ///<summary>This is passed tothe underlying EdgeListener.  By default we do
    ///nothing to this.</summary>
    public override void UpdateLocalTAs(Edge e, TransportAddress ta) {
      WrapperEdge edge = e as WrapperEdge;
      if(edge != null) {
        _el.UpdateLocalTAs(edge.WrappedEdge, ta);
      }
    }
    
    ///<summary>This is passed tothe underlying EdgeListener.  By default we do
    ///nothing to this.</summary>
    public override void UpdateRemoteTAs(IList<TransportAddress> list, Edge e, TransportAddress ta) {
      WrapperEdge edge = e as WrapperEdge;
      if(edge != null) {
        _el.UpdateRemoteTAs(list, edge.WrappedEdge, ta);
      }
    }

    ///<summary>A new underlying edge has been created, this is used to add the
    ///edge to the EdgeListener for handling and to wrap the edge.</summary>
    public void AddEdge(EdgeCreationWrapper ecw) {
      Edge e = ecw.Edge;
      lock(_sync) {
        _edge_to_ecw[e] = ecw;
      }

      try {
        e.CloseEvent += EdgeClose;
      } catch {
        EdgeClose(e, null);
      }

      if(!e.IsClosed) {
        WrapEdge(ecw.Edge);
      }
    }

    ///<summary>This method is usd to wrap the edge.</summary>
    protected virtual void WrapEdge(Edge edge) {
      WrapperEdge wedge = new WrapperEdge(edge);
      Finalize(wedge);
    }

    ///<summary>When the edge is finally wrapped, this is called to finalize
    ///the adding of the edge to the edgelistner.</summary>
    protected void Finalize(WrapperEdge wedge) {
      EdgeCreationWrapper ecw = null;
      lock(_sync) {
        if(_edge_to_ecw.ContainsKey(wedge.WrappedEdge)) {
          ecw = _edge_to_ecw[wedge.WrappedEdge];
          _edge_to_ecw.Remove(wedge.WrappedEdge);
          _edge_to_wrapper_edge[wedge.WrappedEdge] = wedge;
        } else {
          throw new Exception("No record of edge");
        }
      }

      // if ecw is null, that means someone else failed before we announced
      // furthermore, we now have a wedge that needs to be manually closed!
      if(ecw != null) {
        ecw.CreationCallback(true, wedge, null);
      } else { 
        wedge.Close();
      }
    }

    ///<summary>This is called when one of our edges closes.  This handles
    ///removing the state from the EdgeListener as necessary.</summary>
    protected void EdgeClose(object o, EventArgs ea) {
      Edge e = o as Edge;
      if(e == null) {
        throw new Exception("Needs to be an Edge");
      }

      EdgeCreationWrapper ecw = null;
      WrapperEdge we = null;
      lock(_sync) {
        if(_edge_to_ecw.ContainsKey(e)) {
          ecw = _edge_to_ecw[e];
          _edge_to_ecw.Remove(e);
        } else if(_edge_to_wrapper_edge.ContainsKey(e)) {
          we = _edge_to_wrapper_edge[e];
          _edge_to_wrapper_edge.Remove(e);
        }
      }

      if(ecw != null) {
        ecw.CreationCallback(false, null, new EdgeException("Close on unwrapped edge!"));
      } else if(we != null) {
        we.Close();
      }
    }
  }

  ///<summary>Provides a wrapper for the creation of new Edges via ECB and EdgeEvent.</summary>
  public class EdgeCreationWrapper {
    public readonly EdgeListener.EdgeCreationCallback ExternalECB;
    public readonly TransportAddress TA;
    public readonly WrapperEdgeListener Parent;

    public bool Success { get { return _success; } }
    public Edge Edge { get { return _edge; } }
    public Exception Exception { get { return _exception; } }

    protected bool _success;
    protected Edge _edge;
    protected Exception _exception;
    protected int _called;

    public EdgeCreationWrapper(TransportAddress ta, EdgeListener.EdgeCreationCallback ecb,
        Edge edge, WrapperEdgeListener parent)
    {
      ExternalECB = ecb;
      TA = ta;
      Parent = parent;
      _edge = edge;
      _called = 0;
    }

    public void Callback(bool success, Edge e, Exception x) {
      _success = success;
      _edge = e;
      _exception = x;
      if(!success) {
        if(System.Threading.Interlocked.Exchange(ref _called, 1) == 1) {
          return;
        }
        ExternalECB(false, e, x);
      } else {
        Parent.AddEdge(this);
      }
    }

    public void CreationCallback(bool success, Edge e, Exception x) {
      if(!success && _edge != null) {
        _edge.Close();
      }
      if(System.Threading.Interlocked.Exchange(ref _called, 1) == 1) {
        return;
      }

      ExternalECB(success, e, x);
    }
  }
}
