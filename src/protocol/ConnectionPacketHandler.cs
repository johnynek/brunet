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
 * Dependencies : 
 * Brunet.Address;
 * Brunet.AHPacket
 * Brunet.CloseMessage
 * Brunet.ConnectionEventArgs
 * Brunet.ConnectionPacket
 * Brunet.ConnectionMessage
 * Brunet.ConnectionMessageParser
 * Brunet.ConnectionType
 * Brunet.ConnectionTable
 * Brunet.Edge
 * Brunet.EdgeException
 * Brunet.LinkMessage
 * Brunet.Packet
 * Brunet.ParseException
 * Brunet.PingMessage
 * Brunet.TransportAddress
 * Brunet.ErrorMessage
 */

//#define DEBUG

//#define POB_LINK_DEBUG

using System;
using System.Collections;

namespace Brunet
{

  /**
   * ConnectionPacketHandler handles all the ConnectionPacket objects sent to
   * the Node.  This includes responding to pings and performing the "incoming"
   * side of the Link protocol.  The Linker performs the "outgoing" side of the
   * Link protocol.
   *
   * Since ConnectionPacketHandler only responds to ConnectionPacket objects
   * that it receives, it does not need any timing information.
   *
   * IMPORTANT: It is essential that the ConnectionPacketHandler is informed
   * about all unconnected edges.
   * 
   * Also, ConnectionPacketHandler never closes edges unless it is asked to
   * (with an appropriate CloseMessage).
   * @see Linker
   */

  public class ConnectionPacketHandler : IPacketHandler
  {

    /*private static readonly log4net.ILog log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/

    protected Address _local_add;
    protected ConnectionTable _tab;
    protected ConnectionMessageParser _cmp;
    /**
     * This is the only stateful object here.  The rest
     * do not need thread synchronization.
     */
    protected Hashtable _edge_to_lm;

    /** global lock for thread synchronization */
    protected object _sync;

    /**
     * @param add Address of the Node we work for
     * @param tab ConnectionTable to put new connections into
     * @param el EdgeListener to listen to new edges from
     */
    public ConnectionPacketHandler(Address add, ConnectionTable tab)
    {
      _sync = new object();
      lock(_sync) {
        _tab = tab;
        _local_add = add;
        _edge_to_lm = new Hashtable();
        _cmp = new ConnectionMessageParser();
        /*
         * Once a connection is made, we need to listen to the packets
         * on that connection for close and ping messages.
         */
        _tab.ConnectionEvent += new EventHandler(this.ConnectionHandler);
      }
    }

