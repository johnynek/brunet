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
using Brunet.Applications;
using NDesk.Options;
using System;
using System.Collections.Generic;
using System.IO;

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
        Console.WriteLine("P2PNode: ");
        Console.WriteLine(e.Message);
        Console.WriteLine("Try `P2PNode --help' for more information.");
        return -1;
      }

      if(show_help) {
        ShowHelp(opts);
        return -1;
      }

      if(node_config_path == string.Empty || !File.Exists(node_config_path)) {
        Console.WriteLine("P2PNode: ");
        Console.WriteLine("\tMissing NodeConfig.");
        Console.WriteLine("Try `P2PNode --help' for more information.");
        return -1;
      }

      if(str_count != string.Empty && !Int32.TryParse(str_count, out count) && count > 0) {
        Console.WriteLine("P2PNode: ");
        Console.WriteLine("\tInvalid count.  Count must be a positive integer.");
        Console.WriteLine("Try `P2PNode --help' for more information.");
        return -1;
      }

      NodeConfig node_config = null;
      try {
        ConfigurationValidator cv = new ConfigurationValidator();
        cv.Validate(node_config_path, "Node.xsd");
        node_config = Utils.ReadConfig<NodeConfig>(node_config_path);
        node_config.Path = node_config_path;
      } catch (Exception e) {
        Console.WriteLine("Invalid NodeConfig file:");
        Console.WriteLine("\t" + e);
        return -1;
      }

      if(node_config.NodeAddress == null) {
        node_config.NodeAddress = (Utils.GenerateAHAddress()).ToString();
        node_config.WriteConfig();
      }

      if(count == 1) {
        BasicNode node = new BasicNode(node_config);
        node.Run();
      } else {
        MultiNode node = new MultiNode(node_config, count);
        node.Run();
      }
      return 0;
    }

    public static void ShowHelp(OptionSet p) {
      Console.WriteLine("Usage: IpopRouter --IpopConfig=filename --NodeConfig=filename");
      Console.WriteLine("IpopRouter - Virtual Networking Daemon.");
      Console.WriteLine();
      Console.WriteLine("Options:");
      p.WriteOptionDescriptions(Console.Out);
    }
  }
}
