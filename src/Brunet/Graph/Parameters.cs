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

namespace Brunet.Graph {
  public class Parameters {
    public readonly string AppName;
    public readonly string AppDescription;

    public int Size { get { return _size; } }
    public int Shortcuts { get { return _shortcuts; } }
    public int Near { get { return _near; } }
    public int Seed { get { return _seed; } }
    public string Dataset { get { return _dataset; } }
    public string Outfile { get { return _outfile; } }
    public string ErrorMessage { get { return _error_message; } }
    public bool Help { get { return _help; } }
    public List<List<int>> LatencyMap { get { return _latency_map; } }
    public OptionSet Options { get { return _options; } }

    protected int _size = 100;
    protected int _shortcuts = -1;
    protected int _near = 2;
    protected int _seed = -1;
    protected string _dataset = string.Empty;
    protected string _outfile = string.Empty;
    protected string _error_message = string.Empty;
    protected bool _help = false;
    protected List<List<int>> _latency_map = null;
    protected OptionSet _options;

    public Parameters(string app_name, string app_description)
    {
      AppName = app_name;
      AppDescription = app_description;

      _options = new OptionSet() {
        {"s|size=", "Network size",
          v => _size = Int32.Parse(v)},
        {"c|shortcuts=", "Shortcuts (default .5 log n)",
          v => _shortcuts = Int32.Parse(v)},
        {"n|near=", "Near neighbors per side",
          v => _near = Int32.Parse(v)},
        {"r|seed=", "Random number seed",
          v => _seed = Int32.Parse(v)},
        {"o|outfile=", "Data out file",
          v => _outfile = v},
        {"d|dataset=", "Peer latency dataset",
          v => _dataset = v},
        {"h|help", "Help",
          v => _help = true},
      };
    }

    public virtual void Parse(string[] args)
    {
      try {
        _options.Parse(args);
      } catch(Exception e) {
        _error_message = e.Message;
      }

      if(_size < 0) {
        _error_message = "Size is less than 0";
      }

      if(_seed == -1) {
        _seed = (new Random()).Next();
      } else if(_seed < 0) {
        _error_message = "Seed is less than 0";
      }

      if(_shortcuts == -1) {
        _shortcuts = (int) System.Math.Ceiling(0.5*Math.Log(_size)/Math.Log(2.0));
      } else if(_shortcuts < 0) {
        _error_message = "Shortcuts is less than 0";
      }

      if(_near < 0) {
        _error_message = "Near is less than 0";
      }

      if(_dataset != string.Empty) {
        _latency_map = Graph.ReadLatencyDataSet(_dataset);
      }
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
