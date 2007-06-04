using System.Net;

namespace Ipop {
  public class IPPacketParser {
    readonly static int DEST_ADDR_START = 16;
    readonly static int SRC_ADDR_START = 12;
    //return the destination address
    public static IPAddress DestAddr(byte[] packet) {
      /* Commented out the following: no reason why shouldn't work.*/
      //       byte[] address = new byte[4];
      //       for (int i = 0, k = DEST_ADDR_START; i < address.Length; i++, k++) {
      // 	System.Console.Error.WriteLine(packet[k]);
      // 	address[i] = packet[k];
      //       }
      //       return new IPAddress(address);
      /* short term fix. */
      string address = "";
      int addr_len = 4;
      int k = DEST_ADDR_START;
      for (int i = 0; i < addr_len - 1; i++,  k++) {
        address += packet[k].ToString() + ".";
      }
      address += packet[k].ToString();
      return IPAddress.Parse(address);
    }

    public static IPAddress SrcAddr(byte[] packet) {
      /* Commented out the following: no reason why shouldn't work.*/
//       byte[] address = new byte[4];
//       for (int i = 0, k = SRC_ADDR_START; i < address.Length; i++, k++) {
// 	address[i] = packet[k];
//       }
//       return new IPAddress(address);
      /* short term fix. */
      string address = "";
      int addr_len = 4;
      int k = SRC_ADDR_START;
      for (int i = 0; i < addr_len - 1; i++,  k++) {
        address += packet[k].ToString() + ".";
      }
      address += packet[k].ToString();
      return IPAddress.Parse(address);
    }
  }
}
