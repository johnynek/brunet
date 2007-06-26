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

using Brunet;
using System;
using System.Net;
using System.Net.Sockets;
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

#if PLAB_LOG
    private BrunetLogger _logger;
    public BrunetLogger Logger{
	get{
	  return _logger;
	}
	set
	{
	  _logger = value;          
	}
    }
#endif
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
      if( EdgeEvent != null ) 
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
            list.Insert(0, ta);
          }
        }
      }
    }
  }
}
