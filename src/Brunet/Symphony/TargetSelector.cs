/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida
                   Arijit Ganguly <aganguly@acis.ufl.edu:> University of Florida

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

using System.Collections;
#if BRUNET_NUNIT
using System.Collections.Specialized;
using System.Security.Cryptography;
using NUnit.Framework;
#endif

namespace Brunet.Symphony {
  public delegate void TargetSelectorDelegate(Address start, SortedList score_table, Address current);
  /**
   * Interface that allows the StructuredConnectionOverlord to select an appropriate
   * target while forming connections. This will asynchronously put invoke a callback into the 
   * providing information about the candidates in a score table.
   */
  
  public abstract class TargetSelector {
  /**
   * Selects an optimal target given a start address, the range starting at that address, and the current address (could be null).
   * @param start start address of the range.
   * @param range number of candidates
   * @param callback callback function into the caller
   * @param current currently selected optimal
   */
    public abstract void ComputeCandidates(Address start, int range, TargetSelectorDelegate callback, Address current);
  }

  /**
   * This class provides a default target selector.
   */
  public class DefaultTargetSelector: TargetSelector {
    public DefaultTargetSelector() {}
  /**
   * Selects an optimal target given a start address, the range starting at that address, and the current address (could be null).
   * If the current address is not null, it is returned as optimal, or else start address is returned.
   * @param start start address of the range.
   * @param range number of candidates
   * @param callback callback function into the caller
   * @param current currently selected optimal
   */
    public override void ComputeCandidates(Address start, int range, TargetSelectorDelegate callback, Address current) {
      SortedList sorted = new SortedList();
      if (current != null) {
        sorted[1.0] = (Address) current;
      } else {
        sorted[1.0] = start;
      }
      callback(start, sorted, current);
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class TargetSelectorTester {
    protected int _idx;
    protected Address[] _addr_list = new Address[100];
    public TargetSelectorTester() {
    }

    protected void TargetSelectorCallback(Address start, SortedList score_table, Address current) {
      Assert.IsTrue(score_table.Count > 0);
      if (current == null) {
        Address min_target = (Address) score_table.GetByIndex(0);
        Assert.AreEqual(_addr_list[_idx++], min_target);
      }
    }
    
    [Test]
    public void Test() {
      RandomNumberGenerator rng = new RNGCryptoServiceProvider();
      TargetSelector ts = new DefaultTargetSelector();
      //in this case we set the current to an address in our list
      for (int i = 0; i < _addr_list.Length; i++ ) {
        AHAddress tmp_addr = new AHAddress(rng);
        _addr_list[i] = tmp_addr;
      }
      _idx = 0;
      for (int i = 0; i < _addr_list.Length; i++ ) {
        ts.ComputeCandidates(new AHAddress(rng), 10, TargetSelectorCallback, _addr_list[i]);
      }

      //in this case we set the current address to null
      for (int i = 0; i < _addr_list.Length; i++ ) {
        AHAddress tmp_addr = new AHAddress(rng);
        _addr_list[i] = tmp_addr;
      }
      _idx = 0;
      for (int i = 0; i < _addr_list.Length; i++ ) {
        ts.ComputeCandidates(_addr_list[i], 10, TargetSelectorCallback, null);
      }
    }
  }
#endif
}
