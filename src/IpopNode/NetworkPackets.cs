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

namespace NetworkPackets {
  /**
  <summary>Provides an abstraction to sue a generic packet idea, that is you
  can use the ICPacket portion to make a large packet and just copy the final 
  object to a byte array in the end rather then at each stage.  When Packet
  is accessed and is undefined, it will perform the copy automatically for 
  you from ICPacket to Packet.</summary>
  */
  public abstract class DataPacket {
    /// <summary>The packet in ICopyable format.</summary>
    protected ICopyable _icpacket;
    /// <summary>The packet in ICopyable format.</summary>
    public ICopyable ICPacket { get { return _icpacket; } }

    /// <summary>The packet in MemBlock format</summary>
    protected MemBlock _packet;
    /// <summary>The packet in ICopyable format.  Creates the _packet if it
    /// does not already exist.</summary>
    public MemBlock Packet {
      get {
        if(_packet == null) {
          if(_icpacket is MemBlock) {
            _packet = (MemBlock) _icpacket;
          }
          else {
            byte[] tmp = new byte[_icpacket.Length];
            _icpacket.CopyTo(tmp, 0);
            _packet = MemBlock.Reference(tmp);
          }
        }
        return _packet;
      }
    }
  }

  /**
  <summary>Similar to DataPacket but also provides a(n) (IC)Payload for packet
  types that have a header and a body, as Ethernet and IP Packets do.</summary>
  */
  public abstract class NetworkPacket: DataPacket {
    /// <summary>The payload in ICopyable format.</summary>
    protected ICopyable _icpayload;
    /// <summary>The payload in ICopyable format.</summary>
    public ICopyable ICPayload { get { return _icpayload; } }
    /// <summary>The packet in MemBlock format</summary>
    protected MemBlock _payload;
    /// <summary>The packet in ICopyable format.  Creates the _packet if it
    /// does not already exist.</summary>
    public MemBlock Payload {
      get {
        if(_payload == null) {
          if(_icpayload is MemBlock) {
            _payload = (MemBlock) _icpayload;
          }
          else {
            byte[] tmp = new byte[_icpayload.Length];
            _icpayload.CopyTo(tmp, 0);
            _payload = MemBlock.Reference(tmp);
          }
        }
        return _payload;
      }
    }
  }

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
    /**  <summary>This enumeration holds the types of Ethernet packets, listed
    are only the types, Ipop is interested in.</summary>
    */
    public enum Types {
      /// <summary>Payload is an IP Packet</summary>
      IP = 0x800,
      /// <summary>Payload is an ARP Packet</summary>
      ARP = 0x806
    }
    /// <summary>The type for the Ethernet payload</summary>
    public readonly Types Type;
    /// <summary>The default unicast address</summary>
    public static readonly MemBlock UnicastAddress = MemBlock.Reference(
        new byte[]{0xFE, 0xFD, 0, 0, 0, 0});
    /// <summary>The default broadcast (multicast) address</summary>
    public static readonly MemBlock BroadcastAddress = MemBlock.Reference(
        new byte[]{0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF});

    /**
    <summary>This parses a MemBlock into the Ethernet fields</summary>
    <param name="Packet">The Ethernet packet</param>
    */
    public EthernetPacket(MemBlock Packet) {
      _icpacket = _packet = Packet;
      DestinationAddress = Packet.Slice(0, 6);
      SourceAddress = Packet.Slice(6, 6);
      Type = (Types) ((Packet[12] << 8) | Packet[13]);
      _icpayload = _payload = Packet.Slice(14);
    }

    /**
    <summary>Creates an Ethernet Packet from Ethernet fields and the 
    payload</summary>
    <param name="DestinationAddress">Where the Ethernet packet is going.</param>
    <param name="SourceAddress">Where the Ethernet packet originated.</param>
    <param name="Type">Type of Ethernet payload.</param>
    <param name="Payload">Payload as an ICopyable</param>
    */
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
    <term>TTL</term>
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
  public class IPPacket: NetworkPacket {
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

