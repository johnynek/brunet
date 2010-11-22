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
    OutboundOnly,
    Disabled
  }

  static public class NatFactory {
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

      switch(type) {
        case NatTypes.Cone:
          simel.Nat = new ConeNat(ta, 60000);
          break;
        case NatTypes.Disabled:
          break;
        case NatTypes.OutboundOnly:
          break;
        case NatTypes.Public:
          simel.Nat = new PublicNat(ta);
          break;
        case NatTypes.RestrictedCone:
          break;
        case NatTypes.Symmetric:
          break;
      }
    }
  }
}
