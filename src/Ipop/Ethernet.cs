/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using Brunet;
using Brunet.Messaging;
using Brunet.Util;
using Ipop.Tap;
using NetworkPackets;
using System;
using System.Runtime.InteropServices;
using System.Net;
using System.Threading;

namespace Ipop {
  /// <summary>Provides an interface to the Virtual Network device known as a
  /// TAP device.  Ethernet is an ISender, meaning to write to the device, one must
  /// call the Send method.  It is also an ISource, meaning to receive packets one
  /// must subscribe via the Subscribe method to the output.</summary>
  public class Ethernet: SimpleSource, ISender {
    /// <summary>This is the maximum size for any packet according to 
    /// wikipedia</summary>
    public const int MTU = 1522;
    /// <summary>This is a constant buffer used for writing to the tap
    /// device </summary>
    protected byte[] _send_buffer = new byte[MTU];
    /// <summary>Set to false when we're exiting</summary>
    protected bool _running;
    /// <summary>The thread that provides the ISource for Ethernet</summary>
    protected Thread _read_thread;
    /// <summary>The address of our TapDevice</summary>
    public readonly MemBlock Address;

    protected TapDevice _tap;
    
    /// <summary>Initializes the Ethernet device by opening the TAP device and 
    /// starting the ISource thread.</summary>
    /// <param name="tap">The name of the TAP device on the host.</param>
    public Ethernet(string device_name)
    {
      for(int i = 0; i < 15; i++) {
        try {
          _tap = Ipop.Tap.TapDevice.GetTapDevice(device_name);
          break;
        } catch {}
        Thread.Sleep(2);
        ProtocolLog.WriteIf(ProtocolLog.Exceptions,
            "Unable to set up the tap, trying again...");
      }

      if(_tap == null) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "Unable to set up the tap.");
        Environment.Exit(1);
      }

      Address = _tap.Address;

      _running = true;
      _read_thread = new Thread(ReadLoop);
      _read_thread.IsBackground = true;
      _read_thread.Start();
    }

    /// <summary>The thread acting as the ISource for Ethernet, this is where
    /// reading of the TAP is performed.  Use Subscribe to receive the packets
    /// coming from here.</summary>
    /// <remarks>The same max MTU byte array is always read into.  This is then
    /// copied to a minimum sized MemBlock and send to the subscriber. </remarks>
    protected void ReadLoop()
    {
      byte[] read_buffer = new byte[MTU];
      BufferAllocator ba = new BufferAllocator(MTU, 1.1);
      while(_running) {
        int length = -1;
        try {
          length  = _tap.Read(read_buffer);
        } catch(ThreadInterruptedException x) {
          if(_running && ProtocolLog.Exceptions.Enabled) {
            ProtocolLog.Write(ProtocolLog.Exceptions, x.ToString());
          }
        } catch(Exception e) {
          ProtocolLog.WriteIf(ProtocolLog.Exceptions, e.ToString());
        }

        if (length == 0 || length == -1) {
          ProtocolLog.WriteIf(IpopLog.TapLog, "Couldn't read TAP");
          continue;
        }

        Array.Copy(read_buffer, 0, ba.Buffer, ba.Offset, length);
        MemBlock packet = MemBlock.Reference(ba.Buffer, ba.Offset, length);
        ba.AdvanceBuffer(length);

        Subscriber s = _sub;
        if(s != null) {
          try {
            s.Handle(packet, this);
          } catch(Exception e) {
            ProtocolLog.WriteIf(ProtocolLog.Exceptions, e.ToString());
          }
        }
      }
    }

    /// <summary>Writes the data to the Ethernet device.</summary>
    /// <remarks>The same max MTU byte array is always used to write the data to
    /// the device.  Since the incoming data is an ICopyable, it needs to be copied
    /// to a single byte array before being sent to the unmanaged language.
    /// </remarks>
    /// <param name="data">ICopyable of the data to be written.</param>
    public void Send(ICopyable data)
    {
      int length = 0, n = 0;
      lock(_send_buffer) {
        length = data.CopyTo(_send_buffer, 0);
        n = _tap.Write(_send_buffer, length); 
      }

      if(n != length) {
        if(_running) {
          ProtocolLog.WriteIf(IpopLog.TapLog, String.Format(
            "TAP: Didn't write all data ... only {0} / {1}", n, length));
        }
      }
    }

    /// <summary>Call this method when exiting to stop the _read_thread.</summary>
    public void Stop()
    {
      _running = false;
      Thread.MemoryBarrier();
      _read_thread.Interrupt();
      _tap.Close();
    }

    public String ToUri()
    {
      throw new NotImplementedException();
    }
  }
}
