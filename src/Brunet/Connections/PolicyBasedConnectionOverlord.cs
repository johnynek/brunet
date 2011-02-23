/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using Brunet.Collections;
using Brunet.Connections;
using Brunet.Messaging;
using Brunet.Transport;
using Brunet.Util;

namespace Brunet.Connections {
  /// <summary>Creates new connections ... on demand, when an address has
  /// data being sent to it, the method Active should be calld.  An internal
  /// internal timer (FuzzyTimer) automatically removes inactive connections.</summary>
  abstract public class PolicyBasedConnectionOverlord : ConnectionOverlord {
    protected bool _active;
    override public bool IsActive 
    {
      get { return _active; }
      set {
        _active = value;
        System.Threading.Thread.MemoryBarrier();
      }
    }

    override public bool NeedConnection { get { return false; } }

    abstract public string Type { get; }
    abstract public ConnectionType MainType { get; }

    public PolicyBasedConnectionOverlord(Node node)
    {
      _node = node;
      _node.ConnectionTable.ConnectionEvent += ConnectHandler;
      _node.ConnectionTable.DisconnectionEvent += DisconnectHandler;
    }

    /// <summary>Express desire to connect to the remote address.</summary>
    abstract public void Set(Address addr);
    /// <summary>Express desire to end connections to the remote address.</summary>
    abstract public void Unset(Address addr);

    /// <summary>Returns true if a connection to the address specified is
    /// desirable, false otherwise.</summary>
    abstract protected bool ConnectionDesired(Address addr);
    /// <summary>Called when an attempt to the address specified has failed.</summary>
    abstract protected void FailedConnectionAttempt(Address addr);
    /// <summary>Called when a connection has been lost.</summary>
    abstract protected void LostConnection(Connection con);
    /// <summary>Called when a connection has been formed.</summary>
    abstract protected void ObtainedConnection(Connection con);

    /// <summary>This method is called when there is connection added to the
    /// ConnectionTable.</summary>
    protected void ConnectHandler(object tab, EventArgs eargs)
    {
      if(!IsActive) {
        return;
      }

      Connection con = ((ConnectionEventArgs)eargs).Connection;
      if(!con.MainType.Equals(MainType)) {
        return;
      }

      ObtainedConnection(con);
    }

    /// <summary>This method is called when there is a Disconnection from
    /// the ConnectionTable.</summary>
    protected void DisconnectHandler(object tab, EventArgs eargs)
    {
      if(!IsActive) {
        return;
      }

      Connection con = ((ConnectionEventArgs)eargs).Connection;
      if(!con.MainType.Equals(MainType)) {
        return;
      }

      LostConnection(con);
    }

    /// <summary> The connector has finished, if it succeeded, there should be
    /// some CTM, else the attempt failed.</summary>
    override protected void ConnectorEndHandler(object o, EventArgs eargs)
    {
      Connector con = o as Connector;
      Address addr = con.State as Address;
      if(addr != null && con.ReceivedCTMs.Count == 0) {
        CheckForConnection(addr);
      }
    }

    /// <summary>If we get here, the Linker has completed, we are either
    /// connected or need to reattempt the connection.</summary>
    override protected void LinkerEndHandler(object o, EventArgs eargs)
    {
      if(!IsActive) {
        return;
      }
      Linker linker = o as Linker;
      Address addr = linker.Target;
      CheckForConnection(addr);
    }

    /// <summary>Checks the connection table for a connection.  We call the
    /// appropriate abstract method based upon the result.</summary>
    protected void CheckForConnection(Address addr)
    {
      Connection con = _node.ConnectionTable.GetConnection(MainType, addr);

      if(con == null) {
        FailedConnectionAttempt(addr);
      } else {
        ObtainedConnection(con);
      }
    }

    protected void ConnectTo(Address addr)
    {
      ConnectTo(addr, Type, _node.Address.ToString());
    }

    /// <summary>Perform delayed operations to prevent infinite loops due to eventing
    /// issues or another worker performing the same task.</summary>
    protected void DelayedConnectTo(Address addr, bool fast)
    {
      Action<DateTime> callback = delegate(DateTime now) {
        // Maybe after the delay, the connection is no longer desired...
        if(!ConnectionDesired(addr)) {
          return;
        }

        ProtocolLog.WriteIf(ProtocolLog.OnDemandCO, "Retrying: " + addr);
        ConnectTo(addr);
      };

      if(fast) {
        FuzzyTimer.Instance.DoAfter(callback, 500, 500);
      } else {
        FuzzyTimer.Instance.DoAfter(callback, 15000, 500);
      }
    }

    /// <summary>Perform delayed operations so we don't end up unnecessarily churning
    /// through connections... the need for this should probably be evaluated.</summary>
    protected void DelayedRemove(Address addr)
    {
      Action<DateTime> callback = delegate(DateTime now) {
        // Maybe after the delay, the connection is now desired...
        if(ConnectionDesired(addr)) {
          return;
        }

        Connection con = _node.ConnectionTable.GetConnection(MainType, addr);
        // Don't proceed if there is no con, this CO didn't make the con, or
        // the con was initiated by the remote peer
        if(con == null || !con.ConType.Equals(Type) ||
            _node.Address.ToString().Equals(con.State.PeerLinkMessage.Token))
        {
          return;
        }

        ProtocolLog.WriteIf(ProtocolLog.OnDemandCO, "Closing: " + con);
        con.Close(_node.Rpc, "Closed by request of CO");
      };
      FuzzyTimer.Instance.DoAfter(callback, 60000, 500);
    }
  }
}
