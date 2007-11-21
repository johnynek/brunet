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
    private Node _node;
    private DateTime _last_call;
    private RpcManager _rpc;
    bool _active;

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
      _node = node;
      _rpc = RpcManager.GetInstance(node);
      _rpc.AddHandler("LocalCO", this);
    }

    /**
     * If IsActive, then start trying to get connections.
     */
    public override void Activate() {
      _last_call = DateTime.UtcNow;
      Announce();
      _node.HeartBeatEvent += CheckConnection;
    }

    /**
     * @return true if the ConnectionOverlord needs a connection
     */
    public override bool NeedConnection
    {
      get { return _node.ConnectionTable.TotalCount == 0; }
    }
    /**
     * @return true if the ConnectionOverlord has sufficient connections
     *  for connectivity (no routing performance yet!)
     */
    public override bool IsConnected
    {
      get { throw new Exception("Not implemented!  LocalConnectionOverlord.IsConnected"); }
    }

    /**
     * Do we need to use this to get connections?
     */
    public void CheckConnection(object o, EventArgs ea)
    {
      if(!_active || !NeedConnection) {
        return;
      }
      DateTime now = DateTime.UtcNow;
      if(now - _last_call > TimeSpan.FromSeconds(600)) {
        _last_call = now;
        Announce();
      }
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
      try {
        RpcResult rpc_reply = (RpcResult) queue.Dequeue();
        Hashtable ht = (Hashtable) rpc_reply.Result;
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
          throw new Exception("Dht.Exception:  Invalid method");
        }
      }
      catch (Exception e) {
        result = new AdrException(-32602, e);
     }
      _rpc.SendResult(rs, result);
    }
  }
}
