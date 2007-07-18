/*
Copyright (C) 2005-2007  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

//#define DEBUG

using System.Collections;
using System.Threading;
using System;
#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet
{

/**
 * This class offers a means to pass objects in a queue
 * between threads (or in the same thread).  The Dequeue
 * method will block until there is something in the Queue
 *
 * This implementation uses two thread synchronization tools:
 * mutex (locking) and a WaitHandle.  The WaitHandle is in the
 * Set state when there are 1 or more items in the queue.
 * When there are 1 or more items, Enqueue can't change the "Set"
 * state, so there is no need to call Set.  When there are two
 * or more items, Dequeue can't change the set state.  This
 * observation makes the BlockingQueue much faster because in the
 * high throughput case, there is often more than 1 item in the queue,
 * and so we only use the Mutex and never touch the WaitHandle,
 * which can be a little slow (according to testing).
 */
#if BRUNET_NUNIT
[TestFixture]
#endif
public sealed class BlockingQueue {
  
  public BlockingQueue() {
    _re = new AutoResetEvent(false); 
    _closed = false;
    _sync = new object();
    _queue = new Queue();
    _close_on_enqueue = false;
    _waiters = 0;
  }

  ~BlockingQueue() {
    //Make sure the close method is eventually called:
    if( !Closed ) {
      Console.Error.WriteLine("ERROR: BlockingQueue.Close called in Destructor");
    }
    Close();
  }
  protected readonly Queue _queue;
  protected readonly object _sync; 
  protected AutoResetEvent _re;
  protected bool _closed;
  protected bool _close_on_enqueue;
  
  protected int _waiters;

  public bool Closed { get { lock ( _sync ) { return _closed; } } }
  
  public int Count { get { lock ( _sync ) { return _queue.Count; } } }
 
  /**
   * When an item is enqueued, this event is fire
   */
  public event EventHandler EnqueueEvent;
  /**
   * When the queue is closed, this event is fired
   */
  public event EventHandler CloseEvent;
  
  /* **********************************************
   * Here all the methods
   */
  
  /**
   * Once this method is called, and the queue is emptied,
   * all future Dequeue's will throw exceptions
   */
  public void Close() {
    bool fire = false;
    AutoResetEvent re = null;
    EventHandler ch = null;
    lock( _sync ) {
      if( _closed == false ) {
        fire = true;
        _closed = true;
        //Null out some underlying objects:
        re = _re;
        _re = null;
        ch = CloseEvent;
        CloseEvent = null;
      }
    }
    //Fire the close event
    if( fire ) {
      //Set for all the waiting Dequeues:
      while( Thread.VolatileRead( ref _waiters ) > 0 ) {
        re.Set();
      }
      re.Close();
      if( ch != null ) {
        ch(this, EventArgs.Empty);
      }
    }

#if DEBUG
    System.Console.Error.WriteLine("Close set");
#endif
  }

  /**
   * After the next enqueue, close
   * If the queue is already closed, this does nothing.
   * This is totally equivalent to listening to the
   * EnqueueEvent and calling Close on the queue after
   * an Enqueue.
   * @return Closed
   */
  public bool CloseAfterEnqueue() {
    lock( _sync ) {
      if( !_closed ) {
        _close_on_enqueue = true;
        return false;
      }
      else {
        return true;
      }
    }
  }
  
  /**
   * @throw Exception if the queue is closed
   */
  public object Dequeue() {
    bool timedout = false;
    return Dequeue(-1, out timedout);
  }

  /**
   * @param millisec how many milliseconds to wait if the queue is empty
   * @param timedout true if we have to wait too long to get an object
   * @return the object, if we timeout we return null
   * @throw Exception if the BlockingQueue is closed and empty
   */
  public object Dequeue(int millisec, out bool timedout) {
    return Dequeue(millisec, out timedout, true);
  }
  
  protected object Dequeue(int millisec, out bool timedout, bool advance)
  {
    AutoResetEvent re = null;
    lock( _sync ) {
      if( (_queue.Count > 1) || _closed ) { 
        /**
         * If _queue.Count == 1, the Dequeue may return us to the empty
         * state, which we always handled below
         */
        timedout = false;
        if( advance ) {
          return _queue.Dequeue();
        }
        else {
          return _queue.Peek();
        }
      }
      /*
       * We have to wait, make sure we could this waiter
       * before the queue is closed.  The lock on _sync
       * make sure that we can't close while in this block
       * of code
       */
      Interlocked.Increment(ref _waiters);
      re = _re;
    }
    bool got_set = true;
    //Wait for the next one... 
    try{
      got_set = re.WaitOne(millisec, false);
    }
    catch { }
    Interlocked.Decrement(ref _waiters);
    