    /**
     * When a ConnectionPacket comes to an Edge, this
     * method is called.
     *
     * It is a protected method so that it is clear
     * that it is connected to the Edge by the
     * ConnectionPacketHandler, and by no other object.
     */
    public void HandlePacket(Packet p, Edge from)
    {
      try {
        ConnectionPacket packet = (ConnectionPacket)p;
        ConnectionMessage cm = _cmp.Parse(packet);
        if (cm.Dir == ConnectionMessage.Direction.Request) {
          ConnectionMessage response = null;
	  if (cm is PingMessage) {
	    /**
	     * Ping messages are just used to test that
	     * a node is still active.
	     */
            response = new PingMessage();
            response.Dir = ConnectionMessage.Direction.Response;
            response.Id = cm.Id;
            //log.Info("Sending Ping response:" + response.ToString());
            from.Send(response.ToPacket());
	  }
	  else if (cm is StatusMessage) {
            /**
            * StatusMessage objects are used to verify the completion
            * of the Link protocol.  If we receive a StatusMessage request
            * after we send a LinkMessage response, we know the other
            * Node got our LinkMessage response, and the connection
            * is active
            */
	    StatusMessage sm = (StatusMessage)cm;
	    ArrayList neighbors = new ArrayList();
	    //Get the neighbors of this type:
	    lock( _tab.SyncRoot ) {
	      /*
	       * Send the whole list of connections of this type.
	       * @todo make sure we don't send more than will
	       * fit in a packet.
	       */
	      ConnectionType ct = Connection.StringToMainType( sm.NeighborType );
	      foreach(Connection c in _tab.GetConnections( ct ) ) {
                neighbors.Add( new NodeInfo( c.Address, c.Edge.RemoteTA ) );
	      }
	    }
	    response = new StatusMessage(sm.NeighborType, neighbors);
            response.Dir = ConnectionMessage.Direction.Response;
            response.Id = cm.Id;
            //log.Info("Sending Status response:" + response.ToString());
            from.Send(response.ToPacket());

            LinkMessage lm_to_add = null;
            lock( _sync ) {
              if( _edge_to_lm.ContainsKey( from ) ) {
                //Add the connection:
                lm_to_add = (LinkMessage)_edge_to_lm[from];
                //We can forget about this LinkMessage now:
                _edge_to_lm.Remove(from);
              }
            }
            //Release the lock before calling this function:
            if( lm_to_add != null ) {
              Console.WriteLine("About to add: {0},{1},{2}",
                                lm_to_add.ConTypeString,
				lm_to_add.Local.Address,
                                from );
	      Connection con = new Connection(from,
			                      lm_to_add.Local.Address,
					      lm_to_add.ConTypeString,
					      sm);
	      _tab.Add(con);
              //Unlock after we add the connection
              _tab.Unlock(lm_to_add.Local.Address, lm_to_add.ConnectionType, this);
            }
          }
          else if (cm is CloseMessage) {
            /**
             * Only Close an Edge when explicitly told
             * to do so.  ConnectionPacketHandler never
             * does so as a result of a timeout
             */
            response = new CloseMessage();
            response.Dir = ConnectionMessage.Direction.Response;
            response.Id = cm.Id;
            from.Send(response.ToPacket());
            /**
             * In order to make sure that we close gracefully, we simply
             * move this edge to the unconnected list.  The node will
             * close edges that have been there for some time
             */
            if( !_tab.IsUnconnected(from) ) {
              _tab.Disconnect(from);
            }
          }
          else if (cm is LinkMessage) {
            /**
            * When we get a LinkMessage there are three cases:
            *
            * 1) It is a LinkMessage from an edge that is starting a new Link
            * 2) It is a duplicate LinkMessage (due to packet duplication or error).
            *
            * @todo If the LinkMessage is not the same, send an error response
            * We must lock the address in case 1), but not in case 2).
            */
            LinkMessage lm = (LinkMessage)cm;
            ErrorMessage err = null;
            lock( _sync ) {
              if( !_edge_to_lm.ContainsKey( from ) ) {

                if( CanConnect(lm, from, out err) ) {
                  //We can connect, add this LinkMessage to the table
                  _edge_to_lm[from] = lm;
                }
                //We send a response after we drop the lock on _sync
              }
              else {
                //Case 2: we have seen a LinkMessage from this edge
                /**
                * @todo what if this LinkMessage is different than the previous?
                */
              }
            }
            //Now we prepare our response
            if( err == null ) {
              //We send a response:
	      NodeInfo local_info = new NodeInfo( _local_add, from.LocalTA );
	      NodeInfo remote_info = new NodeInfo( null, from.RemoteTA );
              response = new LinkMessage( lm.ConTypeString, local_info, remote_info );
              response.Id = lm.Id;
              response.Dir = ConnectionMessage.Direction.Response;
            }
            else {
              response = err;
            }
            //We know what we want to say, just send the response
            from.Send( response.ToPacket() );
          }
        }
        else {
          /* This is a response, we should never get it */
          /*log.Error("Got an unexpected ConnectionMessage response"
            + cm.ToString());*/
        }
      }
      catch(Exception x) {
        /* Just don't do anything */
        //log.Error("HandlePacket exception", x);
      }
    }

