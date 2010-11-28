/*
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet.Transport;
using System;
using System.Collections;

namespace Brunet.Simulator.Transport {
  /// <summary> Randomly breaks all edges to remote entity.</summary>
  public class BrokenTAAuth : TAAuthorizer {
    public readonly double Prob;
    protected readonly Hashtable _allowed;

    public BrokenTAAuth(double probability)
    {
      Prob = probability;
      _allowed = new Hashtable();
    }

    public override TAAuthorizer.Decision Authorize(TransportAddress a)
    {
      int id = ((SimulationTransportAddress) a).ID;
      if(id == 0) {
        return TAAuthorizer.Decision.Allow;
      }

      if(!_allowed.Contains(id)) {
        if(Node.SimulatorRandom.NextDouble() > Prob) {
          _allowed[id] = TAAuthorizer.Decision.Allow;
        } else {
          _allowed[id] = TAAuthorizer.Decision.Deny;
        }
      }

      return (TAAuthorizer.Decision) _allowed[id];
    }
  }
}
