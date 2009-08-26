/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using System;
using System.Collections.Generic;
using System.Threading;

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

    override protected void LinkerEndHandler(object o, EventArgs eargs)
    {
      Linker linker = o as Linker;
      Address target = linker.Target;
      if(target != null) {
        FinishConnectTo(target);
      }
    }

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
