/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

namespace NetworkPackets {
  /// <summary>Internet Control Message Packets (Ping)</summary>
  public class ICMPPacket: DataPacket {
    public enum Types {
      EchoReply = 0,
      EchoRequest = 8
    };

    public readonly Types Type;
    public readonly byte Code;
    public readonly short Identifier;
    public readonly short SequenceNumber;
    public readonly MemBlock Data;

    public ICMPPacket(Types type, short id, short seq_num) {
      Type = type;
      Identifier = id;
      SequenceNumber = seq_num;
      Code = (byte) 0;

      byte[] msg = new byte[64];
      Random rand = new Random();
      rand.NextBytes(msg);
      msg[0] = (byte) type;
      msg[1] = Code;
      msg[2] = (byte) 0;
      msg[3] = (byte) 0;


      NumberSerializer.WriteShort(Identifier, msg, 4);
      NumberSerializer.WriteShort(SequenceNumber, msg, 6);

      short checksum = (short) IPPacket.GenerateChecksum(MemBlock.Reference(msg));
      NumberSerializer.WriteShort(checksum, msg, 2);

      _icpacket = MemBlock.Reference(msg);
      _packet = MemBlock.Reference(msg);
    }

    public ICMPPacket(Types type) : this(type, 0, 0) {
    }

    public ICMPPacket(MemBlock Packet) {
      if(Packet.Length < 4) {
        throw new Exception("ICMP: Not long enough!");
      }

      _icpacket = Packet;
      _packet = Packet;

      Type = (Types) Packet[0];
      Code = Packet[1];

      if(Packet.Length >= 8) {
        Identifier = NumberSerializer.ReadShort(Packet, 4);
        SequenceNumber = NumberSerializer.ReadShort(Packet, 6);
      } else {
        Identifier = 0;
        SequenceNumber = 0;
      }
    }
  }
}
