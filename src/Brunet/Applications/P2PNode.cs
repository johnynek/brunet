/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using NDesk.Options;
using System;

namespace Brunet.Applications {
  public class P2PNodeParameters : RuntimeParameters {
    public int Count { get { return _count; } }
    protected int _count = 1;
    protected string _count_s = string.Empty;

    public P2PNodeParameters() :
      base("P2PNode", "P2PNode -- Multifunction Brunet Node")
    {
      _options.Add("c|Count=", "Number of nodes to instantiate.", v => _count_s = v);
    }

    public override int Parse(string[] args)
    {
      if(base.Parse(args) != 0) {
        return -1;
      }

      if(_count_s != string.Empty) {
        if(!Int32.TryParse(_count_s, out _count) || _count <= 0) {
          _error_message = "Invalid count.  Count must be a positive integer.";
          return -1;
        }
      }

      return 0;
    }
  }

  public class P2PNode {
    public static int Main(String[] args) {
      P2PNodeParameters parameters = new P2PNodeParameters();
      if(parameters.Parse(args) != 0) {
        Console.WriteLine(parameters.ErrorMessage);
        parameters.ShowHelp();
        return -1;
      } else if(parameters.Help) {
        parameters.ShowHelp();
        return 0;
      }

      NodeConfig node_config = parameters.NodeConfig;
      if(node_config.NodeAddress == null) {
        node_config.NodeAddress = Utils.GenerateAHAddress().ToString();
        node_config.WriteConfig();
      }

      BasicNode node = null;
      if(parameters.Count == 1) {
        node = new BasicNode(node_config);
      } else {
        node = new MultiNode(node_config, parameters.Count);
      }
      node.Run();

      return 0;
    }
  }
}
