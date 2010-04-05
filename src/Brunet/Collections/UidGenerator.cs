/*
Copyright (C) 2009  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

namespace Brunet.Collections {

/**
 * Generates random int ID number
 */
public class UidGenerator<T> : IEnumerable<T> {

  protected readonly Random _rand;
  protected readonly bool _nonneg;
  protected readonly Dictionary<int, T> _obj_map;

  public UidGenerator(Random r) : this(r, false) { }

  public UidGenerator(Random r, bool nonneg) {
    _rand = r;
    _nonneg = nonneg;
    _obj_map = new Dictionary<int, T>();
  }

  /**
   * This method is not thread-safe.  You need to handle it!
   * Only one call to this at a time is safe.
   * this never returns 0, so 0 can we used as a special value
   * @param obj the object to generate an ID for and store.
   * @return the new, unique ID, for obj
   */
  public int GenerateID(T obj) {
    int new_id;
    do {
      new_id = _rand.Next();
      if( _nonneg && new_id < 0 ) {
        new_id = ~new_id;
      }
    } while( new_id == 0 || _obj_map.ContainsKey(new_id) );
    //Awesome! this is a new ID:
    _obj_map.Add(new_id, obj);
    return new_id;
  }

  public Pair<int,T> GenerateID(System.Converter<int, T> genfun) {
    int new_id;
    do {
      new_id = _rand.Next();
      if( _nonneg && new_id < 0 ) {
        new_id = ~new_id;
      }
    } while( new_id == 0 || _obj_map.ContainsKey(new_id) );
    //Awesome! this is a new ID:
    T obj = genfun(new_id);
    _obj_map.Add(new_id, obj);
    return new Pair<int, T>(new_id, obj);
  }

  /** Enumerate over all the values
   */
  public IEnumerator<T> GetEnumerator() {
    return _obj_map.Values.GetEnumerator(); 
  }

  IEnumerator IEnumerable.GetEnumerator() {
    return _obj_map.Values.GetEnumerator(); 
  }

  /**
   * @return false if there is no matching ID
   */
  public bool TryGet(int id, out T obj) {
    return _obj_map.TryGetValue(id, out obj);
  }

  /**
   * This method is not thread-safe.  You need to handle it!
   * Only one call to this at a time is safe.
   *
   * @param id the id of the object to take out
   * @param obj the object we are taking
   * @return true if the object was released
   */
  public bool TryTake(int id, out T obj) {
    if( _obj_map.TryGetValue(id, out obj) ) {
      _obj_map.Remove(id);
      return true;
    }
    else {
      return false;
    }
  }
}

}
