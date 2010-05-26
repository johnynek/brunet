/*
This program is part of Brunet, a library for autonomic overlay networks.
Copyright (C) 2010 David Wolinsky davidiw@ufl.edu, Unversity of Florida

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
