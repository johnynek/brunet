/*
 * using Brunet.TransportAddress;
 * using Brunet.Edge;
 */

using Brunet;
using System;
using System.Collections;

namespace Brunet
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
    public abstract ArrayList LocalTAs
    {
      get;
    }

    /**
     * What type of TransportAddress does this EdgeListener use
     */
    public abstract Brunet.TransportAddress.TAType TAType
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
     * @param ta TransportAddress to create an edge to
     * @param ecb the EdgeCreationCallback to call when done
     * @throw EdgeException if we try to call this before calling
     * Start.
     */
    public abstract void CreateEdgeTo(TransportAddress ta, EdgeCreationCallback ecb);
    
    public event System.EventHandler EdgeEvent;

//This function sends the New Edge event
    protected void SendEdgeEvent(Edge e)
    {
      EdgeEvent(e, EventArgs.Empty);
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
    public abstract void Stop();
  }

}
