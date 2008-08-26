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

namespace Brunet
{

  /**
   * Everything that can go wrong with an edge is represented
   * by this exception
   */

  public class EdgeException : SendException
  {
    /**
     * By default, this is considered a transient exception
     */
    public EdgeException():base(true) {
    }

    public EdgeException(string message):base(true, message) {
    }

    public EdgeException(bool istransient, string message) : base(istransient, message) {
    }
  }

  /**
   * This type is here to make the code easier to read and less error prone
   * Once an Edge IsClosed, then it will never be unclosed, so this type
   * of exception is never transient
   */
  public class EdgeClosedException : EdgeException {
    public EdgeClosedException(string message) : base(false, message) { }
  }

}
