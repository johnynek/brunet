/**
 * Dependencies : 
 * Brunet.Edge;
 * Brunet.TransportAddress;
 * Brunet.EdgeListener
 */

using System;
using System.Collections;

namespace Brunet
{

 /**
  * Creates an Edge using the TransportAddress.
  */

  public class EdgeFactory
  {

    public EdgeFactory()
    {
      _el_map = new Hashtable();
    }
	  
    /**
     * Keeps track of all the edgelisteners
     */
    protected Hashtable _el_map;

  /**
   * NOTE: This should only be called by the Node that
   * owns this EdgeFactory.  DO NOT ADD EdgeListener objects
   * to the EdgeFactory.  Add them through the Node, and it
   * will add them to its EdgeFactory.
   * @see Node
   * 
   * EdgeListener objects actually make the edges of a given
   * type.  In order to make an Edge of a given type, you need
   * to register an EdgeListener to do that job.
   */
    public void AddListener(EdgeListener el)
    {
      _el_map[ el.TAType ] = el;
    }

  /**
   * This creates Edges of a given type
   */
    public void CreateEdgeTo(TransportAddress destination,
		             EdgeListener.EdgeCreationCallback ecb)
    {
      TransportAddress.TAType t = destination.TransportAddressType;
      if( _el_map.Contains( t ) ) {
        EdgeListener el = (EdgeListener)_el_map[ t ];
	el.CreateEdgeTo( destination, ecb );
      }
      else {
        ecb(false, null, new EdgeException("No EdgeListener for TA type: " +
				           t.ToString() ) );
      }
    }
  }
}
