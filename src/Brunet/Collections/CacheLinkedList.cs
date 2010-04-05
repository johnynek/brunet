/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 David Wolinsky <davidiw@ufl.edu>, University of Florida

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
