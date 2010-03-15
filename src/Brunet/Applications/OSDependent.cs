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
using System.Collections;

namespace Brunet.Applications {
  /**
  <summary>Determines the current operating system, between Linux and
  Windows.  This is a static class unified class rather than have other
  parts of the code look at Environment.OSVersion.Platform and figure 
  information from there.</summary>
  */
  public class OSDependent {
    /// <summary>Operating System enumeration</summary>
    public enum OS {
      Linux = 0,
      Windows = 1
    }
    /// <summary>Contains the current operating system.</summary>
    public static readonly OS OSVersion;

    /// <summary>Static constructor to setup OSVersion</summary>
    static OSDependent() {
      int p = (int) Environment.OSVersion.Platform;
      if ((p == 4) || (p == 128)) {
        OSVersion = OS.Linux;
      }
      else {
        OSVersion = OS.Windows;
      }
    }
  }
}