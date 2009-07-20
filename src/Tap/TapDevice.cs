using Brunet;
using Brunet.Applications;

namespace Ipop.Tap {
  public abstract class TapDevice {
    public static TapDevice GetTapDevice(string device_name)
    {
      TapDevice tap = null;
      if(OSDependent.OSVersion == OSDependent.OS.Linux) {
        tap = new LinuxTap(device_name);
      } else if(OSDependent.OSVersion == OSDependent.OS.Windows) {
        tap = new cTap(device_name);
      } else {
        tap = new cTap(device_name);
      }
      return tap;
    }

    public abstract MemBlock Address { get; }
    public abstract int Write(byte[] packet, int length);
    public abstract int Read(byte[] packet);
    public abstract void Close();
  }
}
