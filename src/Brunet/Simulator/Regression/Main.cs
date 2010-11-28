/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Brunet.Security.PeerSec.Symphony;
using Brunet.Simulator.Tasks;
using Brunet.Transport;
using Brunet.Util;

namespace Brunet.Simulator.Regression {
  public class Runner {
    public static int Main(string []args)
    {
      RegressionParameters p = new RegressionParameters("RegressionTest", "RegressionTest - Tests for discovering regressions in Brunet");
      if(p.Parse(args) != 0) {
        Console.WriteLine(p.ErrorMessage);
        p.ShowHelp();
        return -1;
      } else if(p.Help) {
        p.ShowHelp();
        return -1;
      }

      RegressionTest test = null;
      switch(p.Test.ToLower()) {
        case "system":
          test = new SystemTest(p);
          break;
        case "nat":
          test = new NatTest(p);
          break;
        default:
          Console.WriteLine("Invalid test: " + p.Test);
          p.ShowHelp();
          return -1;
      }

      test.Start();
      return 0;
    }
  }
}
