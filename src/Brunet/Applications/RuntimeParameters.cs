/*
This program is part of Brunet, a library for autonomic overlay networks.
Copyright (C) 2010 David Wolinsky davidiw@ufl.edu, Unversity of Florida

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

using NDesk.Options;
using System;
using System.Collections.Generic;

namespace Brunet.Applications {
  public class RuntimeParameters {
    public const string NODE_XSD = "Node.xsd";
    public readonly string AppName;
    public readonly string AppDescription;
    public readonly ConfigurationValidator Validator;

    public NodeConfig NodeConfig { get { return _node_config; } }
    public bool Help { get { return _help; } }
    public string ErrorMessage { get { return _error_message; } }
    public OptionSet Options { get { return _options; } }


    protected NodeConfig _node_config;
    protected string _node_config_path = string.Empty;
    protected bool _help = false;
    protected string _error_message = string.Empty;
    protected OptionSet _options;

    public RuntimeParameters(string app_name, string app_description)
    {
      AppName = app_name;
      AppDescription = app_description;
      Validator = new ConfigurationValidator();

      _options = new OptionSet() {
        { "n|NodeConfig=", "Path to a NodeConfig file.",
          v => _node_config_path = v },
        { "h|help", "Display this help and exit.",
          v => _help = v != null },
      };
    }

    public virtual int Parse(string[] args)
    {
      try {
        _options.Parse(args);
      } catch(Exception e) {
        _error_message = e.Message;
        return -1;
      }

      if(_node_config_path == string.Empty || !System.IO.File.Exists(_node_config_path)) {
        _error_message = "Missing NodeConfig";
        return -1;
      }

      try {
        Validator.Validate(_node_config_path, NODE_XSD);
        _node_config = Utils.ReadConfig<NodeConfig>(_node_config_path);
        _node_config.Path = _node_config_path;
      } catch (Exception e) {
        _error_message = "Invalid NodeConfig file: " + e.Message;
        Console.WriteLine(e);
        return -1;
      }

      return 0;
    }

    public void ShowHelp()
    {
      Console.WriteLine("Usage: {0} [options]", AppName);
      Console.WriteLine(AppDescription);
      Console.WriteLine();
      Console.WriteLine("Options:");
      Options.WriteOptionDescriptions(Console.Out);
    }
  }
}
