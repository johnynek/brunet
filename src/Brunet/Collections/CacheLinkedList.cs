/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 David Wolinsky <davidiw@ufl.edu>, University of Florida

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
  public class CacheLinkedList<T>: IEnumerable {
    /**
    * We use a max, so that this does not grow to big and create problems for us
    */
    protected int _count = 0;
    public int count { get { return _count; } }
    public static int MAX_COUNT = 2048;

    protected T _head;
    public T Head { get { return _head; } }
    protected CacheLinkedList<T> _tail;
    public CacheLinkedList<T> Tail { get { return _tail; } }

    public CacheLinkedList() {
      throw new Exception("Don't call this directly!");
    }

    public CacheLinkedList(T o) {
      _count = 1;
      _head = o;
      _tail = null;
    }

    /**
    * Makes a new list by appending this new object.
    * Does not change the old list.
    */
    public CacheLinkedList(CacheLinkedList<T> cll, T o) {
      if(cll != null) {
        if(cll.count == MAX_COUNT) {
          cll = Take(cll);
        }
        _count = cll.count + 1;
      }
      else {
        _count = 1;
      }
      _head = o;
      _tail = cll;
    }

    /**
    * Takes the MAX_COUNT / 2 entries at most and returns a new CacheLinkedList
    */
    public static CacheLinkedList<T> Take(CacheLinkedList<T> cll) {
      int count = 0;
      List<T> new_cll = new List<T>(MAX_COUNT / 2);
      while(cll != null && count++ < MAX_COUNT / 2) {
        new_cll.Add(cll.Head);
        cll = cll.Tail;
      }
      cll = null;
      for(int i = new_cll.Count - 1; i >= 0; i--) {
        cll += new_cll[i];
      }
      return cll;
    }

    /**
    * This is syntactic sugar so we can do:
    * cll = cll + object
    * to append a data point.
    */
    public static CacheLinkedList<T> operator + (CacheLinkedList<T> cll, T o) {
      return new CacheLinkedList<T>(cll, o);
    }
    /**
    * This goes from most recent to least recent data point
    */
    public IEnumerator GetEnumerator() {
      CacheLinkedList<T> cll = this;
      do {
        yield return cll.Head;
        cll = cll.Tail;
      }
      while( cll != null );
    }
  }
}
