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
using System.Collections.Generic;
using System.Threading;
using System.Net;

using Brunet.Services.Dht;
using Brunet.Services.XmlRpc;
using Brunet;

namespace Brunet.Applications {
  /// <summary>This class provides a layer on top of BasicNode to support
  /// creating multiple Brunet.Nodes in a single application.</summary>
  public class MultiNode: BasicNode {
    /// <summary>Contains a list of all the ApplicationNodes.</summary>
    protected List<ApplicationNode> _nodes;
    /// <summary>Contains a list of all the Brunet.Nodes Connect calls.</summary>
    protected List<Thread> _threads;
    /// <summary>The total amount of Brunet.Nodes.</summary>
    protected int _count;

    public MultiNode(NodeConfig node_config, int count) : base(node_config) 
    {
      _count = count;
      _nodes = new List<ApplicationNode>(count);
      _threads = new List<Thread>(count - 1);
    }

    /// <summary>This is where the magic happens!  Sets up Shutdown, creates all
    /// the nodes, and call Connect on them in separate threads.</summary>
    public override void Run()
    {
      string node_addr = _node_config.NodeAddress;
      for(int i = 1; i < _count; i++) {
        _node_config.NodeAddress = (Utils.GenerateAHAddress()).ToString();
        ApplicationNode node = CreateNode(_node_config);
        new Information(node.Node, "MultiNode", node.SecurityOverlord);
        _nodes.Add(node);
        Thread thread = new Thread(node.Node.Connect);
        thread.Start();
        _threads.Add(thread);
      }

      _node_config.NodeAddress = node_addr;
      _app_node = CreateNode(_node_config);
      new Information(_app_node.Node, "MultiNode", _app_node.SecurityOverlord);
      _nodes.Add(_app_node);
      Console.WriteLine("Starting at {0}, {1} is connecting to {2}.",
          DateTime.UtcNow, _app_node.Node.Address, _app_node.Node.Realm);
      _app_node.Node.Connect();
    }

    /// <summary>All nodes are disconnected?  Stop the PathEL.</summary>
    protected override void StopPem(DateTime now)
    {
      bool stop = true;
      foreach(ApplicationNode node in _nodes) {
        if(node.Node.ConState != Node.ConnectionState.Disconnected) {
          stop = false;
          break;
        }
      }

      if(stop) {
        foreach(PathELManager pem in _type_to_pem.Values) {
          pem.Stop();
        }
      }
    }

    /// <summary>Disconnect all the nodes.  Called by Shutdown.OnExit</summary>
    public override void OnExit()
    {
      foreach(ApplicationNode node in _nodes) {
        node.Node.Disconnect();
      }
      base.OnExit();
    }
  }
}
