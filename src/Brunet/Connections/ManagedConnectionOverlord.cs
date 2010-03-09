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
using Brunet.Connections;
using Brunet.Messaging;
using BU = Brunet.Util;
namespace Brunet.Connections
{

  /// <summary>This CO is administered by "user-level" applications to specify
  /// end-points that the user would like to be connected with.</summary>
  /// <remarks>When AddAddress is called, the address is added to
  /// _connection_state and ConnectTo is called on the address.  If the node
  /// connects, all is fine, if not, this will attempt twice more to connect.
  /// If that fails, every hour thereafter, this CO will try 3 more times to
  /// get connected until it does or it is removed.  If a Managed connection
  /// disconnects, this will automatically attempt to repair the connection
  /// by acting as if the address had just been added.  All this is handled
  /// by using _connection_state as the state holder per address.</remarks>
  public class ManagedConnectionOverlord: ConnectionOverlord
  {
    public enum MCState {
      ///<summary>Attempted 3 times and failed, wait...</summary>
      Off,
      ///<summary>Attempted 1 time, 2 more to go!</summary>
      Attempt1,
      ///<summary>Attempted 2 times, 1 more to go!</summary>
      Attempt2,
      ///<summary>Connection!</summary>
      On
    }

    /// <summary> Keeps the state for all registered addresses</summary>
    protected Dictionary<Address, MCState> _connection_state;
    protected DateTime _last_call;
    protected readonly RpcManager _rpc;
    protected Object _sync;
    protected volatile bool _active;

    public static readonly string struc_managed = "structured.managed";

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

    ///<summary>Enables HeartBeat and ConnectionTable hooks</summary>
    protected void Enable() {
      lock(_sync) {
        _node.HeartBeatEvent += CheckState;
        _node.ConnectionTable.ConnectionEvent += ConnectHandler;
        _node.ConnectionTable.DisconnectionEvent += DisconnectHandler;
      }
    }

    ///<summary>Disables HeartBeat and ConnectionTable hooks</summary>
    protected void Disable() {
      lock(_sync) {
        _node.HeartBeatEvent -= CheckState;
        _node.ConnectionTable.ConnectionEvent -= ConnectHandler;
        _node.ConnectionTable.DisconnectionEvent -= DisconnectHandler;
      }
    }

    ///<summary>Once every hour, the CO will attempt to connect to remote end
    ///points in _connection_state that aren't connected</summary>
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

    ///<summary>Once every hour, the CO will attempt to connect to remote end
    ///points in _connection_state that aren't connected</summary>
    public override void Activate() {
      if(!_active) {
        return;
      }
      List<Address> connect_to = new List<Address>();
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

    ///<summary>This isn't used for this CO</summary>
    public override bool NeedConnection {
      get { return false; }
    }

    ///<summary>This isn't used for this CO</summary>
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
      lock(_sync) {
        if(!_connection_state.ContainsKey(new_con.Address)) {
          return;
        }
        _connection_state[new_con.Address] = MCState.On;
      }

      if(BU.ProtocolLog.ManagedCO.Enabled) {
        BU.ProtocolLog.Write(BU.ProtocolLog.ManagedCO, String.Format(
                          "Connect a {0}: {1} at: {2}",
                          struc_managed, new_con, DateTime.UtcNow));
      }
    }

    /// <summary>This method is called when there is a Disconnection from
    /// the ConnectionTable.  If a disconnect occurs and it is an address
    /// managed by the ManagedCO, reconnect.</summary>
    protected void DisconnectHandler(object tab, EventArgs eargs) {
      Connection new_con = ((ConnectionEventArgs)eargs).Connection;
      lock(_sync) {
        if(!_connection_state.ContainsKey(new_con.Address)) {
         return;
        }
        
        _connection_state[new_con.Address] = MCState.Off;
      }

      if(BU.ProtocolLog.ManagedCO.Enabled) {
        BU.ProtocolLog.Write(BU.ProtocolLog.ManagedCO, String.Format(
                          "Disconnect a {0}: {1} at: {2}",
                          struc_managed, new_con, DateTime.UtcNow));
      }
      if(_active) {
        ConnectTo(new_con.Address, struc_managed);
      }
    }

    /// <summary>Find out if the Connector succeeded, if not, let's call
    /// UpdateStatus.</summary>
    override protected void ConnectorEndHandler(object o, EventArgs eargs)
    {
      Connector con = o as Connector;
      Address addr = con.State as Address;
      if(addr != null && con.ReceivedCTMs.Count == 0) {
        UpdateState(addr);
      }
    }

    /// <summary>If we get here, a Connector attempt has ended, let's call
    /// UpdateStatus.</summary>
    override protected void LinkerEndHandler(object o, EventArgs eargs)
    {
      Linker linker = o as Linker;
      Address addr = linker.Target;
      if(addr != null) {
        UpdateState(addr);
      }
    }

    /// <summary>We will check to see if the connection succeeded.  If it
    /// hasn't, we attempt to connect again.</summary>
    protected void UpdateState(Address addr)
    {
      lock(_sync) {
        // First case, why are we here
        // Second case, have we connected
        if(!_connection_state.ContainsKey(addr))
          return;

        switch(_connection_state[addr]) {
          case MCState.Off:
            _connection_state[addr] = MCState.Attempt1;
            break;
          case MCState.Attempt1:
            _connection_state[addr] = MCState.Attempt2;
            break;
          case MCState.Attempt2:
            _connection_state[addr] = MCState.Off;
            return;
          case MCState.On:
            return;
        }
      }
      if(_active) {
        // Well we should be connected, but we aren't, try again!
        ConnectTo(addr, struc_managed);
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
        if(_connection_state.ContainsKey(RemoteAddress)) {
          return true;
        }
        _connection_state[RemoteAddress] = MCState.Off;

        if(_connection_state.Count == 1) {
          Enable();
        }
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
        if(!_connection_state.ContainsKey(RemoteAddress)) {
          return true;
        }
        _connection_state.Remove(RemoteAddress);

        if(_connection_state.Count == 0) {
          Disable();
        }
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
