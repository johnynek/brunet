/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

using System;
using System.Threading;
using System.Collections;
using System.Collections.Specialized;

using Brunet.Util;
using Brunet.Transport;

using Brunet.Messaging;
using Brunet.Symphony;
namespace Brunet.Connections
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

  public class ConnectionPacketHandler : IRpcHandler
  {
    /**
     * This is the only stateful object here.  The rest
     * do not need thread synchronization.
     */
    protected readonly Hashtable _edge_to_cphstate;

    protected readonly Node _node;

    public class CphState : ILinkLocker {
      public readonly Edge Edge;
      public readonly LinkMessage LM;
      protected Address _target_lock;
      public Object TargetLock {
        get { return _target_lock; }
        set { _target_lock = (Address) value; }
      }

      public CphState(Edge e, LinkMessage lm) {
        Edge = e;
        LM = lm;
      }

      public bool AllowLockTransfer(Address a, string contype, ILinkLocker new_locker) {
        return false;
      }
    }

    /** global lock for thread synchronization */
    protected readonly object _sync;

    //This is true when the node starts to disconnect
    protected int _disconnecting;
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
      _disconnecting = 0;
      //When Disconnect is called, set disconnecting to true, disallowing new
      //connections.
      _node.DepartureEvent += delegate(object o, EventArgs a) {
        Interlocked.Exchange(ref _disconnecting, 1);
      };
    }

    /**
     * Handle the notification that the other side is going to close the edge
     */
    public IDictionary Close(IDictionary close_message, ISender edge) {
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
      if( c != null ) {
        c.Abort(); //Go ahead and end this connection
      }
      /** 
       * Release locks when the close message arrives; do not wait
       * until the edge actually closes.
       */
      CloseHandler(from, null);

      if(ProtocolLog.EdgeClose.Enabled) {
        String reason = String.Empty;
        if(close_message.Contains(reason)) {
          reason = (String) close_message["reason"];
        }
        ProtocolLog.Write(ProtocolLog.EdgeClose, String.Format(
                          "sys:link.Close - " + from + ": " + reason));
      }
      /**
       * Try to close the edge after a small time span:
       */
      Brunet.Util.FuzzyTimer.Instance.DoAfter(delegate(DateTime now) {
        _node.EnqueueAction(new Edge.CloseAction(from));
      }, 5000, 1000);
      return new ListDictionary();
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
    public IDictionary GetNeighbors() {
      AHAddress self = (AHAddress)_node.Address;
      IDictionary result = new ListDictionary();
      //Put it in:
      result["self"] = self.ToString();

      // if we have no connections, this throws an exception
      try {
        Connection left = _node.ConnectionTable.GetLeftStructuredNeighborOf(self);
        if( left != null ) {
          AHAddress la = (AHAddress)left.Address;
          result["left"] = la.ToString();
          Connection left2 = _node.ConnectionTable.GetLeftStructuredNeighborOf(la);
          if( left2 != null ) {
            result["left2"] = left2.Address.ToString();
          }
        }
      } catch {}
      // If there are no connections, this throws an exception
      try {
        Connection right = _node.ConnectionTable.GetRightStructuredNeighborOf(self);
        if( right != null ) {
          AHAddress ra = (AHAddress)right.Address;
          result["right"] = ra.ToString();
          Connection right2 = _node.ConnectionTable.GetRightStructuredNeighborOf(ra);
          if( right2 != null ) {
            result["right2"] = right2.Address.ToString();
          }
        }
      }
      catch {}
      //Get a random shortcut:
      ArrayList shortcuts = new ArrayList();
      // If there are no connections, this throws an exception
      try {
        foreach(Connection c in _node.ConnectionTable.GetConnections("structured.shortcut") ) {
          shortcuts.Add(c);
        }
      }
      catch {}
      if( shortcuts.Count > 0 ) {
        Random r = new Random();
        Connection sc = (Connection)shortcuts[ r.Next( shortcuts.Count ) ];
        result["shortcut"] = sc.Address.ToString();
      }
      return result;
    }

    /** Dispatch RPC requests
     */
    public void HandleRpc(ISender caller,
                          string method, IList arguments, object request_state) {
      object result;
      switch(method) {
        case "Ping": //Most common method
          result = Ping(arguments[0],caller);
          break;
        case "Close":
          result = Close((IDictionary)arguments[0],caller);
          break;
        case "GetLocalIPAddresses":
          result = GetLocalIPAddresses();
          break;
        case "GetNeighbors":
          result = GetNeighbors();
          break;
        case "GetNodeInfo":
          result = _node.GetNodeInfo(12).ToDictionary();
          break;
        case "GetStatus":
          result = GetStatus((IDictionary)arguments[0],caller);
          break;
        case "Start":
          result = Start((IDictionary)arguments[0],caller);
          break;
        default:
          throw new Exception("Unknown method: " + method);
      }
      _node.Rpc.SendResult(request_state, result);
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

      CphState cph = new CphState(from,lm);
      lock( _sync ) {
        if( !_edge_to_cphstate.ContainsKey( from ) ) {
          _edge_to_cphstate[from] = cph;
        }
        else {
          throw new AdrException((int)ErrorMessage.ErrorCode.InProgress,
                                 "Already have a link in progress on this edge");
        }
      }
      ErrorMessage err = null;
      if( CanConnect(cph, out err) ) {
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
      else {
        lock( _sync ) {
          _edge_to_cphstate.Remove(from);
        }
      }
      //Now we prepare our response
      LinkMessage lm_resp = null;
      if( err == null ) {
        //We send a response:
        NodeInfo n_info = NodeInfo.CreateInstance( _node.Address, from.LocalTA );
        NodeInfo remote_info = NodeInfo.CreateInstance( null, from.RemoteTA );
        lm_resp = new LinkMessage(lm.ConTypeString, n_info, remote_info , _node.Realm, lm.Token);
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
          ConnectionTable tab = _node.ConnectionTable;
          Connection c = tab.GetConnection( lm.ConnectionType,
                                             lm.Local.Address );
          if( c != null ) {
            RpcManager rpc = _node.Rpc;
            //This can throw, but it won't cause any failure since
            //this is only called when the connection attempt already failed.
            rpc.Invoke(c, null, "sys:link.Ping", String.Empty);
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
        return s as Edge;
      }
    }
    /**
     * Get a StatusMessage for this node
     */
    public IDictionary GetStatus(IDictionary status_message, ISender edge) {
      //we just got s status request
      if( 1 == _disconnecting ) {
        throw new AdrException((int)ErrorMessage.ErrorCode.Disconnecting, "disconnecting");
      }

      StatusMessage sm = new StatusMessage(status_message);
      Edge from = GetEdge(edge);

      if(ProtocolLog.LinkDebug.Enabled) {
        ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
          "{0} -start- sys:link.GetStatus({1},{2})", _node.Address, sm, edge));
      }

      CphState cphstate = null;
      if(from != null) {
        cphstate = _edge_to_cphstate[from] as CphState;
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
      if (cphstate != null) {
        try {
          LinkMessage lm_to_add = cphstate.LM;
          //This is part of connection process:
          response = _node.GetStatus( sm.NeighborType, lm_to_add.Local.Address );
          Connection con = new Connection(from,
                                        lm_to_add.Local.Address,
                                        lm_to_add.ConTypeString,
                                        sm,
                                        lm_to_add);
          tab.Add(con);
        }
        finally {
          from.CloseEvent -= this.CloseHandler;
          CloseHandler(from, null);
        }
      } else {
        //This is just a "regular" status request
        //update our table:
        Address fadd = null;
        Connection c = tab.GetConnection(from);
        if( c != null ) {
          fadd = c.Address;
          c.SetStatus(sm);
        }
        response = _node.GetStatus( sm.NeighborType, fadd );
      }
      
      if(ProtocolLog.LinkDebug.Enabled) {
        ProtocolLog.Write(ProtocolLog.LinkDebug, String.Format(
          "{0} -end- sys:link.GetStatus()->{1}", _node.Address,response));
      }

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
    public IList GetLocalIPAddresses() {
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
     * @param cph The CphState
     * @param err ErrorMessage to return.  Is null if there is no error
     * @return true if we can connect, if false, err != null
     */
    protected bool CanConnect(CphState cph, out ErrorMessage err)
    {
      Address local_add = _node.Address;
      LinkMessage lm = cph.LM;
      err = null;
      /* We lock the connection table so it doesn't change between
       * the call to Contains and the call to Lock
       */
      if( lm.Realm != _node.Realm ) {
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
      else if( 1 == _disconnecting ) {
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

          _node.LockMgr.Lock( lm.Local.Address, lm.ConTypeString, cph );
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
      CphState cphstate = null;
      lock(_sync) {
        cphstate = (CphState)_edge_to_cphstate[edge];
        if( cphstate != null ) {
          _edge_to_cphstate.Remove(edge);
        }
      }
      if( cphstate != null ) {
        _node.LockMgr.Unlock( cphstate.LM.ConTypeString, cphstate );
      }
    }
  }
}
