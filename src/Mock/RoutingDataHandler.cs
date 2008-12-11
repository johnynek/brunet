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
using System.Collections;

#if BRUNET_NUNIT
using NUnit.Framework;
using System.Security.Cryptography;
#endif

namespace Brunet.Mock {
  /// <summary>This class provides a RoutingDataHandler object that provides a
  /// filter to remove PTypes and route to subscribed IDataHandler.</summary>
  public class RoutingDataHandler: SimpleSource, IDataHandler {
    public RoutingDataHandler() {
    }

    public void HandleData(MemBlock payload, ISender return_path, object state) {
      if(_sub != null) {
        MemBlock rest = null;
        PType.Parse(payload, out rest);
        _sub.Handle(rest, return_path);
      }
    }
  }
}
