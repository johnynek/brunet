/*
Copyright (C) 2008  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System.Collections;
using System.Collections.Generic;

namespace Brunet.Util
{
/**
 * This class represents intervals.  We need to supply
 * a comparer, or Comparer<T>.Default must exist.
 */
public class Interval<T> : IComparable, IComparable<Interval<T>> {

  readonly IComparer<T> _comp;

  /** This is the start of the interval
   */
  readonly public T Start;
  /** This is the end of the interval
   */
  readonly public T End;

  /**
   * @throw ArgumentException if there is no overlap
   */
  public Interval(T start, T end, IComparer<T> comp) {
    if( comp.Compare(start, end) > 0 ) {
      string err = String.Format(
                    "Start({0}) must be less than or equal to End({1})",
                    start, end);
      throw new System.ArgumentException(err);
    }
    Start = start;
    End = end;
    _comp = comp; 
  }

  /**
   * @throw ArgumentException if there is no overlap
   */
  public Interval(T start, T end) : this(start, end, Comparer<T>.Default) { }

  /**
   * Sort an interval based first on start, then on end, if they have
   * Equal underlying IComparer<T> objects
   * @throw ArgumentException if they do not have Equal IComparer<T>
   */
  public int CompareTo(Interval<T> other) {
    if( false == _comp.Equals(other._comp) ) {
      throw new ArgumentException("Intervals do not share a comparer"); 
    }
    int start_cmp = _comp.Compare(Start, other.Start);
    if( start_cmp != 0 ) {
      return start_cmp;
    }
    else {
      return _comp.Compare(End, other.End);
    }
  }

  int IComparable.CompareTo(object o) {
    return CompareTo((Interval<T>)o);
  }

  /** @return true if this interval completely contains the argument
   * Use the Comparer from this instance
   */
  public bool Contains(Interval<T> other) {
    bool start_le = (_comp.Compare(Start, other.Start) <= 0);
    bool end_ge = (_comp.Compare(End, other.End) >= 0);
    return start_le && end_ge;
  }

  public override bool Equals(object other) {
    Interval<T> other_int = other as Interval<T>;
    if( other_int != null ) {
      return (Start.Equals(other_int.Start)) &&
             (End.Equals(other_int.End)) &&
             (_comp.Equals(other_int._comp));
    }
    else {
      return false;
    }
  }

  public override int GetHashCode() {
    return Start.GetHashCode();
  }
  
  /** @return the intersection of this interval with the other.
   * @return null if there is no overlap
   */
  public Interval<T> Intersection(Interval<T> other) {
    T bigger_start = _comp.Compare(Start, other.Start) > 0 ? Start : other.Start;
    T smaller_end = _comp.Compare(End, other.End) < 0 ? End : other.End;
    if( _comp.Compare(bigger_start, smaller_end) <= 0 ) {
      return new Interval<T>(bigger_start, smaller_end, _comp); 
    }
    else {
      return null; 
    }
  }
  
  /** @return true if the intersection with the other is not empty
   * Use the Comparer from this instance
   */
  public bool Overlaps(Interval<T> other) {
    T bigger_start = _comp.Compare(Start, other.Start) > 0 ? Start : other.Start;
    T smaller_end = _comp.Compare(End, other.End) < 0 ? End : other.End;
    //For this to make sense, the start has to be less than the end:
    return (_comp.Compare(bigger_start, smaller_end) <= 0);
  }
 
  public override string ToString() {
    return String.Format("Interval({0}, {1}, {2})", 
                         Start, End, _comp); 
  }
  
}

