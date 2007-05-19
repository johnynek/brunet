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

//#define DEBUG

//#define LINK_DEBUG

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

  public class ConnectionPacketHandler : IDataHandler, ILinkLocker
  {

    /*private static readonly log4net.ILog log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/

    protected readonly ConnectionMessageParser _cmp;
    /**
     * This is the only stateful object here.  The rest
     * do not need thread synchronization.
     */
    protected readonly Hashtable _edge_to_cphstate;

    protected class CphState {
      public Edge Edge;
      public LinkMessage LM;
      public Node Local;
    }

    /** global lock for thread synchronization */
    protected readonly object _sync;
    /**
     * You should subscribe this to a Node, with the state being the node
     * it is subscribed to.  It can work for more than one node
     * simultaneously.
     */
    public ConnectionPacketHandler()
    {
      _sync = new object();
      lock(_sync) {
        _edge_to_cphstate = new Hashtable();
        _cmp = new ConnectionMessageParser();
      }
    }

    /**
     * Implement the ILockLinker interface.  These objects never
     * transfer locks since if they hold them, they must have received
     * a packet.  This means their locks are held only when connectivity
     * is guaranteed (unlike Linkers).
     */
    public bool AllowLockTransfer(Address a, string contype, ILinkLocker holder)
    {
      return false;
    }
    /**
     * When a ConnectionPacket comes to an Edge, this
     * method is called.
     *
     * It is a protected method so that it is clear
     * that it is connected to the Edge by the
     * ConnectionPacketHandler, and by no other object.
     */
    public void HandleData(MemBlock b, ISender ret_path, object state)
    {
      try {
        Node local = (Node)state;
        ConnectionTable tab = local.ConnectionTable;
        Address local_add = local.Address;

        ConnectionMessage cm = _cmp.Parse( b.ToMemoryStream() );
        Edge from = (Edge)ret_path;
        if (cm.Dir == ConnectionMessage.Direction.Request) {
          ConnectionMessage response = null;
	  if (cm is PingMessage) {
// #if LINK_DEBUG
// 	    Console.Error.WriteLine("ConnectionPacketHandler - Getting a ping request; edge: {0}; length: {1}",
//                               from, p.Length);
// #endif

	    /**
	     * Ping messages are just used to test that
	     * a node is still active.
	     */
            response = new PingMessage();
            response.Dir = ConnectionMessage.Direction.Response;
            response.Id = cm.Id;
// #if LINK_DEBUG
// 	    Console.Error.WriteLine("ConnectionPacketHandler - Sending a ping response; edge: {0};",
//                               from);
// #endif
            //log.Info("Sending Ping response:" + response.ToString());
            from.Send(response.ToPacket());
	  }
	  else if (cm is StatusMessage) {
#if LINK_DEBUG
	    Console.Error.WriteLine("ConnectionPacketHandler - Getting a status request; edge: {0}; length: {1} at: {2}",
                              from, p.Length, DateTime.Now);
#endif
	    //we just got s status request
	    LinkMessage lm_to_add = null;
            lock( _sync ) {
              if( _edge_to_cphstate.ContainsKey( from ) ) {
                //Add the connection:
                CphState cphstate = (CphState)_edge_to_cphstate[from];
                lm_to_add = cphstate.LM;
                //We can forget about this LinkMessage now:
                _edge_to_cphstate.Remove(from);
                from.CloseEvent -= this.CloseHandler;
              }
            }
            /**
            * StatusMessage objects are used to verify the completion
            * of the Link protocol.  If we receive a StatusMessage request
            * after we send a LinkMessage response, we know the other
            * Node got our LinkMessage response, and the connection
            * is active
            */
	    StatusMessage sm = (StatusMessage)cm;
	    if (lm_to_add != null) {
	      //This is part of connection process:
	      response = local.GetStatus( sm.NeighborType, lm_to_add.Local.Address );
	    } else {
	      //This is just a "regular" status request
	      //update our table:
	      Address fadd = null;
	      Connection c = tab.GetConnection(from);
	      if( c != null ) {
	        fadd = c.Address;
		tab.UpdateStatus(c, sm);
              }  
	      response = local.GetStatus( sm.NeighborType, fadd );
	    }
            response.Dir = ConnectionMessage.Direction.Response;
            response.Id = cm.Id;
#if LINK_DEBUG
	    Console.Error.WriteLine("ConnectionPacketHandler -  Sending status response: {0}; length: {1}, at: {2}", response, 
			      response.ToPacket().Length, DateTime.Now);
#endif
            from.Send(response.ToPacket());

            
            //Release the lock before calling this function:
            if( lm_to_add != null ) {
              /*Console.Error.WriteLine("About to add: {0},{1},{2}",
                                lm_to_add.ConTypeString,
				lm_to_add.Local.Address,
                                from );*/
	      Connection con = new Connection(from,
			                      lm_to_add.Local.Address,
					      lm_to_add.ConTypeString,
					      sm,
					      lm_to_add);
#if LINK_DEBUG
	      Console.Error.WriteLine("ConnectionPacketHandler - Creating a new connection: {0}, at: {1}", con, DateTime.Now);
#endif
	      tab.Add(con);
              //Unlock after we add the connection
              tab.Unlock(lm_to_add.Local.Address, lm_to_add.ConTypeString, this);
            }
          }
          else if (cm is CloseMessage) {
#if LINK_DEBUG
	    Console.Error.WriteLine("ConnectionPacketHandler - Getting a close request; edge: {0}; length: {1}, at: {2}",
                              from, p.Length, DateTime.Now);
#endif
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
            if( !tab.IsUnconnected(from) ) {
              tab.Disconnect(from);
            }
            /** 
             * Release locks when the close message arrives; do not wait
             * until the edge actually closes.
             */
            CloseHandler(from, null);
          }
          else if (cm is LinkMessage) {
#if LINK_DEBUG
	    Console.Error.WriteLine("ConnectionPacketHandler - Getting a link request; edge: {0}; length: {1} at: {2}",
                              from, p.Length, DateTime.Now);
#endif
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
              if( !_edge_to_cphstate.ContainsKey( from ) ) {
#if LINK_DEBUG
		Console.Error.WriteLine("ConnectionPacketHandler - Checking if can connect.");
#endif
                if( CanConnect(local, lm, from, out err) ) {
#if LINK_DEBUG
		  Console.Error.WriteLine("ConnectionPacketHandler - Yes we can connect connect.");
#endif
                  CphState cphstate = new CphState();
                  cphstate.LM = lm;
                  cphstate.Edge = from;
                  cphstate.Local = local;
                  //We can connect, add this LinkMessage to the table
                  _edge_to_cphstate[from] = cphstate;
                  from.CloseEvent += this.CloseHandler;
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
	      NodeInfo local_info = new NodeInfo( local_add, from.LocalTA );
	      NodeInfo remote_info = new NodeInfo( null, from.RemoteTA );
	      System.Collections.Specialized.StringDictionary attrs =
	        new System.Collections.Specialized.StringDictionary();
	      attrs["type"] = lm.ConTypeString;
	      attrs["realm"] = local.Realm;
              response = new LinkMessage( attrs, local_info, remote_info );
              response.Id = lm.Id;
              response.Dir = ConnectionMessage.Direction.Response;
#if LINK_DEBUG
	      Console.Error.WriteLine("ConnectionPacketHandler - Sending a link response on : {0} at {1}.", from, DateTime.Now);
#endif
            }
            else {
#if LINK_DEBUG
	      Console.Error.WriteLine("ConnectionPacketHandler - Sending an error response on: {0} at {1}.", from, DateTime.Now);
#endif
              response = err;
              if( err.Ec == ErrorMessage.ErrorCode.AlreadyConnected ) {
                /**
                 * When we send the ErrorCode.AlreadyConnected,
                 * we could have a stale connection, lets try pinging
                 * the other node, if they are there, but have lost
                 * the Edge, this may trigger the edge to close, causing
                 * us to remove the Connection.
                 * @todo consider putting this address on a "fast track"
                 * to removal if we don't hear from it soon
                 */
                Connection c = tab.GetConnection( lm.ConnectionType,
                                                   lm.Local.Address );
                if( c != null ) {
                  ConnectionMessage preq = new PingMessage();
                  preq.Dir = ConnectionMessage.Direction.Request;
                  preq.Id = 1;
                  try {
                    c.Edge.Send(preq.ToPacket());
                  }
                  catch(EdgeException) {
                    //This edge could close on us when we least expect it.
                    //if it does, it will throw an exception, we catch it.
                  }
                }
              }
            }
            //We know what we want to say, just send the response
            from.Send( response.ToPacket() );
          }
        }
        else {
          if (cm is StatusMessage) {
#if LINK_DEBUG
	    Console.Error.WriteLine("ConnectionPacketHandler - Getting a status message -- testlink-- edge: {0}; length: {1}",
                              from, p.Length);
#endif
            /**
             * Here we see if we should connect to any of these 
             * 
             * StatusMessage objects are used to verify the completion
             * of the Link protocol.  We also use it to exchange neighbor
             * lists.
             */
            
            StatusMessage sm = (StatusMessage)cm;
            Connection con = tab.GetConnection(from);
            tab.UpdateStatus(con,sm);
          }
        }
      }
      catch(Exception) {
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
     * @param local the Node we are working for
     * @param lm LinkMessage received from the other Node
     * @param from Edge that lm came from
     * @param err ErrorMessage to return.  Is null if there is no error
     * @return true if we can connect, if false, err != null
     */
    protected bool CanConnect(Node local, LinkMessage lm, Edge from, out ErrorMessage err)
    {
      ConnectionTable tab = local.ConnectionTable;
      Address local_add = local.Address;
      err = null;
      lock( tab.SyncRoot ) {

	if( lm.Attributes["realm"] != local.Realm ) {
          err = new ErrorMessage(ErrorMessage.ErrorCode.RealmMismatch,
			         "We are not in the same realm");
	}
        else if( (lm.Remote.Address != null )
                 && !local_add.Equals( lm.Remote.Address ) ) {
          /*
           * They are trying to reach a specific node, but it's not
           * us
           */
          err = new ErrorMessage(ErrorMessage.ErrorCode.TargetMismatch,
                                 String.Format("target is {0}, but reached {1}",
                                               lm.Remote.Address, local_add));
        }
	else if( tab.Contains( lm.ConnectionType, lm.Local.Address) ) {
          //We already have a connection of this type to this address
          err = new ErrorMessage(ErrorMessage.ErrorCode.AlreadyConnected,
                                 String.Format("We are already connected: {0}", local_add));
        }
        else if( lm.Local.Address.Equals( local_add ) ) {
          //You are me!!!
          err = new ErrorMessage(ErrorMessage.ErrorCode.ConnectToSelf,
                                 "You are me: ");
        }
        else {
          //Everything is looking good:
          try {
#if LINK_DEBUG
	    Console.Error.WriteLine("ConnectionPacketHandler - Trying to lock connection table: {0}", lm);
#endif
            tab.Lock( lm.Local.Address, lm.ConTypeString, this );
#if LINK_DEBUG
	    Console.Error.WriteLine("ConnectionPacketHandler - Successfully locked connection table: {0}", lm);
#endif
          }
          catch(InvalidOperationException) {
#if LINK_DEBUG
	    Console.Error.WriteLine("ConnectionPacketHandler - Cannot lock connection table: {0}", lm);
#endif
            //Lock can throw this type of exception
            err = new ErrorMessage(ErrorMessage.ErrorCode.InProgress,
                                   "Address: " + lm.Local.Address.ToString() +
                                   " is locked");
          }
        }
      } //We can release the lock on the ConnectionTable now

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
    public void CloseHandler(object edge, EventArgs args)
    {
      LinkMessage lm = null;
      ConnectionTable tab = null;
      lock(_sync) {
        if( _edge_to_cphstate.ContainsKey(edge) ) {
          CphState cphstate = (CphState)_edge_to_cphstate[edge];
          lm = cphstate.LM;
          tab = cphstate.Local.ConnectionTable;
          _edge_to_cphstate.Remove(edge);
        }
      }
      if( lm != null ) {
        tab.Unlock( lm.Local.Address, lm.ConTypeString, this );
      }
    }
  }
}
