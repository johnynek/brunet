/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

using Brunet.Util;

namespace Brunet.Symphony
{

  /**
   * The Brunet system routes messages by sending them
   * towards a Connection which has the closest Address.
   * This comparer does address comparing for use in .Net
   * framework classes which use the IComparer interface.
   */

  public class AHAddressComparer:System.Collections.IComparer, System.Collections.Generic.IComparer<AHAddress>
  {
    protected AHAddress _zero;
    /**
     * Zero is where we will count zero from.  Half Is half way point
     */
    public AHAddress Zero { get { return _zero; } }
    public AHAddressComparer()
    {
      _zero = new AHAddress( MemBlock.Reference(Address.Half.getBytes(), 0, Address.MemSize) );
    }

    /**
     * @param zero the address to use as the zero in the space
     */
    public AHAddressComparer(Address zero)
    {
      byte[] binzero = new byte[Address.MemSize];
      zero.CopyTo(binzero);
      //Make sure the last bit is zero, so the address is class 0
      Address.SetClass(binzero, 0);
      _zero = new AHAddress( MemBlock.Reference(binzero, 0, Address.MemSize) );
    }

    public int Compare(AHAddress x, AHAddress y) {
      return Compare((object) x, (object) y);
    }

    public int Compare(object x, object y)
    {
      //Equals is fast to check, lets do it before we
      //do more intense stuff :
      if (x.Equals(y)) {
        return 0;
      }

      if ((x is AHAddress) && (y is AHAddress)) {
        AHAddress add_x = (AHAddress) x;
        AHAddress add_y = (AHAddress) y;
        /**
         * We compute the distances with the given zero
         * n_x - n_y = (n_x - n_z) - (n_y - n_z);
         *
         * The AHAddress.DistanceTo function gives
         * the distance as measured from the node.
         *
         * We can use this to set the "zero" we want : 
         */
        BigInteger dist_x = _zero.DistanceTo(add_x);
        BigInteger dist_y = _zero.DistanceTo(add_y);
        //Since we know they are not equal, either n_x is bigger
        //that n_y or vice-versa :
        if (dist_x > dist_y) {
          //Then dist_x - dist_y > 0, and n_x is the bigger
          return 1;
        }
        else {
          return -1;
        }
      }
      else {
        /**
        * Just to make sure we can compare any type of address : 
        */
        BigInteger bi_x = ((Address) x).ToBigInteger();
        BigInteger bi_y = ((Address) y).ToBigInteger();
        if (bi_x > bi_y) {
          return 1;
        }
        else {
          return -1;
        }
      }
    }

  }
#if BRUNET_NUNIT
  [TestFixture]
  public class AHAddCompTester {
    [Test]
    public void Test() {
      Address a1 = new AHAddress( Address.Half );
      Address a2 = new AHAddress(MemBlock.Reference(new byte[Address.MemSize], 0, Address.MemSize) );
      Address a3 = new AHAddress(Address.Full - 2);
      AHAddressComparer cmp = new AHAddressComparer();
      //The default zero is half, since a1 is half, it is zero,
      //the below should all be true:
      Assert.IsTrue( cmp.Compare(a1, a2) > 0, "Half is greater than 0");
      Assert.IsTrue( cmp.Compare(a3, a2) > 0, "Biggest is greater than 0");
      Assert.IsTrue( cmp.Compare(a3, a1) > 0, "Biggest is greater than half");
      Assert.IsTrue( a1.CompareTo( a2 ) == cmp.Compare(a1, a2),
                      "CompareTo and Compare 1");
      Assert.IsTrue( a1.CompareTo( a3 ) == cmp.Compare(a1, a3),
                     "CompareTo and Compare 2");
    }
  }
#endif
}
