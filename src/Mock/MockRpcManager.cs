/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2008 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using System;

#if BRUNET_NUNIT
using NUnit.Framework;
using System.Collections;
#endif

namespace Brunet.Mock {
  /// <summary>This class provides a many to one Rpc class for connection
  /// between many clients and a single server.  The server should as normal
  /// call AddHandler and the clients call Invoke.</summary>
  /// <remarks>Currently the use of senders is non-existant methods that rely
  /// on the sender will fail.</remarks>
  public class MockRpcManager: RpcManager {
    public MockRpcManager(): base(null)
    {
    }

    override public void Invoke(ISender target, Channel q, string method,
                                params object[] args)
    {
      IRpcHandler handler = null;
      string mname = null;
      lock( _sync ) {
        object[] info = (object[]) _method_cache[method];
        if( info == null ) {
          string[] parts = method.Split('.');
          string hname = parts[0];
          mname = parts[1];
          
          handler = (IRpcHandler)_method_handlers[ hname ];
          if( handler == null ) {
            //No handler for this.
            throw new AdrException(-32601, "No Handler for method: " + method);
          }
          info = new object[2];
          info[0] = handler;
          info[1] = mname;
          _method_cache[ method ] = info;
        }
        else {
          handler = (IRpcHandler)info[0];
          mname = (string)info[1];
        }
      }

      handler.HandleRpc(target, mname, args, q);
    }

    override public void SendResult(object request_state, object result) {
      Channel q = (Channel) request_state;
      q.Enqueue(new RpcResult(null, result, null));
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class MRpcMTestReflection {
    protected RpcManager rpc;
    public MRpcMTestReflection()
    {
      rpc = new MockRpcManager();
      rpc.AddHandler("test", this); 
    }

    [Test]
    public void Test() {
      Channel q = new Channel(1);
      rpc.Invoke(null, q, "test.test");
      RpcResult res = (RpcResult) q.Dequeue();
      bool val = (bool) res.Result;
      Assert.IsTrue(val, "Reflection Test");
    }

    public bool test() {
      return true;
    }
  }

  [TestFixture]
  public class MRpcMTestIRH: MRpcMTestReflection, IRpcHandler {
    public void HandleRpc(ISender target, string method_name, IList args, object res) {
      object rv = new Exception("Invalid method!");
      if(method_name.Equals("test")) {
        rv = test();
      }

      rpc.SendResult(res, rv);
    }
  }
#endif
}