#if BRUNET_NUNIT
[TestFixture]
public class IntervalTest {
  [Test]
  public void Test0() {
    Interval<int> iint0 = new Interval<int>(0,4);
    Interval<int> iint1 = new Interval<int>(1,3);
    Assert.IsTrue(iint0.Contains(iint1), "Interval contains");
    Assert.IsTrue(iint0.Contains(iint0), "Contains self");
    Assert.IsTrue(iint1.Contains(iint1), "Contains self");
    Assert.IsFalse(iint1.Contains(iint0), "small doesn't contain large");

    Assert.IsTrue(iint0.Overlaps(iint1), "overlap 1");
    Assert.IsTrue(iint1.Overlaps(iint0), "overlap 2");
    
    Interval<int> inter = iint0.Intersection(iint1);
    Assert.AreEqual(inter, iint1, "intersection test");

    Assert.IsTrue(iint0.CompareTo(iint1) < 0, "Comparison");
  }
  [Test]
  public void RandomIntTest() {
    System.Random r = new Random();
    int TEST_COUNT = 100;
    for(int i = 0; i < TEST_COUNT; i++) {
      Interval<int> i1 = MakeInterval(r.Next(), r.Next());
      Interval<int> i2 = MakeInterval(r.Next(), r.Next());
      //Do some sanity checks:
      if( i1.Overlaps(i2) ) {
        //Overlap is reflexive:
        Assert.IsTrue(i2.Overlaps(i1), "overlap reflexivity");
        Interval<int> i3 = i1.Intersection(i2);
        //The intersection is in both:
        Assert.IsTrue(i1.Contains(i3), "intersection overlaps 1");
        Assert.IsTrue(i2.Contains(i3), "intersection overlaps 2");
        if( i1.CompareTo( i2 ) < 0 ) {
          //This implies the intersection is less than or equal to i2:
          Assert.IsTrue( i3.CompareTo(i2) <= 0, "intersection ordering 1");
        }
        else {
          //This implies the intersection is greater than or equal to i2:
          Assert.IsTrue( i3.CompareTo(i2) >= 0, "intersection ordering 1");
        }
        CheckEqualCompHash(i1, i3);
        CheckEqualCompHash(i2, i3);
      }
      else {
        //Overlap is reflexive:
        Assert.IsFalse(i2.Overlaps(i1), "overlap reflexivity");
        Assert.IsNull(i1.Intersection(i2), "Overlap is null 1");
        Assert.IsNull(i2.Intersection(i1), "Overlap is null 2");
        //CompareTo should agree with comparing the starts:
        Assert.IsFalse(i1.CompareTo(i2) == 0, "Non-overlapping are not equal");
        Assert.AreEqual(i1.CompareTo(i2), i1.Start.CompareTo(i2.Start),"compare same as start");
      }
      //Check self intersection equality:
      Interval<int> i4 = i1.Intersection(i1);
      Assert.AreEqual(i1, i4, "self intersection is equivalent");
      CheckEqualCompHash(i1, i2);
      CheckEqualCompHash(i1, i4);
      CheckEqualCompHash(i2, i4);
    }
  }
  [Test]
  public void RandomDoubleTest() {
    System.Random r = new Random();
    int TEST_COUNT = 100;
    for(int i = 0; i < TEST_COUNT; i++) {
      Interval<double> i1 = MakeInterval(r.NextDouble(), r.NextDouble());
      Interval<double> i2 = MakeInterval(r.NextDouble(), r.NextDouble());
      //Do some sanity checks:
      if( i1.Overlaps(i2) ) {
        //Overlap is reflexive:
        Assert.IsTrue(i2.Overlaps(i1), "overlap reflexivity");
        Interval<double> i3 = i1.Intersection(i2);
        //The intersection is in both:
        Assert.IsTrue(i1.Contains(i3), "intersection overlaps 1");
        Assert.IsTrue(i2.Contains(i3), "intersection overlaps 2");
        if( i1.CompareTo( i2 ) < 0 ) {
          //This implies the intersection is less than or equal to i2:
          Assert.IsTrue( i3.CompareTo(i2) <= 0, "intersection ordering 1");
        }
        else {
          //This implies the intersection is greater than or equal to i2:
          Assert.IsTrue( i3.CompareTo(i2) >= 0, "intersection ordering 1");
        }
        CheckEqualCompHash(i1, i3);
        CheckEqualCompHash(i2, i3);
      }
      else {
        //Overlap is reflexive:
        Assert.IsFalse(i2.Overlaps(i1), "overlap reflexivity");
        Assert.IsNull(i1.Intersection(i2), "Overlap is null 1");
        Assert.IsNull(i2.Intersection(i1), "Overlap is null 2");
        //CompareTo should agree with comparing the starts:
        Assert.IsFalse(i1.CompareTo(i2) == 0, "Non-overlapping are not equal");
        Assert.AreEqual(i1.CompareTo(i2), i1.Start.CompareTo(i2.Start),"compare same as start");
      }
      //Check self intersection equality:
      Interval<double> i4 = i1.Intersection(i1);
      Assert.AreEqual(i1, i4, "self intersection is equivalent");
      CheckEqualCompHash(i1, i2);
      CheckEqualCompHash(i1, i4);
      CheckEqualCompHash(i2, i4);
    }
  }

  public void CheckEqualCompHash(IComparable o1, IComparable o2) {
    if( o1.Equals(o2) ) {
      Assert.IsTrue(o2.Equals(o1), "Equals reflexivity (true)");
      Assert.IsTrue(o1.CompareTo(o2) == 0, "Equals consistency 1 (true)");
      Assert.IsTrue(o2.CompareTo(o1) == 0, "Equals (ref) consistency 1 (true)");
      Assert.IsTrue(o1.GetHashCode() == o2.GetHashCode(), "Equals Hash consistency 1");
    }
    else {
      Assert.IsFalse(o2.Equals(o1), "Equals reflexivity (false)");
      Assert.IsFalse(o1.CompareTo(o2) == 0, "Equals consistency 1 (false)");
      Assert.IsTrue(o2.CompareTo(o1) != 0, "Equals (ref) consistency 1 (false)");
    }
  }

  public Interval<T> MakeInterval<T>(T a, T b) {
    T start = a;
    T end = b;
    IComparer<T> comp = Comparer<T>.Default;
    if( comp.Compare(start, end) > 0 ) {
      T svar = start;
      start = end;
      end = svar;
    }
    return new Interval<T>(start, end, comp);
  }
}

#endif
}
