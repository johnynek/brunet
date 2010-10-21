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
  public class OnDemandConnectionOverlord : PolicyBasedConnectionOverlord {
    protected readonly TimeBasedCache<Address, bool> _cache;
    /// <summary>Clean up the cache entries every 7.5 minutes.</summary>
    public const int CLEANUP_TIME_MS = 450000;

    protected const string _type = "structured.ondemand";
    override public string Type { get { return _type; } }
    protected static readonly ConnectionType _main_type =
      Connection.StringToMainType(_type);
    override public ConnectionType MainType { get { return _main_type; } }

    protected readonly static TAAuthorizer _ta_auth = new TATypeAuthorizer(
          new TransportAddress.TAType[]{TransportAddress.TAType.Subring},
          TAAuthorizer.Decision.Deny,
          TAAuthorizer.Decision.None);
    override public TAAuthorizer TAAuth { get { return _ta_auth;} }

    public OnDemandConnectionOverlord(Node node) : base(node)
    {
      _cache = new TimeBasedCache<Address, bool>(CLEANUP_TIME_MS);
      _cache.EvictionHandler += HandleEviction;
    }

    override public void Set(Address addr)
    {
      if(!_cache.Update(addr, true)) {
        return;
      }

      ProtocolLog.WriteIf(ProtocolLog.OnDemandCO, "Trying: " + addr);
      ConnectTo(addr);
    }

    override public void Unset(Address addr)
    {
    }

    override protected bool ConnectionDesired(Address addr)
    {
      bool value;
      _cache.TryGetValue(addr, out value);
      return value;
    }

    override protected void FailedConnectionAttempt(Address addr)
    {
      if(!ConnectionDesired(addr)) {
        return;
      }

      if(IsActive) {
        DelayedConnectTo(addr, false);
      }
    }

    override protected void LostConnection(Connection con)
    {
      if(!ConnectionDesired(con.Address)) {
        if(con.ConType.Equals(Type)) {
          ProtocolLog.WriteIf(ProtocolLog.OnDemandCO, "No longer needed: " + con);
        }
        return;
      }

      if(IsActive) {
        ProtocolLog.WriteIf(ProtocolLog.OnDemandCO, "Lost: " + con);
        DelayedConnectTo(con.Address, true);
      }
    }

    override protected void ObtainedConnection(Connection con)
    {
      if(ConnectionDesired(con.Address)) {
        ProtocolLog.WriteIf(ProtocolLog.OnDemandCO, "Got connection: " + con);
      } else if(con.ConType.Equals(Type)) {
        DelayedRemove(con.Address);
      }
    }

    /// <summary>Need to call stop in order to stop the recycling of the cache.</summary>
    override public void Stop()
    {
      _cache.Stop();
      base.Stop();
    }

    /// <summary>When we get an eviction, we need to consider disconnecting the
    /// connection.</summary>
    protected void HandleEviction(object sender, TimeBasedCache<Address, bool>.EvictionArgs ea)
    {
      DelayedRemove(ea.Key);
    }
  }
}
