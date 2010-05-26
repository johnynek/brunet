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
  <summary>Provides an encapsulation for Udp Packets and can create new Udp
  Packets.</summary>
  <remarks>
  The contents of a Udp Packet:
  <list type="table">
    <listheader>
      <term>Field</term>
      <description>Position</description>
    </listheader>
    <item><term>Source Port</term><description>2 bytes</description></item>
    <item><term>Destination Port</term><description>2 bytes</description></item>
    <item><term>Length</term><description>2 bytes - includes udp header and
      data</description></item>
    <item><term>Checksum</term><description>2 bytes- disabled = 00 00 00 00
      </description></item>
    <item><term>Data</term><description>The rest</description></item>
  </list>
  </remarks>
  */
  public class UdpPacket: NetworkPacket {
    /// <summary>The packets originating port</summary>
    public readonly int SourcePort;
    /// <summary>The packets destination port</summary>
    public readonly int DestinationPort;

    /**
    <summary>Takes in a MemBlock and parses it as a Udp Packet.</summary>
    <param name="packet">The MemBlock containing the Udp Packet</param>
     */
    public UdpPacket(MemBlock packet) {
      _icpacket = _packet = packet;
      SourcePort = (packet[0] << 8) | packet[1];
      DestinationPort = (packet[2] << 8) | packet[3];
      _icpayload = _payload = packet.Slice(8);
    }

    /**
    <summary>Creates a Udp Packet given the source port, destination port
    and the payload.</summary>
    <param name="SourcePort">The packets originating port</param>
    <param name="DestinationPort">The packets destination port</param>
    <param name="Payload">The data for the packet.</param>
    */
    public UdpPacket(int SourcePort, int DestinationPort, ICopyable Payload) {
      byte[] header = new byte[8];
      header[0] = (byte) ((SourcePort >> 8) & 0xFF);
      header[1] = (byte) (SourcePort & 0xFF);
      header[2] = (byte) ((DestinationPort >> 8) & 0xFF);
      header[3] = (byte) (DestinationPort & 0xFF);
      int length = Payload.Length + 8;
      header[4] = (byte) ((length >> 8) & 0xFF);
      header[5] = (byte) (length & 0xFF);
      // Checksums are disabled!
      header[6] = (byte) 0;
      header[7] = (byte) 0;
      _icpacket = new CopyList(MemBlock.Reference(header), Payload);
      _icpayload = Payload;
    }
  }
}
