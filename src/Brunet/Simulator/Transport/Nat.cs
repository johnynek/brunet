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

using System;
using System.Collections;
using Brunet.Transport;

namespace Brunet.Simulator.Transport {
  /// <summary> Provides a pseudo-interface for a NAT system.</summary>
  public interface INat {
    /// <summary> Does the Nat allow inbound connections.</summary>
    bool AllowingIncomingConnections { get; }
    /// <summary> The list of TAs to provide to the remote end point.</summary>
    IEnumerable ExternalTransportAddresses { get; }
    /// <summary> The list of TAs to provide to the remote end point.</summary>
    IEnumerable InternalTransportAddresses { get; }
    /// <summary> A remote peer has notified us of this address.</summary>
    IEnumerable KnownTransportAddresses { get; }
    /// <summary> An incoming message from an external node.</summary>
    bool Incoming(TransportAddress remote_ta);
    /// <summary> A message going to an external node.</summary>
    bool Outgoing(TransportAddress remote_ta);
    /// <summary> Update local transport addresses.</summary>
    void UpdateTAs(TransportAddress remote_ta, TransportAddress local_ta);
  }

  public enum NatTypes {
    Public,
    Cone,
    RestrictedCone,
    Symmetric,
    OutgoingOnly,
    Disabled
  }

  static public class NatFactory {
    static public Node AddNode(Simulator sim, NatTypes type0, NatTypes type1, bool relay)
    {
      Node node = sim.AddNode();
      NatFactory.AddNat(node.EdgeListenerList, type0);

      if(relay) {
        Relay.IRelayOverlap ito = new Relay.SimpleRelayOverlap();
        EdgeListener el = new Relay.RelayEdgeListener(node, ito);
        node.AddEdgeListener(el);
        el.Start();
      }

      if(type1 != NatTypes.Disabled) {
        NodeMapping nm = sim.Nodes[node.Address];
        int id = nm.ID;
        string tas = SimulationTransportAddress.GetString(TransportAddress.TAType.SO, id);
        INat nat = GetNat(TransportAddressFactory.CreateInstance(tas), type1);
        SimulationEdgeListener el = new SimulationEdgeListener(id, 0, null,
            true, TransportAddress.TAType.SO, nat);
        node.AddEdgeListener(el);
        el.Start();
      }
      return node;
    }

    static public INat GetNat(TransportAddress ta, NatTypes type)
    {
      switch(type) {
        case NatTypes.Cone:
          return new ConeNat(ta, 60000);
        case NatTypes.OutgoingOnly:
          return new OutgoingOnly(ta);
        case NatTypes.Public:
          return new PublicNat(ta);
        case NatTypes.RestrictedCone:
          return new RestrictedConeNat(ta, 60000);
        case NatTypes.Symmetric:
          return new SymmetricNat(ta, 60000);
        default:
          throw new NotImplementedException("NAT Type: " + type + " not implemented.");
      }
    }

    static public void AddNat(IEnumerable el_list, NatTypes type)
    {
      SimulationEdgeListener simel = null;
      foreach(EdgeListener el in el_list) {
        simel = el as SimulationEdgeListener;
        if(simel != null) {
          break;
        }
      }

      if(simel == null) {
        throw new Exception("There was no SimulationEdgeListener");
      }

      TransportAddress ta = null;
      foreach(TransportAddress lta in simel.LocalTAs) {
        ta = lta;
        break;
      }

      if(ta == null) {
        throw new Exception("How did we get a null TA?");
      }

      simel.Nat = GetNat(ta, type);
    }
  }
}
