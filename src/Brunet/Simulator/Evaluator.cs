/*
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Brunet.Util;

namespace Brunet.Simulator {
  public class Evaluator {
    public static void Main(string []args)
    {
      bool complete = false;
      Simulator sim = new Simulator();
      Runner.ParseCommandLine(args, out complete, sim);

      DateTime now = DateTime.UtcNow;
      sim.Complete();
      SimpleTimer.RunSteps(20000000, false);
      sim.Complete();
      Console.WriteLine("Time spent setting up: " + (DateTime.UtcNow - now).ToString());
      sim.AllToAll(false);
      sim.Crawl(false, false);
    }
  }
}
