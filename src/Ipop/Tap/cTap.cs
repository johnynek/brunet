using Brunet;
using Brunet.Util;
using System.Runtime.InteropServices;

namespace Ipop.Tap {
  public class cTap : TapDevice {
    /// <summary>Opens a TAP device on the host</summary>
    /// <param name="device">Name of the TAP device on the host</param>
    /// <returns>File descriptor of the TAP device</returns>
    [DllImport("libtuntap")]
    private static extern int open_tap(string device, byte[] addr);

    /// <summary>Closes the open TAP device on the host</summary>
    /// <param name="device">Name of the TAP device on the host</param>
    [DllImport("libtuntap")]
    private static extern int close_tap(int fd);

    /// <summary>Reads from the TAP devices</summary>
    /// <param name="fd">File descriptor gotten from open_tap</param>
    /// <param name="packet">a buffer to read into</param>
    /// <param name="length">maximum amount of bytes to read</param>
    /// <returns>amount of bytes actually read</returns>
    [DllImport("libtuntap")]
    private static extern int read_tap(int fd, byte[] packet, int length);

    /// <summary>Writes to the TAP devices</summary>
    /// <param name="fd">File descriptor gotten from open_tap</param>
    /// <param name="packet">the buffer to write to the device</param>
    /// <param name="length">maximum amount of bytes to write</param>
    /// <returns>amount of bytes actually written</returns>
    [DllImport("libtuntap")]
    private static extern int send_tap(int fd, byte[] packet, int length);

    protected int _fd;
    protected MemBlock _addr;
    public override MemBlock Address { get { return _addr; } }

    public cTap(string device_name)
    {
      byte[] addr = new byte[6];
      _fd = open_tap(device_name, addr);
      _addr = MemBlock.Reference(addr);
    }

    public override int Write(byte[] packet, int length)
    {
      return send_tap(_fd, packet, length);
    }

    public override int Read(byte[] packet)
    {
      return read_tap(_fd, packet, packet.Length);
    }

    public override void Close()
    {
      close_tap(_fd);
    }
  }
}
