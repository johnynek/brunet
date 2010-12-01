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
using System.IO;

using Brunet.Collections;

namespace Brunet.Simulator {
  public class Parameters {
    public readonly string AppName;
    public readonly string AppDescription;

    public int Broadcast { get { return _broadcast; } }
    public double Broken { get { return _broken; } }
    public bool Complete { get { return _complete; } }
    public string Dataset { get { return _dataset; } }
    public double DropRate { get { return _drop_rate; } }
    public bool Dtls { get { return _dtls; } }
    public string ErrorMessage { get { return _error_message; } }
    public bool Evaluation { get { return _evaluation; } }
    public int HeavyChurn { get { return _heavy_churn; } }
    public bool Help { get { return _help; } }
    public bool Pathing {get { return _pathing; } }
    public bool SecureEdges { get { return _se; } }
    public bool SecureSenders { get { return _ss; } }
    public int Seed { get { return _seed; } }
    public int Size { get { return _size; } }
    public List<List<int>> LatencyMap { get { return _latency_map; } }
    public OptionSet Options { get { return _options; } }
    public string Output { get { return _output; } }

    protected int _broadcast = -2;
    protected double _broken = 0;
    protected bool _complete = false;
    protected string _dataset = string.Empty;
    protected bool _dtls = false;
    protected string _error_message = string.Empty;
    protected bool _evaluation = false;
    protected int _heavy_churn = -1;
    protected bool _help = false;
    protected bool _pathing = false;
    protected int _seed = -1;
    protected bool _se = false;
    protected int _size = 100;
    protected bool _ss = false;
    protected List<List<int>> _latency_map = null;
    protected OptionSet _options;
    protected string _output = "output.out";
    protected double _drop_rate = 0;
    protected bool _random_latency = false;

    public Parameters(string app_name, string app_description)
    {
      AppName = app_name;
      AppDescription = app_description;

      _options = new OptionSet() {
        {"broadcast=", "Broadcast evaluation", v => _broadcast = Int32.Parse(v)} ,
        {"b|broken=", "Ratio of broken edges", v => _broken = Double.Parse(v)},
        {"c|complete", "Complete full connectivity and quit", v => _complete = true},
        {"e|evaluation", "Evaluation", v => _evaluation = true},
        {"s|size=", "Network size", v => _size = Int32.Parse(v)},
        {"p|pathing", "Enable pathing", v => _pathing = true},
        {"secure_edges", "SecureEdges", v => _se = true},
        {"secure_senders", "SecureSenders", v => _ss = true},
        {"heavy_churn=", "HeavyChurn Test and time", v => _heavy_churn = Int32.Parse(v)},
        {"seed=", "Random number seed", v => _seed = Int32.Parse(v)},
        {"d|dataset=", "Peer latency latency_map", v => _dataset = v},
        {"dtls", "Use Dtls instead of PeerSec", v => _dtls = true},
        {"output=", "Output file name", v => _output = v}, 
        {"drop_rate=", "Drop rate", v => _drop_rate = Double.Parse(v)},
        {"random_latency", v => _random_latency = true},
        {"h|help", "Help", v => _help = true},
      };
    }

    /// <summary>Copy constructor.</summary>
    public Parameters(Parameters copy)
    {
      _broadcast = copy.Broadcast;
      _broken = copy.Broken;
      _complete = copy.Complete;
      _dataset = copy.Dataset;
      _dtls = copy.Dtls;
      _error_message = copy.ErrorMessage;
      _evaluation = copy.Evaluation;
      _heavy_churn = copy.HeavyChurn;
      _pathing = copy.Pathing;
      _se = copy.SecureEdges;
      _ss = copy.SecureSenders;
      _seed = copy.Seed;
      _size = copy.Size;
      _latency_map = copy.LatencyMap;
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
        Node.SimulatorRandom = new Random();
      } else {
        Node.SimulatorRandom = new Random(_seed);
      }
      
      if(_dataset != string.Empty) {
        try {
          _latency_map = Utils.ReadLatencyDataSet(_dataset, _size);
        } catch(Exception e) {
          _error_message = e.Message;
          return -1;
        }
      } else if(_random_latency) {
        _latency_map = Utils.RandomLatencyMap(_size);
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
