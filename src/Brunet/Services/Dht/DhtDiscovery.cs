/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida
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

using Brunet.Connections;
using Brunet.Concurrent;
using Brunet.Symphony;
using Brunet.Transport;
using Brunet.Util;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Text;

namespace Brunet.Services.Dht {
  /// <summary>Use another nodes Dht to exchange SubringTransportAddresses.</summary>
  public class DhtDiscovery : Discovery {
    /// <summary>Make the TTL last an hour.</summary>
    public const int PUT_DELAY_S = 3600;
    protected readonly IDht _dht;
    protected readonly RpcDhtProxy _dht_proxy;
    protected readonly StructuredNode _node;
    protected int _ongoing;
    protected readonly MemBlock _p2p_address;
    protected readonly MemBlock _private_dht_key;
    protected readonly string _shared_namespace;
    protected int _steady_state;

    /// <summary>Uses the Dht for the bootstrap problem.</summary>
    /// <param name="node">The node needing remote tas.</param>
    /// <param name="dht">The dht for the shared overlay.</param>
    /// <param name="dht_proxy">A dht proxy for the shared overlay.</param>
    public DhtDiscovery(StructuredNode node, IDht dht, string shared_namespace,
        RpcDhtProxy dht_proxy) :
      base(node)
    {
      _dht = dht;
      _dht_proxy = dht_proxy;
      _node = node;
      _shared_namespace = shared_namespace;
      string skey = "PrivateOverlay:" + node.Realm;
      byte[] bkey = Encoding.UTF8.GetBytes(skey);
      _p2p_address = node.Address.ToMemBlock();
      _private_dht_key = MemBlock.Reference(bkey);

      _ongoing = 0;
      _steady_state = 0;
      _dht_proxy.Register(_private_dht_key, _p2p_address, PUT_DELAY_S);
    }

    /// <summary>Stops publishing using the dht proxy.</summary>
    override public bool Stop()
    {
      _dht_proxy.Unregister(_private_dht_key, _p2p_address);

      bool first = base.EndFindingTAs();

      if(Interlocked.Exchange(ref _steady_state, 0) == 1) {
        FuzzyEvent fe = _fe;
        if(fe != null) {
          _fe.TryCancel();
        }
        first = true;
      }

      return first;
    }

    override protected void SeekTAs(DateTime now)
    {
      if(Interlocked.Exchange(ref _ongoing, 1) == 1) {
        return;
      }

      Channel chan = new Channel();

      EventHandler handler = delegate(object o, EventArgs ea) {
        List<TransportAddress> tas = new List<TransportAddress>();
        while(chan.Count > 0) {
          AHAddress addr = null;
          try {
            IDictionary dict = (IDictionary) chan.Dequeue();
            byte[] baddr = (byte[]) dict["value"];
            addr = new AHAddress(MemBlock.Reference(baddr));
          } catch {
            continue;
          }
          tas.Add(new SubringTransportAddress(addr, _shared_namespace));
        }

        if(tas.Count > 0) {
          CheckAndUpdateRemoteTAs(tas);
        }

        if(chan.Closed) {
          Interlocked.Exchange(ref _ongoing, 0);
        }
      };

      if(_steady_state == 0) {
        chan.EnqueueEvent += handler;
      }
      chan.CloseEvent += handler;

      try {
        _dht.AsyncGet(_private_dht_key, chan);
      } catch(DhtException) {
        chan.Close();
      }
    }

    /// <summary>Make sure there are no entries in the Dht, who we should be
    /// connected to, but aren't.</summary>
    protected void CheckAndUpdateRemoteTAs(List<TransportAddress> tas)
    {
      AHAddress right = null, left = null;
      BigInteger right_dist = null, left_dist = null;
      AHAddress addr = _node.Address as AHAddress;

      // Find the closest left and right nodes
      foreach(TransportAddress ta in tas) {
        AHAddress target = (ta as SubringTransportAddress).Target;
        if(target.Equals(addr)) {
          continue;
        }
        BigInteger ldist = addr.LeftDistanceTo(target);
        BigInteger rdist = addr.RightDistanceTo(target);

        if(left_dist == null || ldist < left_dist) {
          left_dist = ldist;
          left = target;
        }

        if(right_dist == null || rdist < right_dist) {
          right_dist = rdist;
          right = target;
        }
      }

      ConnectionList cl = _node.ConnectionTable.GetConnections(ConnectionType.Structured);
      int local_idx = ~cl.IndexOf(_node.Address);

      if(left != null) {
        int remote_idx = ~cl.IndexOf(left);
        // If we're not connected to the left closest and its closer than any
        // of our current peers, let's connect to it
        if(remote_idx > 0 && Math.Abs(local_idx - remote_idx) < 2) {
          List<TransportAddress> tmp_tas = new List<TransportAddress>(1);
          tmp_tas.Add(new SubringTransportAddress(left, _shared_namespace));
          Linker linker = new Linker(_node, null, tmp_tas, "leaf", addr.ToString());
          linker.Start();
        }
      }

      if(right != null && right != left) {
        int remote_idx = ~cl.IndexOf(right);
        // If we're not connected to the right closest and its closer than any
        // of our current peers, let's connect to it
        if(remote_idx > 0 && Math.Abs(local_idx - remote_idx) < 2) {
          List<TransportAddress> tmp_tas = new List<TransportAddress>(1);
          tas.Add(new SubringTransportAddress(right, _shared_namespace));
          Linker linker = new Linker(_node, null, tmp_tas, "leaf", addr.ToString());
          linker.Start();
        }
      }

      UpdateRemoteTAs(tas);
    }

    override public bool BeginFindingTAs()
    {
      if(Interlocked.Exchange(ref _steady_state, 0) == 1) {
        FuzzyEvent fe = _fe;
        if(fe != null) {
          _fe.TryCancel();
        }
      }

      if(base.BeginFindingTAs()) {
        return true;
      }

      return false;
    }

    override public bool EndFindingTAs()
    {
      if(!base.EndFindingTAs()) {
        return false;
      }

      // Steady-state arrived at once the node has been connected.  Steady-state
      // means we need to continually check the Dht to prevent partitions.
      Interlocked.Exchange(ref _steady_state, 1);
      _fe = Brunet.Util.FuzzyTimer.Instance.DoEvery(SeekTAs, DELAY_MS * 30, DELAY_MS / 2);
      SeekTAs(DateTime.UtcNow);
      return true;
    }
  }
}
