/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida
                    Yonggang Liu <myidpt@gmail.com>, University of Florida
                    Juho Juho Vähä-Herttua <juhovh@gmail.com>

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using Brunet;
using Mono.Unix.Native;
using System;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Ipop.Tap {
  public class LinuxTap : TapDevice, IDisposable {
    // Marshaling info:
    private const int AF_INET  = 02; // added
    private const int SOCK_DGRAM = 02; // added
    private const int SIOCGIFHWADDR = 0x8927; // added, from bits/ioctls.h

    private const int IFF_TAP   = 0x0002;
    private const int IFF_NO_PI = 0x1000;
    
    [StructLayout(LayoutKind.Sequential)]
    private struct sockaddr { // bits/socket.h 
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst=14)]
      public string sa_data;
      // padding to 16 bytes
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst=2)]
      public string data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ifreq1 { // net/if.h
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst=16)]
      public string ifrn_name;
      public short ifru_flags; // 2 bytes
      // padding to 32 bytes
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst=14)]
      public string data;
    }
    private struct ifreq2 { // net/if.h
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst=16)]
      public string ifrn_name;
      public sockaddr ifr_hwaddr; // 16 bytes
    }

    [DllImport("libc", CharSet=CharSet.Ansi)]
    private extern static int ioctl(int fd, int request, ref ifreq1 arg);

    [DllImport("libc", CharSet=CharSet.Ansi)]
    private extern static int ioctl(int fd, int request, ref ifreq2 arg);
    
    [DllImport("libc", CharSet=CharSet.Ansi)]
    private extern static int socket(int domain, int type, int protocol);
    // End Marshaling info

    private volatile bool _disposed = false;
    private MemBlock _addr;
    public override MemBlock Address { get { return _addr; } }
    private int _fd;

    public LinuxTap(string dev)
    {
      int TUNSETIFF = GetIOCtlWrite('T', 202, 4); /* size of int 4, from if_tun.h */

      ifreq1 ifr1 = new ifreq1();
      int fd;

      if ((fd = Syscall.open("/dev/net/tun", OpenFlags.O_RDWR)) < 0) {
        throw new Exception("Failed to open /dev/net/tun");
      }

      ifr1.ifru_flags = IFF_TAP | IFF_NO_PI;
      ifr1.ifrn_name = dev;
      if (ioctl(fd, TUNSETIFF, ref ifr1) < 0) {
        Syscall.close(fd);
        throw new Exception("TUNSETIFF failed");
      }
      _fd = fd;

      int ctrl_fd = socket(AF_INET, SOCK_DGRAM, 0);
      if(ctrl_fd == -1) {
        Syscall.close(fd);
        throw new Exception("Unable to open CTL_FD");
      }

      ifreq2 ifr2 = new ifreq2();
      ifr2.ifrn_name = dev; // ioctl changed the name?
      if(ioctl(ctrl_fd, SIOCGIFHWADDR, ref ifr2) <0) {
        Console.WriteLine("Failed to get hw addr.");
        Syscall.close(ctrl_fd);
        throw new Exception("Failed to get hw addr.");
      }
      _addr =  MemBlock.Reference(ASCIIEncoding.UTF8.GetBytes(ifr2.ifr_hwaddr.sa_data));
    }

    unsafe public override int Write(byte[] packet, int length)
    {
      fixed(byte *p = packet) {
        return (int) Syscall.write(_fd, (IntPtr) p, (ulong) length);
        // See Mono source; they are doing the same thing (mcs/class/Mono.Posix/Mono.Unix.Native/Syscall.cs).
      }
    }

    unsafe public override int Read(byte[] packet)
    {
      fixed(byte *p = packet) {
        return (int) Syscall.read(_fd, (IntPtr) p, (ulong) packet.Length);
      }
    }

    public override void Close() {
      Dispose();
    }

    /* This function from asm-generic/ioctl.h macros */
    private int GetIOCtlWrite(char type, int nr, int size) {
      int ret = 0;

      ret |= 1 << (8+8+14);     // _IOC_WRITE << _IOC_DIRSHIFT
      ret |= size << (8+8);     // size << _IOC_SIZESHIFT
      ret |= ((int) type) << 8; // type << _IOC_TYPESHIFT
      ret |= nr;                // nr   << _IOC_NRSHIFT

      return ret;
    }

    public void Dispose() {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing) {
      if (_disposed) {
        return;
      }

      Syscall.close(_fd);
      _disposed = true;
    } 
  }
}
