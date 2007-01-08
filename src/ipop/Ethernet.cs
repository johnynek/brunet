//this class simulates the ethernet; and ARP free
using System;
using System.Runtime.InteropServices;
using System.Net;
namespace Ipop {
  public class Ethernet {

    public static readonly int MTU = 1500;
    public static readonly int ETHER_HEADER_SIZE = 14;
    public static readonly int ETHER_ADDR_LEN = 6;

    private string device;
    private byte[] routerMAC;
    //file descriptor for the device
    int tapFD;

    //initialize with the tap device;
    //and the local MAC address
    public Ethernet(string tap, byte []routerMAC) {
      device = tap;
      this.routerMAC = routerMAC;
    }

    [DllImport("libtuntap")]
    private static extern int open_tap(string device);
    [DllImport("libtuntap")]
    private static extern int read_tap(int fd, byte[] packet, int length);
    [DllImport("libtuntap")]
    private static extern int send_tap(int fd, byte[] packet, int length);

    public int Open() {
      return tapFD = open_tap(device);
    }
    public byte[] ReceivePacket(out int n) {
      byte[] packet = new byte[MTU + ETHER_HEADER_SIZE];
      n = read_tap(tapFD, packet, packet.Length);
      if (n == 0 || n == -1) 
      	return null;
      return packet;
    }

    public bool SendPacket(byte []l3Packet, int type, byte [] dstMacAddr) {
      byte[] packet = new byte[l3Packet.Length + ETHER_HEADER_SIZE];
      //Build the Ethernet header
      Array.Copy(dstMacAddr, 0, packet, 0, ETHER_ADDR_LEN);
      Array.Copy(routerMAC, 0, packet, ETHER_ADDR_LEN, ETHER_ADDR_LEN);
      packet[2*ETHER_ADDR_LEN] = (byte) ((type >> 8) & 255);
      packet[2*ETHER_ADDR_LEN + 1] = (byte) (type & 255);
      Array.Copy(l3Packet, 0, packet, ETHER_HEADER_SIZE, l3Packet.Length);
      int n = send_tap(tapFD, packet, packet.Length);
      if (n != packet.Length) {
      	return false;
      }
      return true;
    }
  }
}
