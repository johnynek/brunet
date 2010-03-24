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
  public class Runner {
    public static int Main(String[] args) {
      string node_config_path = string.Empty;
      string str_count = string.Empty;
      int count = 1;
      bool show_help = false;

      OptionSet opts = new OptionSet() {
        { "n|NodeConfig=", "Path to a NodeConfig file.",
          v => node_config_path = v },
        { "c|Count=", "Number of nodes to instantiate.",
          v => str_count = v },
        { "h|help", "Display this help and exit.",
          v => show_help = v != null },
      };

      try {
        opts.Parse(args);
      } catch (OptionException e) {
        HandleError(e.Message);
        return -1;
      }

      if(show_help) {
        ShowHelp(opts);
        return -1;
      }

      if(str_count != string.Empty && (!Int32.TryParse(str_count, out count) || count < 0)) {
        HandleError("Invalid count.  Count must be a positive integer.");
        return -1;
      }

      NodeConfig node_config = null;
      try {
        node_config = NodeConfig.ReadConfig(node_config_path);
      } catch (Exception e) {
        HandleError("Invalid NodeConfig file: " + e.Message);
        return -1;
      }

      if(node_config.NodeAddress == null) {
        node_config.NodeAddress = Utils.GenerateAHAddress().ToString();
        node_config.WriteConfig();
      }

      BasicNode node = null;
      if(count == 1) {
        node = new BasicNode(node_config);
      } else {
        node = new MultiNode(node_config, count);
      }
      node.Run();

      return 0;
    }

    public static void HandleError(string error)
    {
      Console.WriteLine("P2PNode:");
      Console.WriteLine("\t{0}", error);
      Console.WriteLine("Try `P2PNode --help' for more information.");
    }

    public static void ShowHelp(OptionSet p) {
      Console.WriteLine("Usage: P2PNode Options");
      Console.WriteLine("P2PNode -- Multifunction Brunet Node");
      Console.WriteLine();
      Console.WriteLine("Options:");
      p.WriteOptionDescriptions(Console.Out);
    }
  }
}
