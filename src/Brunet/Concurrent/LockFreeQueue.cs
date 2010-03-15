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

namespace Brunet.Concurrent
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

  /*
   * this inner class represents the state-commit pattern.  You
   * make one immutable class that encapsulates the state machine:
   * it creates new instances for each allowed transition.
   * Then, the outer class: LockFreeQueue keeps one state object
   * and uses CompareExchange to do "commits" of the transactions.
   * It is almost trivial to see that the logic is correc then,
   * and no locking is ever needed.
   * NOTE: this is ALMOST that pattern, the Enqueue action actually
   * changes state but similiar to the WriteOnce variable, so it is
   * you can clearly see that it is logically correct.
   */
  protected class State<R> {
    public readonly Element<R> Head;
    //This is only a hint of where to find the tail
    //The "real tail" is the first null found in linked list started at Head
    public readonly Element<R> TailHint;
    
    public State(Element<R> head, Element<R> tail) {
      Head = head;
      TailHint = tail;
    }

    /*
     * If we return false, we can't dequeue, ignore newstate
     * If we return true, the newstate is computed.  The dequeue
     * is not final until the _state of the LockFreeQueue is updated
     */
    public bool TryDequeue(out State<R> newstate) {
      if( Head.Next == null ) {
        //We can't move forward.
        newstate = null;
        return false;
      }
      else if( Head == TailHint ) {
        //We can move forward, BUT TailHint would be behind Head,
        //update TailHint at the same time
        newstate = new State<R>(Head.Next, Head.Next);
        return true;
      }
      else {
        newstate = new State<R>(Head.Next, TailHint);
        return true; 
      }
    }
    /*
     * If this returns true, the enqueue was successful.  Updating the state
     * should be attempted, but if it fails, we must let a future dequeue or
     * enqueue fix it.  DON'T TRY TO ENQUEUE AGAIN IF THIS RETURNS TRUE,
     * THE DATA HAS ALREADY BEEN ADDED!
     */
    public bool TryEnqueue(Element<R> newtail, out State<R> newstate) {
      var old_next = Interlocked.CompareExchange<Element<R>>(ref TailHint.Next, newtail, null);
      if( old_next == null ) {
        //This was success:
        newstate = new State<R>(Head, newtail);
        return true;
      }
      else {
        //This was a failure, we have a new TailHint
        newstate = new State<R>(Head, old_next);
        return false;
      }
    }
  }

  /*
   * this is the only mutable variable for the queue.
   */
  protected State<T> _state;
  //This is used to denote an Element that has been removed from the list
  protected static readonly Element<T> REMOVED = new Element<T>();

  /** Is the queue currently empty 
   * This method is of limited usefulness since this object
   * is designed to be used from multiple threads.  It is probably
   * better to just TryPeek or TryDequeue and see if it works.
   * Remember: just because it is not empty when the call was made
   * doesn't mean a subsequent Dequeue would succeed (unless you
   * can guarantee there is only one thread dequeueing at a time).
   */
  public bool IsEmpty {
    get {
      return _state.Head.Next == null;
    }
  }

  public LockFreeQueue() {
    /*
     * Head and tail are never null.  Empty is when they
     * point to the same element and _head.Next is null.
     */
     var head = new Element<T>();
    _state = new State<T>(head, head);
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
    var state = _state;
    State<T> new_state;
    State<T> oldstate;

    Element<T> el = new Element<T>();
    el.Data = item;
    bool success;
    do {
      success = state.TryEnqueue(el, out new_state);
      /*
       * We *TRY* to update the state, but since Enqueue can
       * only change the TailHint, and as the name suggests
       * TailHint is only a HINT as to where the tail is,
       * we don't care if this fails.  We only care if the
       * above fails and success is false, which would indicate
       * that the item was not added to the list yet.
       */
      oldstate = Interlocked.CompareExchange<State<T>>(ref _state, new_state, state);
      if( oldstate == state ) {
        //The update worked:
        state = new_state;
      }
      else {
        //Someone else updated first
        state = oldstate;
      }
    } while( false == success );
  }
  
  /** "traditional" Peek, throws an exception if empty
   * @return the next item in the queue
   * @throws InvalidOperationException if the queue is empty
   */
  public T Peek() {
    bool success;
    T res = TryPeek(out success);
    if(success) {
      return res;
    }
    else {
      throw new InvalidOperationException("Queue is empty");
    }
  }
  
  /** Dequeue if possible and return if the queue is now empty
   * @param success is true if the dequeue is successful
   * @return the next item in the queue
   */
  public T TryDequeue(out bool success) {
    //This is the only place we read _state outside of a Interlocked call
    var state = _state;
    State<T> newstate;
    State<T> oldstate;
    success = false;
    do {
      //This doesn't effect the state at all
      if( !state.TryDequeue(out newstate) ) {
        //We couldn't dequeue:
        return default(T);
      }
      //Otherwise, we have try to update the state:
      oldstate = Interlocked.CompareExchange<State<T>>(ref _state, newstate, state);
      if( oldstate == state ) {
        success = true;
      }
      else {
        //Update the state and try again
        state = oldstate;
      }
    } while(false == success);
    /*
     * If we got here, oldstate == state, and newstate was stored as state
     * also, oldstate.Head.Next == newstate.Head
     *
     * Now, no one will ever touch oldstate again, so
     * let's go ahead and null it out to possibly help
     * in garbage collection.  In benchmarks with mono
     * if we don't do this, it's easy for the memory
     * to get filled up due to the difficulty of collecting
     * a linked list.
     */
    T result = newstate.Head.Data;
    oldstate.Head.Next = REMOVED;
    newstate.Head.Data = default(T);
    return result;
  }

  /** Try to see what a TryDequeue would have returned
   */
  public T TryPeek(out bool success) {
    var node = _state.Head.Next;
    success = node != null;
    if( success ) {
      return node.Data;
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

  ~LFBlockingQueue() {
    _are.Close();
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
