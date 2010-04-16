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
using System.IO;

namespace Brunet.Simulator {
  public class Parameters {
    public readonly string AppName;
    public readonly string AppDescription;

    public double Broken { get { return _broken; } }
    public bool Complete { get { return _complete; } }
    public string Dataset { get { return _dataset; } }
    public string ErrorMessage { get { return _error_message; } }
    public bool Evaluation { get { return _evaluation; } }
    public int EvaluationTime { get { return _runtime; } }
    public bool HeavyChurn { get { return _heavy_churn; } }
    public bool Help { get { return _help; } }
    public bool Pathing {get { return _pathing; } }
    public bool SecureEdges { get { return _se; } }
    public bool SecureSenders { get { return _ss; } }
    public int Seed { get { return _seed; } }
    public int Size { get { return _size; } }
    public List<List<int>> LatencyMap { get { return _latency_map; } }
    public OptionSet Options { get { return _options; } }

    protected double _broken = 0;
    protected bool _complete = false;
    protected string _dataset = string.Empty;
    protected string _error_message = string.Empty;
    protected bool _evaluation = false;
    protected bool _heavy_churn = false;
    protected bool _help = false;
    protected bool _pathing = false;
    protected int _seed = -1;
    protected bool _se = false;
    protected int _size = 100;
    protected bool _ss = false;
    protected int _runtime = 3600000;
    protected List<List<int>> _latency_map = null;
    protected OptionSet _options;

    public Parameters(string app_name, string app_description)
    {
      AppName = app_name;
      AppDescription = app_description;

      _options = new OptionSet() {
        {"b|broken", "Ratio of broken edges", v => _broken = Double.Parse(v)},
        {"c|complete", "Complete full connectivity and quit", v => _complete = true},
        {"e|evaluation", "Evaluation", v => _evaluation = true},
        {"s|size=", "Network size", v => _size = Int32.Parse(v)},
        {"t|execution_time=", "Time in sec to run the evaluation",
          v => _runtime = Int32.Parse(v) * 1000},
        {"p|pathing", "Enable pathing", v => _pathing = true},
        {"se|secure_edges", "SecureEdges", v => _se = true},
        {"ss|secure_senders", "SecureSenders", v => _ss = true},
        {"hc|heavy_churn", "HeavyChurn Test", v => _heavy_churn = true},
        {"seed=", "Random number seed", v => _seed = Int32.Parse(v)},
        {"d|dataset=", "Peer latency latency_map", v => _dataset = v},
        {"h|help", "Help", v => _help = true},
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

      if(_size <= 0) {
        _error_message = "Size needs to be positive.";
        return -1;
      }

      if(_seed == -1) {
        _seed = (new Random()).Next();
      } else if(_seed < 0) {
        _error_message = "Seed is less than 0";
        return -1;
      }

      if(_dataset != string.Empty) {
        try {
          _latency_map = ReadLatencyDataSet();
        } catch(Exception e) {
          _error_message = e.Message;
          return -1;
        }
      }
      
      return 0;
    }

    protected List<List<int>> ReadLatencyDataSet()
    {
      var latency_map = new List<List<int>>();
      using(StreamReader fs = new StreamReader(new FileStream(_dataset, FileMode.Open))) {
        string line = null;
        while((line = fs.ReadLine()) != null) {
          string[] points = line.Split(' ');
          List<int> current = new List<int>(points.Length);
          foreach(string point in points) {
            int val;
            if(!Int32.TryParse(point, out val)) {
              val = 500;
            } else if(val < 0) {
              val = 500;
            }
            current.Add(val);
          }
          latency_map.Add(current);
        }
      }
      
      if(_size == 100) {
        _size = latency_map.Count;
      }
      
      //If the size is less than the data set, we may get inconclusive
      // results as network size changes due to the table potentially being
      // heavy set early and lighter later.  This randomly orders all entries
      // so that multiple calls to the graph will provide a good distribution.
      if(_size > latency_map.Count) {
        Random rand = new Random(_seed);
        Dictionary<int, int> chosen = new Dictionary<int, int>(_size);
        for(int i = 0; i < _size; i++) {
          int index = rand.Next(0, latency_map.Count - 1);
          while(chosen.ContainsKey(index)) {
            index = rand.Next(0, latency_map.Count - 1);
          }
          chosen.Add(i, index);
        }

        var new_latency_map = new List<List<int>>(latency_map.Count);
        for(int i = 0; i < _size; i++) {
          List<int> map = new List<int>(_size);
          for(int j = 0; j < _size; j++) {
            map.Add(latency_map[chosen[i]][chosen[j]]);
          }
          new_latency_map.Add(map);
        }
        latency_map = new_latency_map;
      }
      return latency_map;
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
