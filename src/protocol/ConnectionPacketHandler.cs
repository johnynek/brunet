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

using System;
using System.Collections;
using System.Collections.Specialized;

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
   * IDictionary sys:link.Close(IDictionary)
   * IDictionary sys:link.GetStatus(IDictionary)
   * object sys:link.Ping(object)
   * IDictionary sys:link.Start(IDictionary)
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
    protected readonly ListDictionary _to_close;

    //This is true when the node starts to disconnect
    protected volatile bool _disconnecting;
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
      _node.HeartBeatEvent += this.DelayedCloseHandler;
      _to_close = new ListDictionary();
      _disconnecting = false;
      //When Disconnect is called, set disconnecting to true, disallowing new
      //connections.
      _node.DepartureEvent += delegate(object o, EventArgs a) {
        _disconnecting = true;
      };
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
    public IDictionary Close(IDictionary close_message, ISender edge) {
      if(ProtocolLog.LinkDebug.Enabled)
        ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
          "{0} -start- sys:link.Close({1},{2})", _node.Address, close_message,edge));
      Edge from = GetEdge(edge);
      ConnectionTable tab = _node.ConnectionTable;
      /**
       * In order to make sure that we close gracefully, we simply
       * move this edge to the unconnected list.  The node will
       * close edges that have been there for some time
       */
      Connection c = tab.GetConnection(from);
      if(ProtocolLog.LinkDebug.Enabled)
        ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
          "sys:link.Close on {0} connection: {1}", from, c));
      tab.Disconnect(from);
      /** 
       * Release locks when the close message arrives; do not wait
       * until the edge actually closes.
       */
      CloseHandler(from, null);  
      if(ProtocolLog.LinkDebug.Enabled)
        ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
          "{0} -end- sys:link.Close({1},{2})", _node.Address, close_message,from));

      /**
       * Try to close the edge after a small time span:
       */
      DateTime to_close = DateTime.UtcNow + new TimeSpan(0,0,0,5,0);
      lock( _sync ) {
        _to_close[from] = to_close;
      }
      return new ListDictionary();
    }

    /**
     * This method looks to see if there are any edges we need to
     * try to close.  We try this after the other side has asked
     * up to close an edge (since we don't know if they got our
     * response or not).
     */
    protected void DelayedCloseHandler(object n, EventArgs a) {
      ArrayList l = null;
      lock( _sync ) {
        if( _to_close.Count == 0 ) { return; }
        l = new ArrayList( _to_close.Count );
        DateTime now = DateTime.UtcNow;
        foreach(DictionaryEntry de in _to_close) {
          DateTime to_close_date = (DateTime)de.Value;
          Edge e = (Edge)de.Key;
          if( now > to_close_date ) {
            l.Add(e);
          }
        }
        foreach(object e in l) {
          _to_close.Remove(e);
        }
      }
      foreach(Edge e in l) {
        _node.GracefullyClose(e);
      }
    }

    /**
     * Return an IDictionary with entries:
     * self -> my Address
     * left -> Address of left neighbor
     * right -> Address of right neighbor
     * left2 -> Second left neighbor
     * right2 -> Second right neighbor
     *
     * If the node has any shortcuts:
     * shortcut -> Random shortcut connection
     */
    public IDictionary GetNeighbors(ISender caller) {
      AHAddress self = (AHAddress)_node.Address;
      IDictionary result = new ListDictionary();
      //Put it in:
      result["self"] = self.ToString();
      
      Connection left = _node.ConnectionTable.GetLeftStructuredNeighborOf(self);
      if( left != null ) {
        AHAddress la = (AHAddress)left.Address;
        result["left"] = la.ToString();
        Connection left2 = _node.ConnectionTable.GetLeftStructuredNeighborOf(la);
        if( left2 != null ) {
          result["left2"] = left2.Address.ToString();
        }
      }
      Connection right = _node.ConnectionTable.GetRightStructuredNeighborOf(self);
      if( right != null ) {
        AHAddress ra = (AHAddress)right.Address;
        result["right"] = ra.ToString();
        Connection right2 = _node.ConnectionTable.GetRightStructuredNeighborOf(ra);
        if( right2 != null ) {
          result["right2"] = right2.Address.ToString();
        }
      }
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
    public IDictionary Start(IDictionary link_message, ISender edge) {
      if(ProtocolLog.LinkDebug.Enabled)
        ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
          "{0} -start- sys:link.Start", _node.Address));

      Edge from = GetEdge(edge);
      LinkMessage lm = new LinkMessage(link_message);
      if(ProtocolLog.LinkDebug.Enabled)
        ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
          "{0} -args- sys:link.Start({1},{2})", _node.Address,lm,from));

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
            try {
              //If the CloseEvent was already called, this throws an exception
              from.CloseEvent += this.CloseHandler;
            }
            catch {
              CloseHandler(from, null);
              throw new AdrException((int)ErrorMessage.ErrorCode.EdgeClosed,
                                      "Edge Closed after receiving message");
            }
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
        NodeInfo n_info = NodeInfo.CreateInstance( _node.Address, from.LocalTA );
        NodeInfo remote_info = NodeInfo.CreateInstance( null, from.RemoteTA );
        System.Collections.Specialized.StringDictionary attrs =
                new System.Collections.Specialized.StringDictionary();
        attrs["type"] = String.Intern( lm.ConTypeString );
        attrs["realm"] = String.Intern( _node.Realm );
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
      if(ProtocolLog.LinkDebug.Enabled)
        ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
          "{0} -end- sys:link.Start()->{1}", _node.Address,lm_resp));
      return lm_resp.ToDictionary();
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
    public IDictionary GetStatus(IDictionary status_message, ISender edge) {
      //we just got s status request
      LinkMessage lm_to_add = null;
      StatusMessage sm = new StatusMessage(status_message);
      Edge from = GetEdge(edge);
      if(ProtocolLog.LinkDebug.Enabled)
        ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
          "{0} -start- sys:link.GetStatus({1},{2})", _node.Address,sm,from));

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
      if( _disconnecting ) {
        throw new AdrException((int)ErrorMessage.ErrorCode.Disconnecting, "disconnecting");
      }
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
        try {
          tab.Add(con);
        }
        finally {
          //Unlock after we add the connection
          tab.Unlock(lm_to_add.Local.Address, lm_to_add.ConTypeString, this);
        }
      }
      if(ProtocolLog.LinkDebug.Enabled)
        ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
          "{0} -end- sys:link.GetStatus()->{1}", _node.Address,response));
      return response.ToDictionary();
    }

    /**
     * This just echos back the object passed to it
     */
    public object Ping(object o, ISender edge) {
      if(ProtocolLog.LinkDebug.Enabled)
        ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
          "{0} sys:link.Ping({1},{2})", _node.Address,o,edge));
      return o;
    }

    /**
     * This returns an IList of the Local TAs
     */
    public IList GetLocalIPAddresses(ISender caller) {
      ArrayList lta = new ArrayList();
      foreach(TransportAddress ta in _node.LocalTAs) {
        if(ta.TransportAddressType == TransportAddress.TAType.Udp || 
          ta.TransportAddressType == TransportAddress.TAType.Tcp) {
          string ip = ((IPTransportAddress) ta).Host;
          if(!lta.Contains(ip) && !ip.Equals("127.0.0.1"))
            lta.Add(ip);
        }
      }
      return lta;
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
      else if( _disconnecting ) {
        err = new ErrorMessage(ErrorMessage.ErrorCode.Disconnecting,
                               String.Format("I am disconnecting. local: {0}", local_add));
      }
      else {
        /*
         * Now we go to the ConnectionTable and try to
         * get a lock on the address so we can go forward
         * with the linking
         */
        try {
          if(ProtocolLog.LinkDebug.Enabled)
            ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
              "ConnectionPacketHandler - Trying to lock connection table: {0},{1}",
                                  lm.Local.Address, lm.ConTypeString));

          tab.Lock( lm.Local.Address, lm.ConTypeString, this );
          if(ProtocolLog.LinkDebug.Enabled)
            ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
              "ConnectionPacketHandler - Successfully locked connection table: {0},{1}",
              lm.Local.Address, lm.ConTypeString));
        }
        catch(ConnectionExistsException) {
          //We already have a connection of this type to this address
          err = new ErrorMessage(ErrorMessage.ErrorCode.AlreadyConnected,
                               String.Format("We are already connected: {0}", local_add));
        }
        catch(CTLockException) {
          if(ProtocolLog.LinkDebug.Enabled)
            ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
              "ConnectionPacketHandler - Cannot lock connection table: {0},{1}",
              lm.Local.Address, lm.ConTypeString));
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
