/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida
                   Arijit Ganguly <aganguly@acis.ufl.edu:> University of Florida

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