    if( got_set ) {
      timedout = false;
      bool set = false;
      object result = null;
      try {
        lock( _sync ) {
          if( advance ) {
            result = _queue.Dequeue();
          }
          else {
            result = _queue.Peek();
          }
          /*
           * If there is
           * still more, set the _re so the
           * next reader can get it
           */
          set = ( _queue.Count > 0 ) && ( _re != null );
          if( set ) {
            re = _re;
          }
            
        }
      }
      finally {
        try {
          if( set ) { re.Set(); }
        } catch { }
      }
      return result;
    }
    else {
      timedout = true;
      return null;
    } 
  }

  /**
   * @throw Exception if the queue is closed
   */
  public object Peek() {
    bool timedout = false;
    return Peek(-1, out timedout);
  }

  /**
   * @param millisec how many milliseconds to wait if the queue is empty
   * @param timedout true if we have to wait too long to get an object
   * @return the object, if we timeout we return null
   * @throw Exception if the BlockingQueue is closed and empty
   */
  public object Peek(int millisec, out bool timedout)
  {
    return Dequeue(millisec, out timedout, false);
  }

  public void Enqueue(object a) {
    bool set = false;
    bool close = false;
    AutoResetEvent re = null;
    lock( _sync ) {
      if( !_closed ) {
        _queue.Enqueue(a);
        close = _close_on_enqueue;
      }
      else {
        //We are closed, ignore all future enqueues.
        return;
      }
      //Wake up any waiting threads
#if DEBUG
      System.Console.Error.WriteLine("Enqueue set: count {0}", Count);
#endif
      if( _queue.Count == 1 ) {
        //We just went from 0 -> 1 signal any waiting Dequeue
        set = true;
        re = _re;
      }
    }
    if( set ) { 
      try {
        re.Set();
      }
      catch { }
    }
    //After we have alerted any blocking threads (Set), fire
    //the event:
    if( EnqueueEvent != null ) {
      EnqueueEvent(this, EventArgs.Empty);
    }
    if( close ) {
      Close();  
    }
  }

  /**
   * This method is not defined if there are other Dequeues pending
   * on any of these Queues.  REPEAT: if you are using Select you
   * cannot be doing Dequeues in another thread on any of these Queues.
   *
   * Tests seem to show that mono has a problem if there are more than 64
   * queues that we are waiting on, so this can't scale to huge numbers.
   *
   * @param queues a list of non-null BlockingQueue objects.
   * @param timeout how long to wait in milliseconds
   * @return the index into the list of a queue that is ready to Dequeue, -1 there is a timeout
   */
  public static int Select(IList queues, int timeout) {
    WaitHandle[] wait_handles = new WaitHandle[ queues.Count ];
    int idx = 0; 
    foreach (BlockingQueue q in queues) {
       wait_handles[idx] = q._re;
       idx++;
    }
    idx = WaitHandle.WaitAny(wait_handles, timeout, true);
    if (idx == WaitHandle.WaitTimeout) {
      return -1;
    }
    //Reset the AutoResetEvent
    BlockingQueue t = (BlockingQueue)queues[idx];
    t._re.Set();
    return idx;
  }

  public static ArrayList[] ParallelFetch(BlockingQueue[] queues, int max_results_per_queue) {
    return ParallelFetch(queues, max_results_per_queue, new FetchDelegate(Fetch)); 
  }

