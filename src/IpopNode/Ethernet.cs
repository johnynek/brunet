/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using NetworkPackets;
using System;
using System.Runtime.InteropServices;
using System.Net;
using System.Threading;
using Brunet;

namespace Ipop {
  /**
   <summary>Provides an interface to the Virtual Network device known as a
  TAP device.  Ethernet is an ISender, meaning to write to the device, one must
  call the Send method.  It is also an ISource, meaning to receive packets one
  must subscribe via the Subscribe method to the output.</summary>
  */
  public class Ethernet: SimpleSource, ISender {
    /**  <summary>This is the maximum size for any packet according to 
    wikipedia</summary>*/
    public const int MTU = 1522;
    /**  <summary>This is a constant buffer used for writing to the tap 
    device </summary>*/
    protected byte[] _send_buffer = new byte[MTU];
    /// <summary>Name of the TAP device as listed on the host.</summary>
    protected string device;
    /// <summary>File descriptor for the TAP device</summary>
    protected int fd;
    /// <summary>Set to false when we're exiting</summary>
    protected bool _running;
    /// <summary>The thread that provides the ISource for Ethernet</summary>
    protected Thread _read_thread;
    /// <summary>The address of our TapDevice</summary>
    public readonly MemBlock Address;

    /**
    <summary>Opens a TAP device on the host</summary>
    <param name="device">Name of the TAP device on the host</param>
    <returns>File descriptor of the TAP device</returns>
    */
    [DllImport("libtuntap")]
    private static extern int open_tap(string device, byte[] addr);

    /**
    <summary>Closes the open TAP device on the host</summary>
    <param name="device">Name of the TAP device on the host</param>
    */
    [DllImport("libtuntap")]
    private static extern int close_tap(int fd);

    /**
    <summary>Reads from the TAP devices</summary>
    <param name="fd">File descriptor gotten from open_tap</param>
    <param name="packet">a buffer to read into</param>
    <param name="length">maximum amount of bytes to read</param>
    <returns>amount of bytes actually read</returns>
    */
    [DllImport("libtuntap")]
    private static extern int read_tap(int fd, byte[] packet, int length);

    /**
    <summary>Writes to the TAP devices</summary>
    <param name="fd">File descriptor gotten from open_tap</param>
    <param name="packet">the buffer to write to the device</param>
    <param name="length">maximum amount of bytes to write</param>
    <returns>amount of bytes actually written</returns>
    */
    [DllImport("libtuntap")]
    private static extern int send_tap(int fd, byte[] packet, int length);

    /**
    <summary>Initializes the Ethernet device by opening the TAP device and 
    starting the ISource thread.</summary>
    <param name="tap">The name of the TAP device on the host.</param>
    */
    public Ethernet(string tap) {
      device = tap;
      int count = 0;
      byte[] addr = new byte[6];
      while((fd = open_tap(device, addr)) == -2) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions,
                            "Unable to set up the tap, trying again...");
        Thread.Sleep(2);
        if(count++ >= 15) {
          break;
        }
      }
      if(fd < 0) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions,
                            "Unable to set up the tap");
        Environment.Exit(1);
      }
      Address = MemBlock.Reference(addr);

      _running = true;
      _read_thread = new Thread(ReadLoop);
      _read_thread.IsBackground = true;
      _read_thread.Start();
    }

    /**
    <summary>The thread acting as the ISource for Ethernet, this is where
    reading of the TAP is performed.  Use Subscribe to receive the packets
    coming from here.</summary>
    <remarks>The same max MTU byte array is always read into.  This is then
    copied to a minimum sized MemBlock and send to the subscriber. </remarks>
    */
    protected void ReadLoop() {
      try {
        byte[] read_buffer = new byte[MTU];
        BufferAllocator ba = new BufferAllocator(MTU, 1.1);
        while(_running) {
          int length  = read_tap(fd, read_buffer, read_buffer.Length);
          if (length == 0 || length == -1) {
            ProtocolLog.WriteIf(ProtocolLog.Exceptions, "Couldn't read TAP");
            continue;
          }

          Array.Copy(read_buffer, 0, ba.Buffer, ba.Offset, length);
          MemBlock packet = MemBlock.Reference(ba.Buffer, ba.Offset, length);
          ba.AdvanceBuffer(length);

          Subscriber s = _sub;
          if(s != null) {
            s.Handle(packet, this);
          }
        }
      }
      catch(ThreadInterruptedException x) {
        if(_running && ProtocolLog.Exceptions.Enabled) {
          ProtocolLog.Write(ProtocolLog.Exceptions, x.ToString());
        }
      }
      catch(Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, e.ToString());
      }
    }

    /**
    <summary>Writes the data to the Ethernet device.</summary>
    <remarks>The same max MTU byte array is always used to write the data to
    the device.  Since the incoming data is an ICopyable, it needs to be copied
    to a single byte array before being sent to the unmanaged language.
    </remarks>
    <param name="data">ICopyable of the data to be written.</param>
     */
    public void Send(ICopyable data) {
      int length = data.CopyTo(_send_buffer, 0);
      int n = send_tap(fd, _send_buffer, length);
      if(n != length) {
        if(_running) {
          ProtocolLog.WriteIf(ProtocolLog.Exceptions, String.Format(
            "TAP: Didn't write all data ... only {0} / {1}", n, length));
        }
      }
    }

    /**
    <summary>Call this method when exiting to stop the _read_thread.</summary>
    */
    public void Stop() {
      _running = false;
      close_tap(fd);
      _read_thread.Interrupt();
    }

    public String ToUri() {
      throw new NotImplementedException();
    }
  }
}
