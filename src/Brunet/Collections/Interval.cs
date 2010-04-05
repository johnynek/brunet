/*
Copyright (C) 2008  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System.Collections;
using System.Collections.Generic;

namespace Brunet.Collections
{
/**
 * This class represents intervals.  We need to supply
 * a comparer, or Comparer<T>.Default must exist.
 */
public class Interval<T> : IComparable, IComparable<Interval<T>>, IComparable<T> {

  readonly IComparer<T> _comp;

  /** This is the start of the interval
   */
  readonly public T Start;
  ///True if the interval DOES NOT include Start
  readonly public bool StartIsOpen;
  /** This is the end of the interval
   */
  readonly public T End;
  ///True if the interval DOES NOT include End 
  readonly public bool EndIsOpen;

  /** Create a new open or closed interval
   * @param start the starting point
   * @param s_open if true, the interval does not include the starting point
   * @param end the ending point
   * @param e_open if true, the interval does not include the ending point
   * @param comp the Comparer to use for Interval computations
   * @throw ArgumentException if there is no overlap
   */
  public Interval(T start, bool start_open, T end, bool end_open, IComparer<T> comp) {
    if( comp.Compare(start, end) > 0 ) {
      string err = String.Format(
                    "Start({0}) must be less than or equal to End({1})",
                    start, end);
      throw new System.ArgumentException(err);
    }
    Start = start;
    StartIsOpen = start_open;
    End = end;
    EndIsOpen = end_open;
    _comp = comp; 
  }

  /** Create a new open or closed interval
   * @param start the starting point
   * @param s_open if true, the interval does not include the starting point
   * @param end the ending point
   * @param e_open if true, the interval does not include the ending point
   * @throw ArgumentException if there is no overlap
   */
  public Interval(T start, bool s_open, T end, bool e_open) : this(start, s_open, end, e_open, Comparer<T>.Default) { }

