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

using System;
using System.Collections;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet {

/**
 * This is a simple tuple-like object to allow
 * caching based on a equality of an ordered list.
 * 
 * The hashcode is taken from the first object, so
 * that one should have the "best" hashing function
 * to improve efficiency.
 *
 * You can change the elements of Objects, but you shouldn't
 * if the CacheKey is already in a cache.  You might do this
 * if you want to allocate one fixed CacheKey and change it
 * for each item you want to look up.
 */
public class CacheKey {
  public readonly object[] Objects;

  public CacheKey(params object[] objs) {
    Objects = objs;
  }

  public override int GetHashCode() {
    return Objects[0].GetHashCode();
  }

  public override bool Equals(object o) {
    if( this == o ) { return true; }
    CacheKey other = o as CacheKey;
    if( other == null ) { return false; }
    if( Objects.Length != other.Objects.Length ) { return false; }
    int i = 0;
    bool same = true;
    while( same && i < Objects.Length ) {
      if( Objects[i] != null ) {
        same = Objects[i].Equals( other.Objects[i] );
      }
      else {
        same = (other.Objects[i] == null);
      }
      i++;
    }
    return same;
  }
}

/**
 * This is a simple Hashtable-like Cache.  You set
 * a maximum size and it will keep at most that many
 * elements based on usage.
 *
 * Accessing elements should be as fast as accessing from 
 * a hashtable (O(1) time).
 *
 * @todo it would be nice to have a disk based cache that was
 * a subclass of cache, and the ability to chain memory and disk caches together
 */
public class Cache : IEnumerable {

  protected Hashtable _ht;
  protected Entry _head;
  protected Entry _tail;

  protected int _max_size;
  protected int _current_size;
  public int Count {
    get {
#if BRUNET_NUNIT
      Assert.AreEqual( _ht.Count, _current_size, "Hashtable and Cache Count equality");
#endif
      return _current_size;
    }
  }

  /**
   * When an entry is evicted from the cache,
   * we use this class to pass the item
   */
  public class EvictionArgs : System.EventArgs {
    public readonly object Key;
    public readonly object Value;
    public EvictionArgs(object key, object val) {
      Key = key;
      Value = val;
    }
  }

  /**
   * When a miss occurs in the cache, we use this class
   * to pass the key
   */
  public class MissArgs : System.EventArgs {
    public readonly object Key;
    public MissArgs(object key) {
      Key = key;
    }
  }

  /**
   * We store an ordered doublely-linked list
   * to make removing entries fast
   */
  protected class Entry {
    public object Key;
    public object Value;
    public Entry Previous;
    public Entry Next;
  }

  /**
   * When an item is evicted from the cache, 
   * this method is called.
   */
  public event EventHandler EvictionEvent;

  /**
   * When an miss occurs in the cache, 
   * this method is called.
   */
  public event EventHandler MissEvent;

  public Cache(int max_size) {
    //Bias towards being faster at the expense of using more memory
    float load = 0.15f;
    _current_size = 0;
    _ht = new Hashtable(_max_size, load);
    _max_size = max_size;
  }

  public object this[object key] {
    get {
      return Get(key);
    }
    set {
      Add(key, value);
    }
  }

  public void Add(object key, object val) {
    Entry e = (Entry)_ht[key];
    if( e != null ) {
      //This item is already in the cache, remove it from the list:
      RemoveFromList(e);
    }
    else {
      /*
       * If we are evicting something from the Cache,
       * we can reuse the Entry so we don't have to
       * allocate a new object and garbage collect the
       * old one.
       */
      if( _current_size >= _max_size ) {
        //Remove the oldest item, and we'll reuse the entry
        e = Pop();
        _ht.Remove(e.Key);
        //Let someone know there has been an eviction
        if( EvictionEvent != null ) {
          EvictionEvent(this, new EvictionArgs(e.Key, e.Value));
        }
      }
      else {
        //There is no eviction, so we need to make a new entry:
        e = new Entry();
      }
      e.Key = key;
      _ht[key] = e;
    }
    //Now put it as the last entry in the list:
    e.Value = val;
    PushBack(e);
  }

  /**
   * @return true if the Cache has an entry for this key
   */
  public bool Contains(object key) {
    return _ht.Contains(key);
  }

  public void Clear() {
    _ht.Clear();
    _head = null;
    _tail = null;
    _current_size = 0;
  }

  public object Get(object key) {
    Entry e = (Entry)_ht[key];
    if(e == null && MissEvent != null) {
      MissEvent(this, new MissArgs(key));
      e = (Entry)_ht[key];
    }
    if( e != null ) {
      if( e != _tail ) {
        //Make it the tail
        RemoveFromList(e);
        PushBack(e);
      }
      return e.Value;
    }
    else {
      return null;
    }
  }
 
  /**
   * Support for enumerating all values in the cache
   * This returns an Enumerator of DictionaryEntry objects
   * This goes in order from most recently used to least
   * recently used.
   *
   * It is safe to Remove keys while iterating, it won't
   * invalidate the IEnumerator.
   */
  public IEnumerator GetEnumerator() {
    Entry next = _tail;
    while( next != null ) {
      object key = next.Key;
      object val = next.Value;
      next = next.Previous;
      yield return new DictionaryEntry(key,val);
    }
  }

  //Remove the first element from the list:
  protected Entry Pop() {
    Entry ret_val = _head;
    if( _head != null ) {
      RemoveFromList(_head);
    }
    return ret_val;
  }

  //Add this entry as the list element in the list:
  protected void PushBack(Entry e) {
    _current_size++;
    if( _tail != null ) {
      _tail.Next = e;
    }
    if( _head == null ) {
      _head = e;
    }
    e.Previous = _tail;
    e.Next = null;
    _tail = e;
  }

  protected void RemoveFromList(Entry e) {
    _current_size--;
    Entry prev = e.Previous;
    Entry next = e.Next;
    e.Previous = null;
    e.Next = null;
    if( prev != null ) {
      prev.Next = next;
    }
    if( next != null ) {
      next.Previous = prev;
    }
    if( _head == e ) {
      _head = next;
    }
    if( _tail == e ) {
      _tail = prev;
    }
  }

  /**
   * Removes an item from the Cache.
   * This *does not* trigger a EvictionEvent,
   * @param key the key for the entry being remove
   * @return the value associated with that key.
   */
  public object Remove(object key) {
    Entry e = (Entry)_ht[key];
    object ret_val = null;
    if( e != null ) {
      ret_val = e.Value;
      e.Value = null;
      e.Key = null;
      _ht.Remove(key);
      RemoveFromList(e);
    }
    return ret_val;
  }
}

#if BRUNET_NUNIT
[TestFixture]
public class CacheTest {

  [Test]
  public void CacheKeyTest() {
    CacheKey test1 = new CacheKey("hello", "this", "is", "a", "test");
    CacheKey test2 = new CacheKey("hello", "this", "is", "a", "test");
    CacheKey test3 = new CacheKey("hello", "this", "is", "not", "a", "test");
    CacheKey test4 = new CacheKey("hello", "this", "is", "a different", "test");
    Assert.AreEqual(test1.GetHashCode(), test2.GetHashCode(), "CacheKey Hashcode equality");
    Assert.AreEqual(test1, test2, "CacheKey equality");
    Assert.IsFalse(test1.Equals(test3), "CacheKey non-equality");
    Assert.IsFalse(test2.Equals(test3), "CacheKey non-equality");
    Assert.IsFalse(test1.Equals(test4), "CacheKey non-equality");
    Assert.IsFalse(test2.Equals(test4), "CacheKey non-equality");
    Assert.IsFalse(test3.Equals(test4), "CacheKey non-equality");
  }

  [Test]
  public void TestUpdate() {
    Cache c = new Cache(16);
    object entry = new object();
    object first = new object();
    object second = new object();
    c[entry] = first;
    c[entry] = second;
    Assert.IsTrue(c[entry].Equals(second), "Entry equals second");
  }

  [Test]
  public void TestRecall() {
    const int MAX_SIZE = 100;
    Random r = new Random();
    Cache c = new Cache(MAX_SIZE);
    Hashtable ht = new Hashtable();
    for(int i = 0; i < MAX_SIZE; i++) {
      int k = r.Next();
      int v = r.Next();
      ht[k] = v;
      c[k] = v;
    }
    IDictionaryEnumerator ide = ht.GetEnumerator();
    while(ide.MoveNext()) {
      int key = (int)ide.Key;
      int val = (int)ide.Value;
      object c_val = c[key];
      Assert.AreEqual(c_val, val, "Test lookup");
    }
  }
  [Test]

  public void TestEnumeration() {
    const int MAX_SIZE = 100;
    Random r = new Random();
    Cache c = new Cache(MAX_SIZE);
    Hashtable ht = new Hashtable();
    for(int i = 0; i < MAX_SIZE; i++) {
      int k = r.Next();
      int v = r.Next();
      ht[k] = v;
      c[k] = v;
    }
    int enum_count = 0;
    foreach(DictionaryEntry de in c) {
      Assert.IsNotNull( c[de.Key], "Enumeration");
      enum_count++;
    }
    Assert.AreEqual(enum_count, c.Count, "Enumeration count");
    //Remove a bunch at random:
    ArrayList removed = new ArrayList();
    for(int i = 0; i < MAX_SIZE / 2; i++) {
      object k = r.Next(0, MAX_SIZE);
      removed.Add( k );
      c.Remove( k );
    }
    //Make sure they are really gone:
    enum_count = 0;
    foreach(DictionaryEntry de in c) {
      Assert.IsNotNull( c[de.Key], "Enumeration after remove");
      enum_count++;
    }
    Assert.AreEqual(enum_count, c.Count, "Enumeration count after remove");
    foreach(object k in removed) {
      Assert.IsNull(c[k], "removed objects removed");
    }
    //Let's enumerate and removed:
    foreach(DictionaryEntry de in c) {
      c.Remove(de.Key);
      Assert.IsNull( c[de.Key], "Removing with enumeration");
    }
    Assert.AreEqual(0, c.Count, "Removed everything");
  }

  [Test]
  public void TestEviction() {
    const int MAX_SIZE = 1000;
    Random r = new Random();
    Cache c = new Cache(MAX_SIZE);
    Hashtable ht = new Hashtable();
    Hashtable ht_evicted = new Hashtable();
    EventHandler eh = delegate(object o, EventArgs args) {
      Cache.EvictionArgs a = (Cache.EvictionArgs)args;
      ht_evicted[a.Key] = a.Value;
    };
    c.EvictionEvent += eh;

    int i = 0;
    for(i = 0; i < 50 * MAX_SIZE; i++) {
      int v = r.Next();
      ht[i] = v;
      c[i] = v;
      int exp_size = Math.Min(i+1, MAX_SIZE);
      Assert.AreEqual(c.Count, exp_size, "Size check");
      Assert.AreEqual(ht[i], c[i], "equivalence check");
      //Keep the zero'th element in the cache:
      object v_0 = c[0];
      Assert.IsNotNull(v_0, "0th element still in the cache");
    }
    Assert.AreEqual(c.Count, MAX_SIZE, "Full cache"); 
    //Now check that everything is either in the Cache or was evicted:
    IDictionaryEnumerator ide = ht.GetEnumerator();
    while(ide.MoveNext()) {
      int key = (int)ide.Key;
      int val = (int)ide.Value;
      object c_val = c[key];
      if( !c.Contains(key) ) {
        Assert.IsNull(c_val, "Evicted entry is null");
        c_val = ht_evicted[key];
        Assert.AreEqual(c_val, val, "Evicted lookup");
      }
      else {
        //Not in the cache:
        Assert.AreEqual(c_val, val, "Cache lookup");
      }
    }
    //Let's remove from the Cache and see if that worked:
    int s0 = c.Count;
    object rv = c.Remove(0);
    Assert.AreEqual( rv, ht[0], "Removed value matches");
    Assert.IsNull(c[0], "Remove really removed");
    Assert.AreEqual( s0 - 1, c.Count, "Removed decreased size");

  }

}

#endif

}
