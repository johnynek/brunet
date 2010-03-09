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

using System;
using System.Security.Cryptography;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif
using Brunet.Util;

namespace Brunet.Symphony
{

  /**
   * Represents unicast addresses which have a one to
   * one relationship with Nodes in the Brunet system.
   * Each node has at most one AHAddress, and each
   * AHAddress has at most one Node associated with
   * it.
   */

  public class AHAddress : StructuredAddress
  {
    public const int ClassValue = 0;
    ///The class of this address type
    public static readonly int _class = 0;

    protected readonly uint _prefix;

    public override int Class
    {
      get
      {
        return ClassValue;
      }
    }

    /**
     * Return a random AHAddress initialized from the given rng
     */
    public AHAddress(RandomNumberGenerator rng)
    {
      byte[] buffer = new byte[MemSize];
      rng.GetBytes(buffer);
      SetClass(buffer, this.Class);
      _buffer = MemBlock.Reference(buffer, 0, MemSize);
      _prefix = (uint)NumberSerializer.ReadInt(_buffer, 0);
    }

    /**
     * @deprecated
     * This makes a copy of b and initializes the AHAddress with
     * that value
     */
    public AHAddress(byte[] b) : this(MemBlock.Copy(b, 0, MemSize)) {
      _prefix = (uint)NumberSerializer.ReadInt(_buffer, 0);
    }
    
    /**
     * @deprecated
     * This makes a copy of b and initializes the AHAddress with
     * that value
     */
    public AHAddress(byte[] b, int off) : this(MemBlock.Copy(b, off, MemSize)) {
      _prefix = (uint)NumberSerializer.ReadInt(_buffer, 0);
    }
    
    public AHAddress(MemBlock mb) : base(mb)
    {
      if (ClassOf(_buffer) != this.Class) {
        throw new System.
        ArgumentException("Class of address is not my class:  ",
                          this.ToString());
      }
      _prefix = (uint)NumberSerializer.ReadInt(_buffer, 0);
    }

    public AHAddress(BigInteger big_int):base(big_int)
    {
      if (ClassOf(_buffer) != this.Class) {
        throw new System.
        ArgumentException("Class of address is not my class:  ",
                          this.ToString());
      }
      _prefix = (uint)NumberSerializer.ReadInt(_buffer, 0);
    }

    public override int CompareTo(object o) {
      if( Object.ReferenceEquals(this, o) ) { 
        return 0;
      }

      AHAddress other = o as AHAddress;

      if(other == null) {
        return base.CompareTo(o);
      } else if( other._prefix != _prefix ) {
        return _prefix < other._prefix ? -1 : 1;
      } else {
        return _buffer.CompareTo(other._buffer);
      }
    }

    /**
     * Compute the distance from this to add such that
     * the magnitude is less than or equal to Address.Half
     */
    public virtual BigInteger DistanceTo(AHAddress a)
    {
      BigInteger n_x = this.ToBigInteger();
      BigInteger n_y = a.ToBigInteger();

      BigInteger dist = n_y - n_x;
      if (n_y > n_x) {
        //(n_y > n_x ) == (dist > 0),
        //but the former is faster for BigInteger
        if (dist >= Address.Half) {
          dist = dist - AHAddress.Full;
        }
      }
      else {
        //we know dist <= 0:
        
        //If dist < -Address.Half
        //if (0 > (Address.Half + dist)) {
        //same as below, but below doesn't require BigInteger(0),
        //so it saves memory and CPU:
        if (n_x > (Address.Half + n_y)) {
          //
          dist = dist + AHAddress.Full;
        }
      }
      return dist;
    }

    /**
     * Use the DistanceTo function.  If this node is
     * positive distance from add it is right of.
     */
    public bool IsRightOf(AHAddress add)
    {
      return (add.DistanceTo(this) < 0);
    }

    /**
     * Use the DistanceTo function.  If this node is
     * positive distance from add, it is left of.
     */
    public bool IsLeftOf(AHAddress add)
    {
      return (add.DistanceTo(this) > 0);
    }

    /**
     * All AHAddresses are unicast
     */
    public override bool IsUnicast
    {
      get
      {
        return true;
      }
    }