  /** Factory method to create an open interval
   */
  public static Interval<T> CreateOpen(T start, T end) {
    return CreateOpen(start, end, Comparer<T>.Default);
  }
  /** Factory method to create an open interval
   */
  public static Interval<T> CreateOpen(T start, T end, IComparer<T> comp) {
    return new Interval<T>(start, true, end, true, comp);
  }
  /** Factory method to create a closed interval
   */
  public static Interval<T> CreateClosed(T start, T end) {
    return CreateClosed(start, end, Comparer<T>.Default);
  }
  /** Factory method to create a closed interval
   */
  public static Interval<T> CreateClosed(T start, T end, IComparer<T> comp) {
    return new Interval<T>(start, false, end, false, comp);
  }

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
      if (StartIsOpen == other.StartIsOpen) {
        int end_cmp = _comp.Compare(End, other.End);
        if( end_cmp == 0 ) {
          if( EndIsOpen == other.EndIsOpen ) {
            return 0;
          }
          else {
            //Put the open end interval first:
            return EndIsOpen ? -1 : 1;
          }
        }
        else {
          return end_cmp;
        }
      }
      else {
        /*
         * these intervals start at the same point, but only one is open.
         * put the closed interval first
         */
        return StartIsOpen ? 1 : -1;
      }
    }
  }

  int IComparable.CompareTo(object o) {
    Interval<T> o_int = o as Interval<T>;
    if( o_int != null ) {
      return CompareTo(o_int);
    }
    else {
      return CompareTo((T)o);
    }
  }
  /** Is the point below, in, or above the interval
   * @return 1 if everything in the interval is greater than 1, -1 if
   * everything is below, 0 if the point is in the interval
   */
  public int CompareTo(T point) {
    int scmp = _comp.Compare(Start, point);
    if( scmp > 0 ) {
      //point is strictly below
      return 1;
    }
    int ecmp = _comp.Compare(point, End);
    if( ecmp > 0 ) {
      //point is strictly above
      return -1;
    }
    if( (scmp < 0) && (ecmp < 0) ) {
      //Strictly inside
      return 0;
    }
    //Not strictly inside or out, it must be on the boundary:
    if( scmp == 0 ) {
      //Point is same as Start
      return StartIsOpen ? 1 : 0;
    }
    else {
      //Must be on the end:
      return EndIsOpen ? -1 : 0;
    }
  }


  public bool Contains(T point) {
    return CompareTo(point) == 0;
  }
  /** @return true if this interval completely contains the argument
   * Use the Comparer from this instance
   */
  public bool Contains(Interval<T> other) {
    int start_cmp = _comp.Compare(Start, other.Start);
    int end_cmp = _comp.Compare(End, other.End);
    bool strictly_not_bigger = (start_cmp > 0) || (end_cmp < 0);
    if( strictly_not_bigger ) {
      return false;
    }
    else {
      bool start_is_good = (start_cmp < 0);
      bool end_is_good = (end_cmp > 0);
      if(  false == start_is_good ) {
        if( start_cmp == 0 ) {
          start_is_good = (StartIsOpen == false) || other.StartIsOpen;
        }
      }
      if( false == end_is_good ) {
        if( end_cmp == 0 ) {
          end_is_good = (EndIsOpen == false) || other.EndIsOpen;
        }
      }
      return start_is_good && end_is_good;
    }
  }

  public override bool Equals(object other) {
    if( System.Object.ReferenceEquals(other, this) ) { return true; }
    Interval<T> other_int = other as Interval<T>;
    if( other_int != null ) {
      return _comp.Equals(other_int._comp) &&
             (_comp.Compare(Start, other_int.Start) == 0) &&
             (_comp.Compare(End, other_int.End) == 0) &&
             (StartIsOpen == other_int.StartIsOpen) &&
             (EndIsOpen == other_int.EndIsOpen);
    }
    else {
      return false;
    }
  }

  public override int GetHashCode() {
    return Start.GetHashCode();
  }
  
  /** @return the intersection of this interval with the other.
   * @return null if there is no overlap (the overlap is the empty set)
   */
  public Interval<T> Intersection(Interval<T> other) {
    T new_start;
    bool new_s_open;
    T new_end;
    bool new_e_open;

    int s_cmp = _comp.Compare(Start, other.Start);
    int e_cmp = _comp.Compare(End, other.End);

    if( s_cmp > 0 ) {
      new_start = Start;
      new_s_open = StartIsOpen;
    }
    else if( s_cmp < 0 ) {
      new_start = other.Start;
      new_s_open = other.StartIsOpen;
    }
    else {
      new_start = Start;
      new_s_open = StartIsOpen || other.StartIsOpen;
    }
    if( e_cmp < 0 ) {
      new_end = End;
      new_e_open = EndIsOpen;
    }
    else if( e_cmp > 0 ) {
      new_end = other.End;
      new_e_open = other.EndIsOpen;
    }
    else {
      new_end = End;
      new_e_open = EndIsOpen || other.EndIsOpen;
    }
   
    int new_cmp = _comp.Compare(new_start, new_end); 
    if( new_cmp < 0 ) {
      return new Interval<T>(new_start, new_s_open, new_end, new_e_open,  _comp); 
    }
    else if( new_cmp > 0 ) {
      //Start can't be before end
      return null;
    }
    else {
      //This is only a valid interval if it is closed, i.e. it is a single
      //point:
      if( new_s_open || new_e_open ) {
        //only one point, but that point is not in the interval, so empty set:
        return null;
      }
      else {
        //Both ends are closed:
        return new Interval<T>(new_start, false, new_end, false, _comp);
      }
    }
  }
  
  /** @return true if the intersection with the other is not empty
   * Use the Comparer from this instance
   */
  public bool Intersects(Interval<T> other) {
    return Intersection(other) != null;
  }
 
  public override string ToString() {
    return String.Format("Interval<{1}{0}, {2}{3}:{4}>", 
                         Start, StartIsOpen ? '(' : '[', End, EndIsOpen ? ')' : ']', _comp); 
  }
  
}

