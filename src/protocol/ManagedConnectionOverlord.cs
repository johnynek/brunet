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
   * find local nodes and create StructuredLocalConnections.  To protect the
   * node from abuse, an connection is attempted no more than 3 times every
   * 60 seconds!
   */

  public class ManagedConnectionOverlord: ConnectionOverlord
  {
    public enum MCState {
      Off,
      Attempt1,
      Attempt2,
      On
    }

    protected Dictionary<Address, MCState> _connection_state;
    protected DateTime _last_call;
    protected readonly RpcManager _rpc;
    protected Object _sync;
    protected volatile bool _active;

    public static readonly string struc_managed = "structured.managed";

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

    public ManagedConnectionOverlord(Node node) {
      _sync = new Object();
      _active = false;
      _node = node;
      _connection_state = new Dictionary<Address, MCState>();
      _last_call = DateTime.MinValue;
    }

    protected void Enable() {
      lock(_sync) {
        _node.HeartBeatEvent += CheckState;
        _node.ConnectionTable.ConnectionEvent += ConnectHandler;
        _node.ConnectionTable.DisconnectionEvent += DisconnectHandler;
      }
    }

    protected void Disable() {
      lock(_sync) {
        _node.HeartBeatEvent -= CheckState;
        _node.ConnectionTable.ConnectionEvent -= ConnectHandler;
        _node.ConnectionTable.DisconnectionEvent -= DisconnectHandler;
      }
    }

    public void CheckState(object o, EventArgs ea) {
      DateTime now = DateTime.UtcNow;
      lock(_sync) {
        if(_last_call.AddHours(1) > now) {
          return;
        }
        _last_call = now;
      }
      Activate();
    }

    /**
     * If IsActive, then start trying to get connections.
     */
    public override void Activate() {
      if(!_active) {
        return;
      }
      List<Address> connect_to = new List<Address>(_connection_state.Count);
      lock(_sync) {
        foreach(KeyValuePair<Address, MCState> kvp in _connection_state) {
          if(kvp.Value == MCState.Off) {
            connect_to.Add(kvp.Key);
          }
        }
      }
      foreach(Address addr in connect_to) {
        ConnectTo(addr, struc_managed);
      }
    }

    /**
     * @return true if the ConnectionOverlord needs a connection
     */
    public override bool NeedConnection {
      get { return false; }
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

    /// <summary>This method is called when there is connection added to the
    /// ConnectionTable.  We set the connection state to true and thus won't
    /// attempt reconnecting to it, unless there is a disconnection</summary>
    protected void ConnectHandler(object tab, EventArgs eargs) {
      Connection new_con = ((ConnectionEventArgs)eargs).Connection;
      if(_connection_state.ContainsKey(new_con.Address)) {
        lock(_sync) {
          _connection_state[new_con.Address] = MCState.On;
        }
        if(ProtocolLog.ManagedCO.Enabled) {
          ProtocolLog.Write(ProtocolLog.ManagedCO, String.Format(
                            "Connect a {0}: {1} at: {2}",
                            struc_managed, new_con, DateTime.UtcNow));
        }
      }
    }

    /// <summary>This method is called when there is a Disconnection from
    /// the ConnectionTable.  If a disconnect occurs and it is an address
    /// managed by the ManagedCO, reconnect.</summary>
    protected void DisconnectHandler(object tab, EventArgs eargs) {
      Connection new_con = ((ConnectionEventArgs)eargs).Connection;
      if(_connection_state.ContainsKey(new_con.Address)) {
        lock(_sync) {
          _connection_state[new_con.Address] = MCState.Off;
        }
        if(ProtocolLog.ManagedCO.Enabled) {
          ProtocolLog.Write(ProtocolLog.ManagedCO, String.Format(
                            "Disconnect a {0}: {1} at: {2}",
                            struc_managed, new_con, DateTime.UtcNow));
        }
        if(_active) {
          ConnectTo(new_con.Address, struc_managed);
        }
      }
    }

    /// <summary>If we get here, a Connector attempt has ended, we will check
    /// to see if the connection succeeded.  If it hasn't, we attempt to
    /// connect again.</summary>
    override protected void ConnectorEndHandler(object o, EventArgs eargs) {
      Connector c = (Connector) o;
      Address caddr = c.State as Address;
      if(caddr == null) {
        return;
      }

      lock(_sync) {
        // First case, why are we here
        // Second case, have we connected
        if(!_connection_state.ContainsKey(caddr))
          return;

        switch(_connection_state[caddr]) {
          case MCState.Off:
            _connection_state[caddr] = MCState.Attempt1;
            break;
          case MCState.Attempt1:
            _connection_state[caddr] = MCState.Attempt2;
            break;
          case MCState.Attempt2:
            _connection_state[caddr] = MCState.Off;
            return;
          case MCState.On:
            return;
        }
      }
      if(_active) {
        // Well we should be connected, but we aren't, try again!
        ConnectTo(caddr, struc_managed);
      }
    }

    /// <summary>Add a specific address that you would like to get connected
    /// to.</summary>
    /// <param name="RemoteAddress">The address to get connected to</param>
    /// <returns>Should always be true, unless an unhandled exception
    /// occurs.</returns>
    public bool AddAddress(Address RemoteAddress)
    {
      lock(_sync) {
        if(_connection_state.Count == 0) {
          Enable();
        }

        if(_connection_state.ContainsKey(RemoteAddress)) {
          return true;
        }
        _connection_state[RemoteAddress] = MCState.Off;
      }
      if(_active) {
        ConnectTo(RemoteAddress, struc_managed);
      }
      return true;
    }

    /// <summary>Remove a specific address from being automatically connected
    /// to and close an existing managed connection if one exists.</summary>
    /// <param name="RemoteAddress">The address to get disconnected from and
    /// stop connecting to through the ManagedCO.</param>
    /// <returns>Should always be true, unless an unhandled exception
    /// occurs.</returns>
    public bool RemoveAddress(Address RemoteAddress)
    {
      lock(_sync) {
        if(_connection_state.Count == 0) {
          Disable();
        }

        if(!_connection_state.ContainsKey(RemoteAddress)) {
          return true;
        }
        _connection_state.Remove(RemoteAddress);
      }

      ConnectionType ct = Connection.StringToMainType(struc_managed);
      Connection c = _node.ConnectionTable.GetConnection(ct, RemoteAddress);
      if(c != null && c.ConType.Equals(struc_managed)) {
        _node.GracefullyClose(c.Edge, "RemoveAddress called from ManagedCO");
      }
      return true;
    }
  }
}
