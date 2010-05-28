/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using Brunet.Util;
using System;

namespace NetworkPackets {
  /// <summary>Internet Control Message Packets (Ping)</summary>
  public class IcmpPacket: DataPacket {
    public enum Types {
      EchoReply = 0,
      EchoRequest = 8
    };

    public readonly Types Type;
    public readonly byte Code;
    public readonly short Identifier;
    public readonly short SequenceNumber;
    public readonly MemBlock Data;

    public IcmpPacket(Types type, short id, short seq_num) {
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

    public IcmpPacket(Types type) : this(type, 0, 0) {
    }

    public IcmpPacket(MemBlock Packet) {
      if(Packet.Length < 4) {
        throw new Exception("Icmp: Not long enough!");
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