  public static ArrayList[] ParallelFetch(BlockingQueue[] queues, 
					  int max_results_per_queue,
					  FetchDelegate fetch_delegate) {

    FetchDelegate [] fetch_dlgt = new FetchDelegate[queues.Length];
    IAsyncResult [] ar = new IAsyncResult[queues.Length];
    //we also maintain an array of WaitHandles
    WaitHandle [] wait_handle = new WaitHandle[queues.Length];
    
    for (int k = 0; k < queues.Length; k++) {
      fetch_dlgt[k] = new FetchDelegate(fetch_delegate);
      ar[k]  = fetch_dlgt[k].BeginInvoke(queues[k], max_results_per_queue, null, null);
      wait_handle[k] = ar[k].AsyncWaitHandle;
    }
    //we now wait for all invocations to finish
    Console.Error.WriteLine("Waiting for all invocations to finish...");
    WaitHandle.WaitAll(wait_handle);
    //we know that all invocations of Fetch have completed
    ArrayList []results = new ArrayList[queues.Length];
    for (int k = 0; k < queues.Length; k++) {
      //BlockingQueue q = (BlockingQueue) queues[k];
      results[k] = fetch_dlgt[k].EndInvoke(ar[k]);
    }
    return results;
  }

  public static ArrayList[] ParallelFetchWithTimeout(BlockingQueue[] queues, int millisec) {
    return ParallelFetchWithTimeout(queues, millisec, new FetchDelegate(Fetch)); 
  }  
  public static ArrayList[] ParallelFetchWithTimeout(BlockingQueue[] queues, 
						     int millisec,
						     FetchDelegate fetch_delegate) {

    FetchDelegate [] fetch_dlgt = new FetchDelegate[queues.Length];
    IAsyncResult [] ar = new IAsyncResult[queues.Length];
    //we also maintain an array of WaitHandles
    WaitHandle [] wait_handle = new WaitHandle[queues.Length];
    
    for (int k = 0; k < queues.Length; k++) {
      fetch_dlgt[k] = new FetchDelegate(fetch_delegate);
      ar[k]  = fetch_dlgt[k].BeginInvoke(queues[k], -1, null, null);
      wait_handle[k] = ar[k].AsyncWaitHandle;
    }
    //we now forcefully close all the queues after waiting for the timeout
    Thread.Sleep(millisec);
    for (int k = 0; k < queues.Length; k++) {
      try {
	Console.Error.WriteLine("Closing queue: {0}", k);	
	queues[k].Close();
      } catch(InvalidOperationException) {
	
      }
    }
    //we now wait for all invocations to finish
    Console.Error.WriteLine("Waiting for all parallel invocations to finish...");
    WaitHandle.WaitAll(wait_handle);
    Console.Error.WriteLine("All parallel invocations are over.");
    //we know that all invocations of Fetch have completed
    ArrayList []results = new ArrayList[queues.Length];
    for (int k = 0; k < queues.Length; k++) {
      //BlockingQueue q = (BlockingQueue) queues[k];
      results[k] = fetch_dlgt[k].EndInvoke(ar[k]);
    }
    return results;
  }  



  public delegate ArrayList FetchDelegate(BlockingQueue q, int max_replies);
  protected static ArrayList Fetch(BlockingQueue q, int max_replies) {
    ArrayList replies = new ArrayList();
    try {
      while (true) {
	if (max_replies == 0) {
	  break;
	}
	object res = q.Dequeue();
	replies.Add(res);
	max_replies--;
      }
    } catch (InvalidOperationException ) {

    }
    //Console.Error.WriteLine("fetch finished");
    return replies;
  }

#if BRUNET_NUNIT
  public void TestThread1()
  {
    //See a random number generator with the number 1.
    Random r = new Random(1);
    for(int i = 0; i < 100000; i++) { 
      Enqueue( r.Next() );
    }
    Close();
  }
  
  [Test]
  public void TestThread2()
  {
    Thread t = new Thread(this.TestThread1);
    t.Start();
    Random r = new Random(1);
    for(int i = 0; i < 100000; i++) { 
      Assert.AreEqual( Dequeue(), r.Next(), "dequeue equality test" );
    }
//    System.Console.Error.WriteLine("Trying to get an exception");
    //The next dequeue should throw an exception
    bool got_exception = false;
    try {
      Dequeue();
    }
    catch(Exception) { got_exception = true; }
    Assert.IsTrue(got_exception, "got exception");
    //Try it again
    got_exception = false;
    try {
      Dequeue();
    }
    catch(Exception) { got_exception = true; }
    Assert.IsTrue(got_exception, "got exception");
  }

  /*
   * Used to test the select method
   */
  protected class SelectTester {
    protected IList _queues;
    protected const int TRIALS = 50000;
    public SelectTester(IList queues) {
      _queues = queues;
    }

