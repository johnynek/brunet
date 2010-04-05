/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using Brunet.Util;
using Brunet.Concurrent;

using System;
using System.Collections;
using System.Security.Cryptography;

using Brunet.Messaging;
namespace Brunet.Services.Dht {
  /// <summary>Provides a BrunetRpc wrapper around the Dht so that the local
  /// node can perform Dht operations.</summary>
  /// <remarks>This replaces the older DhtClient.  The default behavior is to
  /// limit requests to the local node.</remarks>
  public class RpcDht : IRpcHandler {
    protected IDht _dht;
    protected Node _node;
    /* Use cache so that we don't experience a leak, this should be replaced 
     * with some timeout mechanism to support multiple interactions at the 
     * same time.
     */
    protected Brunet.Collections.Cache _channels;
    /// <summary>If true, only local node can perform ops.  When false anyone
    /// can. </summary>
    public bool LocalUseOnly;

    public RpcDht(IDht dht, Node node) {
      LocalUseOnly = true;
      _channels = new Brunet.Collections.Cache(100);
      _node = node;
      _dht = dht;
      _node.Rpc.AddHandler("DhtClient", this);
    }

    public void HandleRpc(ISender caller, string method, IList args, object rs) {
      if(LocalUseOnly) {
        try {
          ReqrepManager.ReplyState _rs = (ReqrepManager.ReplyState) caller;
          Node node = (Node) _rs.ReturnPath;
          if(node != _node) {
            throw new Exception();
          }
        } catch {
          AdrException e = new AdrException(-32602, new Exception("Must send from local node!"));
          _node.Rpc.SendResult(rs, e);
          return;
        }
      }

      object result = null;
      try {
        switch(method) {
          case "Create":
          {
            // Needs to be Async so we don't deadlock!
            MemBlock key = MemBlock.Reference((byte[]) args[0]);
            MemBlock value = MemBlock.Reference((byte[]) args[1]);
            int ttl = (int) args[2];
            Channel returns = new Channel(1);
            returns.CloseEvent += delegate(object o, EventArgs eargs) {
              _node.Rpc.SendResult(rs, returns.Dequeue());
            };
            _dht.AsyncCreate(key, value, ttl, returns);
            return;
          }
          case "Put":
          {
            // Needs to be Async so we don't deadlock!
            MemBlock key = MemBlock.Reference((byte[]) args[0]);
            MemBlock value = MemBlock.Reference((byte[]) args[1]);
            int ttl = (int) args[2];
            Channel returns = new Channel(1);
            returns.CloseEvent += delegate(object o, EventArgs eargs) {
              _node.Rpc.SendResult(rs, returns.Dequeue());
            };
            _dht.AsyncPut(key, value, ttl, returns);
            return;
          }
          case "Get":
          {
            // Needs to be Async so we don't deadlock!
            MemBlock key = MemBlock.Reference((byte[]) args[0]);
            Channel returns = new Channel();
            returns.CloseEvent += delegate(object o, EventArgs eargs) {
              Hashtable []results = new Hashtable[returns.Count];
              int pos = 0;
              while(returns.Count > 0) {
                results[pos++] = (Hashtable) returns.Dequeue();
              }
              _node.Rpc.SendResult(rs, results);
            };
            _dht.AsyncGet(key, returns);
            return;
          }
          case "BeginGet":
          {
            MemBlock key = MemBlock.Reference((byte[]) args[0]);
            result = BeginGet(key);
            break;
          }
          case "ContinueGet":
          {
            MemBlock token = MemBlock.Reference((byte[]) args[0]);
            ContinueGet(token, rs);
            return;
          }
          case "EndGet":
          {
            MemBlock token = MemBlock.Reference((byte[]) args[0]);
            EndGet(token);
            result = true;
            break;
          }
        }
      } catch (Exception e) {
        result = new AdrException(-32602, e);
      }
      _node.Rpc.SendResult(rs, result);
    }

    /// <summary>A slighlty more async version of Get</summary>
    /// <param name="key">Key to retrieve</param>
    /// <returns>A token used to continue or end the get</returns>
    public byte[] BeginGet(MemBlock key) {
      Channel q = new Channel();
      _dht.AsyncGet(key, q);
      byte[] tk = GenToken(key);
      _channels.Add(MemBlock.Reference(tk), q);
      return tk;
    }

    /// <summary>Block until a value returns from the get or the get ends.</summary>
    /// <param name="token">A token to specify which get to follow up.</param>
    /// <returns>A value from the get.</returns>
    public void ContinueGet(MemBlock token, object rs) {
      Channel q = (Channel) _channels[token];
      if(q == null) {
        throw new ArgumentException("Invalid token");
      }

      int sent = 0;
      EventHandler eh = null;
      eh = delegate(object o, EventArgs ea) {
        if(System.Threading.Interlocked.Exchange(ref sent, 1) == 1) {
          return;
        }
        Channel ch = o as Channel;

        IDictionary res = null;
        try {
          res = (IDictionary) ch.Dequeue();
        } catch {
          if(ch.Closed) {
            ch.Close();
            res = new Hashtable();
          }
        }

        if(res != null) {
          _node.Rpc.SendResult(rs, res);
          ch.CloseEvent -= eh;
          ch.EnqueueEvent -= eh;
        }
      };

      try {
        q.CloseEvent += eh;
      } catch {
        eh(q, EventArgs.Empty);
      }

      if(!q.Closed) {
        if(q.Count > 0) {
          eh(q, EventArgs.Empty);
        } else {
          q.EnqueueEvent += eh;
        }
      }
    }

    /// <summary>Closes a BeginGet.</summary>
    /// <param name="token">A token to specify which get to close.</param>
    public void EndGet(MemBlock token) {
      Channel q = (Channel) _channels[token];
      if (q == null) {
        throw new ArgumentException("Invalid token");
      }
      else {
        q.Close();
        _channels.Remove(q);
      }
    }

    protected byte[] GenToken(MemBlock key) {
      RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
      byte[] token = new byte[20];
      provider.GetBytes(token);
      byte[] res = new byte[40];
      key.CopyTo(res, 0);
      token.CopyTo(res, 20);
      return res;
    }
  }
}
