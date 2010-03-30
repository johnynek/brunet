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
   * Unlike AHAddressComparer, this comparer compares addresses
   * by calculating distance to the left (clockwise).
   * returns 1 if distance to the address from zero is greater,
   *        -1 if distance to the address from zero is less,
   *         0 if adresses are the same.
   */

  public class ConnectionLeftComparer:System.Collections.IComparer, System.Collections.Generic.IComparer<Connection>
  {
    protected AHAddress _zero;
    /**
     * Zero is where we will count zero from.  Half Is half way point
     */
    public AHAddress Zero { get { return _zero; } }
    public ConnectionLeftComparer()
    {
      _zero = new AHAddress( MemBlock.Reference(Address.Half.getBytes(), 0, Address.MemSize) );
    }

    /**
     * @param zero the address to use as the zero in the space
     */
    public ConnectionLeftComparer(Address zero)
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
         * The AHAddress.LeftDistanceTo function gives
         * the distance as measured from the node in clockwise.
         *
         * We can use this to set the "zero" we want : 
         */
        BigInteger dist_x = _zero.LeftDistanceTo(add_x);
        BigInteger dist_y = _zero.LeftDistanceTo(add_y);
        //Since we know they are not equal, either dist_x is bigger
        //that dist_y or vice-versa :
        if (dist_x > dist_y) {
          //Then dist_x - dist_y > 0, and x is the farther.
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
  public class ConLeftCompTester {
    [Test]
    public void Test() {
      AHAddress a1 = new AHAddress( Address.Full -2);
      AHAddress a2 = new AHAddress( Address.Half - 2);
      AHAddress a3 = new AHAddress( Address.Half + 2);
      Connection c1 = new Connection(null, a1, "struectured",null,null);
      Connection c2 = new Connection(null, a2, "struectured",null,null);
      Connection c3 = new Connection(null, a3, "struectured",null,null);
      ConnectionLeftComparer cmp = new ConnectionLeftComparer();
      //The default zero is half, since a1 is half, it is zero,
      //the below should all be true:
      Assert.IsTrue( cmp.Compare(c1, c2) < 0, "Biggest is closer than half -2");
      Assert.IsTrue( cmp.Compare(c3, c2) < 0, "half +2 is closer than half -2");
      Assert.IsTrue( cmp.Compare(c3, c1) < 0, "half +2 is closer than Biggest");
    }
  }
#endif
}
