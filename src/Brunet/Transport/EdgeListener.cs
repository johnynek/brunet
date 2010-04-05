/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

using Brunet;
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;

namespace Brunet.Transport
{

  /**
   * Abstract class which represents the listers for
   * new edges.  When these listeners "hear" and edge,
   * they send the EdgeEvent
   * 
   * The EdgeListener also creates edges for its
   * own TransportAddress type.
   */

  public abstract class EdgeListener
  {
    protected TAAuthorizer _ta_auth;
    virtual public TAAuthorizer TAAuth {
      get { return _ta_auth; }
      set { _ta_auth = value; }
    }

    /**
     * @param success if the CreateEdgeTo was successful, this is true
     * @param e the newly created edge, if success is true, else e is null
     * @param x if success is false this may contain an exception
     */
    public delegate void EdgeCreationCallback(bool success, Edge e, Exception x);

    /**
     * A ReadOnly list of TransportAddress objects for
     * this EdgeListener
     */
    public abstract IEnumerable LocalTAs
    {
      get;
    }

      /**
       * What type of TransportAddress does this EdgeListener use
       */
    public abstract TransportAddress.TAType TAType
    {
      get;
    }

        /**
         * @return true if the Start method has been called
         */
    public abstract bool IsStarted
    {
      get;
    }
          /**
           * It is imperative that the ECB be called and not ignored.  Not
           * calling back will leave the system in a stale state as it waits
           * for the CreateEdgeTo to complete.
           * @param ta TransportAddress to create an edge to
           * @param ecb the EdgeCreationCallback to call when done
           * @throw EdgeException if we try to call this before calling
           */
    public abstract void CreateEdgeTo(TransportAddress ta, EdgeCreationCallback ecb);

    /**
     * When an incoming edge is created, this event is fired with the edge
     * as the Sender.
     */
    public event System.EventHandler EdgeEvent;

    /**
     * If you want to close all edges in some other thread,
     * handle this event.  If there is no handler for this
     * event, Edges are potentially closed inside EdgeListener threads
     * which can complicate thread synchronization.
     */
    public event System.EventHandler EdgeCloseRequestEvent;

    /**
     * This function sends the New Edge event
     */
    protected void SendEdgeEvent(Edge e)
    {
      EventHandler eh = EdgeEvent;
      if( eh != null ) {
        eh(e, EventArgs.Empty);
      }
    }

    protected void RequestClose(Edge e) {
      EventHandler eh = EdgeCloseRequestEvent;
      if( eh == null ) {
        //We have to close the edge:
        e.Close();
      }
      else {
        try {
          eh(this, new EdgeCloseRequestArgs(e));
        }
        catch(Exception x) {
          Console.Error.WriteLine("ERROR: closing: {0} -- {1}", e, x);
          e.Close();
        }
      }
    }

    /**
     * Start listening for edges.  Edges
     * received will be announced with the EdgeEvent
     * 
     * This must be called before CreateEdgeTo.
     */
    public abstract void Start();
    /**
     * Stop listening for edges.
     * The edgelistener may not be garbage collected
     * until this is called
     */
    public virtual void Stop() {
      /*
       * Hopefully we can get garbage collection moving
       * sooner.  Some EdgeListeners have threads that
       * won't stop immediately, so the idea is to make
       * sure they don't keep references to the node
       * around
       */
      EdgeCloseRequestEvent = null;
      EdgeEvent = null;
    }

    /**
     * When a new Connection is added, we may need to update the list
     * of TAs to make sure it is not too long, and that the it is sorted
     * from most likely to least likely to be successful
     * @param e the new Edge
     * @param ta the TransportAddress our TA according to our peer
     */
    public virtual void UpdateLocalTAs(Edge e, TransportAddress ta) {
    
    }
    /**
     * We learn RemotaTAs in case we need to get connected again in the
     * future.  These are TransportAddress objects that should be good
     * at some point in the future.
     * @param list the list of TransportAddress objects to update
     * @param e the new Edge
     * @param ta the TransportAddress the remote end of the
     * edge according to our peer
     */
    public virtual void UpdateRemoteTAs(IList list, Edge e, TransportAddress ta) {
      if( e.TAType == this.TAType ) {
        if( e.RemoteTANotEphemeral ) {
          //There is some chance this will be good again in the future
          //But, we only keep non-natted TAs, since NAT mappings change
          //so frequently, a NATed TA will probably be bad in the future
          if( ta.Equals( e.RemoteTA ) ) {
            //This node is not behind a NAT.
            int idx = list.IndexOf(ta);
            if( idx >= 0 ) {
              list.Remove(ta);
            }
            //Now put the i
            list.Insert(0, ta);
          }
        }
      }
    }

    public virtual int Count {
      get {
        throw new NotImplementedException();
      }
    }
  }

  /**
   * When an EdgeListener wants an edge to be closed, it uses an event
   * with this event args.
   */
  public class EdgeCloseRequestArgs : System.EventArgs {
    public readonly Edge Edge;
    public readonly string Reason;
    public EdgeCloseRequestArgs(Edge e) : this(e, String.Empty) { }
    public EdgeCloseRequestArgs(Edge e, string reason) {
      Edge = e;
      Reason = reason;
    }
  }
}
