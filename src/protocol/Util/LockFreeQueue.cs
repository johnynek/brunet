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
  //This is used to denote an Element that has been removed from the list
  protected static readonly Element<T> REMOVED = new Element<T>();

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
    Element<T> old_tail = _tail;
    Element<T> old_next;
    Element<T> tmp_tail;

    Element<T> el = new Element<T>();
    el.Data = item;
    while(true) {
      /*
       * We optimistically try to do the enqueue rather than checking to see
       * that tail might be out of date.  If it is out of date, we'll find out
       * below
       */
      old_next = Interlocked.CompareExchange<Element<T>>(ref old_tail.Next, el, null);
      if( old_next == null ) {
        //This is success!
        //Try to update the tail, if it doesn't work, the next Enqueue will get it
        Interlocked.CompareExchange<Element<T>>(ref _tail, el, old_tail);
        return;
      }
      else {
        /*
         * Looks like the tail is out of date, try to update it with the next
         * item after the out of date tail
         */
        tmp_tail = old_tail;
        old_tail = Interlocked.CompareExchange<Element<T>>(ref _tail, old_next, tmp_tail);
        //Set old_tail to the most up-to-date value
        if( tmp_tail == old_tail ) {
          old_tail = old_next;
        }
      }
    }
  }
  
  /** Dequeue if possible and return if the queue is now empty
   * @param success is true if the dequeue is successful
   * @return the next item in the queue
   */
  public T TryDequeue(out bool success) {
    Element<T> head;
    Element<T> tail;
    //This is the only place we read _head outside of a Interlocked call
    //By the time we read these, they may be old:
    Element<T> old_tail = _tail;
    Element<T> old_head = _head;
    Element<T> old_head_next;
    success = false;
    do {
      head = old_head;
      old_head_next = old_head.Next;
      if( old_head_next == null ) {
        /*
         * Make sure to never set _head to null
         */
        return default(T);
      }
      else {
        if( old_head != old_tail ) {
          old_head = Interlocked.CompareExchange<Element<T>>(ref _head, old_head_next, head);
          /*
           * Either head == old_head and we moved, or we just read a new value
           * for _head and we can try again:
           */
          success = (head == old_head);
        }
        else {
        /*
         * head.Next exists, but tail is pointing at head, let's try to
         * advance tail.
         * 
         * We don't advance head if head == _tail, because we don't want head past
         * tail.  This can happen if a TryDequeue happens concurrently with an
         * Enqueue
         */
          tail = old_tail;
          old_tail = Interlocked.CompareExchange<Element<T>>(ref _tail, old_head_next, tail);
          //Make old_tail as current as we can see:
          if( old_tail == tail ) {
            old_tail = old_head_next;
          }
        }
      }
    } while(false == success);
    T result = old_head_next.Data;
    /*
     * Now, no one will ever touch this field again, so
     * let's go ahead and null it out to possibly help
     * in garbage collection.  In benchmarks with mono
     * if we don't do this, it's easy for the memory
     * to get filled up due to the difficulty of collecting
     * a linked list.
     */
    old_head_next.Data = default(T);
    head.Next = REMOVED;
    return result;
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
public class LFBlockingQueue<T> {

  protected readonly LockFreeQueue<T> _lfq;
  protected int _count;
  public int Count { get { return _count; } }
  protected readonly AutoResetEvent _are;

  public LFBlockingQueue() {
    _lfq = new LockFreeQueue<T>();
    _count = 0;
    _are = new AutoResetEvent(false);
  }

  /** Safe for multiple threads to call simulataneously
   */
  public int Enqueue(T a) {
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
  public T Dequeue(int millisec, out bool timedout) {
    bool success;
    T result = _lfq.TryDequeue(out success); 
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
  protected static System.Random _rand = new System.Random();

  public class SimpleReader {
    public int Reads;
    protected LockFreeQueue<object> _q;
    protected readonly object _stopval;

    public SimpleReader(object stopval, LockFreeQueue<object> q) {
      Reads = 0;
      _q = q;
      _stopval = stopval;
    } 

    public void Run() {
      object val;
      bool suc;
      bool stop = false;
      do {
        val = _q.TryDequeue(out suc);
        if( suc ) {
          Reads++;
          stop = (val == _stopval);
        }
      } while(false == stop);
      _q.Enqueue(_stopval); 
    }
  }
  public class SimpleWriter {
    public int Writes;
    protected object _val;
    protected LockFreeQueue<object> _q;

    public SimpleWriter(int writes, object val, LockFreeQueue<object> q) {
      Writes = writes;
      _q = q;
      _val = val;
    } 

    public void Run() {
      for(int i = 0; i < Writes; i++) {
        _q.Enqueue(_val);
      }
    }
  }
  /*
   * This test can be used to make sure the GC can keep up
   * with the allocations in a LockFreeQueue
   */
  //[Test]
  public void LFQMemTest() {
    /*
     * We dump a huge amount of data into the queue and
     * make sure memory doesn't blow up
     */
    LockFreeQueue<object> queue = new LockFreeQueue<object>();
    SimpleReader read = new SimpleReader(null, queue);
    //We should try about 2^30 so make sure we would fill nearly fill the
    //memory if there was a leak
    int runs = 1 << 30;
    SimpleWriter write = new SimpleWriter(runs, new object(), queue);
    Thread rt = new Thread(read.Run);
    Thread wt = new Thread(write.Run);
    rt.Start();
    wt.Start();
    wt.Join();
    //Put in the stop value:
    queue.Enqueue(null);
    rt.Join();
    //We added one more item (null)
    Assert.AreEqual(write.Writes + 1, read.Reads, "Writes equals reads");
  }

  [Test]
  public void SimpleQueueTest() {
    LockFreeQueue<object> lfqo = new LockFreeQueue<object>();
    LockFreeQueue<int> lfqi = new LockFreeQueue<int>();
    Queue q = new Queue();
    Random r = _rand;
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

    public Writer(int max, LockFreeQueue<object> q) {
      _q = q;
      _count = max;
      Items = new Dictionary<object,object>();
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

    public Reader(object stop, LockFreeQueue<object> q) {
      _q = q;
      Items = new List<object[]>();
      _stop = stop;
    }
    public void Run() {
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
    MultiTester(1, 1);
    MultiTester(2, 1);
    MultiTester(4, 1);
    MultiTester(8, 1);
    MultiTester(16, 1);
    MultiTester(32, 1);
  }
  [Test]
  public void MultipleReaderTest() {
    MultiTester(1, 1);
    MultiTester(1, 2);
    MultiTester(1, 4);
    MultiTester(1, 8);
    MultiTester(1, 16);
    MultiTester(1, 32);
  }
  [Test]
  public void MultipleReaderWriterTest() {
    MultiTester(1,1);
    MultiTester(2,2);
    MultiTester(4,4);
    MultiTester(8,8);
    MultiTester(16,16);
  }

  public static void Shuffle<T>(IList<T> l) {
    T o_tmp;
    int length = l.Count;
    int idx_swp;
    for(int i = 0; i < (length-1); i++) {
      idx_swp = _rand.Next(i, length);
      if( idx_swp != i ) {
        o_tmp = l[i];
        l[i] = l[idx_swp];
        l[idx_swp] = o_tmp; 
      }
    }
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
    //Start them in a random order:
    Shuffle(allthreads);
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
    foreach(Reader r in readers) {
      foreach(object[] o in r.Items) {
        read_items++;
        Writer w = (Writer)o[0];
        object data = o[1];
        Assert.AreEqual(w.Items[data], data, "matching data");
        w.Items.Remove(data);
      }
    }
    Assert.AreEqual(read_items, MAX_WRITES * WRITERS, "All writes read");
    foreach(Writer w in writers) {
      Assert.AreEqual(0, w.Items.Count, "Dequeued all items");
    }
  }

  protected class BQWriter {
    public readonly Dictionary<object, object> Items;
    protected readonly int _count;
    protected readonly LFBlockingQueue<object> _q;

    public BQWriter(int max, LFBlockingQueue<object> q) {
      _q = q;
      _count = max;
      Items = new Dictionary<object,object>();
    }

    public void Run() {
      for(int i = 0; i < _count; i++) {
        object o = new object();
        Items[o] = o;
        _q.Enqueue(new object[]{this, o});
      }
    }
  }
  protected class BQReader {
    public readonly List<object[]> Items;
    protected readonly object _stop;
    protected readonly LFBlockingQueue<object> _q;

    public BQReader(object stop, LFBlockingQueue<object> q) {
      _q = q;
      Items = new List<object[]>();
      _stop = stop;
    }
    public void Run() {
      bool timedout;
      object next;
      do {
        next = _q.Dequeue(500, out timedout);
        if( false == timedout ) {
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
  public void LFBlockingQueueTest() {
    MultiBQTester(1);
    MultiBQTester(2);
    MultiBQTester(4);
    MultiBQTester(8);
    MultiBQTester(16);
  }
  public void MultiBQTester(int WRITERS) {
    int MAX_WRITES = 50000;
    object stop = new object();
    LFBlockingQueue<object> q = new LFBlockingQueue<object>();

    List<Thread> wthreads = new List<Thread>();
    List<BQWriter> writers = new List<BQWriter>();
    for(int i = 0; i < WRITERS; i++) {
      BQWriter w = new BQWriter(MAX_WRITES, q);
      writers.Add(w);
      Thread t = new Thread(w.Run);
      wthreads.Add( t );
    }
    foreach(Thread t in wthreads) {
      t.Start();
    }
    BQReader r = new BQReader(stop, q);
    Thread read_thread = new Thread(r.Run);
    read_thread.Start();
    //Now, let's make sure all the writers are done:
    foreach(Thread t in wthreads) {
      t.Join();
    }
    //Now put the stop object in:
    q.Enqueue(stop);
    //That should stop all the reader threads:
    read_thread.Join();
    int read_items = 0;
    foreach(object[] o in r.Items) {
      read_items++;
      BQWriter w = (BQWriter)o[0];
      object data = o[1];
      Assert.AreEqual(w.Items[data], data, "matching data");
      w.Items.Remove(data);
    }
    Assert.AreEqual(read_items, MAX_WRITES * WRITERS, "All writes read");
    foreach(BQWriter w in writers) {
      Assert.AreEqual(0, w.Items.Count, "Dequeued all items");
    }
    bool timedout;
    //Take out the stop object:
    object s = q.Dequeue(500,out timedout);
    Assert.IsFalse(timedout, "stop containing queue timed out");
    Assert.AreEqual(s, stop, "Stop removed");
    q.Dequeue(500,out timedout);
    Assert.IsTrue(timedout, "Empty queue timed out");
  }

}
#endif

}