    /**
     * The Left (increasing, clockwise) distance to
     * the given AHAddress
     * @param addr the AHAddress to compute the distance to
     * @return the distance
     */
    public BigInteger LeftDistanceTo(AHAddress addr)
    {
      BigInteger n_x = ToBigInteger();
      BigInteger n_y = addr.ToBigInteger();

      BigInteger dist;
      
      if (n_y > n_x) {
	//The given address is larger than us, just subtract
        dist = n_y - n_x;
      }
      else {
	//We need to add AHAddress.Full to the result:
        dist = n_y - n_x + AHAddress.Full;
      }
      return dist;
    }
    /**
     * The Right (decreasing, counterclockwise) distance to
     * the given AHAddress
     * @param addr the AHAddress to compute the distance to
     * @return the distance
     */
    public BigInteger RightDistanceTo(AHAddress addr)
    {
      BigInteger n_x = ToBigInteger();
      BigInteger n_y = addr.ToBigInteger();

      BigInteger dist;
      
      if (n_y < n_x) {
	//The given address is smaller than us, just subtract
        dist = n_x - n_y;
      }
      else {
	//We need to add AHAddress.Full to the result:
        dist = n_x - n_y + AHAddress.Full;
      }
      return dist;
    }
    /** Utility method to determine if this address is between start and end
     *  from the left, i.e. it satisfies the following constraints:
     *  1. Is to the left of start, and
     *  2. Is to the right of end.
     *  @return 1 in case its within
     *  @return -1 in case it is not
     */
    public bool IsBetweenFromLeft(AHAddress start, AHAddress end) {
      int se_comp = start.CompareTo(end);
      //simple case of no wrap around where "within" is greater
      if (se_comp < 0) {
	return start.CompareTo(this) < 0 && this.CompareTo(end) < 0;
      }
      else if( se_comp == 0 ) {
        //When start == end, nothing is between them
        return false;
      }
      else {
        //in case there is a wrap around
        //"within" has become lesser than "this"
        return start.CompareTo(this) < 0 || this.CompareTo(end) < 0;
      }
    }
    
    /** Utility method to determine if this address is between start and end
     *  from the right, i.e. its satisfies the following constraints:
     *  1. Is to the right of start, and
     *  2. Is to the left of end
     *  @return 1 in case its within
     *  @return -1 in case it is not
     */
    public bool IsBetweenFromRight(AHAddress start, AHAddress end){
      int se_comp = start.CompareTo(end);
      //simple case of no wrap around where "within" is lesser
      if (se_comp > 0) {
	return start.CompareTo(this) > 0 && this.CompareTo(end) > 0;
      }
      else if( se_comp == 0 ) {
        //When start == end, nothing is between them
        return false;
      }
      else {
        //in case there is a wrap around
        //"within" has become greater than "this"
        return start.CompareTo(this) > 0 || this.CompareTo(end) > 0;
      }
    }

