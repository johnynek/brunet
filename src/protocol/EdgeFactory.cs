/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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
