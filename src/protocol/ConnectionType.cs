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

/**
 * Simple Enum to signify which edges are what types
 */

namespace Brunet
{

  /**
   * These are the Major connection types.  Connections can have subtypes,
   * which are denoted with dots followed by these names:
   * e.g. "structured.near" "structed.shortcut" etc...
   */
  public enum ConnectionType
  {
    Leaf,                       //Connections which are point-to-point edge.
    Structured,                 //Connections for routing structured addresses
    Unstructured,               //Connections for routing unstructured addresses
    Unknown                     //Refers to all connections which are not in the above
  }

}
