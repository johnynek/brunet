/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using Brunet.Applications;
using Brunet.Util;
using System;

namespace Ipop.Tap {
  public abstract class TapDevice {
    public static TapDevice GetTapDevice(string device_name)
    {
      TapDevice tap = null;
      if(OSDependent.OSVersion == OSDependent.OS.Linux) {
        tap = new LinuxTap(device_name);
      } else if(OSDependent.OSVersion == OSDependent.OS.Windows) {
        tap = new WindowsTap(device_name);
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
