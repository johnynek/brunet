/*
Copyright (C) 2008  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System.Threading;
#if BRUNET_NUNIT
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
#endif

namespace Brunet.Util
{

/** A thread-safe lock-free Queue.
 *
 * This queue supports arbitrary readers (dequeue) and writers (enqueue).
 */
public class LockFreeQueue<T> {

  /*
   * A standard linked-list data structure
   */
  protected class Element<R> {
    public R Data;
    public Element<R> Next;
  }

  protected Element<T> _head;
  protected Element<T> _tail;

  public LockFreeQueue() {
    /*
     * Head and tail are never null.  Empty is when they
     * point to the same element and _head.Next is null.
     */
    _head = new Element<T>();
    _tail = _head;
  }

  
  /** "traditional" dequeue, throws an exception if empty
   * @return the next item in the queue
   * @throws InvalidOperationException if the queue is empty
   */
  public T Dequeue() {
    bool success;
    T res = TryDequeue(out success);
    if(success) {
      return res;
    }
    else {
      throw new InvalidOperationException("Queue is empty");
    }
  }

  /** Enqueue an item.
   */
  public void Enqueue(T item) {
    Element<T> old_next;
    Element<T> old_tail;
    bool done = false;

    Element<T> el = new Element<T>();
    el.Data = item;
    do {
      old_tail = _tail;
      old_next = old_tail.Next;
      if( old_next == null ) {
        //We can try to update
        done = null == Interlocked.CompareExchange<Element<T>>(ref _tail.Next, el, null);
      }
      else {
        //Looks like the tail is out of date
        Interlocked.CompareExchange<Element<T>>(ref _tail, old_next, old_tail);
      }
    } while(false == done);
    //Try to update the tail, if it doesn't work, the next Enqueue will get it
    Interlocked.CompareExchange<Element<T>>(ref _tail, el, old_tail);
  }
  
  /** Dequeue if possible and return if the queue is now empty
   * @param success is true if the dequeue is successful
   * @return the next item in the queue
   */
  public T TryDequeue(out bool success) {
    /*
     * move the head
     */
    Element<T> old_head;
    Element<T> old_head_next;
    bool done = false;
    success = true;
    do {
      old_head = _head;
      old_head_next = old_head.Next;
      if( old_head_next == null ) {
        success = false;
        return default(T);
      }
      else {
        //Just move the head 
        done = Interlocked.CompareExchange<Element<T>>(ref _head, old_head_next, old_head)==old_head;
      }
    } while(false == done);
    return old_head_next.Data;
  }

}

/** Reads from only one thread are safe, but multiple threads can write
 */
public class SingleReaderLockFreeQueue<T> {
  
  protected class Element<R> {
    public R Data;
    public Element<R> Next;
  }

  protected Element<T> _head;
  protected Element<T> _tail;

  public SingleReaderLockFreeQueue() {
    _head = new Element<T>();
    _tail = _head;
  }
  
  /** "traditional" dequeue, throws an exception if empty
   * @return the next item in the queue
   * @throws InvalidOperationException if the queue is empty
   */
  public T Dequeue() {
    bool success;
    T res = TryDequeue(out success);
    if(success) {
      return res;
    }
    else {
      throw new InvalidOperationException("Queue is empty");
    }
  }
  
  /** Enqueue an item.
   */
  public void Enqueue(T item) {
    Element<T> old_next;
    Element<T> old_tail;
    bool done = false;

    Element<T> el = new Element<T>();
    el.Data = item;
    do {
      old_tail = _tail;
      old_next = old_tail.Next;
      if( old_next == null ) {
        //We can try to update
        done = null == Interlocked.CompareExchange<Element<T>>(ref _tail.Next, el, null);
      }
      else {
        //Looks like the tail is out of date
        Interlocked.CompareExchange<Element<T>>(ref _tail, old_next, old_tail);
      }
    } while(false == done);
    //Try to update the tail, if it doesn't work, the next Enqueue will get it
    Interlocked.CompareExchange<Element<T>>(ref _tail, el, old_tail);
  }