    public void StartEnqueues() {
      Random q_r = new Random();
      for(int i = 0; i < TRIALS; i++) {
        int idx = q_r.Next(0, _queues.Count);
        BlockingQueue q = (BlockingQueue)_queues[ idx ];
	q.Enqueue( idx );
      }
    }

    public void CheckQueues() {
      for(int i = 0; i < TRIALS; i++) {
        int idx = BlockingQueue.Select( _queues, 5000 );
	Assert.AreNotEqual( idx, -1, "Timeout check");
	BlockingQueue b = (BlockingQueue)_queues[idx];
	bool timedout;
	object val = b.Dequeue(0, out timedout);
	Assert.IsFalse(timedout, "Dequeue didn't time out");
        Assert.AreEqual(val, idx, "Dequeue matches index");
      }
      //Any future selects *should* timeout
      int idx2 = BlockingQueue.Select( _queues, 500 );
      Assert.AreEqual( idx2, -1, "Did timeout");
    }
  }

  [Test]
  public void SelectTest() {
    ArrayList l = new ArrayList();
    for(int i = 0; i < 64; i++) {
      l.Add( new BlockingQueue() );
    }
    SelectTester test = new SelectTester(l);
    Thread t = new Thread( test.StartEnqueues );
    t.Start();
    test.CheckQueues();
    foreach(BlockingQueue q in l) {
      q.Close();
    }
  }

  protected class WriterState {
    protected readonly BlockingQueue _q;
    protected readonly ArrayList _list;
    protected readonly int _runs;

    public WriterState(BlockingQueue q, ArrayList all, int runs) {
      _q = q;
      _list = all;
      _runs = runs;
    }
    public void Start() {
      Random r = new Random();
      for(int i = 0; i < _runs; i++) {
        int rn = r.Next();
        _q.Enqueue(rn);
        lock( _list ) {
          _list.Add( rn );
        }
      }
    }
  }
  protected class ReaderState {
    protected readonly BlockingQueue _q;
    protected readonly ArrayList _list;

    public ReaderState(BlockingQueue q, ArrayList all) {
      _q = q;
      _list = all;
    }
    public void Start() {
      try {
        while(true) {
          object o = _q.Dequeue();
          lock( _list ) { _list.Add( o ); }
        }
      }
      catch(InvalidOperationException) {
        //Queue is closed now.
      }
    }
  }

  [Test]
  public void MultipleWriterTest() {
    const int WRITERS = 5;
    const int READERS = 5;
    const int writes = 10000;
    ArrayList written_list = new ArrayList();
    ArrayList read_list = new ArrayList();
    ArrayList write_threads = new ArrayList();
    ArrayList read_threads = new ArrayList();
    BlockingQueue q = new BlockingQueue();

    /* Start the writers */
    for( int i = 0; i < WRITERS; i++ ) {
      WriterState ws = new WriterState(q, written_list, writes);
      Thread t = new Thread( ws.Start );
      write_threads.Add( t );
      t.Start();
    }
    /* Start the readers */
    for( int i = 0; i < READERS; i++) {
      ReaderState rs = new ReaderState(q, read_list);
      Thread t = new Thread( rs.Start );
      read_threads.Add( t );
      t.Start();
    }
    foreach(Thread t in write_threads) {
      t.Join();
    }
    //Writing is done, close the queue, and join the readers:
    q.Close();
    foreach(Thread t in read_threads) {
      t.Join();
    }

    //Check that the reader list is the same as the written list:
    ArrayList read_copy = new ArrayList(read_list);
    ArrayList write_copy = new ArrayList(written_list);
    //Remove all the reads from the written copy:
    foreach(object o in read_list) {
      int i = write_copy.IndexOf(o);
      Assert.IsTrue( i >= 0, "read something not in written");
      write_copy.RemoveAt(i);
    }
    Assert.IsTrue( write_copy.Count == 0, "More written than read");
    //Remove all the writes from the read copy:
    foreach(object o in written_list) {
      int i = read_copy.IndexOf(o);
      Assert.IsTrue( i >= 0, "wrote something not in read");
      read_copy.RemoveAt(i);
    }
    Assert.IsTrue( read_copy.Count == 0, "More written than read");
  }
#endif
}

}
