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
using Brunet.Util;
using System;

namespace NetworkPackets {
  /**
  <summary>Unsupported, this class is too big to support now!</summary>
  */
  public class IgmpPacket: NetworkPacket {
  /**
  <summary>Unsupported, this class is too big to support now!</summary>
  */
    public enum Types { Join = 0x16, Leave = 0x17};
    public readonly byte Type;
    public readonly MemBlock GroupAddress;

    public IgmpPacket(MemBlock packet) {
      _icpacket = _packet = packet;
      Type = packet[0];
      GroupAddress = packet.Slice(4, 4);
      _icpayload = _payload = packet.Slice(8);
    }

    public IgmpPacket(byte Type, MemBlock GroupAddress) {
//      byte[] header = new byte[8];
    }
  }
}
