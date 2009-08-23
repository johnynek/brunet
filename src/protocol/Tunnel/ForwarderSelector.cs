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
  /// <summary>Because the only way to instantiate objects from type information
  /// is via reflection and I'm not a big fan of reflection, I use an object
  /// factory to create new objects.  In this case, we are generating Forwarder 
  /// Selectors.</summary>
  public interface IForwarderSelectorFactory {
    IForwarderSelector GetForwarderSelector();
  }

  /// <summary>Creates new SimpleForwarderSelector.</summary>
  public class SimpleForwarderSelectorFactory : IForwarderSelectorFactory {
    public IForwarderSelector GetForwarderSelector()
    {
      return new SimpleForwarderSelector();
    }
  }

  /// <summary>Selects an address from a list of addresses.</summary>
  public interface IForwarderSelector {
    void Update(List<Connection> cons);
    Connection NextForwarder { get; }
  }

  /// <summary>An ForwarderSelector that returns an address from the list
  /// pseudorandomly.</summary>
  public class SimpleForwarderSelector : IForwarderSelector {
    protected int _count;
    protected List<Connection> _cons;

    public SimpleForwarderSelector()
    {
      _count = 0;
    }

    public Connection NextForwarder {
      get {
        int pos = Interlocked.Increment(ref _count);
        List<Connection> cons = _cons;
        return cons[pos % cons.Count];
      }
    }

    public void Update(List<Connection> cons)
    {
      // Strip out tunnels as forwarders, if there is a non-tunnel forwarder it
      // would probably be closer in the ring then one of our tunnel forwarders
      List<Connection> no_tunnels = new List<Connection>();
      foreach(Connection con in cons) {
        if(con.Edge is TunnelEdge) {
          continue;
        }
        no_tunnels.Add(con);
      }

      // We only swap if there is at least one tunnel that is not a forwarder
      if(no_tunnels.Count > 0) {
        cons = no_tunnels;
      }

      Interlocked.Exchange(ref _cons, cons);
    }
  }
}