    /** check which address is closed to this one
     * @return true if we are closer to the first than second
     */
    public bool IsCloserToFirst(AHAddress first, AHAddress sec) {
      uint pre0 = _prefix;
      uint pref = first._prefix;
      uint pres = sec._prefix;
      if( pref == pres ) {
        //They could be the same:
        if( first.Equals( sec ) ) { return false; }
        return DistanceTo(first).abs() < DistanceTo(sec).abs();
      }
      //See if the upper and lower bounds can avoid doing big-int stuff
      uint udf = pre0 > pref ? pre0 - pref : pref - pre0;
      uint uds = pre0 > pres ? pre0 - pres : pres - pre0;
      if( udf > Int32.MaxValue ) {
        //Wrap it around:
        udf = UInt32.MaxValue - udf;
      }
      if( uds > Int32.MaxValue ) {
        uds = UInt32.MaxValue - uds;
      }
      uint upperbound_f = udf + 1;
      uint lowerbound_s = uds > 0 ? uds - 1 : 0;
      if( upperbound_f <= lowerbound_s ) {
        //There is no way the exact value could make df bigger than ds:
        return true;
      }
      uint upperbound_s = uds + 1;
      uint lowerbound_f = udf > 0 ? udf - 1 : 0;
      if( upperbound_s <= lowerbound_f ) {
        //There is no way the exact value could make ds bigger than df:
        return false;
      }
      //Else just do it the simple, but costly way
      BigInteger df = DistanceTo(first).abs();
      BigInteger ds = DistanceTo(sec).abs();
      return (df < ds);
    }
  }
  
  
  #if BRUNET_NUNIT
  [TestFixture]
  public class AHAddressTester {
    [Test]
    public void Test() {
      byte[]  buf1 = new byte[20];
      for (int i = 0; i <= 18; i++)
      {
        buf1[i] = 0x00;
      }
      buf1[19] = 0x0A;
      AHAddress test_address_1 = new AHAddress( MemBlock.Reference(buf1, 0, buf1.Length) );

      byte[] buf2 = new byte[20];
      for (int i = 0; i <= 18; i++) {
        buf2[i] = 0xFF;
      }
      buf2[19] = 0xFE;
      AHAddress test_address_2 = new AHAddress( MemBlock.Reference(buf2, 0, buf2.Length) );
      //test_address_1 is to the left of test_address_2
      //because it only a few steps in the clockwise direction:
      Assert.IsTrue( test_address_1.IsLeftOf( test_address_2 ), "IsLeftOf");
      Assert.IsTrue( test_address_2.IsRightOf( test_address_1 ), "IsRightOf");
      //This distance is twelve:
      Assert.AreEqual( test_address_2.DistanceTo( test_address_1),
                       new BigInteger(12), "DistanceTo");
      Assert.IsTrue( test_address_1.CompareTo(test_address_2) < 0, "CompareTo");
      Assert.IsTrue( test_address_2.CompareTo(test_address_1) > 0, "CompareTo");
      byte[] buf3 = new byte[Address.MemSize];
      test_address_2.CopyTo(buf3);
      AHAddress test3 = new AHAddress(MemBlock.Reference(buf3,0,buf3.Length)); 
      Assert.IsTrue( test3.CompareTo( test_address_2 ) == 0 , "CompareTo");
      Assert.IsTrue( test3.CompareTo( test3 ) == 0, "CompareTo");
      //As long as the address does not wrap around, adding should increase it:
      AHAddress a4 = new AHAddress( test_address_1.ToBigInteger() + 100 );
      Assert.IsTrue( a4.CompareTo( test_address_1 ) > 0, "adding increases");
      Assert.IsTrue( a4.CompareTo( test_address_2 ) < 0, "smaller than biggest");
      //Here are some consistency tests:
      for( int i = 0; i < 1000; i++) {
        System.Random r = new Random();
        byte[] b1 = new byte[Address.MemSize];
        r.NextBytes(b1);
        //Make sure it is class 0:
        Address.SetClass(b1, 0);
        byte[] b2 = new byte[Address.MemSize];
        r.NextBytes(b2);
        //Make sure it is class 0:
        Address.SetClass(b2, 0);
        byte[] b3 = new byte[Address.MemSize];
        r.NextBytes(b3);
        //Make sure it is class 0:
        Address.SetClass(b3, 0);
        AHAddress a5 = new AHAddress( MemBlock.Reference(b1,0,b1.Length) );
        AHAddress a6 = new AHAddress( MemBlock.Reference(b2,0,b2.Length) );
        AHAddress a7 = new AHAddress( MemBlock.Reference(b3,0,b3.Length) );
        Assert.IsTrue( a5.CompareTo(a6) == -1 * a6.CompareTo(a5), "consistency");
        //Nothing is between the same address:
        Assert.IsFalse( a5.IsBetweenFromLeft(a6, a6), "Empty Between Left");
        Assert.IsFalse( a5.IsBetweenFromRight(a7, a7), "Empty Between Right");
        //Endpoints are not between:
        Assert.IsFalse( a6.IsBetweenFromLeft(a6, a7), "End point Between Left");
        Assert.IsFalse( a6.IsBetweenFromRight(a6, a7), "End point Between Right");
        Assert.IsFalse( a7.IsBetweenFromLeft(a6, a7), "End point Between Left");
        Assert.IsFalse( a7.IsBetweenFromRight(a6, a7), "End point Between Right");

        if ( a5.IsBetweenFromLeft(a6, a7) ) {
          //Then the following must be true:
          Assert.IsTrue( a6.LeftDistanceTo(a5) < a6.LeftDistanceTo(a7),
                         "BetweenLeft true");
        }
        else {
          //Then the following must be false:
          Assert.IsFalse( a6.LeftDistanceTo(a5) < a6.LeftDistanceTo(a7),
                          "BetweenLeft false");
        }
        if ( a5.IsBetweenFromRight(a6, a7) ) {
          //Then the following must be true:
          Assert.IsTrue( a6.RightDistanceTo(a5) < a6.RightDistanceTo(a7),
                         "BetweenRight true");
        }
        else {
          //Then the following must be false:
          Assert.IsFalse( a6.RightDistanceTo(a5) < a6.RightDistanceTo(a7),
                          "BetweenRight false");
        }
        if( a5.IsCloserToFirst(a6, a7) ) {
          Assert.IsTrue( a5.DistanceTo(a6).abs() < a5.DistanceTo(a7).abs(), "IsCloser 1");
        }
        else {
          Assert.IsFalse( a5.DistanceTo(a6).abs() < a5.DistanceTo(a7).abs(), "IsCloser 2");
        }
        Assert.IsFalse(a5.IsCloserToFirst(a6, a7) && a5.IsCloserToFirst(a7,a6), "can only be closer to one!");
        if( false == a5.Equals(a6) ) {
          Assert.IsTrue(a5.IsCloserToFirst(a5, a6), "Always closer to self");
        }
        Assert.IsFalse(a5.IsBetweenFromLeft(a6, a7) ==
                            a5.IsBetweenFromRight(a6, a7),
                            "can't be between left and between right");
      }
    }
  }
#endif

}

