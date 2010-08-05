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

namespace Brunet.Symphony {
  /// <summary>Creates new connections ... on demand, when an address has
  /// data being sent to it, the method Active should be calld.  An internal
  /// internal timer (FuzzyTimer) automatically removes inactive connections.</summary>
  public class OnDemandConnectionOverlord : ConnectionOverlord {
    protected bool _active;
    public override bool IsActive 
    {
      get { return _active; }
      set {
        _active = value;
        System.Threading.Thread.MemoryBarrier();
      }
    }

    protected readonly TimeBasedCache<Address, bool> _cache;
    /// <summary>Clean up the cache entries every 7.5 minutes.</summary>
    public const int CLEANUP_TIME_MS = 450000;

    override public bool NeedConnection { get { return false; } }
    public const string TYPE = "structured.ondemand";
    public static readonly ConnectionType MAIN_TYPE = Connection.StringToMainType(TYPE);

    protected readonly static TAAuthorizer _ta_auth = new TATypeAuthorizer(
          new TransportAddress.TAType[]{TransportAddress.TAType.Subring},
          TAAuthorizer.Decision.Deny,
          TAAuthorizer.Decision.None);
    public override TAAuthorizer TAAuth { get { return _ta_auth;} }

    public OnDemandConnectionOverlord(Node node)
    {
      _node = node;
      _cache = new TimeBasedCache<Address, bool>(CLEANUP_TIME_MS);
      _cache.EvictionHandler += HandleEviction;
      _node.ConnectionTable.ConnectionEvent += ConnectHandler;
      _node.ConnectionTable.DisconnectionEvent += DisconnectHandler;
    }

    /// <summary>There's nothing to activate ... its all handled by state.</summary>
    public override void Activate()
    {
    }

    /// <summary>The connection is actively being used.</summary>
    /// <returns>True if the entry is already "active."</returns>
    public bool Active(Address dest)
    {
      if(!_cache.Update(dest, true)) {
        return true;
      }

      ProtocolLog.WriteIf(ProtocolLog.OnDemandCO, "Trying: " + dest);
      ConnectTo(dest, TYPE);
      return false;
    }

    /// <summary>Need to call stop in order to stop the recycling of the cache.</summary>
    public void Stop()
    {
      _cache.Stop();
    }

    /// <summary>When we get an eviction, we need to consider disconnecting the
    /// connection.</summary>
    protected void HandleEviction(object sender, TimeBasedCache<Address, bool>.EvictionArgs ea)
    {
      DelayedRemove(ea.Key);
    }

    /// <summary>This method is called when there is connection added to the
    /// ConnectionTable.  We set the connection state to true and thus won't
    /// attempt reconnecting to it, unless there is a disconnection</summary>
    protected void ConnectHandler(object tab, EventArgs eargs)
    {
      if(!_active) {
        return;
      }
      Connection con = ((ConnectionEventArgs)eargs).Connection;
      bool value;
      if(_cache.TryGetValue(con.Address, out value)) {
        ProtocolLog.WriteIf(ProtocolLog.OnDemandCO, "Got connection: " + con);
        return;
      } else if(con.ConType.Equals(TYPE)) {
        DelayedRemove(con.Address);
      }
    }

    /// <summary>This method is called when there is a Disconnection from
    /// the ConnectionTable.  If a disconnect occurs and it is an address
    /// managed by the OnDemandCO, reconnect.</summary>
    protected void DisconnectHandler(object tab, EventArgs eargs)
    {
      if(!_active) {
        return;
      }
      Connection con = ((ConnectionEventArgs)eargs).Connection;
      if(!con.ConType.Equals(TYPE)) {
        return;
      }
      bool value;
      if(!_cache.TryGetValue(con.Address, out value)) {
        ProtocolLog.WriteIf(ProtocolLog.OnDemandCO, "No longer needed: " + con);
        return;
      }
      if(_active) {
        ProtocolLog.WriteIf(ProtocolLog.OnDemandCO, "Lost: " + con);
        DelayedConnectTo(con.Address, true);
      }
    }

    /// <summary>If we get here, the Linker has completed, we are either
    /// connected or need to reattempt the connection.</summary>
    override protected void LinkerEndHandler(object o, EventArgs eargs)
    {
      if(!_active) {
        return;
      }
      Linker linker = o as Linker;
      Address addr = linker.Target;
      Connection con = _node.ConnectionTable.GetConnection(MAIN_TYPE, addr);

      bool value;
      if(!_cache.TryGetValue(addr, out value) && con != null && con.ConType.Equals(TYPE)) {
        DelayedRemove(addr);
      } else if(_active && con == null) {
        DelayedConnectTo(addr, false);
      } else if(con != null) {
        ProtocolLog.WriteIf(ProtocolLog.OnDemandCO, "We made the connection: " + con);
      }
    }

    /// <summary>Perform delayed operations to prevent infinite loops due to eventing
    /// issues or another worker performing the same task.</summary>
    protected void DelayedConnectTo(Address addr, bool fast)
    {
      Action<DateTime> callback = delegate(DateTime now) {
        bool value;
        if(_cache.TryGetValue(addr, out value)) {
          ProtocolLog.WriteIf(ProtocolLog.OnDemandCO, "Retrying: " + addr);
          ConnectTo(addr, TYPE);
        }
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
        bool value;
        if(_cache.TryGetValue(addr, out value)) {
          return;
        }

        Connection con = _node.ConnectionTable.GetConnection(MAIN_TYPE, addr);
        if(con == null) {
          return;
        }
        ProtocolLog.WriteIf(ProtocolLog.OnDemandCO, "Closing: " + con);
        con.State.Edge.Close();
      };
      FuzzyTimer.Instance.DoAfter(callback, 60000, 500);
    }
  }
}
