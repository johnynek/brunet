/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com> University of Florida

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

  public class FunctionEdge : Edge
  {

    public static Random _rand = new Random();

    /**
     * Adding logger
     */
    /*private static readonly log4net.ILog log =
      log4net.LogManager.GetLogger(System.Reflection.MethodBase.
      GetCurrentMethod().DeclaringType);*/

    protected readonly int _l_id;
    protected readonly int _r_id;
    protected readonly IEdgeSendHandler _sh;

    public FunctionEdge(IEdgeSendHandler s, int local_id, int remote_id, bool is_in)
    {
      _sh = s;
      _create_dt = DateTime.UtcNow;
      _l_id = local_id;
      _r_id = remote_id;
      inbound = is_in;
      _is_closed = 0;
    }

    protected readonly DateTime _create_dt;
    public override DateTime CreatedDateTime {
      get { return _create_dt; }
    }
    protected long _last_out_packet_datetime;
    public override DateTime LastOutPacketDateTime {
      get { return new DateTime(Interlocked.Read(ref _last_out_packet_datetime)); }
    }

    protected int _is_closed;
    public override void Close()
    {
      if(0 == Interlocked.Exchange(ref _is_closed, 1)) {
        base.Close();
      }
    }

    public override bool IsClosed
    {
      get
      {
        return (1 == _is_closed);
      }
    }
    protected readonly bool inbound;
    public override bool IsInbound
    {
      get
      {
        return inbound;
      }
    }

    protected FunctionEdge _partner;
    public FunctionEdge Partner
    {
      get
      {
        return _partner;
      }
      set
      {
        Interlocked.Exchange<FunctionEdge>(ref _partner, value);
      }
    }


    public override void Send(ICopyable p)
    {
      if( p == null ) {
        throw new System.NullReferenceException(
                         "FunctionEdge.Send: argument can't be null");
      }

      if( 0 == _is_closed ) {
        _sh.HandleEdgeSend(this, p);
        Interlocked.Exchange(ref _last_out_packet_datetime, DateTime.UtcNow.Ticks);
      }
      else {
        throw new EdgeClosedException(
                    String.Format("Can't send on closed edge: {0}", this) );
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
        ReceivedPacketEvent(p);
      }
    }

  }
}
