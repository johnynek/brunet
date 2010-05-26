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
