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
  /// factory to create new objects.  In this case, we are generating Address
  /// Selectors.</summary>
  public interface IAddressSelectorFactory {
    IAddressSelector GetAddressSelector();
  }

  /// <summary>Creates new SimpleAddressSelector.</summary>
  public class SimpleAddressSelectorFactory : IAddressSelectorFactory {
    public IAddressSelector GetAddressSelector()
    {
      return new SimpleAddressSelector();
    }
  }

  /// <summary>Selects an address from a list of addresses.</summary>
  public interface IAddressSelector {
    void Update(List<Address> addresses);
    Address NextAddress { get; }
  }

  /// <summary>An AddressSelector that returns an address from the list
  /// pseudorandomly.</summary>
  public class SimpleAddressSelector : IAddressSelector {
    protected int _count;
    protected List<Address> _addresses;

    public SimpleAddressSelector()
    {
      _count = 0;
    }

    public Address NextAddress {
      get {
        int pos = Interlocked.Increment(ref _count);
        var addresses = _addresses;
        return addresses[pos % addresses.Count];
      }
    }

    public void Update(List<Address> addresses)
    {
      Interlocked.Exchange(ref _addresses, addresses);
    }
  }
}
