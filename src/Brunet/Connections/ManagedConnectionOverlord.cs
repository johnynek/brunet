/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Brunet.Connections;
using Brunet.Messaging;
using Brunet.Transport;
using Brunet.Util;

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
  public class ManagedConnectionOverlord: PolicyBasedConnectionOverlord {
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
    protected Object _sync;
    protected FuzzyEvent _check_state;

    protected const string _type = "structured.managed";
    override public string Type { get { return _type; } }
    protected static readonly ConnectionType _main_type =
      Connection.StringToMainType(_type);
    override public ConnectionType MainType { get { return _main_type; } }

    override public TAAuthorizer TAAuth { get { return _ta_auth;} }
    protected readonly static TAAuthorizer _ta_auth = new TATypeAuthorizer(
          new TransportAddress.TAType[]{TransportAddress.TAType.Subring},
          TAAuthorizer.Decision.Deny,
          TAAuthorizer.Decision.None);

    public ManagedConnectionOverlord(Node node) : base(node) {
      _sync = new Object();
      _node = node;
      _connection_state = new Dictionary<Address, MCState>();
    }

    ///<summary>Once every hour, the CO will attempt to connect to remote end
    ///points in _connection_state that aren't connected</summary>
    protected void Activate(DateTime now) {
      Activate();
    }

    override public void Start() {
      _check_state = FuzzyTimer.Instance.DoEvery(Activate, 3600000, 500);
      base.Start();
    }

    override public void Stop() {
      _check_state.TryCancel();
      base.Stop();
    }

    ///<summary>Once every hour, the CO will attempt to connect to remote end
    ///points in _connection_state that aren't connected</summary>
    override public void Activate() {
      if(!IsActive) {
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
        ConnectTo(addr);
      }
    }

    override protected bool ConnectionDesired(Address addr)
    {
      lock(_sync) {
        return _connection_state.ContainsKey(addr);
      }
    }

    override protected void FailedConnectionAttempt(Address addr)
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
      if(IsActive) {
        // Well we should be connected, but we aren't, try again!
        DelayedConnectTo(addr, true);
      }
    }

    override protected void LostConnection(Connection con)
    {
      lock(_sync) {
        if(!_connection_state.ContainsKey(con.Address)) {
         return;
        }
        
        _connection_state[con.Address] = MCState.Off;
      }

      if(ProtocolLog.ManagedCO.Enabled) {
        ProtocolLog.Write(ProtocolLog.ManagedCO, String.Format(
                          "Disconnection: {0} at {1}",
                          con, DateTime.UtcNow));
      }
      if(IsActive) {
        ConnectTo(con.Address);
      }
    }

    override protected void ObtainedConnection(Connection con)
    {
      lock(_sync) {
        if(!_connection_state.ContainsKey(con.Address)) {
          return;
        }
        _connection_state[con.Address] = MCState.On;
      }

      if(ProtocolLog.ManagedCO.Enabled) {
        ProtocolLog.Write(ProtocolLog.ManagedCO, String.Format(
                          "Connection: {0} at {1}",
                          con, DateTime.UtcNow));
      }
    }

    override public void Set(Address addr)
    {
      lock(_sync) {
        if(_connection_state.ContainsKey(addr)) {
          return;
        }
        _connection_state[addr] = MCState.Off;
      }


      if(IsActive) {
        ConnectTo(addr);
      }
    }

    override public void Unset(Address addr)
    {
      lock(_sync) {
        if(!_connection_state.ContainsKey(addr)) {
          return;
        }
        _connection_state.Remove(addr);
      }

      DelayedRemove(addr);
    }
  }
}
