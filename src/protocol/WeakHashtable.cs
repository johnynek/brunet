/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
//#define DEBUG

using System;
using System.Collections;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif
namespace Brunet {
  /**
   * Keeps a weak reference to the key, but a normal
   * reference to the value.
   *
   * @todo make this a real .Net IDictionary
   */
#if BRUNET_NUNIT
[TestFixture]
#endif
public class WeakHashtable {

  ///These are the bins for the hashtable.  They hold lists.
  protected object[] _table;
  
  /**
   * Inner class to represent the elements of the Weakhashtable
   */
  protected class Element {
    protected System.WeakReference _key;
    /**
     * The key in the (key,value) pair
     * If the key is not alive (has been GC'ed), this
     * returns null and sets the Value to null (so it can
     * be GC'ed).
     */
    public object Key {
      get {
        if( _key.IsAlive ) {
          return _key.Target;
        }
        else {
          //Go ahead and let the value get freed up:
          _value = null;
          return null;
        }
      }
    }
    protected object _value;
    /**
     * The value in the (key, value) pair
     */
    public object Value {
      get {
        return _value;
      }
      set {
        _value = value;
      }
    }
    ///The hashcode of the key.
    protected int _hc;

    ///Constructs an element, which is a (key, value) pair
    public Element(object key, object val) {
      _key = new System.WeakReference(key);
      _value = val;
      _hc = key.GetHashCode();
    }
    /**
     * This only looks at the hashcode of the key
     */
    public int KeyHashCode() {
      return _hc;
    }
    /**
     * Checks for Key equality for o.
     * Since null keys are not allowed, it returns
     * false if the key is null or o is null
     */
    public bool KeyEquals(object o) {
      object key = this.Key;
      if( key != null ) {
        return key.Equals(o);
      }
      else {
        //Null keys not allowed
        return false;
      }
    }
  }
  
  ///Current count of elements in the hashtable.  Some may have been GC'ed
  protected int _count;
  ///The number of bins in the table is 2^{_expon}.
  protected int _expon;
  ///The number of bins in the table
  protected int _size;
  ///The bit mask used on the hashcode to lookup the bin.
  protected int _mask;
  ///The smallest value for _expon (2^6 = 64 bins).
  protected static readonly int _MIN_EXP = 6;
  
  ///Make a new hashtable.
  public WeakHashtable() {
    Init(_MIN_EXP);
  }
  protected void Init(int exp) {
#if DEBUG
    Console.WriteLine("Old exp: {0}, new exp: {1}", _expon, exp);
#endif
    _expon = exp;
    _size = 1 << _expon; //2^exp
    _mask = _size - 1; //some number of 1's forming a bitmask
    _table = new object[ _size ];
    _count = 0;
  }
 
  /**
   * Implements the IDictionary interface
   */
  public void Add(object key, object val) {
    //By default, allow the system to rebalance
    this.Add(key, val, false, true);
  }
  /**
   * Add (or replace) this key value pair
   * @param key the key we are adding
   * @param val the value we are adding
   * @param replace if true, allow replacements, otherwise throw ArgumentException
   * @param rebalance if true, allow the hashtable to rebalance (if need be).
   *
   * @throws ArgumentNullException if key is null
   * @throws ArgumentException if replace is true and key is already present
   */
  protected void Add(object key, object val, bool replace, bool rebalance) {
#if DEBUG
    Console.WriteLine("WeakHashtable.Add({0},{1})", key, val);
#endif
    Element old = GetElement(key);
    if( old == null ) {
      /* New item */
      int hc = key.GetHashCode();
      //Get the list here.
      IList l = (IList)_table[ hc & _mask ];
      if( l == null ) {
        //This is a new list
        l = new ArrayList();
        _table[ hc & _mask ] = l;
      }
      l.Add( new Element(key, val) );
      _count++;
    }
    else if (replace) {
      //This is a replacement
      old.Value = val;
    }
    else {
      throw new ArgumentException("Key: " + key.ToString() + " already present");
    }
    if( rebalance ) {
      Rebalance();
    }
#if DEBUG
    Console.WriteLine("Done: Add");
#endif
  }
  /**
   * Empty all elements from the table
   */
  public void Clear() {
    Init(_MIN_EXP);
  }
  /**
   * @return true if this object is in the table.
   */
  public bool Contains(object key) {
    return (GetElement(key) != null);
  }

  /**
   * The number of pairs in the table.  This is an upper bound, since
   * the pairs could be garbage collected at any time.
   */
  public int Count {
    get { return _count; }
  }

  /**
   * Get the Element for this key.  If there is
   * no such live key, the Element is null.  This method
   * is thread safe: multiple reads can go on simulataneously.
   * @throw ArgumentNullException if key is null
   */
  protected Element GetElement(object key) {
#if DEBUG
    Console.WriteLine("WeakHashtable.GetElement({0})", key);
#endif
    if( key == null ) {
      throw new ArgumentNullException("Key cannot be null");
    }
    int hc = key.GetHashCode();
    Element res = null;
    //Get the list here.
    IList l = (IList)_table[ hc & _mask ];
    if( l != null ) {
      /*
       * Remember the live ones and replace the list.  This makes sure
       * we don't invalidate any Enumerators that might be happenening
       * in another thread (keep the lists immutable once created)
       */
      ArrayList live_ones = null;
      foreach(Element e in l) {
        if( key.Equals( e.Key ) ) {
          res = e;
        }
        if( e.Key != null ) {
	  if( live_ones == null ) { live_ones = new ArrayList(); }
          live_ones.Add(e);
        }
      }
      int live_count = 0;
      if( live_ones == null ) {
        _table[ hc & _mask ] = null;
	live_count = 0;
      }
      else {
        _table[ hc & _mask ] = live_ones;
	live_count = live_ones.Count;
      }
      _count += live_count - l.Count;
    }
#if DEBUG
    Console.WriteLine("Done: GetElement: {0}", res);
#endif
    return res;
  }

