/*
 * Dependencies : 
 * Brunet.Edge
 * Brunet.EdgeException
 * Brunet.EdgeListener;
 * Brunet.TransportAddress;
 * Brunet.FunctionEdge;
 */

using Brunet;
using System;
using System.Collections;

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

  public class FunctionEdgeListener:EdgeListener
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
    public override ArrayList LocalTAs
    {
      get
      {
        return ArrayList.ReadOnly(_tas);
      }
    }

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
      _listener_map[id] = this;
      _tas = new ArrayList();
      _tas.Add( new TransportAddress("brunet.function://localhost:" +
                                     _listener_id.ToString()) );
    }

    protected bool _is_started = false;
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
      }

      Edge e = null;
      if( ta.TransportAddressType == this.TAType ) {
        int remote_id = ta.Port;
        //Get the edgelistener:
        FunctionEdgeListener remote = (FunctionEdgeListener)
                                      _listener_map[remote_id];
        //Outbound edge:
        FunctionEdge fe_l = new FunctionEdge(_listener_id, false);
        //Inbound edge:
        FunctionEdge fe_r = new FunctionEdge(remote_id, true);
        fe_l.Partner = fe_r;
        fe_r.Partner = fe_l;
        remote.SendEdgeEvent(fe_r);
        //Here is the new edge:
        e = fe_l;

        // add this to the global edge table for step-by-step simulation


#if false
        System.Console.WriteLine("Adding New Pair of Edges:");
        System.Console.WriteLine("New Edge {0}", fe_l.ToString());
        System.Console.WriteLine("New Edge {0}", fe_r.ToString());
#endif
        FunctionEdge.edgeList.Add(fe_l);
        FunctionEdge.edgeList.Add(fe_r);

#if false
        System.Console.WriteLine("Press enter to continue.");
        System.Console.WriteLine("------------------------");
        System.Console.ReadLine();
#endif
        ecb(true, e, null);
      }
      else {
        //Can't make an edge of this type
        ecb(false, null, null);
      }
    }

    public override void Start()
    {
      _is_started = true;
    }

    public override void Stop()
    {
      _is_started = false;
    }

  }
}
