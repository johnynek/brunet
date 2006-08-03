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
  protected object _sync;
  protected bool _cleaning;
  protected Random _rand;

  public Cache(int max_size) {
    _ht = new Hashtable();
    _max_size = max_size;
    _sync = new object();
    _cleaning = false;
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
    bool start_bal = false;
    if( !_cleaning ) {
      lock( _sync ) {
        _ht[key] = val;
        if( _ht.Count > _max_size ) {
          _cleaning = true;
          start_bal = true;
        }
      }
    }
    if ( start_bal ) {
      //Start balancing
      ThreadPool.QueueUserWorkItem(this.Clean);
    }
  }
  public void Clear() {
    lock( _sync ) {
      _ht.Clear();
    }
  }

  public object Get(object key) {
    object res = null;
    if( !_cleaning ) {
      lock( _sync ) {
        res = _ht[key]; 
      }
    }
    return res;
  }

  /**
   * Right now, just remove a random half of the elements
   */
  protected void Clean(object state) {
    lock( _sync ) {
      IDictionaryEnumerator en = _ht.GetEnumerator();
      ArrayList to_remove = new ArrayList();
      while( en.MoveNext() ) {
        if( _rand.Next(2) == 0 ) {
          //Remove this item
          to_remove.Add( en.Key );
        }
      }
      foreach(object key in to_remove) {
        _ht.Remove(key);
      }
      //We are done now
      _cleaning = false; 
    }
  }
}

}
