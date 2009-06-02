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
using Brunet.Applications;
using Brunet.Security;
using NDesk.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Ipop.DhtNode {
  public class Runner {
    public static int Main(String[] args) {
      string node_config_path = string.Empty;
      string ipop_config_path = string.Empty;
      bool show_help = false;

      OptionSet opts = new OptionSet() {
        { "n|NodeConfig=", "Path to a NodeConfig file.",
          v => node_config_path = v },
        { "i|IpopConfig=", "Path to an IpopConfig file.",
          v => ipop_config_path = v },
        { "h|help", "Display this help and exit.",
          v => show_help = v != null },
      };

      try {
        opts.Parse(args);
      } catch (OptionException e) {
        Console.WriteLine("DhtIpop: ");
        Console.WriteLine(e.Message);
        Console.WriteLine("Try `DhtIpop.exe --help' for more information.");
        return -1;
      }

      if(show_help) {
        ShowHelp(opts);
        return -1;
      }

      if(node_config_path == string.Empty || !File.Exists(node_config_path)) {
        Console.WriteLine("DhtIpop: ");
        Console.WriteLine("\tMissing NodeConfig.");
        Console.WriteLine("Try `DhtIpop --help' for more information.");
        return -1;
      }

      if(ipop_config_path == string.Empty || !File.Exists(ipop_config_path)) {
        Console.WriteLine("DhtIpop: ");
        Console.WriteLine("\tMissing IpopConfig.");
        Console.WriteLine("Try `DhtIpop --help' for more information.");
        return -1;
      }

      NodeConfig node_config = null;
      try {
        node_config = Utils.ReadConfig<NodeConfig>(node_config_path);
        node_config.Path = node_config_path;
      } catch (Exception e) {
        Console.WriteLine("Invalid NodeConfig file:");
        Console.WriteLine("\t" + e.Message);
        return -1;
      }

      if(node_config.NodeAddress == null) {
        node_config.NodeAddress = (Utils.GenerateAHAddress()).ToString();
        node_config.WriteConfig();
      }

      IpopConfig ipop_config = null;
      try {
        ipop_config = Utils.ReadConfig<IpopConfig>(ipop_config_path);
        ipop_config.Path = ipop_config_path;
      } catch (Exception e) {
        Console.WriteLine("Invalid IpopConfig file:");
        Console.WriteLine("\t" + e.Message);
        return -1;
      }

      DhtIpopNode node = new DhtIpopNode(node_config, ipop_config);
      node.Run();
      return 0;
    }

    public static void ShowHelp(OptionSet p) {
      Console.WriteLine("Usage: DhtIpop --IpopConfig=filename --NodeConfig=filename");
      Console.WriteLine("DhtIpop - Virtual Networking Daemon.");
      Console.WriteLine();
      Console.WriteLine("Options:");
      p.WriteOptionDescriptions(Console.Out);
    }
  }
}
