/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida
                    Pierre St. Juste <ptony82@ufl.edu>, University of Florida

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

using System;
using System.IO;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Threading;
using System.Runtime.InteropServices;
using Brunet;
using Brunet.Util;

namespace Ipop.Tap {
  /// <summary>A C# Tap driver interface.  We use CreateFile to create a
  /// SafeFileHandle, which than be used to instantiate a FileStream.  The
  /// trick to the FileStream is that we need to flush the device after each
  /// Read and Write and that it is instantiated knowing that is an
  /// asnychronous device, even though we read and write to it synchronously.
  /// Asynch in the case, purely means that read and write operations occur
  /// simulataneously (overlapping).</summary>
  public class WindowsTap : TapDevice, IDisposable {
    // Marshaling data
    private const string NETWORKPATH = "SYSTEM\\CurrentControlSet\\Control" +
      "\\Network\\{4D36E972-E325-11CE-BFC1-08002BE10318}";
    private const string GUIDPATH = "SYSTEM\\CurrentControlSet\\Services" +
      "\\Tcpip\\Parameters\\Adapters";

    private const string USERMODEDEVICEDIR = "\\\\.\\Global\\";
    private const string TAPSUFFIX = ".tap";

    private const uint FILE_DEVICE_UNKNOWN = 0x00000022;
    private const uint METHOD_BUFFERED = 0;
    private const uint FILE_ANY_ACCESS = 0;
    
    private static uint TAP_IOCTL_SET_MEDIA_STATUS = CTL_CODE(
                                                     FILE_DEVICE_UNKNOWN, 
                                                     6,
                                                     METHOD_BUFFERED, 
                                                     FILE_ANY_ACCESS);
                                                     
    private static uint TAP_IOCTL_GET_MAC = CTL_CODE(FILE_DEVICE_UNKNOWN, 
                                             1,
                                             METHOD_BUFFERED, 
                                             FILE_ANY_ACCESS);

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
    private const uint FILE_FLAG_OVERLAPPED = 0x40000000;
                                             
    static uint CTL_CODE(uint DeviceType, uint Function, uint Method, 
      uint Access) {
      return ((DeviceType) << 16) | ((Access) << 14) | ((Function) << 2) | 
        (Method);
    }
    
    [DllImport("kernel32.dll", CharSet=CharSet.Auto, SetLastError=true)]
    private static extern SafeFileHandle CreateFile(
      string lpFileName,
      uint dwDesiredAccess,
      uint dwShareMode,
      IntPtr securityAttributes,
      uint dwCreationDisposition,
      uint dwFlagsAndAttributes,
      IntPtr hTemplateFile);
      
    [DllImport("kernel32.dll", CharSet=CharSet.Auto, SetLastError=true)]
    private static extern bool DeviceIoControl(
      SafeFileHandle hDevice,
      uint dwIoControlCode,
      ref int InBuffer,
      int nInBufferSize, 
      byte[] OutBuffer,
      int nOutBufferSize,
      ref int out_count,
      IntPtr lpOverlapped);
      
    [DllImport("kernel32.dll", CharSet=CharSet.Auto, SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(SafeFileHandle hObject);
    // End of Marshaling data

    private volatile bool _disposed = false;
    protected SafeFileHandle _handle;
    protected readonly MemBlock _addr;
    protected FileStream _fs;
    
    public override MemBlock Address { get { return _addr; } }

    public WindowsTap(string device) {
      string guid = GetGuid(device);
      string device_path = USERMODEDEVICEDIR + guid + TAPSUFFIX;

      _handle = CreateFile(device_path, GENERIC_READ | GENERIC_WRITE, 0, 
                        IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_SYSTEM | 
                        FILE_FLAG_OVERLAPPED, IntPtr.Zero);

      if(_handle == null) {
        throw new Exception("Unable to access Tap");
      }

      int out_length = 0;
      bool success = false;

      int status = 1;
      byte[] addr = new byte[6];

      success = DeviceIoControl(_handle, TAP_IOCTL_SET_MEDIA_STATUS, 
                                ref status, Marshal.SizeOf(status),
                                addr, addr.Length,
                                ref out_length, IntPtr.Zero);

      if (!success) {
        CloseHandle(_handle);
        throw new Exception("Device open failed");
      }

      success = DeviceIoControl(_handle, TAP_IOCTL_GET_MAC, 
                                 ref status, 0,
                                 addr, addr.Length,
                                 ref out_length, IntPtr.Zero);

      if (!success) {
        CloseHandle(_handle);
        throw new Exception("Device open failed");
      }

      _addr = MemBlock.Reference(addr);
      _fs = new FileStream(_handle, FileAccess.ReadWrite, 32768, true);
    }

    ///<summary>Converts a device name into its GUID (i.e., a global system
    ///reference value).</summary>
    private string GetGuid(string device) {
      RegistryKey rk1 = Registry.LocalMachine.OpenSubKey(NETWORKPATH);
      RegistryKey rk2 = Registry.LocalMachine.OpenSubKey(GUIDPATH);
      string[] guids = rk2.GetSubKeyNames();
      
      foreach(string guid in guids) {
        try {
          RegistryKey rkt = rk1.OpenSubKey(guid + "\\Connection");
          if((string)rkt.GetValue("Name") == device) {
            return guid;
          }
        }
        catch(Exception e){}
      }
      return null;
    }

    public override int Read(byte[] data) {
      int read = _fs.Read(data, 0, data.Length);
      _fs.Flush();
      return read;
    }
    
    public override int Write(byte[] data, int length) {
      _fs.Write(data, 0, length);
      _fs.Flush();
      return length;
    }
    
    public override void Close() {
      Dispose();
    }

    public void Dispose() {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    public void Dispose(bool disposing) {
      if(_disposed) {
        return;
      }
      _fs.Close();
//    Windows documentation says that we should close this, but it appears that
//    it is being closed by the above call during the FileStream close
//      CloseHandle(_handle);
      _disposed = true;
    }
  }
}
