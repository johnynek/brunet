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
  /// <summary>Encapsulates an Arp Packet and provides the mechanisms to
  /// generate new Arp Packets.  This is immutable.</summary>
  /// <remarks>
  /// The Header is of the format:
  /// <list type="table">
  ///   <listheader>
  ///     <term>Field</term>
  ///     <description>Position</description>
  ///   </listheader>
  ///   <item><term>Hardware Type</term><description>2 Bytes</description></item>
  ///   <item><term>Protocol Type</term><description>2 Bytes</description></item>
  ///   <item><term>Hardware Length</term><description>1 Byte</description></item>
  ///   <item><term>Protocol Length</term><description>1 Byte</description></item>
  ///   <item><term>Operation</term><description>2 Bytes</description></item>
  ///   <item><term>Sender HW Address</term><description>HW Length</description></item>
  ///   <item><term>Sender Proto Address</term><description>Proto Length</description></item>
  ///   <item><term>Target HW Address</term><description>HW Length</description></item>
  ///   <item><term>Target Proto Address</term><description>Proto Length</description></item>
  /// </list>
  /// </remarks>
  public class ArpPacket : NetworkPacket {
    /// <summary>Hardware type -- Ethernet is 1.</summary>
    public readonly int HardwareType;
    /// <summary>Protocol Type -- IP is 0x0800.</summary>
    public readonly int ProtocolType;

    public enum Operations {
      Request = 1,
      Reply = 2,
      ReverseRequest = 3,
      ReverseReply = 4
    }

    public readonly Operations Operation;

    public readonly MemBlock SenderHWAddress;
    public readonly MemBlock SenderProtoAddress;
    public readonly MemBlock TargetHWAddress;
    public readonly MemBlock TargetProtoAddress;

    public ArpPacket(MemBlock Packet)
    {
      _icpacket = _packet = Packet;
      HardwareType = NumberSerializer.ReadShort(Packet, 0);
      ProtocolType = NumberSerializer.ReadShort(Packet, 2);
      int hw_len = Packet[4];
      int proto_len = Packet[5];
      Operation = (Operations) NumberSerializer.ReadShort(Packet, 6);
      int pos = 8;
      SenderHWAddress = MemBlock.Reference(Packet, pos, hw_len);
      pos += hw_len;
      SenderProtoAddress = MemBlock.Reference(Packet, pos, proto_len);
      pos += proto_len;
      TargetHWAddress = MemBlock.Reference(Packet, pos, hw_len);
      pos += hw_len;
      TargetProtoAddress = MemBlock.Reference(Packet, pos, proto_len);
    }

    public ArpPacket(int HardwareType, int ProtocolType, Operations Operation,
        MemBlock SenderHWAddress, MemBlock SenderProtoAddress, MemBlock TargetHWAddress,
        MemBlock TargetProtoAddress)
    {
      byte[] header = new byte[8];
      NumberSerializer.WriteUShort((ushort) HardwareType, header, 0);
      NumberSerializer.WriteUShort((ushort) ProtocolType, header, 2);
      header[4] = (byte) SenderHWAddress.Length;
      header[5] = (byte) SenderProtoAddress.Length;
      NumberSerializer.WriteUShort((ushort) Operation, header, 6);

      _icpacket = new CopyList(MemBlock.Reference(header), SenderHWAddress,
          SenderProtoAddress, TargetHWAddress, TargetProtoAddress);
    }

    public ArpPacket Respond(MemBlock response)
    {
      MemBlock target_proto = SenderProtoAddress;
      if(SenderProtoAddress.Equals(IPPacket.ZeroAddress)) {
        target_proto = IPPacket.BroadcastAddress;
      }

      return new ArpPacket(HardwareType, ProtocolType, Operations.Reply,
          response, TargetProtoAddress, SenderHWAddress, target_proto);
    }
  }
}
