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
using System.Security.Cryptography;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet
{

  /**
   * Represents unicast addresses which have a one to
   * one relationship with Nodes in the Brunet system.
   * Each node has at most one AHAddress, and each
   * AHAddress has at most one Node associated with
   * it.
   */

  public class AHAddress:Brunet.StructuredAddress
  {
    ///The class of this address type
    public static readonly int _class = 0;

    public override int Class
    {
      get
      {
        return _class;
      }
    }

    /**
     * Return a random AHAddress initialized from the given rng
     */
    public AHAddress(RandomNumberGenerator rng)
    {
      buffer = new byte[MemSize];
      rng.GetBytes(buffer);
      SetClass(this.Class);
    }
    
    public AHAddress(byte[] add) : base(add)
    {
      if (ClassOf(add) != this.Class) {
        throw new System.
        ArgumentException("This is not an AHAddress (Class 0) :  ",
                          this.ToString());
      }
    }

    public AHAddress(byte[] add, int offset):base(add, offset)
    {
      if (ClassOf(add,offset) != this.Class) {
        throw new System.
        ArgumentException("This is not an AHAddress (Class 0) :  ",
                          this.ToString());
      }
    }

    public AHAddress(BigInteger big_int):base(big_int)
    {
      if (ClassOf(buffer) != this.Class) {
        throw new System.
        ArgumentException("This is not an AHAddress (Class 0) :  ",
                          this.ToString());
      }
    }

    /**
     * Compute the distance from this to add such that
     * the magnitude is less than or equal to Address.Half
     */
    public virtual BigInteger DistanceTo(AHAddress add)
    {
      BigInteger n_x = this.ToBigInteger();
      BigInteger n_y = add.ToBigInteger();

      BigInteger dist = n_y - n_x;
      if (dist > 0) {
        if (dist >= Address.Half) {
          dist = dist - AHAddress.Full;
        }
      }
      else {
        //If dist < -Address.Half
        if (0 > (Address.Half + dist)) {
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
    /** Utility method to determine if some key lies between
     *  two addresses (to the left of "us"). 
     *  @return 1 in case its within
     *  @return -1 in case its on th
     */
    public bool IsToLeftWithin(AHAddress start, AHAddress end) {
      AHAddressComparer cmp = new AHAddressComparer();
      //simple case of no wrap around where "within" is greater
      if (cmp.Compare(start, end) < 0) {
	return cmp.Compare(start, this) < 0 && cmp.Compare(this, end) < 0;
      }
      //in case there is a wrap around
      //"within" has become lesser than "us"
      return cmp.Compare(start, this) < 0 || cmp.Compare(this, end) < 0;
    }
    
    /** Utility method to determine if some key lies between
     *  two addresses (to the right of "us"). 
     */
    public bool IsToRightWithin(AHAddress start, AHAddress end){
      AHAddressComparer cmp = new AHAddressComparer();
      //simple case of no wrap around where "within" is lesser
      if (cmp.Compare(start, end) > 0) {
	return cmp.Compare(start, this) > 0 && cmp.Compare(this, end) > 0;
      }
      //in case there is a wrap around
      //"within" has become greater than "us"
      return cmp.Compare(start, this) > 0 || cmp.Compare(this, end) > 0;
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
      AHAddress test_address_1 = new AHAddress(buf1);

      byte[] buf2 = new byte[20];
      for (int i = 0; i <= 18; i++) {
        buf2[i] = 0xFF;
      }
      buf2[19] = 0xFE;
      AHAddress test_address_2 = new AHAddress(buf2);
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
      AHAddress test3 = new AHAddress(buf3); 
      Assert.IsTrue( test3.CompareTo( test_address_2 ) == 0 , "CompareTo");
      Assert.IsTrue( test3.CompareTo( test3 ) == 0, "CompareTo");
      //As long as the address does not wrap around, adding should increase it:
      AHAddress a4 = new AHAddress( test_address_1.ToBigInteger() + 100 );
      Assert.IsTrue( a4.CompareTo( test_address_1 ) > 0, "adding increases");
      Assert.IsTrue( a4.CompareTo( test_address_2 ) < 0, "smaller than biggest");
      //Here are some consistency tests:
      for( int i = 0; i < 100; i++) {
        System.Random r = new Random();
        byte[] b1 = new byte[Address.MemSize];
        r.NextBytes(b1);
        //Make sure it is class 0:
        b1[Address.MemSize - 1] = (byte)(b1[Address.MemSize - 1] &= 0xFE);
        byte[] b2 = new byte[Address.MemSize];
        r.NextBytes(b2);
        //Make sure it is class 0:
        b2[Address.MemSize - 1] = (byte)(b2[Address.MemSize - 1] &= 0xFE);
        Address a5 = new AHAddress(b1);
        Address a6 = new AHAddress(b2);
        Assert.IsTrue( a5.CompareTo(a6) == -1 * a6.CompareTo(a5), "consistency");
      }
    }
  }
#endif

}

