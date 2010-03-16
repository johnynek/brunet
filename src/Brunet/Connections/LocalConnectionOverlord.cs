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

using Brunet.Concurrent;
using Brunet.Connections;
using Brunet.Util;
using Brunet.Transport;

using Brunet.Messaging;
using Brunet.Symphony;
namespace Brunet.Connections
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
    protected List<AHAddress> _local_addresses;

    protected DateTime _last_announce_call;
    protected DateTime _last_activate_call;
    protected readonly RpcManager _rpc;
    protected Object _sync;
    protected bool _active;
    protected bool _allow_localcons;
    protected int _local_cons = 0;

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

      lock(_sync) {
        _rpc = node.Rpc;
        _rpc.AddHandler("LocalCO", this);

        _node.HeartBeatEvent += CheckConnection;
        _node.StateChangeEvent += StateChangeHandler;
        _node.ConnectionTable.ConnectionEvent += ConnectHandler;
        _node.ConnectionTable.DisconnectionEvent += DisconnectHandler;
        _last_announce_call = DateTime.MinValue;
        _last_activate_call = DateTime.MinValue;
      }
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

      lock(_sync) {
        DateTime now = DateTime.UtcNow;
        if(now - _last_activate_call < TimeSpan.FromSeconds(10)) {
          return;
        }
        _last_activate_call = now;

        Random rand = new Random();
        for(int i = 0; i < MAX_LC - _local_cons; i++) {
          Address target = _local_addresses[rand.Next(0, _local_addresses.Count)];
          ConnectTo(target, struc_local);
        }
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
        bool ann = false;
        lock(_sync) {
          if(now - _last_announce_call > TimeSpan.FromSeconds(600)) {
            _last_announce_call = now;
            ann = true;
          }
        }

        if(ann) {
          Announce();
        }
        // We can establish some local connections!
        if(NeedConnection) {
          Activate();
        }
      }
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

    /**
     * Used to request other nodes existence
     */
    protected void Announce()
    {
      Channel queue = new Channel();
      queue.EnqueueEvent += HandleGetInformation;
      try {
        ISender mcs = _node.IPHandler.CreateMulticastSender();
        _rpc.Invoke(mcs, queue, "LocalCO.GetInformation");
      }
      catch(SendException) {
        /*
         * On planetlab, it is not uncommon to have a node that
         * does not allow Multicast, and it will throw an exception
         * here.  We just ignore this information for now.  If we don't
         * the heartbeatevent in the node will not execute properly.
         */ 
      }
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
        lock(_sync) {
          int pos = _local_addresses.BinarySearch(new_address, addr_compare);
          if(pos < 0) {
            pos = ~pos;
            _local_addresses.Insert(pos, new_address);
          }
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
      IList tas = _node.LocalTAs;
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
