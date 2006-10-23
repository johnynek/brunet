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
    //hard-coded, can as well be read from a file
    private byte[] src_addr = new byte[ETHER_ADDR_LEN];
    private byte[] dst_addr = new byte[ETHER_ADDR_LEN];
    //file descriptor for the device
    int tap_fd; 

    //initialize with the tap device;
    //and the local MAC address
    public Ethernet(string tap, string dst_addr_str, string src_addr_str) {
      device = tap;
      char[] delim = {':'};
      string[] ss = dst_addr_str.Split(delim);
      for (int i = 0; i < ETHER_ADDR_LEN; i++) {
	dst_addr[i] = Byte.Parse(ss[i], System.Globalization.NumberStyles.HexNumber);
      }
      //likewise we build the source address
      ss = src_addr_str.Split(delim);
      for (int i = 0; i < ETHER_ADDR_LEN; i++) {
	src_addr[i] = Byte.Parse(ss[i], System.Globalization.NumberStyles.HexNumber);
      }
    }
    [DllImport("libtuntap")]
    private static extern int open_tap(string device);
    [DllImport("libtuntap")]
    private static extern int read_tap(int fd, byte[] packet, int length);
    [DllImport("libtuntap")]
    private static extern int send_tap(int fd, byte[] packet, int length);

    public int Open() {
      return tap_fd = open_tap(device);
    }
    public byte[] ReceivePacket() {
      //remove the ethernet header
      byte[] packet = new byte[MTU + ETHER_HEADER_SIZE];
      int n = read_tap(tap_fd, packet, packet.Length);
      if (n == 0 || n == -1) 
	return null;
      return packet;
    }
    public bool SendPacket(byte []l3_packet, int type) {
      //this takes care of adding the ethernet header
      byte[] packet = new byte[l3_packet.Length + ETHER_HEADER_SIZE];
      //now add the necassary stuff
      Array.Copy(dst_addr, 0, packet, 0, ETHER_ADDR_LEN);
      Array.Copy(src_addr, 0, packet, ETHER_ADDR_LEN, ETHER_ADDR_LEN);
      packet[2*ETHER_ADDR_LEN] = (byte) ((type >> 8) & 255);
      packet[2*ETHER_ADDR_LEN + 1] = (byte) (type & 255);
      Array.Copy(l3_packet, 0, packet, ETHER_HEADER_SIZE, l3_packet.Length);
      int n = send_tap(tap_fd, packet, packet.Length);
      if (n != packet.Length) {
	return false;
      }
      return true;
    }

    public string Device {
      get 
	{
	  return device;
	}
    }
    /**
       public static void Main(string[] args) {
       Ethernet ether = new Ethernet("tap0", "FE:FD:00:00:00:01", "FE:FD:00:00:00:00");
       int n = ether.Open();
       if (n < 0) {
       Console.WriteLine("error opening device");
       return;
       }
       byte[] packet = ether.ReceivePacket();
       Console.WriteLine("packet size: {0}", packet.Length);
       for (int i = 12; i < 20; i++) {
       Console.WriteLine(packet[i]);
       }
       //do a test of write
       Console.WriteLine(ether.SendPacket(packet));
       }
    **/
  }
}
