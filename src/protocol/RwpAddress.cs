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

/*
 * using Brunet.UnstructuredAddress;
 * using Brunet.NumberSerializer;
 */

namespace Brunet
{

  /**
   * This class is a subclass of UnstructuredAddress
   * 
   */

  public class RwpAddress:Brunet.UnstructuredAddress
  {
    ///The class of this address:
    public static readonly int _class = 126;

    public RwpAddress(byte[] add):base(add)
    {
      if (ClassOf(add) != _class) {
        throw new System.
        ArgumentException("This is not a Class 126 address :  ",
                          this.ToString());
      }
    }
    public RwpAddress(byte[] add, int offset):base(add, offset)
    {
      if (ClassOf(add,offset) != _class) {
        throw new System.
        ArgumentException("This is not a Class 126 address :  ",
                          this.ToString());
      }
    }
    public RwpAddress(bool flag, float p) : base() {
      NumberSerializer.WriteFlag(flag, buffer, 4);
      NumberSerializer.WriteFloat(p, buffer, 0);
    }

    public override int Class
    {
      get
      {
        return _class;
      }
    }
    public bool Flag
    {
      get
      {
        return NumberSerializer.ReadFlag(buffer, 4);
      }
    }

    /**
     * This address is only unicast in the case that the percolation
     * probability is exactly zero.
     */
    public override bool IsUnicast
    {
      get
      {
        return (this.Prob == 0.0);
      }
    }

    public float Prob
    {
      get
      {
        return Brunet.NumberSerializer.ReadFloat(buffer, 0);
        //The probability is stored in the first 4 bytes of the buffer
      }
    }
  }

}
