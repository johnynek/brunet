/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Xml;
using System.Xml.Serialization;
using System.Threading;
using System.Net;

using Brunet.DistributedServices;
using Brunet.Rpc;
using Brunet;

namespace Brunet.Applications {
  /**
   * This class provides a layer on top of BasicNode to support creating
   * multiple Brunet.Nodes in a single application.
   */
  public class MultiNode: BasicNode {
    ArrayList _nodes;
    ArrayList _threads;
    int _count;
    public MultiNode(String path, int count): base(path) {
      _count = count;
      _nodes = new ArrayList(_count);
      _threads = new ArrayList(_count);
    }

    /**
     * This is overloaoded so that we can get the base._node and move it into
     * an ArrayList (_nodes), before we create a new base._node and it is
     * overwritten.
     */

    public override void CreateNode() {
      _node_config.NodeAddress = (Utils.GenerateAHAddress()).ToString();
      base.CreateNode();
      new Information(_node, "MultiNode");
      _nodes.Add(_node);
    }

    /**
     * This is where the magic happens!  Setups Shutdown and places the nodes
     * connect method into a starting thread.
     */

    public override void Run() {
      _shutdown = Shutdown.GetShutdown();
      if(_shutdown != null) {
        _shutdown.OnExit += OnExit;
      }

      for(int i = 0; i < _count; i++) {
        CreateNode();
        Thread thread = new Thread(_node.Connect);
        thread.Start();
        _threads.Add(thread);
      }
    }

    /**
     * Disconnect all the nodes.  Called by Shutdown.OnExit
     */

    public override void OnExit() {
      foreach(StructuredNode node in _nodes) {
        node.Disconnect();
      }
    }

    /**
     * Not implemented, don't call the base classes version either!
     */

    public override void StartServices() {
      throw new Exception("This is not supported for MultiNode, run a BasicNode to access this.");
    }

    /**
     * Not implemented, don't call the base classes version either!
     */

    public override void StopServices() {
      throw new Exception("This is not supported for MultiNode, run a BasicNode to access this.");
    }

    /**
     * Not implemented, don't call the base classes version either!
     */

    public override void SuspendServices() {
      throw new Exception("This is not supported for MultiNode, run a BasicNode to access this.");
    }

    /**
     * Input paramters for MultiNode are NodeConfig and count of Brunet Nodes
     */

    public static new int Main(String[] args) {
      int count = 0;
      try {
        count = Int32.Parse(args[1]);
      }
      catch {
        Console.WriteLine("Input paramters are %0 %1, where %0 is a config" +
            " file and %1 is the count of nodes.");
      }

      MultiNode node = new MultiNode(args[0], count);
      node.Run();
      return 0;
    }
  }
}
