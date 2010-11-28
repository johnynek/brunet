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

namespace Brunet.Simulator.Regression {
  public class RegressionParameters : Parameters {
    public int Iterations { get { return _iterations; } }
    public string Test { get { return _test; } }

    protected int _iterations = 50;
    protected string _test = string.Empty;

    public RegressionParameters(string app_name, string app_description) :
      base(app_name, app_description)
    {
      _options.Add("t|test=", v => _test = v);
      _options.Add("i|iterations=", v => _iterations = Int32.Parse(v));
      _output = "results.db";
    }

    /// <summary>Copy constructor.</summary>
    public RegressionParameters(RegressionParameters copy) : base(copy)
    {
      _iterations = copy.Iterations;
      _test = copy.Test;
    }
  }
}
