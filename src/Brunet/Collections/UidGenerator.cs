/*
Copyright (C) 2009  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