    /**
     * When we get a new link message from an edge, we must
     * check several conditions to see if we can proceed with
     * the Link protocol.
     * This function checks those conditions and returns true
     * if we can proceed.
     * If we cannot proceed, it gives an ErrorMessage to send
     * back to the other side.
     * @param lm LinkMessage received from the other Node
     * @param from Edge that lm came from
     * @param err ErrorMessage to return.  Is null if there is no error
     * @return true if we can connect, if false, err != null
     */
    protected bool CanConnect(LinkMessage lm, Edge from, out ErrorMessage err)
    {
      err = null;
      ConnectionType ct = ConnectionType.Unknown;
      bool have_con = false;
      lock( _tab.SyncRoot ) {

        if( _tab.Contains( lm.ConnectionType, lm.Local.Address) ) {
          //We already have a connection of this type to this address
          err = new ErrorMessage(ErrorMessage.ErrorCode.AlreadyConnected,
                                 "We are already connected");
        }
        else if( lm.Local.Address.Equals( _local_add ) ) {
          //You are me!!!
          err = new ErrorMessage(ErrorMessage.ErrorCode.ConnectToSelf,
                                 "You are me");
        }
        else {
          //Everything is looking good:
          try {
            _tab.Lock( lm.Local.Address, lm.ConnectionType, this );
            int index;
            Address add;
            have_con = _tab.GetConnection( from, out ct, out index, out add );
          }
          catch(InvalidOperationException iox) {
            //Lock can throw this type of exception
            err = new ErrorMessage(ErrorMessage.ErrorCode.InProgress,
                                   "Address: " + lm.Local.Address.ToString() +
                                   " is locked");
          }
        }
      } //We can release the lock on the ConnectionTable now
      if( have_con ) {
	 ///@todo remove Edge promotion.  This is a dead concept.
        //We have some kind of connection on this edge
        if( lm.ConnectionType == ConnectionType.Structured ) {
          /**
           * Everything else looks good.
           * We are promoting to a StructuredConnection.
           *
           * Steps:
           * 1) Disconnect the existing connection
           * 2) connect as usual
           */
          _tab.Disconnect(from);
        }
        else {
          //We need to release the lock we have:
          _tab.Unlock( lm.Local.Address, lm.ConnectionType, this );
          //Cannot promote to types other that structured:
          err = new ErrorMessage(ErrorMessage.ErrorCode.AlreadyConnected,
                                 "Cannot promote edge to: "
                                 + lm.ConnectionType.ToString() );
        }
      }

      /*
       * We have now checked all the error conditions, go
       * forward if there was not an error
       */
      if( err != null ) {
        err.Id = lm.Id;
        err.Dir = ConnectionMessage.Direction.Response;
      }
      return ( err == null );
    }

    /**
     * When an Edge closes, we must remove it from out
     * Hashtable.
     * @param edge the Edge that closed
     */
    protected void CloseHandler(object edge, EventArgs args)
    {
      LinkMessage lm = null;
      lock(_sync) {
        if( _edge_to_lm.ContainsKey(edge) ) {
          lm = (LinkMessage)_edge_to_lm[edge];
          _edge_to_lm.Remove(edge);
        }
      }
      if( lm != null ) {
        _tab.Unlock( lm.Local.Address, lm.ConnectionType, this );
      }
    }

    /**
     * This Handler should be connected to incoming EdgeEvent
     * events.  If it is not, it cannot hear the new edges.
     *
     * When a new edge is created, we make sure we can hear
     * the packets from it.  Also, we make sure we can hear
     * the CloseEvent.
     *
     * @param edge the new Edge
     */
    public void EdgeHandler(object edge, EventArgs args)
    {
      Edge e = (Edge)edge;
      e.SetCallback(Packet.ProtType.Connection, this);
      e.CloseEvent += new EventHandler(this.CloseHandler);
      _tab.AddUnconnected(e);
    }

    /**
     * When there is a new connection happens, this is
     * how we find out about it.  We need to handle the
     * connection packets on this edge.
     */
    protected void ConnectionHandler(object connectiontable,
                                     EventArgs args)
    {
      Edge e = ((ConnectionEventArgs)args).Edge;
      e.SetCallback(Packet.ProtType.Connection, this);
      e.CloseEvent += new EventHandler(this.CloseHandler);
    }
  }
}
