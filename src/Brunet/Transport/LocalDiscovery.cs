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

using Brunet.Concurrent;
using Brunet.Util;
using Brunet.Messaging;

using System;
using System.Collections;

namespace Brunet.Transport {
  /// <summary>Exchange TAs using IP multicast.</summary>
  public class LocalDiscovery : Discovery, IRpcHandler {
    public const string RPC_CLASS = "LocalDiscovery";
    public static readonly string[] EMPTY_LIST = new string[0];
    protected readonly IPHandler _iphandler;
    protected readonly RpcManager _rpc;
    protected readonly string _realm;

    public LocalDiscovery(ITAHandler ta_handler, string realm, RpcManager rpc, IPHandler iphandler) :
      base(ta_handler)
    {
      _rpc = rpc;
      _iphandler = iphandler;
      _realm = realm;
      _rpc.AddHandler(RPC_CLASS, this);
    }

    public void HandleRpc(ISender caller, string method, IList args, object rs) {
      object result = null;
      if(method.Equals("SeekTAs")) {
        if(args.Count != 1) {
          throw new Exception("Not enough parameters");
        } else if(_realm.Equals(args[0])) {
          result = LocalTAsToString(20);
        } else {
          // We can't handle the request, let's send back an empty list
          result = EMPTY_LIST;
        }
      } else {
        throw new Exception("Invalid method");
      }
      _rpc.SendResult(rs, result);
    }

    /// <summary>Seek TAs.</summary>
    protected override void SeekTAs(DateTime now)
    {
      Channel queue = new Channel();
      queue.EnqueueEvent += HandleSeekTAs;
      try {
        ISender mcs = _iphandler.CreateMulticastSender();
        _rpc.Invoke(mcs, queue, RPC_CLASS + ".SeekTAs", _realm);
      } catch(SendException) {
        /*
         * On planetlab, it is not uncommon to have a node that does not allow
         * Multicast, and it will throw an exception here.  We just ignore this
         * information for now.  If we don't the heartbeatevent in the node
         * will not execute properly.
         */ 
      }
    }

    /// <summary>Incoming TAs are added to the ITAHandler</summary>
    protected void HandleSeekTAs(Object o, EventArgs ea)
    {
      Channel queue = (Channel) o;
      IList tas_as_str = null;
      try {
        RpcResult rpc_reply = (RpcResult) queue.Dequeue();
        tas_as_str = (IList) rpc_reply.Result;
      } catch {
        // Remote end point doesn't have LocalDiscovery enabled.
        return;
      }

      UpdateRemoteTAs(tas_as_str);
    }
  }
}
