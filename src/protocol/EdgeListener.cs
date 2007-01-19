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

    /**
     * Looks up the local IP addresses and returns a list
     * of transport Address objects which match them.
     * Loopback addresses will be at the end.
     *
     * Both UdpEdgeListener and TcpEdgeListener make use of this
     *
     * @todo it would be better to have a more precise method here
     * than using the DNS.  It may have to be platform specific,
     * and then fall back to the DNS technique.
     */
    static protected ArrayList GetIPTAs(TransportAddress.TAType tat, int port, 
					IPAddress[] ipList)
    {
      ArrayList tas = new ArrayList();
      IPAddress[] addressList = null;
      try {
	if (ipList == null || ipList.Length == 0) {
	  String StrLocalHost =  (Dns.GetHostName());
	  IPHostEntry IPEntry = Dns.GetHostByName (StrLocalHost);
	  addressList = IPEntry.AddressList;
	} else {
	  addressList = ipList;
	}

        foreach(IPAddress a in addressList) {
          /**
           * We add Loopback addresses to the back, all others to the front
           * This makes sure non-loopback addresses are listed first.
           */
          if( IPAddress.IsLoopback(a) ) {
            //Put it at the back
            tas.Add( new TransportAddress(tat, new IPEndPoint(a, port) ) );
          }
          else {
            //Put it at the front
            tas.Insert(0, new TransportAddress(tat, new IPEndPoint(a, port) ) );
          }
        }
      }
      catch(SocketException) {
        //If the hostname is not properly configured, we could wind
	//up here.  Just put the loopback address is:
        tas.Add( new TransportAddress(tat, new IPEndPoint(IPAddress.Loopback, port) ) );
      }
      return tas;
    }

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
            UpdateTA(list, ta);
          }
        }
      }
    }
    
    /*
     * Given a TA, this removes any TA from the list l which
     * matches the TAType and Host of ta.  Then it puts ta
     * at the top of the front.
     */
    protected void UpdateTA(IList l, TransportAddress ta) {
      
      ArrayList to_remove = new ArrayList();
      //Find the potential duplicates:
      foreach(TransportAddress tmp_ta in l) {
        if( tmp_ta.Host.Equals(ta.Host)
            && tmp_ta.TransportAddressType.Equals(ta.TransportAddressType) ) {
          to_remove.Add(tmp_ta);
        }
      }
      //Remove those:
      foreach(TransportAddress tmp_ta in to_remove) {
        l.Remove(tmp_ta);
      }
      //Put this at the front:
      l.Insert(0, ta);
    }
  }
}
