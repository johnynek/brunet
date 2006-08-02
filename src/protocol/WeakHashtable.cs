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

namespace Brunet {
  /**
   * Keeps a weak reference to the key, but a normal
   * reference to the value.
   *
   * @todo make this a real .Net IDictionary
   */
public class WeakHashtable {

  protected object[] _table;
  
  protected class Element {
    protected System.WeakReference _key;
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
    public object Value {
      get {
        return _value;
      }
      set {
        _value = value;
      }
    }
    protected int _hc;

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

  protected int _count;
  protected int _expon;
  protected int _size;
  protected int _mask;
  protected static readonly int _MIN_EXP = 6;
  
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
  public void Add(object key, object val) {
    //By default, allow the system to rebalance
    this.Add(key, val, true);
  }
  /**
   * Add (or replace) this key value pair
   */
  protected void Add(object key, object val, bool rebalance) {
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
    else {
      //This is a replacement
      old.Value = val;
    }
    Rebalance();
#if DEBUG
    Console.WriteLine("Done: Add");
#endif
  }
  protected Element GetElement(object key) {
#if DEBUG
    Console.WriteLine("WeakHashtable.GetElement({0})", key);
#endif
    int hc = key.GetHashCode();
    Element res = null;
    //Get the list here.
    ArrayList l = (ArrayList)_table[ hc & _mask ];
    if( l != null ) {
      ArrayList to_remove = new ArrayList();
      foreach(Element e in l) {
        if( key.Equals( e.Key ) ) {
          res = e;
        }
        if( e.Key == null ) {
          to_remove.Add(e);
        }
      }
      foreach(Element e in to_remove) {
        l.Remove(e);
        _count--;
      }
      if( l.Count == 0 ) {
        _table[ hc & _mask ] = null;
      }
    }
#if DEBUG
    Console.WriteLine("Done: GetElement: {0}", res);
#endif
    return res;
  }

  public void Remove(object key) {
#if DEBUG
    Console.WriteLine("WeakHashtable.Remove({0})", key);
#endif
    int hc = key.GetHashCode();
    //Get the list here.
    ArrayList l = (ArrayList)_table[ hc & _mask ];
    if( l != null ) {
      ArrayList to_remove = new ArrayList();
      int count = l.Count;
      for(int i=0; i < count; i++) {
        Element e = (Element)l[i];
        object this_key = e.Key;
        if( (this_key == null ) || (key.Equals(this_key) ) ) {
          //Remove it:
          to_remove.Add(i);
        }
      }
      foreach(int i in to_remove) {
        l.RemoveAt(i);
        //Here we are shrinking...
        _count--;
      }
      if( l.Count == 0 ) {
        //Clean up this element
        _table[ hc & _mask ] = null;
      }
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
      Add(key, value);
#if DEBUG
    Console.WriteLine("Done set");
#endif
    }
  }

  protected void Rebalance() {
#if DEBUG
    Console.WriteLine("Rebalance");
#endif
    int new_expon = _expon;
    if( ( _count > ( 4 * _size ) ) ) {
      //On average, there are 4 elements in each bin.
      //go up by a factor of 8 in size
      new_expon += 3; 
    }
    else if( (4 * _count) < _size ) {
      //On average there are 4 bins per object
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
          foreach(Element e in l) {
            all_elements.Add(e);
          }
        }
      }
      //Add all the elements into the reseted table
      Init(new_expon);
      foreach(Element e in all_elements) {
        object key = e.Key;
        if( key != null ) {
          //Make sure the key doesn't disappear until it is added
          //Make sure not to rebalance which could put us into a loop
          this.Add(key, e.Value, false);
        }
      }
    }
  }
}

}
