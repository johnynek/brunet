using System;
using System.Runtime.InteropServices;
using System.Net;
using Brunet;

namespace Ipop {
  /**
   * A class to interact with the underlying TAP device.
   */
  public class Ethernet {

    public static readonly int MTU = 1500;
    public static readonly int ETHER_HEADER_SIZE = 14;
    public static readonly int ETHER_ADDR_LEN = 6;

    private string device;
    private byte[] routerMAC;
    //file descriptor for the device
    int tapFD;

    [DllImport("libtuntap")]
    private static extern int open_tap(string device);
    [DllImport("libtuntap")]
    private static extern int read_tap(int fd, byte[] packet, int length);
    [DllImport("libtuntap")]
    private static extern int send_tap(int fd, byte[] packet, int length);

    //initialize with the tap device;
    //and the local MAC address
    public Ethernet(string tap, byte []routerMAC) {
      device = tap;
      this.routerMAC = routerMAC;
      if((tapFD = open_tap(device)) < 0) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "Unable to set up the tap");
        Environment.Exit(1);
      }
    }

    public MemBlock Read() {
      byte[] packet = new byte[MTU + ETHER_HEADER_SIZE];
      int n = read_tap(tapFD, packet, packet.Length);
      if (n == 0 || n == -1) 
        return null;
      return MemBlock.Reference(packet, 0, n);
    }

    public bool Write(ICopyable packet, EthernetPacket.Types type,
                           MemBlock DestinationAddress) {
      EthernetPacket ep = new EthernetPacket(DestinationAddress, routerMAC, 
                                             type, packet);
      int n = send_tap(tapFD, ep.Packet, ep.Packet.Length);
      if (n != ep.Packet.Length)
        return false;

      return true;
    }
  }
}
