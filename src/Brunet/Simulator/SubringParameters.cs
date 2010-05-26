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

using System;

namespace Brunet.Simulator {
  /// <summary>Parameters for subrings.</summary>
  public class SubringParameters : Parameters {
    /// <summary>Parameters used to create the Private Overlay.</summary>
    public Parameters PrivateParameters { get { return _private_parameters; } }
    /// <summary>Parameters used to create the Public Overlay.</summary>
    public Parameters PublicParameters { get { return _public_parameters; } }
    public bool Subring { get { return _subring; } }

    protected Parameters _private_parameters;
    protected Parameters _public_parameters;
    protected bool _subring = false;
    protected int _subring_count = 0;

    public SubringParameters() :
      base("SubringSimulator", "Simulator for Subrings")
    {
      _options.Add("subring=", "Enable subrings by specifying node count",
          v => _subring_count = Int32.Parse(v));
    }

    public SubringParameters(SubringParameters parameters) : base(parameters)
    {
    }

    override public int Parse(string[] args)
    {
      if(base.Parse(args) < 0) {
        return -1;
      }

      if(_subring_count > 0) {
        _subring = true;
      }

      SubringParameters prip = new SubringParameters(this);
      prip._size = _subring_count;
      prip._pathing = false;
      _private_parameters = prip;

      SubringParameters pubp = new SubringParameters(this);
      pubp._pathing = true;
      pubp._dtls = false;
      pubp._se = false;
      pubp._ss = false;
      _public_parameters = pubp;

      return 0;
    }
  }
}