  public T TryDequeue(out bool success) {
    Element<T> old_head_next = _head.Next;
    success = old_head_next != null;
    if( success ) {
      _head = old_head_next;
      return old_head_next.Data;
    }
    else {
      return default(T);
    }
  }

}

/** A BlockingQueue-like object that does not use locks
 * This is a lock-free version of BlockingQueue.  Only safe
 * for a single thread to call Dequeue.
 *
 * It is useful for pushing objects into a thread.  In that case,
 * there will be only a single reader of the queue but multiple writers.
 *
 * It is not yet settled if this is in general faster that BlockingQueue.
 * 
 */
public class LFBlockingQueue {

  protected SingleReaderLockFreeQueue<object> _lfq;
  protected int _count;
  public int Count { get { return _count; } }
  protected readonly AutoResetEvent _are;

  public LFBlockingQueue() {
    _lfq = new SingleReaderLockFreeQueue<object>();
    _count = 0;
    _are = new AutoResetEvent(false);
  }

  /** Safe for multiple threads to call simulataneously
   */
  public int Enqueue(object a) {
    _lfq.Enqueue(a); 
    int count = Interlocked.Increment(ref _count);
    if( count == 1) {
      //We just transitioned from 0->1
      _are.Set();
    }
    return count;
  }

  /** Only one thread should call this at a time
   */
  public object Dequeue(int millisec, out bool timedout) {
    bool success;
    object result = _lfq.TryDequeue(out success); 
    bool had_to_wait = !success;
    if( had_to_wait ) {
      bool cont = true;
      while(cont) {
        bool got_set = _are.WaitOne(millisec, false);
        if( got_set ) {
          result = _lfq.TryDequeue(out success); 
        }
        /*
         * If the _are is set, there should be something to
         * get.  If there is not, it is because there was 
         * a race going on with an overlapping Dequeue and Enqueue.
         * Just try it again in that case.
         */
        bool false_set = got_set && (success == false);
        cont = false_set;
      }
    }
    timedout = success == false;
    if( success ) {
      Interlocked.Decrement(ref _count);
    }
    return result;
  }

}

#if BRUNET_NUNIT
[TestFixture]
public class LockFreeTest {
  [Test]
  public void SimpleQueueTest() {
    LockFreeQueue<object> lfqo = new LockFreeQueue<object>();
    LockFreeQueue<int> lfqi = new LockFreeQueue<int>();
    Queue q = new Queue();
    Random r = new Random();
    //Put in a bunch of random numbers:
    int max = r.Next(2048, 4096);
    for(int i = 0; i < max; i++) {
      int k = r.Next();
      lfqo.Enqueue(k);
      lfqi.Enqueue(k);
      q.Enqueue(k);
    }
    //Now verify that everything is okay:
    bool success;
    int kq;
    int klfo;
    int klfi;
    for(int i = 0; i < max; i++) {
      klfo = (int)lfqo.TryDequeue(out success);
      Assert.IsTrue(success, "Dequeue<o> success");
      
      klfi = lfqi.TryDequeue(out success);
      Assert.IsTrue(success, "Dequeue<i> success");
      
      kq = (int)q.Dequeue();
      Assert.AreEqual(kq, klfo, "LockFreeQueue<object> == Queue");
      Assert.AreEqual(kq, klfi, "LockFreeQueue<int> == Queue");
    }
    try {
      lfqi.Dequeue();
      Assert.IsTrue(false, "LockFreeQueue<int> post-Dequeue exception");
    }
    catch(InvalidOperationException) { Assert.IsTrue(true, "LockFreeQueue<int> exception"); }
    try {
      lfqo.Dequeue();
      Assert.IsTrue(false, "LockFreeQueue<object> post-Dequeue exception");
    }
    catch(InvalidOperationException) { Assert.IsTrue(true, "LockFreeQueue<int> exception"); }
  }

