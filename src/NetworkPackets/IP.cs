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

#if NUNIT
using NUnit.Framework;
#endif

namespace NetworkPackets {
  /**
  <summary>Encapsulates an IP Packet and can create new IP Packets.</summary>
  <remarks>
  The packet consists of these fields:
  <list type="table">
  <listheader>
    <term>Field</term>
    <description>Description</description>
  </listheader>
  <item>
    <term>Version</term>
    <description>4 bits - Format =  4 - IP Protocol</description>
  </item>
  <item>
    <term>IHL</term>
    <description>4 bits - Length of IP Header in 32-bit words = 5</description>
  </item>
  <item>
    <term>TOS</term>
    <description>8 bits - Type of service = 0 - routine</description>
  </item>
  <item>
    <term>Total Length</term>
    <description>16 bits</description>
  </item>
  <item>
    <term>ID</term>
    <description>16 bits - no fragmenting unnecessary</description>
  </item>
  <item>
    <term>Flags</term>
    <description>3 bits - no fragmenting unnecessary</description>
  </item>
  <item>
    <term>Fragment offset</term>
    <description>13 - no fragmenting unnecessary</description>
  </item>
  <item>
    <term>Ttl</term>
    <description>8 bits - 64 seems reasonable if not absurd!</description>
  </item>
  <item>
    <term>Protocol</term>
    <description>8 bits - udp / tcp / icmp/ igmp</description>
  </item>
  <item>
    <term>Header Checksum</term>
    <description>16 - one's complement checksum of the ip header and ip 
    options</description>
  </item>
  <item>
    <term>Source IP</term>
    <description>32 bits</description>
  </item>
  <item>
    <term>Destination IP</term>
    <description>32 bits</description>
  </item>
  <item>
    <term>Data</term>
    <description>Rest</description>
  </item>
  </list>
  </remarks>
  */
#if NUNIT
  [TestFixture]
#endif
  public class IPPacket: NetworkPacket {
    /// <summary>The zero address</summary>
    public static readonly MemBlock ZeroAddress = MemBlock.Reference(
      new byte[]{0, 0, 0, 0});
    /// <summary>The default broadcast (multicast) address</summary>
    public static readonly MemBlock BroadcastAddress = MemBlock.Reference(
      new byte[]{0xFF, 0xFF, 0xFF, 0xFF});
    /// <summary>The IP Address where the packet originated</summary>
    public readonly MemBlock SourceIP;
    /// <summary>The IP Address where the packet is going</summary>
    public readonly MemBlock DestinationIP;

    /// <summary>Created only if SSourceIP is accessed, contains the string
    /// representation of the Source IP</summary>
    private string _sourceip;
    /// <summary>Contains the string representation of the Source IP</summary>
    public string SSourceIP {
      get {
        if(_sourceip == null) {
          _sourceip = SourceIP[0] + "." + SourceIP[1] + "." + 
              SourceIP[2] + "." + SourceIP[3];
        }
        return _sourceip;
      }
    }

    /// <summary>Created only if SDestinationIP is accessed, contains the
    /// string representation of the Source IP</summary>
    private string _destinationip;
    /// <summary>Contains the string representation of the Destination Ip</summary>
    public string SDestinationIP {
      get {
        if(_destinationip == null) {
          _destinationip = DestinationIP[0] + "." + DestinationIP[1] + "." + 
              DestinationIP[2] + "." + DestinationIP[3];
        }
        return _destinationip;
      }
    }

    /// <summary> Enumeration of Protocols used by Ipop.</summary>
    public enum Protocols {
      /// <summary> Internet Control Message Protocol.</summary>
      Icmp = 1,
      /// <summary>Internet Group Management Protocol.</summary>
      Igmp = 2,
      /// <summary>Transmission Control Protocol.</summary>
      Tcp = 6,
      /// <summary>User Datagram Protocol.</summary>
      Udp = 17
    };
    /// <summary>The protocol for this packet.</summary>
    public readonly Protocols Protocol;

    /**
    <summary>Takes in a MemBlock and parses it into IP Header fields</summary>
    <param name="Packet">The IP Packet to parse.</param>
    */
    public IPPacket(MemBlock Packet) {
      _icpacket = _packet = Packet;
      Protocol = (Protocols) Packet[9];
      SourceIP = Packet.Slice(12, 4);
      DestinationIP = Packet.Slice(16, 4);
      _icpayload = _payload = Packet.Slice(20);
    }

