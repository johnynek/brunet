/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
      /// <summary>Payload is an Arp Packet</summary>
      Arp = 0x806
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

    /// <summary>Generates a multicast mac address based upon the multicast IP address.</summary>
    /// <param name="mcast_ip">The multicast ip address</param>
    public static MemBlock GetMulticastEthernetAddress(MemBlock mcast_ip) {
      // set multicast bit and create address
      byte[] mcast_addr = new byte[6];
      mcast_addr[0] = 0x01;
      mcast_addr[1] = 0x00;
      mcast_addr[2] = 0x5E;
      mcast_addr[3] = (byte)(mcast_ip[1] & 0x7F);
      mcast_addr[4] = mcast_ip[2];
      mcast_addr[5] = mcast_ip[3];
      return MemBlock.Reference(mcast_addr);
    }
  }
}
