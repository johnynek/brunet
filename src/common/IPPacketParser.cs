using System.Net;

namespace Ipop {
  public class IPPacketParser {
    readonly static int DEST_ADDR_START = 16;
    readonly static int SRC_ADDR_START = 12;
    //return the destination address
    public static IPAddress DestAddr(byte[] packet) {
      byte[] address = new byte[4];
      for (int i = 0, k = DEST_ADDR_START; i < address.Length; i++, k++) {
	address[i] = packet[k];
      }
      return new IPAddress(address);
    }

    public static IPAddress SrcAddr(byte[] packet) {
      byte[] address = new byte[4];
      for (int i = 0, k = SRC_ADDR_START; i < address.Length; i++, k++) {
	address[i] = packet[k];
      }
      return new IPAddress(address);
    }
  }
}
