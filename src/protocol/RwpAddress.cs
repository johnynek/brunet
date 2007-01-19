/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

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
    protected float _prob;

    public RwpAddress(MemBlock mb) : base(mb)
    {
      if (ClassOf(mb) != _class) {
        throw new System.
        ArgumentException("This is not a Class 126 address :  ",
                          this.ToString());
      }
      _prob = -1.0f;
    }
    public RwpAddress(bool flag, float p) : base() {
      byte[] buffer = new byte[ MemSize ];
      NumberSerializer.WriteFloat(p, buffer, 0);
      NumberSerializer.WriteFlag(flag, buffer, 4);
      _prob = p;
      SetClass(buffer, this.Class);
      _buffer = MemBlock.Reference(buffer, 0, MemSize);
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
        return ((_buffer[4] & 0x80) == 0x80);
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
        return (_prob == 0.0);
      }
    }

    public float Prob
    {
      get
      {
        if( _prob < 0.0 ) {
        //The probability is stored in the first 4 bytes of the buffer
          _prob = NumberSerializer.ReadFloat( _buffer.Slice(0,4) );
        }
        return _prob;
      }
    }

    #if BRUNET_NUNIT
    [TestFixture]
    public class RwpAddressTester {
      [Test]
      public void Test() {
        byte[] buf = new byte[Address.MemSize];
        System.Random r = new System.Random();
        for(int i = 0; i < 100; i++) {
          float val = (float)r.NextDouble();
          bool flag = (r.Next() & 0x80) == 0x80;
          RwpAddress a1 = new RwpAddress(flag, val);
          
          NumberSerializer.WriteFloat(val, buf, 0);
          NumberSerializer.WriteFlag(flag, buf, 4);
          //Make it a class 126 address:
          SetClass(buf, 126);
          RwpAddress a2 = new RwpAddress(MemBlock.Reference(buf,0,Address.MemSize));
          
          Assert.AreEqual(a1, a2, "RwpAddress Equality");
          Assert.AreEqual(val, a1.Prob, "Probability round trip");
          Assert.AreEqual(flag, a1.Flag, "Flag round trip");
        }
      }
    }
    #endif
  }

}
