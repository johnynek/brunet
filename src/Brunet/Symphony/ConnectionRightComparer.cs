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

using System;
using Brunet.Util;
using Brunet.Connections;

namespace Brunet.Symphony
{

  /**
   * The Brunet system routes messages by sending them
   * towards a Connection which has the closest Address.
   * This comparer does address comparing for use in .Net
   * framework classes which use the IComparer interface.
   */

  public class ConnectionRightComparer:System.Collections.IComparer, System.Collections.Generic.IComparer<Connection>
  {
    protected AHAddress _zero;
    /**
     * Zero is where we will count zero from.  Half Is half way point
     */
    public AHAddress Zero { get { return _zero; } }
    public ConnectionRightComparer()
    {
      _zero = new AHAddress( MemBlock.Reference(Address.Half.getBytes(), 0, Address.MemSize) );
    }

    /**
     * @param zero the address to use as the zero in the space
     */
    public ConnectionRightComparer(Address zero)
    {
      byte[] binzero = new byte[Address.MemSize];
      zero.CopyTo(binzero);
      //Make sure the last bit is zero, so the address is class 0
      Address.SetClass(binzero, 0);
      _zero = new AHAddress( MemBlock.Reference(binzero, 0, Address.MemSize) );
    }

    public int Compare(object x, object y) 
    {
      Connection c_x = (Connection)x;
      Connection c_y = (Connection)y;
      return Compare(c_x, c_y);
    }

    public int Compare(Connection x, Connection y) 
    {
      //Equals is fast to check, lets do it before we
      //do more intense stuff :
      if (x.Equals(y)) {
        return 0;
      }

      if ((x.Address is AHAddress) && (y.Address is AHAddress)) {
        AHAddress add_x = (AHAddress) x.Address;
        AHAddress add_y = (AHAddress) y.Address;
        /**
         * We compute the distances with the given zero
	 * dist_x : distance from zero to x
	 * dist_y : distance from zero to y
         *
         * The AHAddress.RightDistanceTo function gives
         * the distance as measured from the node in count-clockwise.
         *
         * We can use this to set the "zero" we want : 
         */
        BigInteger dist_x = _zero.RightDistanceTo(add_x);
        BigInteger dist_y = _zero.RightDistanceTo(add_y);
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
        * If addresses are not AHAddress, throw an exception 
        */
        throw new Exception(
        String.Format("The addresses are not AHAddress. Only AHAddress can be compared."));
      }
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class ConRightCompTester {
    [Test]
    public void Test() {
      AHAddress a1 = new AHAddress( Address.Full -2);
      AHAddress a2 = new AHAddress( Address.Half - 2);
      AHAddress a3 = new AHAddress( Address.Half + 2);
      Connection c1 = new Connection(null, a1, "struectured",null,null);
      Connection c2 = new Connection(null, a2, "struectured",null,null);
      Connection c3 = new Connection(null, a3, "struectured",null,null);
      ConnectionRightComparer cmp = new ConnectionRightComparer();
      //The default zero is half, since a1 is half, it is zero,
      //the below should all be true:
      Assert.IsTrue( cmp.Compare(c1, c2) > 0, "Biggest is farther than half -2");
      Assert.IsTrue( cmp.Compare(c3, c2) > 0, "half +2 is farther than half -2");
      Assert.IsTrue( cmp.Compare(c3, c1) > 0, "half +2 is farther than Biggest");
    }
  }
#endif  
}
