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

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;

using Brunet.Security.Transport;
using Brunet.Services.Coordinate;
using Brunet.Services.Dht;
using Brunet.Symphony;
using Brunet.Transport;
using Brunet.Util;

namespace Brunet.Simulator {
  public class SubringSimulator : Simulator {
    protected Simulator _shared_overlay;
    protected static readonly List<TransportAddress> EMPTY_TAS =
      new List<TransportAddress>(0);

    public SubringSimulator(SubringParameters sparams) :
      base(sparams.PrivateParameters, true)
    {
      _shared_overlay = new Simulator(sparams.PublicParameters);
      Start();
    }

    /// <summary>Create a new node in the public overlay and a matching one in
    /// the private overlay.</summary>
    override public Node AddNode()
    {
      Node snode = _shared_overlay.AddNode();
      NodeMapping snm = _shared_overlay.Nodes[snode.Address];

      Node node = AddNode(snm.ID, snode.Address as AHAddress);
      EdgeListener el = new SubringEdgeListener(snode, node);
      if(_secure_edges) {
        NodeMapping pnm = Nodes[node.Address] as NodeMapping;
        el = new SecureEdgeListener(el, pnm.SO);
      }
      node.AddEdgeListener(el);
      node.AddTADiscovery(new DhtDiscovery(node as StructuredNode,
            snm.Dht, snm.Node.Realm, snm.DhtProxy));
      StartingNetworkSize++;
      return node;
    }

    /// <summary>Overriden to setup PathELs.</summary>
    protected override EdgeListener CreateEdgeListener(int id)
    {
      NodeMapping snm = _shared_overlay.TakenIDs[id];
      if(snm.PathEM == null) {
        throw new Exception("Pathing should be enabled");
      }
      NodeMapping pnm = TakenIDs[id];
      pnm.PathEM = snm.PathEM;
      PType path_p = PType.Protocol.Pathing;
      pnm.Node.DemuxHandler.GetTypeSource(path_p).Subscribe(pnm.PathEM, path_p);
      return snm.PathEM.CreatePath();
    }

    /// <summary>Clears the TA list for private nodes.</summary>
    override protected List<TransportAddress> GetRemoteTAs()
    {
      return EMPTY_TAS;
    }
  }
}
