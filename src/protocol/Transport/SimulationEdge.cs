/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com> University of Florida
Copyright (C) 2008 David Wolinsky <davidiw@ufl.edu> University of Florida

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
using System.Threading;
using System.Collections;

namespace Brunet
{
  /**
  * A Edge which does its transport locally
  * by calling a method on the other edge
  *
  * This Edge is for debugging purposes on
  * a single machine in a single process.
  */

  public class SimulationEdge : Edge
  {

    public static Random _rand = new Random();

    protected readonly int _l_id;
    protected readonly int _r_id;
    protected readonly IEdgeSendHandler _sh;
    public readonly int Delay;


    public SimulationEdge(IEdgeSendHandler s, int local_id, int remote_id, bool is_in)
      : this(s, local_id, remote_id, is_in, 0) {
    }

    public SimulationEdge(IEdgeSendHandler s, int local_id, int remote_id, bool is_in, int delay) : base(s, is_in)
    {
      _sh = s;
      _l_id = local_id;
      _r_id = remote_id;
      Delay = delay;
    }

    protected SimulationEdge _partner;
    public SimulationEdge Partner
    {
      get
      {
        return _partner;
      }
      set
      {
        Interlocked.Exchange<SimulationEdge>(ref _partner, value);
      }
    }


    public override Brunet.TransportAddress.TAType TAType
    {
      get
      {
        return Brunet.TransportAddress.TAType.Function;
      }
    }

    public int ListenerId {
      get { return _l_id; }
    }

    protected TransportAddress _local_ta;
    public override Brunet.TransportAddress LocalTA
    {
      get {
        if ( _local_ta == null ) {
          _local_ta = TransportAddressFactory.CreateInstance("brunet.function://localhost:"
                                    + _l_id.ToString());
        }
        return _local_ta;
      }
    }

    protected TransportAddress _remote_ta;
    public override Brunet.TransportAddress RemoteTA
    {
      get {
        if ( _remote_ta == null ) {
          _remote_ta = TransportAddressFactory.CreateInstance("brunet.function://localhost:"
                                    + _r_id.ToString());
        }
        return _remote_ta;
      }
    }

    public void Push(MemBlock p) {
      if( 0 == _is_closed ) {
        if(Delay > 0) {
          new BrunetTimer(DelayedPush, p, Delay, 0);
        } else {
          ReceivedPacketEvent(p);
        }
      }
    }

    protected void DelayedPush(object o) {
      if( 0 == _is_closed ) {
        ReceivedPacketEvent((MemBlock) o);
      }
    }
  }
}
