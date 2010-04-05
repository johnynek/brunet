/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Xml;
using System.Xml.Serialization;
using System.Threading;
using System.Net;

using Brunet.Services.Dht;
using Brunet.Services.XmlRpc;
using Brunet;

namespace Brunet.Applications {
  /// <summary>This class provides a layer on top of BasicNode to support
  /// creating multiple Brunet.Nodes in a single application.</summary>
  public class MultiNode: BasicNode {
    /// <summary>Contains a list of all the Brunet.Nodes.</summary>
    protected ArrayList _nodes;
    /// <summary>Contains a list of all the Brunet.Nodes Connect calls.</summary>
    protected ArrayList _threads;
    /// <summary>The total amount of Brunet.Nodes.</summary>
    protected int _count;
    protected NodeConfig _node_config_single;

    public MultiNode(NodeConfig node_config, int count) : base(node_config) 
    {
      _node_config_single = Utils.Copy<NodeConfig>(node_config);
      _node_config_single.Path = node_config.Path;

      foreach(NodeConfig.EdgeListener item in _node_config.EdgeListeners) {
        item.port = 0;
      }

      _count = count;
      _nodes = new ArrayList(_count - 1);
      _threads = new ArrayList(_count - 1);
    }

    /// <summary>This is where the magic happens!  Sets up Shutdown, creates all
    /// the nodes, and call Connect on them in separate threads.</summary>
    public override void Run() {
      _shutdown = Shutdown.GetShutdown();
      _shutdown.OnExit += OnExit;

      for(int i = 1; i < _count; i++) {
        _node_config.NodeAddress = (Utils.GenerateAHAddress()).ToString();
        CreateNode();
        new Information(_node, "MultiNode", _bso);
        _nodes.Add(_node);
        Thread thread = new Thread(_node.Connect);
        thread.Start();
        _threads.Add(thread);
      }

      _node_config = _node_config_single;
      base.Run();
    }

    /// <summary>Disconnect all the nodes.  Called by Shutdown.OnExit</summary>
    public override void OnExit() {
      foreach(Node node in _nodes) {
        node.Disconnect();
      }
      base.OnExit();
    }
  }
}
