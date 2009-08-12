/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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

/*
 * Dependencies : 
 Brunet.TransportAddress;
 Brunet.Edge;
 */

using Brunet;
using System;

namespace Brunet
{

  /**
   * This a derived class from the base class Edge.
   * It is designed for the sole purpose of testing the AHRoutingTable
   * The only functionality is that it has the local and remote TransportAddress
   */

  public class FakeEdge:Brunet.Edge
  {

    private TransportAddress local_add;
    private TransportAddress remote_add;
    private readonly TransportAddress.TAType _tatype;

    public FakeEdge(TransportAddress local, TransportAddress remote) :
      this(local, remote, TransportAddress.TAType.Tcp)
    {
    }

    public FakeEdge(TransportAddress local, TransportAddress remote, TransportAddress.TAType tatype)
    {
      _tatype = tatype;
      local_add = local;
      remote_add = remote;
    }

    public override Brunet.TransportAddress LocalTA
    {
      get
      {
        return local_add;
      }
    }
    public override Brunet.TransportAddress RemoteTA
    {
      get
      {
        return remote_add;
      }
    }

    public override Brunet.TransportAddress.TAType TAType
    {
      get
      {
        return _tatype;
      }
    }
  }

}
