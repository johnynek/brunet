/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>,  University of Florida

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
using System.Collections;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet {

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
  #endif
}

}
