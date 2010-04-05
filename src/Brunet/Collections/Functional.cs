/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>,  University of Florida

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

using System;
using System.Collections;
using System.Collections.Generic;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Collections {

/**
 * A collection of static pure functions to do some functional
 * programming
 */
#if BRUNET_NUNIT
[TestFixture]
#endif
public class Functional {

  static public ArrayList Add(IList l, object o) {
    ArrayList copy = new ArrayList(l);
    copy.Add(o);
    return copy;
  }

  static public Hashtable Add(IDictionary h, object k, object v) {
    Hashtable copy = new Hashtable( h.Count + 1);
    foreach(DictionaryEntry de in h) {
      copy.Add( de.Key, de.Value );
    }
    copy.Add(k, v);
    return copy;
  }

  /** Create a generic enumerable by casting each element of an IEnumerable
   */
  public class CastEnumerable<T> : IEnumerable<T> {
    protected readonly IEnumerable _enum;
    public CastEnumerable(IEnumerable from) {
      _enum = from;
    }

    public IEnumerator<T> GetEnumerator() {
      foreach(T ob in _enum) {
        yield return ob;
      }
    }
    IEnumerator IEnumerable.GetEnumerator() {
      foreach(var ob in _enum) {
        //Cast will throw if there is a problem
        yield return (T)ob;
      }
    }
  }

  public delegate bool FilterFunction(object o);
  /**
   * Use this to filter an enumerable
   */
  public class Filter : IEnumerable {
    protected FilterFunction _f;
    protected IEnumerable _ie;
    public Filter(FilterFunction f, IEnumerable ie) {
      _f = f;
      _ie = ie;
    }

    public IEnumerator GetEnumerator() {
      foreach(object o in _ie) {
        if( _f(o) ) {
          yield return o;
	}
      }
    }
  }


  static public ArrayList Insert(ArrayList l, int pos, object o) {
    ArrayList copy = (ArrayList)l.Clone();
    copy.Insert(pos, o);
    return copy;
  }

  //Goes round-robin through a list of IEnumerable objects
  //and yields first from the first, then second, etc.. until
  //all items have been yielded.
  public class Interleave : IEnumerable, IEnumerable<object> {
    protected readonly IList<IEnumerable> _sources;
    public Interleave(IList<IEnumerable> sources) {
      _sources = sources;
    }
    public IEnumerator<object> GetEnumerator() {
      List<IEnumerator> enums = new List<IEnumerator>();
      foreach(var eable in _sources) {
        enums.Add(eable.GetEnumerator());
      }
      bool all_done;
      do {
        all_done = true;
        foreach(var ietor in enums) {
          bool this_one = ietor.MoveNext();
          //We are all done when all the ienumerators can't move.
          all_done = all_done && (false == this_one);
          if( this_one ) {
            yield return ietor.Current;
          }
        }
      } while(!all_done);
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return this.GetEnumerator();
    }
  }

  public delegate object MapFunction(object o);
  /**
   * Use this to do map an enumerable
   */
  public class Map : IEnumerable {
    protected MapFunction _f;
    protected IEnumerable _ie;
    public Map(MapFunction f, IEnumerable ie) {
      _f = f;
      _ie = ie;
    }

    public IEnumerator GetEnumerator() {
      foreach(object o in _ie) {
        yield return _f(o);
      }
    }
  }

  static public ArrayList RemoveAt(IList l, int pos) {
    ArrayList copy = new ArrayList( l.Count - 1);
    int i = 0;
    foreach(object o in l) {
      if( i != pos ) {
        copy.Add(o);
      }
      i++;
    }
    return copy;
  }
  static public Hashtable Remove(IDictionary h, object k) {
    Hashtable copy = new Hashtable( h.Count - 1);
    foreach(DictionaryEntry de in h) {
      if( !de.Key.Equals(k) ) {
        copy.Add( de.Key, de.Value );
      }
    }
    return copy;
  }

  static public Hashtable SetElement(IDictionary h, object k, object v) {
    Hashtable copy = new Hashtable( h.Count );
    foreach(DictionaryEntry de in h) {
      if( !de.Key.Equals(k) ) {
        copy.Add( de.Key, de.Value );
      }
      else {
        copy.Add(k,v);
      }
    }
    return copy;
  }
  static public ArrayList SetElement(IList l, int k, object v) {
    ArrayList copy = new ArrayList( l );
    copy[k] = v;
    return copy;
  }

  /** Enumerates at most a certain number of items
   */
  public class Take<T> : IEnumerable, IEnumerable<T> {
    protected readonly int _count;
    protected readonly IEnumerable<T> _from;

    public Take(IEnumerable<T> from, int count) {
      _from = from;
      if( count < 0 ) {
        throw new ArgumentOutOfRangeException("count must be non-negative");
      }
      _count = count;
    }

    public IEnumerator<T> GetEnumerator() {
      int c = _count;
      IEnumerator<T> from_e = _from.GetEnumerator();
      while(--c >= 0) {
        if( from_e.MoveNext() ) {
          yield return from_e.Current;
        }
        else {
          break;
        }
      }
    }
    IEnumerator IEnumerable.GetEnumerator() {
      return this.GetEnumerator();
    }
  }

  #if BRUNET_NUNIT
  [Test]
  public void Test() {
    const int TEST_LENGTH = 1000;

    ArrayList l = new ArrayList();
    ArrayList mut = new ArrayList();
    Random r = new Random();
    for(int i = 0; i < TEST_LENGTH; i++ ) {
      int j = r.Next();
      l = Add(l, j);
      mut.Add(j);
    }
    Assert.AreEqual(l.Count, mut.Count, "List count");
    for(int i = 0; i < TEST_LENGTH; i++) {
      Assert.AreEqual(l[i], mut[i], "element equality");
    }
    //Do a bunch of random sets:
    for(int i = 0; i < TEST_LENGTH; i++) {
      int j = r.Next(TEST_LENGTH);
      int k = r.Next();
      l = SetElement(l, j, k);
      mut[j] = k;
    }
    for(int i = 0; i < TEST_LENGTH; i++) {
      Assert.AreEqual(l[i], mut[i], "element equality after sets");
    }
  }

  public void TestInterleave() {
    int[] ints = new int[]{1, 2, 3};
    string[] strs = new string[]{"one", "two", "three"};
    List<IEnumerable> enums = new List<IEnumerable>();
    enums.Add(ints);
    enums.Add(strs);
    var items = new List<object>();
    items.AddRange(new Interleave(enums));
    Assert.AreEqual(items[0], 1, "Interleave test1");
    Assert.AreEqual(items[1], "one", "Interleave test1");
    Assert.AreEqual(items[2], 2, "Interleave test1");
    Assert.AreEqual(items[3], "two", "Interleave test1");
    Assert.AreEqual(items[4], 3, "Interleave test1");
    Assert.AreEqual(items[5], "three", "Interleave test1");
  }

  public void TestTake() {
    int[] ints = new int[]{1, 2, 3, 4};
    var items = new List<int>();
    items.AddRange(new Take<int>(ints, 3));
    Assert.AreEqual(3, items.Count, "Take correct number");
    Assert.AreEqual(items[0], 1, "Take test1");
    Assert.AreEqual(items[1], 2, "Take test2");
    Assert.AreEqual(items[2], 3, "Take test3");
  }
  #endif
}

}
