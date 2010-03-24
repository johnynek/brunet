/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
