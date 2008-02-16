using Brunet;
using System.Collections;

namespace Ipop {
/**
  0                   1                   2                   3
  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
  |     op (1)    |   htype (1)   |   hlen (1)    |   hops (1)    |
  +---------------+---------------+---------------+---------------+
  |                            xid (4)                            |
  +-------------------------------+-------------------------------+
  |           secs (2)            |           flags (2)           |
  +-------------------------------+-------------------------------+
  |                          ciaddr  (4)                          |
  +---------------------------------------------------------------+
  |                          yiaddr  (4)                          |
  +---------------------------------------------------------------+
  |                          siaddr  (4)                          |
  +---------------------------------------------------------------+
  |                          giaddr  (4)                          |
  +---------------------------------------------------------------+
  |                                                               |
  |                          chaddr  (16)                         |
  |                                                               |
  |                                                               |
  +---------------------------------------------------------------+
  |                                                               |
  |                          sname   (64)                         |
  +---------------------------------------------------------------+
  |                                                               |
  |                          file    (128)                        |
  +---------------------------------------------------------------+
  |                                                               |
  |                          options (variable)                   |
  +---------------------------------------------------------------+

    OP - 1 for request, 2 for response
    htype - hardware address type - leave at 1
    hlen - hardware address length - 6 for ethernet mac address
    hops - optional - leave at 0, no relay agents
    xid - transaction id
    secs - seconds since beginning renewal
    flags - 
    ciaddr - clients currrent ip (client in bound, renew, or rebinding state)
    yiaddr - ip address server is giving to client
    siaddr - server address
    giaddr - leave at zero, no relay agents
    chaddr - client hardware address
    sname - optional server hostname
    file - optional
    magic cookie - yuuuum! - byte[4] = {99, 130, 83, 99}
    options - starts at 240!
 */
  public class DHCPPacket: DataPacket {
    public enum MessageTypes {
      DISCOVER = 1,
      OFFER = 2,
      REQUEST = 3,
      DECLINE = 4,
      ACK = 5,
      NACK = 6,
      RELEASE = 7,
      INFORM = 8
    };

    public enum OptionTypes {
      SUBNET_MASK = 1,
      ROUTER = 3,
      NAME_SERVER = 5,
      DOMAIN_NAME_SERVER = 6,
      HOST_NAME = 12,
      DOMAIN_NAME = 15,
      MTU = 26,
      REQUESTED_IP = 50,
      LEASE_TIME = 51,
      MESSAGE_TYPE = 53,
      SERVER_ID = 54,
      PARAMETER_REQUEST_LIST = 55
    };

    public readonly byte op;
    public readonly MemBlock xid, ciaddr, yiaddr, siaddr, chaddr;
    public readonly Hashtable Options;
    public static readonly MemBlock magic_key = 
        MemBlock.Reference(new byte[4] {99, 130, 83, 99});

    public DHCPPacket(MemBlock Packet) {
      _packet = Packet;
      op = Packet[0];
      xid = Packet.Slice(4, 4);
      ciaddr = Packet.Slice(12, 4);
      yiaddr = Packet.Slice(16, 4);
      siaddr = Packet.Slice(20, 4);
      chaddr = Packet.Slice(28, 6);
      int idx = 240;

      /* Parse the options */
      Options = new Hashtable();
      /*  255 is end of options */
      while(Packet[idx] != 255) {
        /* 0 is padding */
        if(Packet[idx] != 0) {
          object type = null;
          try {
            type = (OptionTypes) Packet[idx++];
          }
          catch {
            type = (byte) Packet[idx++];
          }
          byte length = Packet[idx++];
          Options[type] = Packet.Slice(idx, length);
          idx += length;
        }
        else {
          idx++;
        }
      }
    }

    public DHCPPacket(byte op, MemBlock xid, MemBlock ciaddr, MemBlock yiaddr,
                     MemBlock siaddr, MemBlock chaddr, Hashtable Options) {
      this.op = op;
      this.xid = xid;
      this.ciaddr = ciaddr;
      this.yiaddr = yiaddr;
      this.siaddr = siaddr;
      this.chaddr = chaddr;
      this.Options = Options;

      byte[] header = new byte[240];
      header[0] = op;
      header[1] = 1;
      header[2] = 6;
      header[3] = 0;

      xid.CopyTo(header, 4);
      for(int i = 8; i < 12; i++) {
        header[i] = 0;
      }
      ciaddr.CopyTo(header, 12);
      yiaddr.CopyTo(header, 16);
      siaddr.CopyTo(header, 20);
      for(int i = 24; i < 28; i++) {
        header[i] = 0;
      }
      chaddr.CopyTo(header, 28);
      for(int i = 34; i < 236; i++) {
        header[i] = 0;
      }
      magic_key.CopyTo(header, 236);

      _icpacket = new CopyList(MemBlock.Reference(header));
      foreach(DictionaryEntry de in Options) {
        byte[] value = (byte[]) de.Value;
        byte[] tmp = new byte[value.Length + 2];
        try {
          tmp[0] = (byte) (OptionTypes) de.Key;
        }
        catch {
          tmp[0] = (byte) de.Key;
        }
        tmp[1] = (byte) value.Length;
        value.CopyTo(tmp, 2);
        _icpacket = new CopyList(_icpacket, MemBlock.Reference(tmp));
      }
      byte []end = new byte[1]{255}; /* End of Options */
      _icpacket = new CopyList(_icpacket, MemBlock.Reference(end));
    }
  }
}
