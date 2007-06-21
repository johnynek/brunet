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

namespace Brunet {

/**
 * This is an IDictionary optimized for very small dictionaries.
 * It works by using an underlying IList, which should be even
 * in length of key, value pairs.  So, at location 2i and 2i + 1
 * you will find the i^th key and i^th value respectively.
 *
 * This is a read-only collection.  None of these methods change
 * the underlying list.
 */
public class ListDictionary : IDictionary {

  protected readonly IList _list;

  public ListDictionary(IList d) {
    if( _list.Count % 2 != 0 ) {
      throw new Exception("List must contain an even number of items");
    }
    _list = d;
  }
  /*
   * Properties
   */
  public int Count { get { return _list.Count / 2; } }
  public bool IsFixedSize { get { return true; } }
  public bool IsReadOnly  { get { return true; } }
  //This is synchronized because it is read only
  public bool IsSynchronized { get { return true; } }
  public object SyncRoot { get { return _list.SyncRoot; } }

  public object this[object k] {
    get {
      if( k == null ) { throw new ArgumentNullException("Key cannot be null"); }
      IEnumerator e = _list.GetEnumerator();
      while( e.MoveNext() ) {
        object key = e.Current;
        e.MoveNext();
        if( k.Equals( key ) ) {
          //This is it:
          return e.Current;
        }
      }
      return null;
    }
    set {
      throw new NotSupportedException();
    }
  }
  public ICollection Keys {
    get {
      throw new NotSupportedException();
    }
  }
  public ICollection Values {
    get {
      throw new NotSupportedException();
    }
  }
  /*
   * Methods
   */
  public void Add(object k, object v) {
    throw new NotSupportedException();
  }
  public void Clear() {
    throw new NotSupportedException();
  }
  public bool Contains(object k) {
    if( k == null ) { throw new ArgumentNullException("Key cannot be null"); }
    IEnumerator e = _list.GetEnumerator();
    while( e.MoveNext() ) {
      object key = e.Current;
      e.MoveNext();
      if( k.Equals( key ) ) {
        //This is it:
        return true;
      }
    }
    return false;
  }
  public void CopyTo(Array a, int i) {
    _list.CopyTo(a, i);
  }
  IDictionaryEnumerator IDictionary.GetEnumerator() {
    return GetEnumerator();
  }
  IEnumerator IEnumerable.GetEnumerator() {
    return _list.GetEnumerator();
  }
  public IDictionaryEnumerator GetEnumerator() {
    throw new NotSupportedException();
  }
  public void Remove(object k) {
    throw new NotSupportedException();
  }
}

}
