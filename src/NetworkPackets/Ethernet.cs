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

namespace NetworkPackets {
  /**
  <summary>Encapsulates an EthernetPacket and provides the mechanisms to
  generate new Ethernet Packets.  This is immutable.</summary>
  <remarks>
    The Header is of the format:
    <list type="table">
      <listheader>
        <term>Field</term>
        <description>Position</description>
      </listheader>
      <item><term>Destination Address</term><description>6 bytes</description></item>
      <item><term>Source Address</term><description>6 bytes</description></item>
      <item><term>Type</term><description>2 bytes</description></item>
      <item><term>Data</term><description>The rest</description></item>
    </list>
  </remarks>
   */
  public class EthernetPacket: NetworkPacket {
    /// <summary>The address where the Ethernet packet is going</summary>
    public readonly MemBlock DestinationAddress;
    /// <summary>The address where the Ethernet packet originated</summary>
    public readonly MemBlock SourceAddress;
    /// <summary>This enumeration holds the types of Ethernet packets, listed
    /// are only the types, Ipop is interested in.</summary>
    public enum Types {
      /// <summary>Payload is an IP Packet</summary>
      IP = 0x800,
      /// <summary>Payload is an ARP Packet</summary>
      ARP = 0x806
    }

    /// <summary>The type for the Ethernet payload</summary>
    public readonly Types Type;
    /// <summary>The default unicast address</summary>
    public static readonly MemBlock UnicastAddress;
    /// <summary>The default broadcast (multicast) address</summary>
    public static readonly MemBlock BroadcastAddress = MemBlock.Reference(
        new byte[]{0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF});

    static EthernetPacket() {
      Random _rand = new Random();
      byte[] unicast = new byte[6];
      _rand.NextBytes(unicast);
      unicast[0] = 0xFE;
      UnicastAddress = MemBlock.Reference(unicast);
    }

    /// <summary>This parses a MemBlock into the Ethernet fields</summary>
    ///  <param name="Packet">The Ethernet packet</param>
    public EthernetPacket(MemBlock Packet) {
      _icpacket = _packet = Packet;
      DestinationAddress = Packet.Slice(0, 6);
      SourceAddress = Packet.Slice(6, 6);
      Type = (Types) ((Packet[12] << 8) | Packet[13]);
      _icpayload = _payload = Packet.Slice(14);
    }

    /// <summary>Creates an Ethernet Packet from Ethernet fields and the payload</summary>
    /// <param name="DestinationAddress">Where the Ethernet packet is going.</param>
    /// <param name="SourceAddress">Where the Ethernet packet originated.</param>
    /// <param name="Type">Type of Ethernet payload.</param>
    /// <param name="Payload">Payload as an ICopyable</param>
    public EthernetPacket(MemBlock DestinationAddress, MemBlock SourceAddress,
                        Types Type, ICopyable Payload) {
      byte[] header = new byte[14];
      for(int i = 0; i < 6; i++) {
        header[i] = DestinationAddress[i];
        header[6 + i] = SourceAddress[i];
      }

      header[12] = (byte) (((int) Type >> 8) & 0xFF);
      header[13] = (byte) ((int) Type & 0xFF);

      _icpacket = new CopyList(MemBlock.Reference(header), Payload);
      _icpayload = Payload;

      this.DestinationAddress = DestinationAddress;
      this.SourceAddress = SourceAddress;
      this.Type = Type;
    }
  }
}
