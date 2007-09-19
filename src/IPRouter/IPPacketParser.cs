using System.Net;
using System;
using Brunet;

namespace Ipop {
  public class IPPacketParser {
    readonly static int DEST_ADDR_START = 16;
    readonly static int SRC_ADDR_START = 12;
    readonly static int DEST_PORT_START = 22;
    readonly static int SRC_PORT_START = 20;
    readonly static int PROTOCOL_START = 9;

    public static int GetDestPort(MemBlock packet) {
      return (packet[DEST_PORT_START] << 8) + packet[DEST_PORT_START + 1];
    }

    public static int GetSrcPort(MemBlock packet) {
      return (packet[SRC_PORT_START] << 8) + packet[SRC_PORT_START + 1];
    }

    public static int GetProtocol(MemBlock packet) {
      return packet[PROTOCOL_START];
    }

    public static IPAddress GetDestAddr(MemBlock packet) {
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

    public static IPAddress GetSrcAddr(MemBlock packet) {
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

  public class EthernetPacketParser {
    readonly static int PROTOCOL_START = 12;
    readonly static int PAYLOAD_START = 14;

    public static byte[] GetMAC(MemBlock packet) {
      byte[] mac = new byte[6];
      Array.Copy(packet, 6, mac, 0, 6);
      return mac;
    }

    public static int GetProtocol(MemBlock packet) {
      return (packet[PROTOCOL_START] << 8) + packet[PROTOCOL_START + 1];
    }

    public static MemBlock GetPayload(MemBlock packet) {
      return packet.Slice(PAYLOAD_START, packet.Length - PAYLOAD_START);
    }
  }
}
