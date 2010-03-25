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

namespace Brunet.Relay {
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
        if(con.Edge is RelayEdge) {
          continue;
        }
        if(con.Edge.IsClosed) {
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
