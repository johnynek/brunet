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

namespace Ipop {
  public class OSDependent {
    public static int Linux {get { return 0; } }
    public static int Windows {get { return 1; } }
    private static int _osver;
    public static int OSVersion {
      get {
        return _osver;
      }
    }

    static OSDependent() {
      int p = (int) Environment.OSVersion.Platform;
      if ((p == 4) || (p == 128)) {
        _osver = Linux;
      }
      else {
        _osver = Windows;
      }
    }
  }
}