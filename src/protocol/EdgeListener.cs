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
