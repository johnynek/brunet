/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>  University of Florida

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

using Brunet.Util;

namespace Brunet.Symphony
{

  /**
   * This address represents directional routing
   * in the structured system, which is class 124
   */

  public class DirectionalAddress:StructuredAddress
  {

    public DirectionalAddress(MemBlock mb)
    {
      if (ClassOf(mb) != this.Class) {
        throw new System.
        ArgumentException
        ("This is not an AHAddress (Class 124) :  ",
         this.ToString());
      }
      _buffer = mb;
      _dir = (Direction) NumberSerializer.ReadInt(mb, 0);
    }

    public DirectionalAddress(DirectionalAddress.Direction bearing) : base()
    {
      byte[] buffer = new byte[ MemSize ];
      NumberSerializer.WriteInt((int)bearing, buffer, 0);
      SetClass(buffer, this.Class);
      _dir = bearing;
      _buffer = MemBlock.Reference(buffer, 0, MemSize);
    }

#if BRUNET_SIMULATOR
    public const int ClassValue = 1;
#else
    public const int ClassValue = 124;
#endif

    /**
     * This is class 124
     */
    public override int Class
    {
      get
      {
        return ClassValue;
      }
    }

    /**
     * This class is always unicast
     */
    public override bool IsUnicast
    {
      get
      {
        return true;
      }
    }

    /**
     * These are defined in the spec
     */
  public enum Direction:int
    {
      ///Left is "clockwise", or increasing in numerical address
      Left = 1,
      ///Right is "counterclockwise", or decreasing
      Right = 2
    }

    protected Direction _dir;
    /**
     * Use the synonym "Bearing" for the Direction property
     */
    public Direction Bearing
    {
      get
      {
        return _dir;
      }
    }
  }

}
