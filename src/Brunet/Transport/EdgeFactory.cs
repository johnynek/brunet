/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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

/**
 * Dependencies : 
 * Brunet.Edge;
 * Brunet.TransportAddress;
 * Brunet.EdgeListener
 */

using System;
using System.Collections;

namespace Brunet.Transport
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
