/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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

namespace Brunet {

  /**
   * Lots of things can go wrong in the Linker.  When something
   * goes wrong, the Linker throws a LinkException.
   *
   * All LinkExceptions should be caught inside Linker.
   */
  public class LinkException : Exception {
    public LinkException():base()
    {
    }
    public LinkException(string message):base(message)
    {
    }
    public LinkException(string mes, Exception inner):base(mes, inner)
    {
    }

  }
}
