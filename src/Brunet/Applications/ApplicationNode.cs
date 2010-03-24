/*
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

using Brunet.Symphony;
using Brunet.Services.Coordinate;
using Brunet.Services.Dht;
using Brunet.Services.XmlRpc;
using Brunet.Security.Protocol;

namespace Brunet.Applications {
  public class ApplicationNode {
    public readonly StructuredNode Node;
    public readonly IDht Dht;
    public readonly RpcDhtProxy DhtProxy;
    public readonly NCService NCService;
    public readonly ProtocolSecurityOverlord SecurityOverlord;

    public ApplicationNode(StructuredNode node, IDht dht, RpcDhtProxy dht_proxy,
        NCService ncservice ,ProtocolSecurityOverlord security_overlord)
    {
      Node = node;
      Dht = dht;
      DhtProxy = dht_proxy;
      NCService = ncservice;
      SecurityOverlord = security_overlord;
    }
  }
}
