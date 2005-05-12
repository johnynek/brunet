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

namespace Brunet
{

  /**
   * Represents a problem parsing some input or network
   * data in the Brunet system
   */
  using System;

  public class ParseException:Exception
  {
    public ParseException():base()
    {
    }
    public ParseException(string message):base(message)
    {
    }
    public ParseException(string message, Exception inner)
        : base(message, inner)
    {
    }
  }

}
