using System;
using System.Runtime.InteropServices;
using System.Net;
using System.Threading;
using Brunet;

namespace Ipop {
  /**
   * A class to interact with the underlying TAP device.
   */
  public class Ethernet {
    public static readonly int MTU = 1500;
    protected byte[] _send_buffer = new byte[MTU];
    protected byte[] _read_buffer = new byte[MTU];
    protected string device;
    //file descriptor for the device
    protected int fd;

    [DllImport("libtuntap")]
    private static extern int open_tap(string device);
    [DllImport("libtuntap")]
    private static extern int read_tap(int fd, byte[] packet, int length);
    [DllImport("libtuntap")]
    private static extern int send_tap(int fd, byte[] packet, int length);

    //initialize with the tap device;
    //and the local MAC address
    public Ethernet(string tap) {
      device = tap;
      if((fd = open_tap(device)) < 0) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "Unable to set up the tap");
        Environment.Exit(1);
      }
    }

    public MemBlock Read() {
      int length = read_tap(fd, _read_buffer, _read_buffer.Length);
      if (length == 0 || length == -1) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "Couldn't read TAP");
        return null;
      }
      return MemBlock.Copy(_read_buffer, 0, length);
    }

    public void Write(ICopyable data) {
      int length = data.CopyTo(_send_buffer, 0);
      int n = send_tap(fd, _send_buffer, length);
      if(n != length) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, String.Format(
          "TAP: Didn't write all data ... only {0} / {1}", n, length));
      }
    }
  }
}
