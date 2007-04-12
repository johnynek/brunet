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

    protected int _l_id;
    protected int _r_id;
    protected IEdgeSendHandler _sh;

    public FunctionEdge(IEdgeSendHandler s, int local_id, int remote_id, bool is_in)
    {
      _sh = s;
      _create_dt = DateTime.UtcNow;
      _l_id = local_id;
      _r_id = remote_id;
      inbound = is_in;
      _is_closed = false;
    }

    protected DateTime _create_dt;
    public override DateTime CreatedDateTime {
      get { return _create_dt; }
    }
    protected DateTime _last_out_packet_datetime;
    public override DateTime LastOutPacketDateTime {
      get { return _last_out_packet_datetime; }
    }

    protected bool _is_closed;
    public override void Close()
    {
      base.Close();
      _is_closed = true;
    }

    public override bool IsClosed
    {
      get
      {
        return (_is_closed);
      }
    }
    protected bool inbound;
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
        _partner = value;
      }
    }


    public override void Send(ICopyable p)
    {
      if( !_is_closed ) {
        _last_out_packet_datetime = DateTime.UtcNow;
        _sh.HandleEdgeSend(this, p);
      }
    }

    public override Brunet.TransportAddress.TAType TAType
    {
      get
      {
        return Brunet.TransportAddress.TAType.Function;
      }
    }

    public override Brunet.TransportAddress LocalTA
    {
      get
      {
        return TransportAddressFactory.CreateInstance("brunet.function://localhost:"
                                    + _l_id.ToString());
      }
    }
    public override Brunet.TransportAddress RemoteTA
    {
      get
      {
        return TransportAddressFactory.CreateInstance("brunet.function://localhost:"
                                    + _r_id.ToString());
      }
    }
    public void Push(Packet p) {
      //Make a copy:
      if( !_is_closed ) {
        Packet new_p = PacketParser.Parse( MemBlock.Copy(p) );
        ReceivedPacketEvent(new_p);
      }
    }

  }
}
