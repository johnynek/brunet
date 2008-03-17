/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Brunet
{

  /**
   * This CO is uses registers RPC methods that are specifically meant to be
   * called by nodes on the LAN to facilitate connectivity.  This can be used
   * to replace the necessity of RemoteTAs.  Currently it is only active when 
   * there are zero connections.  Eventually, it may prove useful to have it 
   * find local nodes and create StructuredLocalConnections.
   */

  public class LocalConnectionOverlord: ConnectionOverlord, IRpcHandler
  {
    public static readonly int MAX_LC = 4;
    protected volatile List<AHAddress> _local_addresses;

    protected readonly Node _node;
    protected DateTime _last_announce_call;
    protected DateTime _last_activate_call;
    protected readonly RpcManager _rpc;
    protected Object _sync;
    protected volatile bool _active;
    protected volatile bool _allow_localcons;
    protected volatile int _local_cons = 0;

    public static readonly string struc_local = "structured.local";

    protected readonly AHAddressComparer addr_compare = new AHAddressComparer();

    /**
     * When IsActive is false, the ConnectionOverlord does nothing
     * to replace lost connections, or to get connections it needs.
     * When IsActive is true, then any missing connections are
     * made up for
     */
    public override bool IsActive
    {
      get { return _active; }
      set { _active = value; }
    }

    public LocalConnectionOverlord(Node node) {
      _sync = new Object();
      _allow_localcons = false;
      _active = false;
      _local_addresses = new List<AHAddress>();
      _node = node;
      _rpc = RpcManager.GetInstance(node);
      _rpc.AddHandler("LocalCO", this);

      _node.HeartBeatEvent += CheckConnection;
      _node.StateChangeEvent += StateChangeHandler;
      _node.ConnectionTable.ConnectionEvent += ConnectHandler;
      _node.ConnectionTable.DisconnectionEvent += DisconnectHandler;
      _last_announce_call = DateTime.MinValue;
      _last_activate_call = DateTime.MinValue;
    }

    protected void StateChangeHandler(Node n, Node.ConnectionState state) {
      if(state == Node.ConnectionState.Connected) {
        lock(_sync) {
          _allow_localcons = true;
        }
      }
      else if(state == Node.ConnectionState.Leaving) {
        lock(_sync) {
          _active = false;
        }
      }
    }

    /**
     * If IsActive, then start trying to get connections.
     */
    public override void Activate() {
      if(!_allow_localcons || _local_addresses.Count == 0) {
        return;
      }

      DateTime now = DateTime.UtcNow;
      if(now - _last_activate_call < TimeSpan.FromSeconds(10)) {
        return;
      }
      _last_announce_call = now;

      Random rand = new Random();
      for(int i = 0; i < MAX_LC - _local_cons; i++) {
        Address target = null;
        target = _local_addresses[rand.Next(0, _local_addresses.Count)];
        ConnectTo(target);
      }
    }

    /**
     * @return true if the ConnectionOverlord needs a connection
     */
    public override bool NeedConnection {
      get { return false; }
//      get { return _allow_localcons && _local_cons < MAX_LC; }
    }
    /**
     * @return true if the ConnectionOverlord has sufficient connections
     *  for connectivity (no routing performance yet!)
     */
    public override bool IsConnected
    {
      get {
        throw new Exception("Not implemented!  LocalConnectionOverlord.IsConnected");
      }
    }

    /**
     * HeartBeatEvent - Do we need connections?
     */
    public void CheckConnection(object o, EventArgs ea)
    {
      if(!_active) {
        return;
      }

      // We are trying to get StructuredConnections or LocalConnections
      if(_local_cons < MAX_LC) {
        DateTime now = DateTime.UtcNow;
        if(now - _last_announce_call > TimeSpan.FromSeconds(600)) {
          _last_announce_call = now;
          Announce();
        }
        // We can establish some local connections!
        if(NeedConnection) {
          Activate();
        }
      }
    }

    public override bool HandleCtmResponse(Connector c, ISender ret_path,
                                           ConnectToMessage ctm_resp) {
      /**
       * Time to start linking:
       */

      Linker l = new Linker(_node, ctm_resp.Target.Address,
                            ctm_resp.Target.Transports,
                            ctm_resp.ConnectionType,
                            ctm_resp.Token);
      _node.TaskQueue.Enqueue( l );
      return true;
    }

    /**
     * Suppose we know of a node we'd like to connect to, this takes care
     * of just that!
     * @param target The Brunet.Address of the node we want to connect to
     */

    protected void ConnectTo(Address target) {
      ConnectionType mt = Connection.StringToMainType(struc_local);
      /*
       * This is an anonymous delegate which is called before
       * the Connector starts.  If it returns true, the Connector
       * will finish immediately without sending an ConnectToMessage
       */
      Linker l = new Linker(_node, target, null, struc_local, _node.Address.ToString());
      object link_task = l.Task;
      Connector.AbortCheck abort = delegate(Connector c) {
        bool stop = false;
        stop = _node.ConnectionTable.Contains( mt, target );
        if (!stop ) {
          /*
           * Make a linker to get the task.  We won't use
           * this linker.
           * No need in sending a ConnectToMessage if we
           * already have a linker going.
           */
          stop = _node.TaskQueue.HasTask( link_task );
        }
        return stop;
      };
      if (abort(null)) {
        return;
      }

      ConnectToMessage ctm = new ConnectToMessage(struc_local, _node.GetNodeInfo(8), _node.Address.ToString());
      ISender send = new AHSender(_node, target, AHPacket.AHOptions.Exact);
      Connector con = new Connector(_node, send, ctm, this);
      con.FinishEvent += this.ConnectorEndHandler;
      con.AbortIf = abort;
      _node.TaskQueue.Enqueue(con);
    }


    /**
     * This method is called when there is a Disconnection from
     * the ConnectionTable
     */
    protected void ConnectHandler(object tab, EventArgs eargs) {
      Connection new_con2 = ((ConnectionEventArgs)eargs).Connection;
      if (new_con2.ConType.Equals(struc_local)) {
        if(ProtocolLog.LocalCO.Enabled) {
          ProtocolLog.Write(ProtocolLog.LocalCO, String.Format(
                            "Connect a {0}: {1} at: {2}",
                            struc_local, new_con2, DateTime.UtcNow));
        }
        lock(_sync) {
          _local_cons++;
        }
      }
    }

    /**
     * This method is called when there is a Disconnection from
     * the ConnectionTable
     */
    protected void DisconnectHandler(object tab, EventArgs eargs) {
      Connection new_con2 = ((ConnectionEventArgs)eargs).Connection;
      if (new_con2.ConType.Equals(struc_local)) {
        if(ProtocolLog.LocalCO.Enabled) {
          ProtocolLog.Write(ProtocolLog.LocalCO, String.Format(
                            "Disconnect a {0}: {1} at: {2}",
                            struc_local, new_con2, DateTime.UtcNow));
        }
        lock(_sync) {
          _local_cons--;
        }
      }
    }

    protected void ConnectorEndHandler(object o, EventArgs eargs) {
      // Not entirely certain yet...
    }

    /**
     * Used to request other nodes existence
     */
    protected void Announce()
    {
      ISender mcs = _node.IPHandler.CreateMulticastSender();
      Channel queue = new Channel();
      queue.EnqueueEvent += HandleGetInformation;
      _rpc.Invoke(mcs, queue, "LocalCO.GetInformation");
    }

    protected void HandleGetInformation(Object o, EventArgs ea)
    {
      Channel queue = (Channel) o;
      Hashtable ht = null;
      try {
        RpcResult rpc_reply = (RpcResult) queue.Dequeue();
        ht = (Hashtable) rpc_reply.Result;
      }
      catch {
        // Remote end point doesn't have LocalCO enabled.
        return;
      }

      try {
        string remote_realm = (string) ht["namespace"];
        if(!remote_realm.Equals(_node.Realm)) {
          return;
        }
        ArrayList string_tas = (ArrayList) ht["tas"];
        ArrayList remote_tas = new ArrayList();
        foreach(string ta in string_tas) {
          remote_tas.Add(TransportAddressFactory.CreateInstance(ta));
        }
        _node.UpdateRemoteTAs(remote_tas);

        AHAddress new_address = (AHAddress) AddressParser.Parse((string) ht["address"]);
        int pos = _local_addresses.BinarySearch(new_address, addr_compare);
        if(pos < 0) {
          pos = ~pos;
          _local_addresses.Insert(pos, new_address);
        }
      }
      catch (Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "Unexpected exception: " + e);
      }
    }

    /**
     * Send a list of our TAs in string format to the specified end point.
     */
    protected Hashtable GetInformation()
    {
      Hashtable ht = new Hashtable(3);
      IList tas = (IList) ((ArrayList)_node.LocalTAs).Clone();
      string[] tas_string = new string[tas.Count];
      for(int i = 0; i < tas.Count; i++) {
        tas_string[i] = tas[i].ToString();
      }

      ht["tas"] = tas_string;
      ht["address"] = _node.Address.ToString();
      ht["namespace"] = _node.Realm;
      return ht;
    }

    public void HandleRpc(ISender caller, string method, IList args, object rs) {
      object result = null;
      try {
        if(method.Equals("GetInformation")) {
          result = GetInformation();
        }
        else {
          throw new Exception("Invalid method");
        }
      }
      catch (Exception e) {
        result = new AdrException(-32602, e);
     }
      _rpc.SendResult(rs, result);
    }
  }
}
