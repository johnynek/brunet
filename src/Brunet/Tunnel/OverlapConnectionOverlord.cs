/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Collections.Generic;
using System.Threading;
using Brunet.Connections;
using Brunet.Transport;

namespace Brunet.Tunnel {
  /// <summary>A CO used to create Overlap so that tunnels can be made between
  /// two peers that cannot directly connect. </summary>
  public class OverlapConnectionOverlord : ConnectionOverlord {
    public static readonly string STRUC_OVERLAP = "structured.overlap";
    public static readonly ConnectionType MAIN_TYPE = Connection.StringToMainType(STRUC_OVERLAP);
    protected Dictionary<Address, WaitCallback> _address_to_callback;
    protected object _sync;

    public override TAAuthorizer TAAuth { get { return _ta_auth;} }
    protected readonly static TAAuthorizer _ta_auth = new TATypeAuthorizer(
          new TransportAddress.TAType[]{TransportAddress.TAType.Tunnel},
          TAAuthorizer.Decision.Deny,
          TAAuthorizer.Decision.None);

    public OverlapConnectionOverlord(Node node)
    {
      _node = node;
      _address_to_callback = new Dictionary<Address, WaitCallback>();
      _sync = new object();
    }

    /// <summary>We don't do anything here... </summary>
    override public void Activate()
    {
      throw new NotImplementedException();
    }

    protected volatile bool _active;
    override public bool IsActive
    {
      get { return _active; }
      set { _active = value; }
    }

    /// <summary> We never need a connection, we inject connections where
    /// necessary!</summary>
    override public bool NeedConnection { get { return false; }  }

    /// <summary>Attempt to connect to a remote overlay address and notify the
    /// caller when the process has completed, successful or not.</summary>
    public void ConnectTo(Address target, WaitCallback wcb)
    {
      bool exists = false;
      lock(_sync) {
        exists = _address_to_callback.ContainsKey(target);
        if(!exists) {
          _address_to_callback[target] = null;
        }
        _address_to_callback[target] += wcb;
      }
      if(exists) {
        return;
      }

      ConnectTo(target, STRUC_OVERLAP);
    }

    /// <summary>If we get a response, its handled by the base.HandleCtmResponse.
    /// If we don't get a response, we should finish this attempt to connect.</summary>
    override protected void ConnectorEndHandler(object o, EventArgs eargs)
    {
      Connector con = o as Connector;
      if(con.ReceivedCTMs.Count > 0) {
        return;
      }

      Address target = con.State as Address;
      if(target != null) {
        FinishConnectTo(target);
      }
    }

    /// <summary>At the end of the linking process, we are either connected or
    /// not connected to the remote endpoint, so we can notify the caller of the
    /// result.</summary>
    override protected void LinkerEndHandler(object o, EventArgs eargs)
    {
      Linker linker = o as Linker;
      Address target = linker.Target;
      if(target != null) {
        FinishConnectTo(target);
      }
    }

    /// <summary>Cleans up state and calls the callback so that it can
    /// determine by ConnectionTable inspection if the connection was
    /// successful.</summary>
    protected void FinishConnectTo(Address target)
    {
      WaitCallback callback = null;
      lock(_sync) {
        if(!_address_to_callback.TryGetValue(target, out callback)) {
          return;
        }
        _address_to_callback.Remove(target);
      }

      callback(target);
    }
  }
}
