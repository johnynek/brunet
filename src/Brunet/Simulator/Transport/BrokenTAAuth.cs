using Brunet.Transport;
using System;
using System.Collections;

namespace Brunet.Simulator.Transport {
  /// <summary> Randomly breaks all edges to remote entity.</summary>
  public class BrokenTAAuth : TAAuthorizer {
    public readonly double Prob;
    public readonly Random Rand;
    protected readonly Hashtable _allowed;

    public BrokenTAAuth(Random rand, double probability)
    {
      Prob = probability;
      _allowed = new Hashtable();
      Rand = new Random();
    }

    public override TAAuthorizer.Decision Authorize(TransportAddress a)
    {
      int id = ((SimulationTransportAddress) a).ID;
      if(id == 0) {
        return TAAuthorizer.Decision.Allow;
      }

      if(!_allowed.Contains(id)) {
        if(Rand.NextDouble() > Prob) {
          _allowed[id] = TAAuthorizer.Decision.Allow;
        } else {
          _allowed[id] = TAAuthorizer.Decision.Deny;
        }
      }

      return (TAAuthorizer.Decision) _allowed[id];
    }
  }
}