    /**
    <summary>Takes in the IP Header fields and a payload to create an IP
    Packet.  Unlisted fields are generated by this constructor automatically.
    </summary>
    <param name="Protocol">The type of payload</param>
    <param name="SourceIP">The packets originating ip address</param>
    <param name="DestinationIP">The destination for the packet</param>
    <param name="Payload">The data stored in the IP Packet</param>
    */
    public IPPacket(Protocols Protocol, MemBlock SourceIP,
                    MemBlock DestinationIP, ICopyable Payload) {
      byte[] header = new byte[20];
      // Version | IHL
      header[0] = (4 << 4) | 5;
      // Just a routine header!
      header[1] = 0;
      int length = header.Length + Payload.Length;
      header[2] = (byte) ((length >> 8) & 0xFF);
      header[3] = (byte) (length & 0xFF);
      // Fragment crap
      header[4] = 0;
      header[5] = 0;
      header[6] = 0;
      header[7] = 0;
      // Ttl
      header[8] = 64;
      header[9] = (byte) Protocol;
      for(int i = 0; i < 4; i++) {
        header[12 + i] = SourceIP[i];
        header[16 + i] = DestinationIP[i];
      }
      int checksum = GenerateChecksum(MemBlock.Reference(header));
      header[10] = (byte) ((checksum >> 8) & 0xFF);
      header[11] = (byte) (checksum & 0xFF);

      MemBlock Header = MemBlock.Reference(header);
      _icpacket = new CopyList(Header, Payload);

      this.Protocol = Protocol;
      this.SourceIP = SourceIP;
      this.DestinationIP = DestinationIP;
      _icpayload = Payload;
    }


    /**
    <summary>Takes in the IP Header fields and a payload to create an IP
    Packet.  Unlisted fields are generated by this constructor automatically.
    </summary>
    <param name="Protocol">The type of payload</param>
    <param name="SourceIP">The packets originating ip address</param>
    <param name="DestinationIP">The destination for the packet</param>
    <param name="hdr">The original header of the IPPacket</param>
    <param name="Payload">The data stored in the IP Packet</param>
    */
    public IPPacket(Protocols Protocol, MemBlock SourceIP,
                    MemBlock DestinationIP, MemBlock hdr, ICopyable Payload) {
      byte[] header = new byte[20];
      // Version | IHL
      header[0] = hdr[0];
      // Just a routine header!
      header[1] = hdr[1];
      int length = header.Length + Payload.Length;
      header[2] = (byte) ((length >> 8) & 0xFF);
      header[3] = (byte) (length & 0xFF);
      // Fragment crap
      header[4] = hdr[4];
      header[5] = hdr[5];
      header[6] = hdr[6];
      header[7] = hdr[7];
      // Ttl
      header[8] = hdr[8];
      header[9] = hdr[9]; 
      for(int i = 0; i < 4; i++) {
        header[12 + i] = SourceIP[i];
        header[16 + i] = DestinationIP[i];
      }
      int checksum = GenerateChecksum(MemBlock.Reference(header));
      header[10] = (byte) ((checksum >> 8) & 0xFF);
      header[11] = (byte) (checksum & 0xFF);

      MemBlock Header = MemBlock.Reference(header);
      _icpacket = new CopyList(Header, Payload);

      this.Protocol = Protocol;
      this.SourceIP = SourceIP;
      this.DestinationIP = DestinationIP;
      _icpayload = Payload;
    }