  protected class Writer {
    public readonly Dictionary<object, object> Items;
    protected readonly int _count;
    protected readonly LockFreeQueue<object> _q;
    protected int _starts;
    public int Starts { get { return _starts; } }

    public Writer(int max, LockFreeQueue<object> q) {
      _q = q;
      _count = max;
      Items = new Dictionary<object,object>();
      _starts = 0;
    }

    public void Run() {
      for(int i = 0; i < _count; i++) {
        object o = new object();
        Items[o] = o;
        _q.Enqueue(new object[]{this, o});
      }
    }
  }
  protected class Reader {
    public readonly List<object[]> Items;
    protected readonly object _stop;
    protected readonly LockFreeQueue<object> _q;
    protected int _empties;
    public int Empties { get { return _empties; } }

    public Reader(object stop, LockFreeQueue<object> q) {
      _q = q;
      Items = new List<object[]>();
      _stop = stop;
      _empties = 0;
    }
    public void Run() {
      bool empty;
      bool success;
      object next;
      do {
        next = _q.TryDequeue(out success);
        if( success ) {
          if( next != _stop ) {
            Items.Add((object[])next);
          }
        }
      } while( next != _stop );
      //Put stop back in:
      _q.Enqueue(_stop);
    }
  }

  [Test]
  public void MultipleWriterTest() {
    int WRITERS = 15;
    MultiTester(WRITERS,1);
  }
  [Test]
  public void MultipleReaderTest() {
    int READERS = 15;
    MultiTester(1, READERS);
  }
  [Test]
  public void MultipleReaderWriterTest() {
    int WRITERS = 15;
    int READERS = 15;
    MultiTester(WRITERS,READERS);
  }

  public void MultiTester(int WRITERS, int READERS) {
    int MAX_WRITES = 50000;
    object stop = new object();
    LockFreeQueue<object> q = new LockFreeQueue<object>();

    List<Thread> rthreads = new List<Thread>();
    List<Thread> wthreads = new List<Thread>();
    List<Thread> allthreads = new List<Thread>();
    List<Writer> writers = new List<Writer>();
    List<Reader> readers = new List<Reader>();
    for(int i = 0; i < WRITERS; i++) {
      Writer w = new Writer(MAX_WRITES, q);
      writers.Add(w);
      Thread t = new Thread(w.Run);
      wthreads.Add( t );
      allthreads.Add(t);
    }
    for(int i = 0; i < READERS; i++) {
      Reader r = new Reader(stop, q);
      readers.Add(r);
      Thread t = new Thread(r.Run);
      rthreads.Add( t );
      allthreads.Add(t);
    }
    foreach(Thread t in allthreads) {
      t.Start();
    }
    //Now, let's make sure all the writers are done:
    foreach(Thread t in wthreads) {
      t.Join();
    }
    //Now put the stop object in:
    q.Enqueue(stop);
    //That should stop all the reader threads:
    foreach(Thread t in rthreads) {
      t.Join();
    }
    int read_items = 0;
    int total_empties = 0;
    foreach(Reader r in readers) {
      total_empties += r.Empties;
      foreach(object[] o in r.Items) {
        read_items++;
        Writer w = (Writer)o[0];
        object data = o[1];
        Assert.AreEqual(w.Items[data], data, "matching data");
        w.Items.Remove(data);
      }
    }
    Assert.AreEqual(read_items, MAX_WRITES * WRITERS, "All writes read");
    int total_starts = 0;
    foreach(Writer w in writers) {
      total_starts += w.Starts;
      Assert.AreEqual(0, w.Items.Count, "Dequeued all items");
    }
    //Each start must eventually result in an emptying of the queue:
    Assert.AreEqual(total_starts, total_empties, "Starts == Empties");
  }
}
#endif

}
