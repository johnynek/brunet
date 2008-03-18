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

namespace NetworkPackets.DNS {
  /**
  <summary>This is for a SOA type Response and would be considered
  a AR and not an RR.</summary>
  <remarks>
  <para>Authority RR RDATA, this gets placed in a response packet
  RDATA</para>
  <code>
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  /                     MNAME                     /
  /                                               /
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  /                     RNAME                     /
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    SERIAL                     |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    REFRESH                    |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                     RETRY                     |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    EXPIRE                     |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    MINIMUM                    |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  </code>
  </remarks>
  */
  public class ZoneAuthority: DataPacket {
    /// <summary>Incomplete</summary>
    public readonly string MNAME;
    /// <summary>Incomplete</summary>
    public readonly string RNAME;
    /// <summary>Incomplete</summary>
    public readonly int SERIAL;
    /// <summary>Incomplete</summary>
    public readonly int REFRESH;
    /// <summary>Incomplete</summary>
    public readonly int RETRY;
    /// <summary>Incomplete</summary>
    public readonly int EXPIRE;
    /// <summary>Incomplete</summary>
    public readonly int MINIMUM;

    /**
    <summary>Constructor when creating a ZoneAuthority from a MemBlock, this
    is incomplete.</summary>
    */
    public ZoneAuthority(MemBlock data) {
      int idx = 0;
      MNAME = String.Empty;
      while(data[idx] != 0) {
        byte length = data[idx++];
        for(int i = 0; i < length; i++) {
          MNAME += (char) data[idx++];
        }
        if(data[idx] != 0) {
          MNAME  += ".";
        }
      }
      idx++;

      RNAME = String.Empty;
      while(data[idx] != 0) {
        byte length = data[idx++];
        for(int i = 0; i < length; i++) {
          RNAME += (char) data[idx++];
        }
        if(data[idx] != 0) {
          RNAME  += ".";
        }
      }
      idx++;

      SERIAL = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      REFRESH = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      RETRY = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      EXPIRE = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      MINIMUM = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      _icpacket = _packet = data.Slice(0, idx);
    }

    /**
    <summary>Creates a Zone authority from field data in the parameters,
    this is incomplete.</summary>
    */
    public ZoneAuthority(string MNAME, string RNAME, int SERIAL,
                          int REFRESH, int RETRY, int EXPIRE, int MINIMUM) {
      this.MNAME = MNAME;
      this.RNAME = RNAME;
      this.SERIAL = SERIAL;
      this.REFRESH = REFRESH;
      this.RETRY = RETRY;
      this.EXPIRE = EXPIRE;
      this.MINIMUM = MINIMUM;

 //     MemBlock mname = DNSPacket.NameStringToBytes(MNAME, DNSPacket.TYPES.A);
//      MemBlock rname = DNSPacket.NameStringToBytes(RNAME, DNSPacket.TYPES.A);
      byte[] rest = new byte[20];
      int idx = 0;
      rest[idx++] = (byte) ((SERIAL >> 24) & 0xFF);
      rest[idx++] = (byte) ((SERIAL >> 16) & 0xFF);
      rest[idx++] = (byte) ((SERIAL >> 8) & 0xFF);
      rest[idx++] = (byte) (SERIAL  & 0xFF);
      rest[idx++] = (byte) ((REFRESH >> 24) & 0xFF);
      rest[idx++] = (byte) ((REFRESH >> 16) & 0xFF);
      rest[idx++] = (byte) ((REFRESH >> 8) & 0xFF);
      rest[idx++] = (byte) (REFRESH  & 0xFF);
      rest[idx++] = (byte) ((RETRY >> 24) & 0xFF);
      rest[idx++] = (byte) ((RETRY >> 16) & 0xFF);
      rest[idx++] = (byte) ((RETRY >> 8) & 0xFF);
      rest[idx++] = (byte) (RETRY  & 0xFF);
      rest[idx++] = (byte) ((EXPIRE >> 24) & 0xFF);
      rest[idx++] = (byte) ((EXPIRE >> 16) & 0xFF);
      rest[idx++] = (byte) ((EXPIRE >> 8) & 0xFF);
      rest[idx++] = (byte) (EXPIRE  & 0xFF);
      rest[idx++] = (byte) ((MINIMUM >> 24) & 0xFF);
      rest[idx++] = (byte) ((MINIMUM >> 16) & 0xFF);
      rest[idx++] = (byte) ((MINIMUM >> 8) & 0xFF);
      rest[idx++] = (byte) (MINIMUM  & 0xFF);
  //    _icpacket = new CopyList(mname, rname, MemBlock.Reference(rest));
    }

    public ZoneAuthority() : this("grid-appliance.org",
      "grid-appliance.org", 12345678, 1800, 900, 604800, 60480) {}
  }
}