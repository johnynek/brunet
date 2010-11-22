/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com> University of Florida
Copyright (C) 2008 David Wolinsky <davidiw@ufl.edu> University of Florida

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
using System.Threading;
using System.Collections;
using Brunet.Transport;

namespace Brunet.Simulator.Transport {
  /// <summary>Single-threaded edge listener for simulation purposes.</summary>
  public class SimulationEdge : Edge {
    public readonly int Delay;
    public readonly int LocalID;
    public readonly int RemoteID;
    public readonly SimulationEdgeListener SimEL;

    public SimulationEdge(IEdgeSendHandler s, int local_id, int remote_id,
        bool is_in) : this(s, local_id, remote_id, is_in, 0)
    {
    }

    public SimulationEdge(IEdgeSendHandler s, int local_id, int remote_id,
        bool is_in, int delay) :
      this(s, local_id, remote_id, is_in, delay, TransportAddress.TAType.S)
    {
    }
   
    public SimulationEdge(IEdgeSendHandler s, int local_id, int remote_id,
        bool is_in, int delay, TransportAddress.TAType type) : base(s, is_in)
    {
      Delay = delay;
      LocalID = local_id;
      RemoteID = remote_id;
      _ta_type = type;
      _local_ta = GetTransportAddress(local_id);
      _remote_ta = GetTransportAddress(remote_id);
      SimEL = s as SimulationEdgeListener;
    }

    public SimulationEdge Partner;
    public override TransportAddress.TAType TAType { get { return _ta_type; } }
    readonly protected TransportAddress.TAType _ta_type;

    readonly protected TransportAddress _local_ta;
    public override TransportAddress LocalTA { get { return _local_ta; } }
    readonly protected TransportAddress _remote_ta;
    public override TransportAddress RemoteTA { get { return _remote_ta; } }

    protected TransportAddress GetTransportAddress(int id)
    {
      string tas = String.Format("b.{0}://{1}",
          TransportAddress.TATypeToString(TAType), id);
      return TransportAddressFactory.CreateInstance(tas);
    }

    /// <summary>Receive data from the remote end point.</summary>
    public void Push(Brunet.Util.MemBlock p)
    {
      if( 1 == _is_closed ) {
        return;
      }

      if(Delay > 0) {
        var timer = new Brunet.Util.SimpleTimer(DelayedPush, p, Delay, 0);
        timer.Start();
      } else {
        ReceivedPacketEvent(p);
      }
    }

    protected void DelayedPush(object o)
    {
      if( 0 == _is_closed ) {
        ReceivedPacketEvent((Brunet.Util.MemBlock) o);
      }
    }
  }
}
