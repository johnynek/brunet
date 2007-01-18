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
 */
#if BRUNET_NUNIT
[TestFixture]
#endif
public class BlockingQueue : Queue {
  
  public BlockingQueue() {
    _re = new AutoResetEvent(false); 
    _closed = false;
    _exception = null;
  }
 
  protected AutoResetEvent _re;
 
  protected Exception _exception;
  protected bool _closed;

  public bool Closed { get { lock ( this ) { return _closed; } } }
  
  public override int Count { get { return base.Count; } }
 
  /**
   * When an item is enqueued, this event is fire
   */
  public event EventHandler EnqueueEvent;
  
  /* **********************************************
   * Here all the methods
   */
  
  public override void Clear() {
    lock( this ) {
      base.Clear();
    }
  }
  
  public override object Clone() {
    return null;
  }

  /**
   * Once this method is called, and the queue is emptied,
   * all future Dequeue's will throw exceptions
   */
  public void Close() {
    lock( this ) {
      _closed = true;
      _re.Set();
    }
    //Wake up any blocking threads:
#if DEBUG
    System.Console.WriteLine("Close set");
#endif
  }
  
  public override bool Contains(object o) {
    lock( this ) {
      return base.Contains(o);
    }
  }
  
  /**
   * @throw Exception if the queue is closed
   */
  public override object Dequeue() {
    bool timedout = false;
    return Dequeue(-1, out timedout);
  }

  /**
   * @param millisec how many milliseconds to wait if the queue is empty
   * @param timedout true if we have to wait too long to get an object
   * @return the object, if we timeout we return null
   * @throw Exception if the BlockingQueue is closed and empty
   */
  public object Dequeue(int millisec, out bool timedout)
  {
    object val = null;
    bool got_set = _re.WaitOne(millisec, false);
    if( !got_set ) {
      timedout = true;
      return null;
    }
    else {
      lock( this ) {
        if( _exception != null ) {
          Exception x = _exception;
          _exception = null;
          throw x;
        }
#if DEBUG
	System.Console.WriteLine("Got set: count {0}", Count);
#endif
        if( base.Count > 1 || _closed ) {
          /*
           * If the queue is closed, we want Dequeues to suceed immediately
           * if there are elements in the queue, we want Dequeues to succeed
           * immediately, otherwise, the AutoResetEvent would have been reset
           * so no one else will get past WaitOne until the queue is closed
           * or there is an Enqueue event.
           */
          _re.Set();
        }
        val = base.Dequeue();
        timedout = false;
      }
    }
    return val;    
  }
  
  /**
   * @throw Exception if the queue is closed
   */
  public override object Peek() {
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
    object val = null;
    bool got_set = _re.WaitOne(millisec, false);
    if( !got_set ) {
      timedout = true;
      return null;
    }
    else {
      lock( this ) {
        if( _exception != null ) { _exception = null; throw _exception; }
        //We didn't take any out, so we should still be ready to go!
        _re.Set();
        timedout = false;
        
        val = base.Peek();
      }
    }
    return val;    
  }

  public override void Enqueue(object a) {
    bool fire = false;
    lock( this ) {
      if( !_closed ) {
        base.Enqueue(a);
	fire = true;
      }
      else {
        //We are closed, ignore all future enqueues.
      }
      //Wake up any waiting threads
#if DEBUG
      System.Console.WriteLine("Enqueue set: count {0}", Count);
#endif
    }
    _re.Set();
    //After we have alerted any blocking threads (Set), fire
    //the event:
    if( fire && (EnqueueEvent != null) ) {
      EnqueueEvent(this, EventArgs.Empty);
    }
  }

  /*
   * On the next (or pending) Dequeue, throw
   * the given exception.
   */
  public void Throw(Exception x) {
    lock( this ) {
      _exception = x;
    }
    _re.Set();
#if DEBUG
    System.Console.WriteLine("Exception set: ex {0}", x);
#endif
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
    Console.WriteLine("Waiting for all invocations to finish...");
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
    //we now forcefully kill all the queues after waiting for the timeout
    Thread.Sleep(millisec);
    for (int k = 0; k < queues.Length; k++) {
      try {
	Console.WriteLine("Closing queue: {0}", k);	
	queues[k].Close();
      } catch(InvalidOperationException) {
	
      }
    }
    //we now wait for all invocations to finish
    Console.WriteLine("Waiting for all parallel invocations to finish...");
    WaitHandle.WaitAll(wait_handle);
    Console.WriteLine("All parallel invocations to are over.");
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
    //Console.WriteLine("fetch finished");
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
//    System.Console.WriteLine("Trying to get an exception");
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
#endif
}

}