    /**
    <summary>If we're not creating a packet from scratch, this will keep its
    integrity and transform UDP and TCP headers as well (due to their checksums
    being dependent on source and destination ip addresses.</summary>
    <param name="Packet">The packet to translate.</param>
    <param name="SourceIP">The new source ip.</param>
    <param name="DestinationIP">The new destination ip.</param>
    */
    public static MemBlock Translate(MemBlock Packet, MemBlock SourceIP,
                                     MemBlock DestinationIP) {
      byte[] new_packet = new byte[Packet.Length];
      Packet.CopyTo(new_packet, 0);
      int length = (Packet[0] & 0xF) * 4;
      SourceIP.CopyTo(new_packet, 12);
      // Do not copy over multicast addresses!
      if(new_packet[16] < 224 || new_packet[16] > 239) {
        DestinationIP.CopyTo(new_packet, 16);
      }
      // Zero out the checksum so we don't use it in our future calculations
      new_packet[10] = 0;
      new_packet[11] = 0;
      MemBlock header = MemBlock.Reference(new_packet, 0, length);
      int checksum = GenerateChecksum(header);
      new_packet[10] = (byte) ((checksum >> 8) & 0xFF);
      new_packet[11] = (byte) (checksum & 0xFF);
      Protocols p = (Protocols) Packet[9];

      bool fragment = ((Packet[6] & 0x1F) | Packet[7]) != 0;

      if(p == Protocols.Udp && !fragment) {
        // Zero out the checksum to disable it
        new_packet[length + 6] = 0;
        new_packet[length + 7] = 0;
      }
      else if(p == Protocols.Tcp && !fragment) {
        // Zero out the checksum so we don't use it in our future calculations
        new_packet[length + 16] = 0;
        new_packet[length + 17] = 0;
        MemBlock payload = MemBlock.Reference(new_packet).Slice(length);
        MemBlock pseudoheader = IPPacket.MakePseudoHeader(SourceIP,
            DestinationIP, (byte) Protocols.Tcp, (ushort) (Packet.Length - 20));
        checksum = IPPacket.GenerateChecksum(payload, pseudoheader);
        new_packet[length + 16] = (byte) ((checksum >> 8) & 0xFF);
        new_packet[length + 17] = (byte) (checksum & 0xFF);
      }
      return MemBlock.Reference(new_packet);
    }

    /**
    <summary>Generates an 16-bit IP (UDP, TCP) checksum based upon header and
    optional extra memory blocks placed into args.</summary>
    <param name="header">The Header to base the checksum on.</param>
    <param name="args">Any extra memory blocks to include in checksum
    calculations.</param>
    <returns>a 16-bit IP header checksum.</returns>
    */
    public static ushort GenerateChecksum(MemBlock header, params Object[] args) {
      int value = 0;
      int length = header.Length;

      for(int i = 0; i < length; i+=2) {
        byte first = header[i];
        byte second =  (length == i + 1) ? (byte) 0 : header[i+1];
        value += second + (first << 8);
      }

      for(int j = 0; j < args.Length; j++) {
        MemBlock tmp = (MemBlock) args[j];
        length = tmp.Length;
        for(int i = 0; i < length; i+=2) {
          byte first = tmp[i];
          byte second =  (length == i + 1) ? (byte) 0 : tmp[i+1];
          value += second + (first << 8);
        }
      }

      while(value >> 16 > 0) {
        value = (value & 0xFFFF) + (value >> 16);
      }

      return (ushort) (~value & 0xFFFF);
    }

    public static MemBlock MakePseudoHeader(MemBlock source_address,
        MemBlock destination_address, byte protocol, ushort length)
    {
      byte[] pseudoheader = new byte[source_address.Length + destination_address.Length + 4];
      int pos = 0;
      source_address.CopyTo(pseudoheader, pos);
      pos += source_address.Length;
      destination_address.CopyTo(pseudoheader, pos);
      pos += destination_address.Length;
      pseudoheader[++pos] = protocol;
      NumberSerializer.WriteUShort(length, pseudoheader, ++pos);
      return MemBlock.Reference(pseudoheader);
    }

#if NUNIT
    public IPPacket() {}

    [Test]
    public void ChecksumTest() {
      byte[] header = new byte[] {0x45, 0x00, 0x00, 0x34, 0xad, 0xdd, 0x00,
        0x00, 0x38, 0x06, 0x00, 0x00, 0x40, 0xe9, 0xa1, 0xa6, 0xc0, 0xa8, 0x01,
        0x64};
      int checksum = GenerateChecksum(MemBlock.Reference(header));
      Assert.AreEqual(checksum, 0x304b, "IP Header Checksum");

      header = new byte[] {0x40, 0xe9, 0xa1, 0xa6, 0xc0, 0xa8, 0x01, 0x64,
        0x00, 0x06, 0x00, 0x20};
      byte[] packet = new byte[] {0x00, 0x50, 0xe9, 0x39, 0x16, 0xec, 0x28,
        0x09, 0xd0, 0x29, 0xda, 0x38, 0x80, 0x10, 0x00, 0x8b, 0x00, 0x00, 0x00,
        0x00, 0x01, 0x01, 0x08, 0x0a, 0x17, 0x02, 0x34, 0x3c, 0x00, 0x19,
        0x7f, 0x64};
      checksum = GenerateChecksum(MemBlock.Reference(header),
                                  MemBlock.Reference(packet));
      Assert.AreEqual(checksum, 0x33f9, "IP Header Checksum");
    }
#endif
  }
}
