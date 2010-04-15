/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace NetworkPackets.Dns {
  /**
  <summary>This is for a SOA type Response and would be considered
  a AR and not an RR.</summary>
  <remarks>
  <para>Authority RR RDATA, this gets placed in a response packet
  RDATA</para>
  <code>
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  /                     MName                     /
  /                                               /
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  /                     RName                     /
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    Serial                     |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    Refresh                    |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                     Retry                     |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    Expire                     |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    Minimum                    |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  </code>
  </remarks>
  */
  public class ZoneAuthority: DataPacket {
    /// <summary>Incomplete</summary>
    public readonly string MName;
    /// <summary>Incomplete</summary>
    public readonly string RName;
    /// <summary>Incomplete</summary>
    public readonly int Serial;
    /// <summary>Incomplete</summary>
    public readonly int Refresh;
    /// <summary>Incomplete</summary>
    public readonly int Retry;
    /// <summary>Incomplete</summary>
    public readonly int Expire;
    /// <summary>Incomplete</summary>
    public readonly int Minimum;

    /**
    <summary>Constructor when creating a ZoneAuthority from a MemBlock, this
    is incomplete.</summary>
    */
    public ZoneAuthority(MemBlock data) {
      int idx = 0;
      MName = String.Empty;
      while(data[idx] != 0) {
        byte length = data[idx++];
        for(int i = 0; i < length; i++) {
          MName += (char) data[idx++];
        }
        if(data[idx] != 0) {
          MName  += ".";
        }
      }
      idx++;

      RName = String.Empty;
      while(data[idx] != 0) {
        byte length = data[idx++];
        for(int i = 0; i < length; i++) {
          RName += (char) data[idx++];
        }
        if(data[idx] != 0) {
          RName  += ".";
        }
      }
      idx++;

      Serial = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      Refresh = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      Retry = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      Expire = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      Minimum = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      _icpacket = _packet = data.Slice(0, idx);
    }

    /**
    <summary>Creates a Zone authority from field data in the parameters,
    this is incomplete.</summary>
    */
    public ZoneAuthority(string MName, string RName, int Serial,
                          int Refresh, int Retry, int Expire, int Minimum) {
      this.MName = MName;
      this.RName = RName;
      this.Serial = Serial;
      this.Refresh = Refresh;
      this.Retry = Retry;
      this.Expire = Expire;
      this.Minimum = Minimum;

 //     MemBlock mname = DnsPacket.NameStringToBytes(MName, DnsPacket.TYPES.A);
//      MemBlock rname = DnsPacket.NameStringToBytes(RName, DnsPacket.TYPES.A);
      byte[] rest = new byte[20];
      int idx = 0;
      rest[idx++] = (byte) ((Serial >> 24) & 0xFF);
      rest[idx++] = (byte) ((Serial >> 16) & 0xFF);
      rest[idx++] = (byte) ((Serial >> 8) & 0xFF);
      rest[idx++] = (byte) (Serial  & 0xFF);
      rest[idx++] = (byte) ((Refresh >> 24) & 0xFF);
      rest[idx++] = (byte) ((Refresh >> 16) & 0xFF);
      rest[idx++] = (byte) ((Refresh >> 8) & 0xFF);
      rest[idx++] = (byte) (Refresh  & 0xFF);
      rest[idx++] = (byte) ((Retry >> 24) & 0xFF);
      rest[idx++] = (byte) ((Retry >> 16) & 0xFF);
      rest[idx++] = (byte) ((Retry >> 8) & 0xFF);
      rest[idx++] = (byte) (Retry  & 0xFF);
      rest[idx++] = (byte) ((Expire >> 24) & 0xFF);
      rest[idx++] = (byte) ((Expire >> 16) & 0xFF);
      rest[idx++] = (byte) ((Expire >> 8) & 0xFF);
      rest[idx++] = (byte) (Expire  & 0xFF);
      rest[idx++] = (byte) ((Minimum >> 24) & 0xFF);
      rest[idx++] = (byte) ((Minimum >> 16) & 0xFF);
      rest[idx++] = (byte) ((Minimum >> 8) & 0xFF);
      rest[idx++] = (byte) (Minimum  & 0xFF);
  //    _icpacket = new CopyList(mname, rname, MemBlock.Reference(rest));
    }

    public ZoneAuthority() : this("grid-appliance.org",
      "grid-appliance.org", 12345678, 1800, 900, 604800, 60480) {}
  }
}
