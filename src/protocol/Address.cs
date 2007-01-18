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
#if BRUNET_NUNIT
using NUnit.Framework;
#endif
namespace Brunet
{

  /**
   * Represents addresses on the Brunet system.  The Brunet
   * system is a virtual network onto of lower layers (such
   * as IP).  The Address represents addresses on 
   * the virtual system.
   *
   * All Address subclasses are immutable.  Once created,
   * they cannot be changed
   */

  public abstract class Address : System.IComparable
  {

    ///The number of bytes to represent the address
    public static readonly int MemSize = 20;

    protected MemBlock  _buffer;

    /**
     * Static constructor initializes _half and _full
     */
    static Address()
    {
      //Initialize _half
      byte[] tmp = new byte[MemSize];
      for (int i = 1; i < MemSize; i++)
      {
        tmp[i] = 0;
      }
      //Set the first bit to 1, all else to zero :
      tmp[0] = 0x80;
      _half = new BigInteger(tmp);
      _full = _half * 2;
    }

    public Address()
    {
      byte[] buffer = new byte[MemSize];
      SetClass(buffer, this.Class);
      _buffer = MemBlock.Reference(buffer, 0, MemSize);
    }

    /**
     * Create an address from a MemBlock
     */
    public Address(MemBlock mb)
    {
      _buffer = mb;
      if (ClassOf(_buffer) != this.Class) {
        throw new System.
        ArgumentException("Class of address is not my class:  ",
                          this.ToString());
      }
    }

    public Address(BigInteger big_int)
    {
      byte[] buffer = ConvertToAddressBuffer(big_int);
      _buffer = MemBlock.Reference(buffer, 0, MemSize);
      if (ClassOf(_buffer) != this.Class) {
        throw new System.
        ArgumentException("Class of address is not my class:  ",
                          this.ToString());
      }
    }

    abstract public int Class {
      get;
    }


    /**
     * Gives the class of the Address at the given MemBlock
     */
    public static int ClassOf(MemBlock mb)
    {
      int i = MemSize - 1;
      byte[] mask = { 0x01, 0x02, 0x04, 0x08,
                      0x10, 0x20, 0x40, 0x80 };
      byte this_byte = mb[i];
      
      //For the do while, we start with one less:
      int consecutive_ones = -1;
      int idx = -1;
      do {
        consecutive_ones++;
        idx++;
        if( idx > 7 ) {
          //Move to the next byte:
          idx = 0;
          i--;
          this_byte = mb[i];
        }
      }
      while( (this_byte & mask[idx]) != 0 );
      return consecutive_ones;
    }

    /**
     * Compares them by treating them as MSB first integers
     */
    public int CompareTo(object obj) {
      if( obj == this ) { return 0; }
      Address a = obj as Address;
      if( null != a ) {
        return this._buffer.CompareTo( a._buffer );
      }
      else {
        //These are really incomparable.
	throw new System.ArgumentException("Cannot compare " + ToString() + " to "
	                    + obj.ToString());
      }
    }

    public override bool Equals(object a)
    {
      if( a == this ) { return true; }
      Address addr = a as Address;
      if (addr != null) {
        return this._buffer.Equals( addr._buffer );
      }
      return false;
    }
    protected bool _computed_hash = false;
    protected int _hc;
    //The first int in the buffer should be good enough
    public override int GetHashCode() {
      if( !_computed_hash ) {
        //There is no race here because calling the function
        //more than once has the same result.
        _hc = _buffer.GetHashCode(); 
        _computed_hash = true;
      }
      return _hc;
    }
    protected static BigInteger _half;
    /**
     * Half is 2^159
     */
    public static BigInteger Half
    {
      get
      {
        return _half;
      }
    }
    protected static BigInteger _full;
    /**
     * Full is 2 * Half = 2^160
     */
    public static BigInteger Full
    {
      get
      {
        return _full;
      }
    }

    /**
     * If this address is unicast (can only be passed to one Node)
     * this is true.  If it may be passed to more than one Node
     * it is false.
     */
    public abstract bool IsUnicast
    {
      get;
    }

    /**
     * Copy the buffer out
     */
    public virtual void CopyTo(byte[] b, int offset)
    {
      _buffer.CopyTo(b,offset);
    }

    public virtual void CopyTo(byte[] b)
    {
      CopyTo(b, 0);
    }

    private bool _made_big_int = false;
    private BigInteger _big_int;
    public virtual BigInteger ToBigInteger()
    {
      if( !_made_big_int ) {
        byte[] buffer = new byte[ MemSize ];
        _buffer.CopyTo(buffer,0);
        _big_int = new BigInteger(buffer);
      }
      return _big_int;
    }

