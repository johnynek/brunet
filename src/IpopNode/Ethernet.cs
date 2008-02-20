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

using System;
using System.Runtime.InteropServices;
using System.Net;
using System.Threading;
using Brunet;

namespace Ipop {
  /**
   * A class to interact with the underlying TAP device.
   */
  public class Ethernet: ISender, ISource {
    public static readonly int MTU = 1500;
    protected byte[] _send_buffer = new byte[MTU];
    protected byte[] _read_buffer = new byte[MTU];
    protected string device;
    //file descriptor for the device
    protected int fd;
    protected bool _running;
    protected Thread _read_thread;

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
      _running = true;
      _read_thread = new Thread(ReadLoop);
      _read_thread.Start();
    }

    protected void ReadLoop() {
      try {
        while(_running) {
          int length = read_tap(fd, _read_buffer, _read_buffer.Length);

          if (length == 0 || length == -1) {
            ProtocolLog.WriteIf(ProtocolLog.Exceptions, "Couldn't read TAP");
            continue;
          }

          MemBlock packet = MemBlock.Copy(_read_buffer, 0, length);
          Sub s = _sub;
          if(s != null) {
            s.Handle(packet, this);
          }
        }
      }
      catch(Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, e.ToString());
      }
    }

    public void Send(ICopyable data) {
      int length = data.CopyTo(_send_buffer, 0);
      int n = send_tap(fd, _send_buffer, length);
      if(n != length) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, String.Format(
          "TAP: Didn't write all data ... only {0} / {1}", n, length));
      }
    }

    public void Stop() {
      _running = false;
    }

    // Usual code for a one way ISource
    protected class Sub {
      public readonly IDataHandler Handler;
      public readonly object State;
      public Sub(IDataHandler h, object s) { Handler = h; State =s; }
      public void Handle(MemBlock b, ISender f) { Handler.HandleData(b, f, State); }
    }
    protected readonly object _sync = new object();
    protected volatile Sub _sub;

    public virtual void Subscribe(IDataHandler hand, object state) {
      lock( _sync ) {
        _sub = new Sub(hand, state);
      }
    }
    public virtual void Unsubscribe(IDataHandler hand) {
      lock( _sync ) {
        if( _sub.Handler == hand ) {
          _sub = null;
        }
        else {
          throw new Exception(String.Format("Handler: {0}, not subscribed", hand));
        }
      }
    }
    // End ISource code
  }
}
