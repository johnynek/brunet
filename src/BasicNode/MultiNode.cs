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
  <summary>This class provides a layer on top of BasicNode to support creating
  multiple Brunet.Nodes in a single application.</summary>
  */
  public class MultiNode: BasicNode {
    /// <summary>Contains a list of all the Brunet.Nodes.</summary>
    protected ArrayList _nodes;
    /// <summary>Contains a list of all the Brunet.Nodes Connect calls.</summary>
    protected ArrayList _threads;
    /// <summary>The total amount of Brunet.Nodes.</summary>
    protected int _count;
    public MultiNode(String path, int count): base(path) {
      _count = count;
      _nodes = new ArrayList(_count);
      _threads = new ArrayList(_count);
    }

    /**
    <summary>This is overloaded so that we can get the base._node and move it
    into an ArrayList (_nodes), before we create a new base._node and it is
    overwritten.  This uses BasicNode.CreateNode to create new nodes.  Every
    time this is called a new NodeAddress (BrunetAddress) is generated, these
    are one time use and are not stored for future use.</summary>
    */
    public override void CreateNode() {
      _node_config.NodeAddress = (Utils.GenerateAHAddress()).ToString();
      base.CreateNode();
      new Information(_node, "MultiNode");
      _nodes.Add(_node);
    }

    /**
    <summary>This is where the magic happens!  Sets up Shutdown, creates all
    the nodes, and call Connect on them in separate threads.</summary>
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

    /// <summary>Disconnect all the nodes.  Called by Shutdown.OnExit</summary>
    public override void OnExit() {
      foreach(StructuredNode node in _nodes) {
        node.Disconnect();
      }
    }

    /**
    <summary>Not implemented, don't call the base classes version either!</summary>
    <exception cref="Exception">This method should not be called.</exception>
    */
    public override void StartServices() {
      throw new Exception("This is not supported for MultiNode, run a BasicNode to access this.");
    }

    /**
    <summary>Not implemented, don't call the base classes version either!</summary>
    <exception cref="Exception">This method should not be called.</exception>
    */
    public override void StopServices() {
      throw new Exception("This is not supported for MultiNode, run a BasicNode to access this.");
    }

    /**
    <summary>Not implemented, don't call the base classes version either!</summary>
    <exception cref="Exception">This method should not be called.</exception>
    */
    public override void SuspendServices() {
      throw new Exception("This is not supported for MultiNode, run a BasicNode to access this.");
    }

    /**
    <summary>Runs the MultiNode.</summary>
    <remarks>
    <para>To execute this at a command-line using Mono with 10 nodes:</para>
    <code>
    mono MultiNode.exe path/to/node_config 10
    </code>
    <para>To execute this at a command-line using Windows .NET with 15 nodes:
    </para>
    <code>
    MultiNode.exe path/to/node_config 15
    </code>
    </remarks>
    <param name="args">The command line arguments required are a path to a
    NodeConfig and the count of Brunet.Nodes to run.</param>
    */
    public static new int Main(String[] args) {
      int count = 0;
      try {
        count = Int32.Parse(args[1]);
      }
      catch {
        Console.WriteLine("Input paramters are %0 %1, where %0 is a config" +
            " file and %1 is the count of nodes.");
        return 0;
      }

      MultiNode node = new MultiNode(args[0], count);
      node.Run();
      return 0;
    }
  }
}
