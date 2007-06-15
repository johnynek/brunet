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
   * This is an RPC handler for the following methods:
   *
   * Hashtable sys:link.Close(Hashtable)
   * Hashtable sys:link.GetStatus(Hashtable)
   * object sys:link.Ping(object)
   * Hashtable sys:link.Start(Hashtable)
   * 
   * @see Linker
   */

  public class ConnectionPacketHandler : ILinkLocker
  {

    /*private static readonly log4net.ILog log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/

    /**
     * This is the only stateful object here.  The rest
     * do not need thread synchronization.
     */
    protected readonly Hashtable _edge_to_cphstate;

    protected readonly Node _node;

    protected class CphState {
      public Edge Edge;
      public LinkMessage LM;
    }

    /** global lock for thread synchronization */
    protected readonly object _sync;
    /**
     * You should subscribe this to a Node, with the state being the node
     * it is subscribed to.  It can work for more than one node
     * simultaneously.
     */
    public ConnectionPacketHandler(Node n)
    {
      _sync = new object();
      _edge_to_cphstate = new Hashtable();
      _node = n;
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
     * Handle the notification that the other side is going to close the edge
     */
    public Hashtable Close(Hashtable close_message, ISender edge) {
#if LINK_DEBUG
      Console.Error.WriteLine("{0} -start- sys:link.Close({1},{2})", _node.Address, close_message,edge);
#endif
      Edge from = GetEdge(edge);
      ConnectionTable tab = _node.ConnectionTable;
      /**
       * In order to make sure that we close gracefully, we simply
       * move this edge to the unconnected list.  The node will
       * close edges that have been there for some time
       */
      tab.Disconnect(from);
      /** 
       * Release locks when the close message arrives; do not wait
       * until the edge actually closes.
       */
      CloseHandler(from, null);  
#if LINK_DEBUG
      Console.Error.WriteLine("{0} -end- sys:link.Close({1},{2})", _node.Address, close_message,from);
#endif
      return new Hashtable(1);
    }

    /**
     * Return a hashtable with entries:
     * self -> my Address
     * left -> Address of left neighbor
     * right -> Address of right neighbor
     *
     * If the node has any shortcuts:
     * shortcut -> Random shortcut connection
     */
    public Hashtable GetNeighbors(ISender caller) {
      AHAddress self = (AHAddress)_node.Address;
      Connection left = _node.ConnectionTable.GetLeftStructuredNeighborOf(self);
      Connection right = _node.ConnectionTable.GetRightStructuredNeighborOf(self);

      Hashtable result = new Hashtable(4);
      //Put it in:
      result["self"] = self.ToString();
      result["left"] = left.Address.ToString();
      result["right"] = right.Address.ToString();
      //Get a random shortcut:
      ArrayList shortcuts = new ArrayList();
      foreach(Connection c in _node.ConnectionTable.GetConnections("structured.shortcut") ) {
        shortcuts.Add(c);
      }
      if( shortcuts.Count > 0 ) {
        Random r = new Random();
        Connection sc = (Connection)shortcuts[ r.Next( shortcuts.Count ) ];
        result["shortcut"] = sc.Address.ToString();
      }
      return result;
    }

    /**
     * This starts a linking operation on the given edge
     */
    public Hashtable Start(Hashtable link_message, ISender edge) {
#if LINK_DEBUG
      Console.Error.WriteLine("{0} -start- sys:link.Start", _node.Address);
#endif
      Edge from = GetEdge(edge);
      LinkMessage lm = new LinkMessage(link_message);
#if LINK_DEBUG
      Console.Error.WriteLine("{0} -args- sys:link.Start({1},{2})", _node.Address,lm,from);
#endif
      ErrorMessage err = null;
      ConnectionTable tab = _node.ConnectionTable;
      lock( _sync ) {
        if( !_edge_to_cphstate.ContainsKey( from ) ) {
          if( CanConnect(lm, from, out err) ) {
            CphState cphstate = new CphState();
            cphstate.LM = lm;
            cphstate.Edge = from;
            //We can connect, add this LinkMessage to the table
            _edge_to_cphstate[from] = cphstate;
            from.CloseEvent += this.CloseHandler;
          }
        }
        else {
          throw new AdrException((int)ErrorMessage.ErrorCode.InProgress,
                                 "Already have a link in progress on this edge");
        }
      }
      //Now we prepare our response
      LinkMessage lm_resp = null;
      if( err == null ) {
        //We send a response:
	NodeInfo n_info = new NodeInfo( _node.Address, from.LocalTA );
	NodeInfo remote_info = new NodeInfo( null, from.RemoteTA );
	System.Collections.Specialized.StringDictionary attrs =
	        new System.Collections.Specialized.StringDictionary();
	attrs["type"] = lm.ConTypeString;
	attrs["realm"] = _node.Realm;
        lm_resp = new LinkMessage( attrs, n_info, remote_info );
      }
      else {
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
            RpcManager rpc = RpcManager.GetInstance(_node);
            rpc.Invoke(c.Edge, null, "sys:link.Ping", String.Empty);
          }
        }
      }
      if( err != null ) {
        throw new AdrException((int)err.Ec, err.Message);
      }
#if LINK_DEBUG
      Console.Error.WriteLine("{0} -end- sys:link.Start()->{1}", _node.Address,lm_resp);
#endif
      return lm_resp.ToHashtable();
    }

    /**
     * This returns the edge for this sender, if it can figure it out
     */
    protected Edge GetEdge(ISender s) {
      if( s is ReqrepManager.ReplyState ) {
        return GetEdge( ((ReqrepManager.ReplyState) s).ReturnPath );
      }
      else {
        return (Edge)s;
      }
    }
    /**
     * Get a StatusMessage for this node
     */
    public Hashtable GetStatus(Hashtable status_message, ISender edge) {
      //we just got s status request
      LinkMessage lm_to_add = null;
      StatusMessage sm = new StatusMessage(status_message);
      Edge from = GetEdge(edge);
#if LINK_DEBUG
      Console.Error.WriteLine("{0} -start- sys:link.GetStatus({1},{2})", _node.Address,sm,from);
#endif
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
      StatusMessage response = null;
      ConnectionTable tab = _node.ConnectionTable;
      if (lm_to_add != null) {
        //This is part of connection process:
        response = _node.GetStatus( sm.NeighborType, lm_to_add.Local.Address );
      } else {
        //This is just a "regular" status request
        //update our table:
        Address fadd = null;
        Connection c = tab.GetConnection(from);
        if( c != null ) {
          fadd = c.Address;
  	  tab.UpdateStatus(c, sm);
        }  
        response = _node.GetStatus( sm.NeighborType, fadd );
      }
      if( lm_to_add != null ) {
        Connection con = new Connection(from,
  		                      lm_to_add.Local.Address,
  				      lm_to_add.ConTypeString,
  				      sm,
  				      lm_to_add);
        tab.Add(con);
        //Unlock after we add the connection
        tab.Unlock(lm_to_add.Local.Address, lm_to_add.ConTypeString, this);
      }
#if LINK_DEBUG
      Console.Error.WriteLine("{0} -end- sys:link.GetStatus()->{1}", _node.Address,response);
#endif
      return response.ToHashtable();
    }

    /**
     * This just echos back the object passed to it
     */
    public object Ping(object o, ISender edge) {
#if LINK_DEBUG
      Console.Error.WriteLine("{0} sys:link.Ping({1},{2})", _node.Address,o,edge);
#endif
      return o;
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
    protected bool CanConnect(LinkMessage lm, Edge from, out ErrorMessage err)
    {
      ConnectionTable tab = _node.ConnectionTable;
      Address local_add = _node.Address;
      err = null;
      /* We lock the connection table so it doesn't change between
       * the call to Contains and the call to Lock
       */
      if( lm.Attributes["realm"] != _node.Realm ) {
        err = new ErrorMessage(ErrorMessage.ErrorCode.RealmMismatch,
                               "We are not in the same realm");
      }
      else if( (lm.Remote.Address != null ) && !local_add.Equals( lm.Remote.Address ) ) {
        /*
         * They are trying to reach a specific node, but it's not
         * us
         */
        err = new ErrorMessage(ErrorMessage.ErrorCode.TargetMismatch,
                               String.Format("target is {0}, but reached {1}",
                                             lm.Remote.Address, local_add));
      }
      else if( lm.Local.Address.Equals( local_add ) ) {
        //You are me!!!
        err = new ErrorMessage(ErrorMessage.ErrorCode.ConnectToSelf,
                               "You are me: ");
      }
      else {
        /*
         * Now we go to the ConnectionTable and try to
         * get a lock on the address so we can go forward
         * with the linking
         */
        try {
#if LINK_DEBUG
	    Console.Error.WriteLine("ConnectionPacketHandler - Trying to lock connection table: {0},{1}",
                                  lm.Local.Address, lm.ConTypeString);
#endif
          tab.Lock( lm.Local.Address, lm.ConTypeString, this );
#if LINK_DEBUG
	    Console.Error.WriteLine("ConnectionPacketHandler - Successfully locked connection table: {0},{1}",
                                  lm.Local.Address, lm.ConTypeString);
#endif
        }
        catch(ConnectionExistsException) {
          //We already have a connection of this type to this address
          err = new ErrorMessage(ErrorMessage.ErrorCode.AlreadyConnected,
                               String.Format("We are already connected: {0}", local_add));
        }
        catch(CTLockException) {
#if LINK_DEBUG
	    Console.Error.WriteLine("ConnectionPacketHandler - Cannot lock connection table: {0},{1}",
                                  lm.Local.Address, lm.ConTypeString);
#endif
          //Lock can throw this type of exception
          err = new ErrorMessage(ErrorMessage.ErrorCode.InProgress,
                                 "Address: " + lm.Local.Address.ToString() +
                                 " is locked");
        }
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
      lock(_sync) {
        CphState cphstate = (CphState)_edge_to_cphstate[edge];
        if( cphstate != null ) {
          _edge_to_cphstate.Remove(edge);
          lm = cphstate.LM;
        }
      }
      if( lm != null ) {
        ConnectionTable tab = _node.ConnectionTable;
        tab.Unlock( lm.Local.Address, lm.ConTypeString, this );
      }
    }
  }
}