    /// <summary> Enumeration of Protocols used by Ipop </summary>
    public enum Protocols {
      /// <summary>Internet Group Management Protocol</summary>
      IGMP = 2,
      /// <summary>User Datagram Protocol</summary>
      UDP = 17
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
      // TTL
      header[8] = 64;
      header[9] = (byte) Protocol;
      for(int i = 0; i < 4; i++) {
        header[12 + i] = SourceIP[i];
        header[16 + i] = DestinationIP[i];
      }
      int checksum = GenerateIPHeaderChecksum(MemBlock.Reference(header));
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
    <summary>Generates an 32-bit IP Header checksum based upon the header as
    specified in IP specifications.</summary>
    <param name="header">The IP Header to base the checksum on.</param>
    <returns>a 32-bit IP header checksum</returns>
    */
    protected int GenerateIPHeaderChecksum(MemBlock header) {
      int value = 0;
      for(int i = 0; i < 20; i+=2) {
        byte first = header[i];
        byte second = header[i+1];
        value += second + (first << 8);
      }
      while(value >> 16 > 0) {
        value = (value & 0xFFFF) + (value >> 16);
      }
      return ~value;
    }
  }

  /**
  <summary>Provides an encapsulation for UDP Packets and can create new UDP
  Packets.</summary>
  <remarks>
  The contents of a UDP Packet:
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
  public class UDPPacket: NetworkPacket {
    /// <summary>The packets originating port</summary>
    public readonly int SourcePort;
    /// <summary>The packets destination port</summary>
    public readonly int DestinationPort;

    /**
    <summary>Takes in a MemBlock and parses it as a UDP Packet.</summary>
    <param name="packet">The MemBlock containing the UDP Packet</param>
     */
    public UDPPacket(MemBlock packet) {
      _icpacket = _packet = packet;
      SourcePort = (packet[0] << 8) | packet[1];
      DestinationPort = (packet[2] << 8) | packet[3];
      _icpayload = _payload = packet.Slice(8);
    }

    /**
    <summary>Creates a UDP Packet given the source port, destination port
    and the payload.</summary>
    <param name="SourcePort">The packets originating port</param>
    <param name="DestinationPort">The packets destination port</param>
    <param name="Payload">The data for the packet.</param>
    */
    public UDPPacket(int SourcePort, int DestinationPort, ICopyable Payload) {
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

    /**
    <summary>Generates the udp checksum for the entire packet, this is
    currently broken and unused as it is unnecessary.</summary>
    <returns>Returns back the 16-bit checksum </returns>
    */
    protected int GenerateUDPChecksum() {
      int value = 0;
      for(int i = 12; i < Packet.Length; i+=2) {
        byte first = Packet[i];
        byte second = (i+1 == Packet.Length) ? (byte) 0 : Packet[i+1];
        value += (second + (first << 8));
      }
      value += 17 + Packet.Length;
      while(value>>16 > 0) {
        value = (value & 0xFFFF) + (value >> 16);
      }
      return (0xFFFF & ~value);
    }
  }

  /**
  <summary>Unsupported, this class is too big to support now!</summary>
  */
  public class IGMPPacket: NetworkPacket {
  /**
  <summary>Unsupported, this class is too big to support now!</summary>
  */
    public enum Types { Join = 0x16, Leave = 0x17};
    public readonly byte Type;
    public readonly MemBlock GroupAddress;

    public IGMPPacket(MemBlock packet) {
      _icpacket = _packet = packet;
      Type = packet[0];
      GroupAddress = packet.Slice(4, 4);
      _icpayload = _payload = packet.Slice(8);
    }

    public IGMPPacket(byte Type, MemBlock GroupAddress) {
//      byte[] header = new byte[8];
    }
  }
}
