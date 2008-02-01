using Brunet;

namespace Ipop {
  public class EthernetPacket {
    /*
      Destination Address - 6 bytes
      Source Address - 6 bytes
      Type - 2 bytes
    */

    public readonly MemBlock Packet, DestinationAddress, SourceAddress,
        Payload;
    public enum Types { IP = 0x800, ARP = 0x806 }
    public readonly int Type;

    public EthernetPacket(MemBlock Packet) {
      this.Packet = Packet;
      DestinationAddress = Packet.Slice(0, 6);
      SourceAddress = Packet.Slice(6, 6);
      Type = (Packet[12] << 8) | Packet[13];
      Payload = Packet.Slice(14);
    }

    public EthernetPacket(MemBlock DestinationAddress, MemBlock SourceAddress,
                          Types Type, ICopyable Payload) {
      byte[] packet = new byte[14 + Payload.Length];
      for(int i = 0; i < 6; i++) {
        packet[i] = DestinationAddress[i];
        packet[6 + i] = SourceAddress[i];
      }

      packet[12] = (byte) (((int) Type >> 8) & 0xFF);
      packet[13] = (byte) ((int) Type & 0xFF);

      Payload.CopyTo(packet, 14);
      Packet = MemBlock.Reference(packet);

      this.DestinationAddress = Packet.Slice(0, 6);
      this.SourceAddress = Packet.Slice(6, 6);
      this.Type = (Packet[12] << 8) | Packet[13];
      this.Payload = Packet.Slice(14);
    }
  }

  public class IPPacket {
    /*
      Version - 4 bits - Format =  4 - IP Protocol
      IHL - 4 bits - Length of IP Header in 32-bit words = 5
      TOS - 8 bits - Type of service = 0 - routine
      Total Length - 16 bits
      Id - 16 bits - no fragmenting unnecessary
      Flags - 3 bits - no fragmenting unnecessary
      Fragment Offset - 13 - no fragmenting unnecessary
      TTL - 8 bits - 64 seems reasonable if not absurd!
      Protocol - 8 bits - udp / tcp / icmp/ igmp
      Header Checksum - 16 - one's complement checksum of the ip header and ip options
      Source IP - 32 bits
      Dest IP - 32 bits
      Data - Rest
    */
    public readonly MemBlock SourceIP, DestinationIP;
    private string _sourceip, _destinationip;
    public string SSourceIP {
      get {
        if(_sourceip == null) {
          _sourceip = SourceIP[0] + "." + SourceIP[1] + "." + 
              SourceIP[2] + "." + SourceIP[3];
        }
        return _sourceip;
      }
    }

    public string SDestinationIP {
      get {
        if(_destinationip == null) {
          _destinationip = DestinationIP[0] + "." + DestinationIP[1] + "." + 
              DestinationIP[2] + "." + DestinationIP[3];
        }
        return _destinationip;
      }
    }

    public readonly int SourcePort, DestinationPort;
    public enum Protocols { UDP = 17 };
    public readonly byte Protocol;
    public readonly ICopyable ICPacket, ICPayload;
    protected MemBlock _packet, _payload;
    public MemBlock Packet {
      get {
        if(_packet == null) {
          byte[] tmp = new byte[ICPacket.Length];
          ICPacket.CopyTo(tmp, 0);
          _packet = MemBlock.Reference(tmp);
        }
        return _packet;
      }
    }

    public MemBlock Payload {
      get {
        if(_payload == null) {
          byte[] tmp = new byte[ICPayload.Length];
          ICPayload.CopyTo(tmp, 0);
          _payload = MemBlock.Reference(tmp);
        }
        return _payload;
      }
    }

    public IPPacket(MemBlock Packet) {
      ICPacket = _packet = Packet;
      Protocol = Packet[9];
      SourceIP = Packet.Slice(12, 4);
      DestinationIP = Packet.Slice(16, 4);
      // These are not official but are  common
      SourcePort = (Packet[20] << 8) | Packet[21];
      DestinationPort = (Packet[22] << 8) | Packet[23];
      ICPayload = _payload = Packet.Slice(20);
    }

    public IPPacket(byte Protocol, MemBlock SourceIP, MemBlock DestinationIP,
                    ICopyable Payload) {
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
      header[9] = Protocol;
      for(int i = 0; i < 4; i++) {
        header[12 + i] = SourceIP[i];
        header[16 + i] = DestinationIP[i];
      }
      int checksum = GenerateIPHeaderChecksum(header);
      header[10] = (byte) ((checksum >> 8) & 0xFF);
      header[11] = (byte) (checksum & 0xFF);

      MemBlock Header = MemBlock.Reference(header);
      ICPacket = new CopyList(Header, Payload);

      this.Protocol = Protocol;
      this.SourceIP = SourceIP;
      this.DestinationIP = DestinationIP;
      ICPayload = Payload;
    }

    protected int GenerateIPHeaderChecksum(byte[] header) {
      int value = 0;
      for(int i = 0; i < 20; i+=2) {
        byte first = header[i];
        byte second = header[i+1];
        value += second + (first << 8);
      }
      return (0xFFFF - (value & 0xFFFF) - 2);
    }
  }

  public class UDPPacket {
    /*
      SourcePort - 16 bits
      DestinationPort - 16 bits
      Length - 16 bits - includes udp header and data
      Checksum - 16 bits- disabled = 00 00 00 00
    */

    public readonly int SourcePort, DestinationPort;

    public readonly ICopyable ICPacket, ICPayload;
    protected MemBlock _packet, _payload;
    public MemBlock Packet {
      get {
        if(_packet == null) {
          byte[] tmp = new byte[ICPacket.Length];
          ICPacket.CopyTo(tmp, 0);
          _packet = MemBlock.Reference(tmp);
        }
        return _packet;
      }
    }

    public MemBlock Payload {
      get {
        if(_payload == null) {
          byte[] tmp = new byte[ICPayload.Length];
          ICPayload.CopyTo(tmp, 0);
          _payload = MemBlock.Reference(tmp);
        }
        return _payload;
      }
    }

    public UDPPacket(MemBlock packet) {
      ICPacket = _packet = packet;
      SourcePort = (packet[0] << 8) | packet[1];
      DestinationPort = (packet[2] << 8) | packet[3];
      ICPayload = _payload = packet.Slice(8);
    }


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
      ICPacket = new CopyList(MemBlock.Reference(header), Payload);
      ICPayload = Payload;
    }

    protected int GenerateUDPChecksum() {
      int value = 0;
      for(int i = 12; i < Packet.Length; i+=2) {
        byte first = Packet[i];
        byte second = (i+1 == Packet.Length) ? (byte) 0 : Packet[i+1];
        value += (second + (first << 8));
      }
      value += 17 + Packet.Length - 20;
      while(value>>16 > 0) {
        value = (value & 0xFFFF) + (value >> 16);
      }
      return (0xFFFF & ~value);
    }
  }
}