    /**
     * Return a byte[] of length MemSize, which holds the integer as a
     * buffer which is a binary representation of an Address
     */
    static public byte[] ConvertToAddressBuffer(BigInteger value)
    {

      byte[] bi_buf;
      if( value < 0 ) {
        //if we are less than 0, get 2^160 + value:
        BigInteger value_plus = new BigInteger(value + Full);
        bi_buf = value_plus.getBytes();
      }
      else {
        bi_buf = value.getBytes();
      }
      if (bi_buf.Length < MemSize) {
        //Missing some bytes at the beginning, pad with zero :
        byte[] tmp_bi = new byte[Address.MemSize];
        int missing = (MemSize - bi_buf.Length);
        for (int i = 0; i < missing; i++) {
          tmp_bi[i] = (byte) 0;
        }
        /**
         * @todo throw an ArgumentNullException if sourceArray or destinationArray
         * is null.Throw RankException if sourceArray and destinationArray have 
         * different ranks. Throw ArgumentOutOfRangeException.
         */
        System.Array.Copy(bi_buf, 0, tmp_bi, missing,
                          bi_buf.Length);
        bi_buf = tmp_bi;
      }
      else if (bi_buf.Length > MemSize) {
        throw new System.ArgumentException(
          "Integer too large to fit in 160 bits: " + value.ToString());
      }
      return bi_buf;
    }

    /**
     * Sets the last bits of a byte array
     * to guarantee that it is of a given class
     * @param buf bytes we want to set to myclass
     * @param offset the offset to the bytes we want to set
     * @param myclass the class we want to set to
     */
    static public void SetClass(byte[] buf, int offset, int myclass)
    {
      /*
       * If you want x ones, you want to xor with one_masks[x];
       */
      byte[] one_masks = { 0x00, 0x01, 0x03, 0x07, 0x0F,
                                 0x1F, 0x3F, 0x7F, 0xFF };
      /*
       * if you want a zero in the x position and with zero_mask[x]
       */
      byte[] zero_masks = { 0xFE, 0xFD, 0xFB, 0xF7,
                            0xEF, 0xDF, 0xBF, 0x7F, 0xFF };
      int i = offset + MemSize - 1;
      //Put the ones in:
      int ones_to_go = myclass;
      while( ones_to_go >= 0 ) {
        //We can put any number of ones from 0 to 8:
        int this_one_count = System.Math.Min(ones_to_go, 8);
        ones_to_go -= this_one_count;
        //Put the ones in:
        byte o_mask = one_masks[ this_one_count ];
        //Put the zero in the this_one_count position:
        byte z_mask = zero_masks[ this_one_count  ];
        buf[i] = (byte)( (buf[i] | o_mask) & z_mask );
        if( this_one_count < 8 ) {
          //We just did the last one:
          ones_to_go = -1;
        }
        //Move on to the next:
        i--;
      }
    }
    
    /**
     * Same as SetClass(buf, 0, myclass)
     */
    static public void SetClass(byte[] buf, int myclass)
    {
      SetClass(buf, 0, myclass);
    }
    
    public override string ToString()
    {
      byte[] buffer = new byte[ MemSize ];
      _buffer.CopyTo(buffer,0);
      return ("brunet:" + "node:" + Base32.Encode(buffer));
    }
        #if BRUNET_NUNIT
    [TestFixture]
    public class AddressTester {
      [Test]
      public void Test() {
        System.Random r = new System.Random();
        for(int i = 0; i < 100; i++) {
          //Test ClassOf and SetClass:
          int c = r.Next(160);
          byte[] buf0 = new byte[Address.MemSize];
          //Fill it with junk
          r.NextBytes(buf0);
          Address.SetClass(buf0, c);
          int c2 = Address.ClassOf(MemBlock.Reference(buf0, 0, Address.MemSize));
          Assert.AreEqual(c,c2, "Class Round Trip");
          //Test BigInteger stuff:
          int size = r.Next(1, MemSize + 1);
          byte[] buf1 = new byte[size];
          r.NextBytes(buf1);
          BigInteger b1 = new BigInteger(buf1);
          byte[] buf2 = Address.ConvertToAddressBuffer(b1);
          BigInteger b2 = new BigInteger(buf2);
          Assert.AreEqual(b1, b2, "BigInteger round trip");
        }
      }
    }
    #endif
  }

}