#if BRUNET_NUNIT
[TestFixture]
public class IntervalTest {
  [Test]
  public void Test0() {
    Interval<int> iint0 = Interval<int>.CreateClosed(0,4);
    Interval<int> iint1 = Interval<int>.CreateClosed(1,3);
    Assert.IsTrue(iint0.Contains(iint1), "Interval contains");
    Assert.IsTrue(iint0.Contains(iint0), "Contains self");
    Assert.IsTrue(iint1.Contains(iint1), "Contains self");
    Assert.IsFalse(iint1.Contains(iint0), "small doesn't contain large");

    Assert.IsTrue(iint0.Intersects(iint1), "overlap 1");
    Assert.IsTrue(iint1.Intersects(iint0), "overlap 2");
    
    Interval<int> inter = iint0.Intersection(iint1);
    Assert.AreEqual(inter, iint1, "intersection test");

    Assert.IsTrue(iint0.CompareTo(iint1) < 0, "Comparison");
  }
  [Test]
  public void OpenIntTest() {
    System.Random r = new Random();
    int TEST_COUNT = 100;
    for(int i = 0; i < TEST_COUNT; i++) {
      bool so = (r.Next(0,2) == 0);
      bool eo = (r.Next(0,2) == 0);
      double st = r.NextDouble();
      double en = r.NextDouble();
      Interval<double> i1 = new Interval<double>(Math.Min(st, en), so, Math.Max(st, en), eo, Comparer<double>.Default);

      Assert.IsTrue(i1.Contains(i1.Start) == (false == i1.StartIsOpen), "contains test1");
      Assert.IsTrue(i1.Contains(i1.End) == (false == i1.EndIsOpen), "contains test1");
      //Check self-intersection:
      Assert.AreEqual(i1.Intersection(i1), i1, "Self-intersection");
      // ********************************************
      //Test checking for point membership:
      // ********************************************
      double point1 = r.NextDouble();
      double point2 = r.NextDouble();
      int c1 = i1.CompareTo(point1); //If 1, then everything in i1 is bigger than point1
      int c2 = i1.CompareTo(point2); //If 1, then everything in i1 is bigger than point2
      if( c1 > 0 ) {
        if( i1.StartIsOpen ) {
          Assert.IsTrue(point1 <= i1.Start, "point compareto 1");
        }
        else {
          Assert.IsTrue(point1 < i1.Start, "point compareto 1");
        }
      }
      else if( c1 == 0 ) {
        if( i1.StartIsOpen ) {
          Assert.IsTrue(point1 > i1.Start, "point compareto start 2");
        }
        else {
          Assert.IsTrue(point1 >= i1.Start, "point compareto start 2");
        }
        if( i1.EndIsOpen ) {
          Assert.IsTrue(point1 < i1.End, "point compareto end 2");
        }
        else {
          Assert.IsTrue(point1 <= i1.End, "point compareto end 2");
        }
      }
      else {
        if( i1.EndIsOpen ) {
          Assert.IsTrue(point1 >= i1.End, "point compareto end 3");
        }
        else {
          Assert.IsTrue(point1 > i1.End, "point compareto end 3");
        }
      }
      if( c2 < c1 ) {
        //Then point2 must be greater than point1:
        Assert.IsTrue( point2 > point1, "interval ordering implies point ordering1");
      }
      if( c1 < c2 ) {
        //Then point1 must be greater than point2:
        Assert.IsTrue( point1 > point2, "interval ordering implies point ordering2");
      }
      // ********************************************
      // ********************************************

      so = (r.Next(0,2) == 0);
      eo = (r.Next(0,2) == 0);
      st = r.NextDouble();
      en = r.NextDouble();
      Interval<double> i2 = new Interval<double>(Math.Min(st, en), so, Math.Max(st, en), eo, Comparer<double>.Default);
      if( i1.Intersects( i2 ) ) {
        //Let's see if the intersection code is working correctly:
        Interval<double> intersect = i1.Intersection(i2);
        if( i1.Start > i2.Start ) {
          Assert.IsTrue(intersect.Start == i1.Start, "intersect start");
          Assert.IsTrue(intersect.StartIsOpen == i1.StartIsOpen, "start open test");
        }
        else if( i1.Start < i2.Start ) {
          Assert.IsTrue(intersect.Start == i2.Start, "intersect start");
          Assert.IsTrue(intersect.StartIsOpen == i2.StartIsOpen, "start open test");
        }
        else {
          Assert.IsTrue(intersect.Start == i2.Start, "intersect start");
          Assert.IsTrue(intersect.StartIsOpen == (i1.StartIsOpen || i2.StartIsOpen), "start open test");
        }
        if( i1.End > i2.End ) {
          Assert.IsTrue(intersect.End == i2.End, "intersect end");
          Assert.IsTrue(intersect.EndIsOpen == i2.EndIsOpen, "end open test");
        }
        else if( i1.End < i2.End ) {
          Assert.IsTrue(intersect.End == i1.End, "intersect end");
          Assert.IsTrue(intersect.EndIsOpen == i1.EndIsOpen, "end open test");
        }
        else {
          Assert.IsTrue(intersect.End == i2.End, "intersect end");
          Assert.IsTrue(intersect.EndIsOpen == (i1.EndIsOpen || i2.EndIsOpen), "end open test");
        }
      }
      else {
        Assert.IsTrue( i1.Intersection(i2) == null, "Overlap is false implies intersection is null");
      }

    }
  }

  [Test]
  public void RandomIntTest() {
    System.Random r = new Random();
    int TEST_COUNT = 100;
    for(int i = 0; i < TEST_COUNT; i++) {
      Interval<int> i1 = MakeInterval(r.Next(), r.Next());
      Interval<int> i2 = MakeInterval(r.Next(), r.Next());
      //Do some sanity checks:
      Assert.IsTrue(i1.Contains(i1.Start), "Closed interval start");
      Assert.IsTrue(i1.Contains(i1.End), "Closed interval end");
      if( i1.Intersects(i2) ) {
        //Overlap is reflexive:
        Assert.IsTrue(i2.Intersects(i1), "overlap reflexivity");
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
        Assert.IsFalse(i2.Intersects(i1), "overlap reflexivity");
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
      Assert.IsTrue(i1.Contains(i1.Start), "Closed interval start");
      Assert.IsTrue(i1.Contains(i1.End), "Closed interval end");
      if( i1.Intersects(i2) ) {
        //Overlap is reflexive:
        Assert.IsTrue(i2.Intersects(i1), "overlap reflexivity");
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
        Assert.IsFalse(i2.Intersects(i1), "overlap reflexivity");
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
    return Interval<T>.CreateClosed(start, end, comp);
  }
}

#endif
}
