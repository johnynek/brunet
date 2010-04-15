/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
