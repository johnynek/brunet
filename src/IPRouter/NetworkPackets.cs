using Brunet;

namespace Ipop {
  public abstract class DataPacket {
    protected ICopyable _icpacket;
    public ICopyable ICPacket { get { return _icpacket; } }

    protected MemBlock _packet;
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

  public abstract class NetworkPacket: DataPacket {
    protected ICopyable _icpayload;
    public ICopyable ICPayload { get { return _icpayload; } }

    protected MemBlock _payload;
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

  public class EthernetPacket: NetworkPacket {
    /*
      Destination Address - 6 bytes
      Source Address - 6 bytes
      Type - 2 bytes
    */

    public readonly MemBlock DestinationAddress, SourceAddress;
    public enum Types { IP = 0x800, ARP = 0x806 }
    public readonly int Type;
    public static readonly MemBlock UnicastAddress = MemBlock.Reference(
        new byte[]{0xFE, 0xFD, 0, 0, 0, 0});
    public static readonly MemBlock BroadcastAddress = MemBlock.Reference(
        new byte[]{0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF});

    public EthernetPacket(MemBlock Packet) {
      _icpacket = _packet = Packet;
      DestinationAddress = Packet.Slice(0, 6);
      SourceAddress = Packet.Slice(6, 6);
      Type = (Packet[12] << 8) | Packet[13];
      _icpayload = _payload = Packet.Slice(14);
    }

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
      this.Type = (int) Type;
    }
  }

  public class IPPacket: NetworkPacket {
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

    public enum Protocols { IGMP = 2, UDP = 17 };
    public readonly byte Protocol;

    public IPPacket(MemBlock Packet) {
      _icpacket = _packet = Packet;
      Protocol = Packet[9];
      SourceIP = Packet.Slice(12, 4);
      DestinationIP = Packet.Slice(16, 4);
      _icpayload = _payload = Packet.Slice(20);
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
      _icpacket = new CopyList(Header, Payload);

      this.Protocol = Protocol;
      this.SourceIP = SourceIP;
      this.DestinationIP = DestinationIP;
      _icpayload = Payload;
    }

    protected int GenerateIPHeaderChecksum(byte[] header) {
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

  public class UDPPacket: NetworkPacket {
    /*
      SourcePort - 16 bits
      DestinationPort - 16 bits
      Length - 16 bits - includes udp header and data
      Checksum - 16 bits- disabled = 00 00 00 00
    */

    public readonly int SourcePort, DestinationPort;

    public UDPPacket(MemBlock packet) {
      _icpacket = _packet = packet;
      SourcePort = (packet[0] << 8) | packet[1];
      DestinationPort = (packet[2] << 8) | packet[3];
      _icpayload = _payload = packet.Slice(8);
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
      _icpacket = new CopyList(MemBlock.Reference(header), Payload);
      _icpayload = Payload;
    }

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

  public class IGMPPacket: NetworkPacket {
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