  /**
   * @param key to remove
   * @throw ArgumentNullException if key is null
   */
  public void Remove(object key) {
#if DEBUG
    Console.WriteLine("WeakHashtable.Remove({0})", key);
#endif
    if( key == null ) {
      throw new ArgumentNullException("Key cannot be null");
    }
    int hc = key.GetHashCode();
    //Get the list here.
    IList l = (IList)_table[ hc & _mask ];
    if( l != null ) {
      ArrayList to_keep = null;
      foreach(Element e in l) {
        object this_key = e.Key;
        if( (this_key != null ) && (! key.Equals(this_key) ) ) {
          if( to_keep == null ) { to_keep = new ArrayList(); }
          to_keep.Add(e);
	}
	else {
          //This key has expired or is the one we are removing
	}
      }
      int keep_count;
      if( to_keep != null ) {
        _table[ hc & _mask ] = to_keep;
        keep_count = to_keep.Count;
      }
      else {
        _table[ hc & _mask ] = null;
        keep_count = 0;
      }
      _count += keep_count - l.Count;
    }
    Rebalance();
#if DEBUG
    Console.WriteLine("Done: Remove");
#endif
  }

  //Here is the indexer
  public object this[object key] {
    get {
      Element e = GetElement(key);
      if ( e == null ) {
        return null;
      }
      else {
        return e.Value;  
      }
    }
    set {
#if DEBUG
    Console.WriteLine("WeakHashtable[{0}] = {1}", key, value);
#endif
      //Just call Add
      Add(key, value, true, true);
#if DEBUG
    Console.WriteLine("Done set");
#endif
    }
  }

  /**
   * Check to see if we need to change the number of bins,
   * and if so, make sure to remove GC'ed keys
   */
  protected void Rebalance() {
#if DEBUG
    Console.WriteLine("Rebalance");
#endif
    int new_expon = _expon;
    if( ( ( 2 * _count ) >  _size ) ) {
      //On average, there is more than one element in every 2 bins.
      //go up by a factor of 8 in size
      new_expon += 3; 
    }
    else if( (8 * _count) < _size ) {
      //On average there are more than 8 bins per object
      new_expon -= 1;
      if( new_expon < _MIN_EXP ) {
        //Don't let the exponent get too low
        new_expon = _MIN_EXP;
      }
    }
    if( new_expon != _expon ) {
#if DEBUG
      Console.WriteLine("Rebalance: go");
#endif
      ArrayList all_elements = new ArrayList();
      foreach(IList l in _table) {
        if( l != null ) {
	  all_elements.AddRange(l);
        }
      }
      //Add all the elements into the reseted table
      Init(new_expon);
      foreach(Element e in all_elements) {
        object key = e.Key;
        if( key != null ) {
          //Make sure the key doesn't disappear until it is added
          //Make sure not to rebalance which could put us into a loop
	  //Also make sure we don't add the same key twice
          this.Add(key, e.Value, false, false);
        }
      }
    }
  }
#if BRUNET_NUNIT
  [Test]
  /**
   * NUnit test method
   */
  public void Test() {
    /*
     * Here are some very basic tests
     */
    Hashtable ht = new Hashtable();
    WeakHashtable wht = new WeakHashtable();
    
    /*
     * Check to see if it fails when working as a basic
     * Hashtable.
     */
    /*
     * Make sure we can't add a null key
     */
    bool can_add_null = true;
    try {
      wht[ null ] = "oh no";
    }
    catch(ArgumentNullException x) {
     can_add_null = false;
    }
    Assert.IsFalse(can_add_null, "Null key test");
    /*
     * Check to see that we can't Add() the same key twice:
     */
    bool can_add_twice = true;
    object key2 = "test";
    try {
      wht.Add(key2, "first");
      wht.Add(key2, "second");
    }
    catch(ArgumentException x) {
      can_add_twice = false;
    }
    Assert.IsFalse(can_add_twice, "double Add");
    /*
     * Check to see that we can set twice:
     */
    wht[key2] = "first";
    wht[key2] = "second";
    Assert.AreEqual("second", wht[key2], "double set");
    wht.Clear();
    Assert.AreEqual( wht.Count, 0 );
    /*
     * Put in a bunch of random keys
     */
    Random r = new Random();
    for(int i = 0; i < 1000; i++) {
      //Use a reference type here:
      object key = r.Next().ToString();
      int val = r.Next();
      ht[key] = val;
      wht[key] = val;
    }
    IDictionaryEnumerator enm = ht.GetEnumerator();
    while( enm.MoveNext() ) {
      Assert.AreEqual( enm.Value, wht[ enm.Key ], "basic hashtable test" );
    }
    /*
     * Let's remove all the elements we put in earlier:
     */
    enm = ht.GetEnumerator();
    while( enm.MoveNext() ) {
      wht.Remove( enm.Key );
    }
    Assert.AreEqual( 0, wht.Count, "Zero count after removal");

  }
#endif
}

}
