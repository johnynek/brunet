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
using System.Threading;

namespace Brunet {

/**
 * This is a simple Hashtable-like Cache.  You set
 * a maximum size and it will keep at most that many
 * elements based on usage.
 *
 * @todo improve that caching strategy
 */
public class Cache {

  protected Hashtable _ht;
  protected int _max_size;
  protected Random _rand;

  public Cache(int max_size) {
    //Bias towards being faster at the expense of using more memory
    float load = 0.15f;
    _ht = new Hashtable(_max_size, load);
    _max_size = max_size;
    _rand = new Random();
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
    _ht[key] = val;
    if( _ht.Count > _max_size ) {
      //Start balancing
      ThreadPool.QueueUserWorkItem(this.Clean);
    }
  }
  public void Clear() {
    _ht.Clear();
  }

  public object Get(object key) {
    return _ht[key];
  }

  /**
   * Right now, just remove a random half of the elements
   */
  protected void Clean(object state) {
    try {
      IDictionaryEnumerator en = _ht.GetEnumerator();
      float load = 0.15f;
      Hashtable ht = new Hashtable(_max_size, load);
      while( en.MoveNext() ) {
        if( _rand.Next(2) == 0 ) {
          //Add this item:
          ht[ en.Key ] = en.Value;
        }
      }
      _ht = ht;
    }
    catch(Exception) {
      //If the _ht changes while we are cleaning, we get an exception.
      //Just go on with life, we can finish cleaning later
    }
  }
}
}
