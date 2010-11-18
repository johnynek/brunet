/*
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
      base(sparams.PrivateParameters, false)
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

      // Must do this to remove it after successfully creating the new node
      Node.StateChangeHandler add_node = null;
      
      // Delayed add, removes ~15 seconds off bootstrapping time
      add_node = delegate(Node n, Node.ConnectionState cs) {
        if(cs != Node.ConnectionState.Connected) {
          return;
        }
        snm.Node.StateChangeEvent -= add_node;

        Node node = AddNode(snm.ID, snode.Address as AHAddress);
        EdgeListener el = new SubringEdgeListener(snode, node);
        if(_secure_edges) {
          NodeMapping pnm = Nodes[node.Address] as NodeMapping;
          el = new SecureEdgeListener(el, pnm.SO);
        }
        node.AddEdgeListener(el);
        node.AddTADiscovery(new DhtDiscovery(node as StructuredNode,
              snm.Dht, snm.Node.Realm, snm.DhtProxy));
        CurrentNetworkSize--;
      };

      // Check will return true, since the Node is unregistered
      CurrentNetworkSize++;
      snm.Node.StateChangeEvent += add_node;
      return snode;
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
