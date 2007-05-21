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
  }
 
  protected AutoResetEvent _re;
 
  protected bool _closed;

  public bool Closed { get { lock ( this ) { return _closed; } } }
  
  public override int Count { get { lock ( this) { return base.Count; } } }
 
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
    bool fire = false;
    lock( this ) {
      if( _closed == false ) {
        fire = true;
        _closed = true;
        _re.Set();
      }
    }
    //Fire the close event
    if( fire && CloseEvent != null ) {
      CloseEvent(this, EventArgs.Empty);
    }

    //Wake up any blocking threads:
#if DEBUG
    System.Console.Error.WriteLine("Close set");
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
#if DEBUG
	System.Console.Error.WriteLine("Got set: count {0}", Count);
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
      System.Console.Error.WriteLine("Enqueue set: count {0}", Count);
#endif
      _re.Set();
    }
    //After we have alerted any blocking threads (Set), fire
    //the event:
    if( fire && (EnqueueEvent != null) ) {
      EnqueueEvent(this, EventArgs.Empty);
    }
  }

//   public static BlockingQueue Select(ArrayList queues, int timeout) {
//     WaitHandle[] wait_handle = new WaitHandle[queues.Count];
//     for (int i = 0; i < queues.Count; i++) {
//       BlockingQueue q = (BlockingQueue) queues[i];
//       wait_handle[i] = q._re;
//     }
//     int idx = WaitHandle.WaitAny(wait_handle, timeout, true);
//     if (idx == WaitHandle.WaitTimeout) {
//       Console.Error.WriteLine("wait any returned: {0}", idx);
//       return null;
//     }
//     BlockingQueue t = (BlockingQueue) queues[idx];
//     t._re.Set();
//     return t;
//   }

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
#endif
}


// #if BRUNET_NUNIT
// [TestFixture]
//   public class SelectTester {
//     private ArrayList _queues;
//     public void TestThreadProducer()
//     {
//       Console.Error.WriteLine("producer thread begins");
//       //See a random number generator with the number 1.
//       Random[] r = new Random[_queues.Count];
//       for (int i = 0; i < r.Length; i++) {
// 	r[i] = new Random(i);
//       }
//       Console.Error.WriteLine("created all random number generators");
//       for(int k = 0; k < 100000; k++) { 
// 	for (int i = 0; i < _queues.Count; i++) {
// 	  BlockingQueue q = (BlockingQueue) _queues[i];
// 	  int val = r[i].Next();
// 	  Console.Error.WriteLine("enqueing val: {0} into queue: {1}", val, i);
// 	  q.Enqueue(val) ;
// 	}
//       }
//       Console.Error.WriteLine("finsished all iterations");
//       for (int i = 0; i < _queues.Count; i++) {
// 	Console.Error.WriteLine("closing queue: {0}", i);
// 	BlockingQueue q = (BlockingQueue) _queues[i];
// 	q.Close();
//       }
//       Console.Error.WriteLine("closed all queues");
//     }
    
//     [Test]
//     public void TestMainThread()
//     {
//       Hashtable rr = new Hashtable();
//       _queues = new ArrayList();
//       for (int i = 0; i < 8; i++) {
// 	BlockingQueue q = new BlockingQueue();
// 	_queues.Add(q);
// 	rr[q] = new Random(i);
//       }
      
//       Thread t = new Thread(this.TestThreadProducer);
//       Console.Error.WriteLine("starting producer thread");
//       t.Start();
      
//       ArrayList local_list = (ArrayList) _queues.Clone();

//       while(true) {
// 	Console.Error.WriteLine("queues.count: {0}", local_list.Count);
// 	if (local_list.Count == 0) {
// 	  break;
// 	}
// 	//a 10 second timeout
// 	BlockingQueue q = BlockingQueue.Select(local_list, 10000);
// 	if (q == null) {
// 	  Console.Error.WriteLine("select returned a null");
// 	  continue;
// 	}
// 	int idx = _queues.IndexOf(q);
// 	Console.Error.WriteLine("select returning queue: {0}", idx);
// 	//get random number generator
// 	Random r = (Random) rr[q];
// 	try {
// 	  Console.Error.WriteLine("attempting a dequeue at: {0}", idx);
// 	  bool timedout;
// 	  object o = q.Dequeue(0, out timedout);
// 	  if (!timedout) {
// 	    Assert.AreEqual( o, r.Next(), "dequeue equality test" );
// 	  } else {
// 	    Console.Error.WriteLine("nothing dequeued");
// 	  }
// 	} catch(InvalidOperationException e) {
// 	  //in case the queue closed down
// 	  Console.Error.WriteLine("removing queue: {0} from the list", idx);
// 	  local_list.Remove(q);
// 	}
//       }
//     }
//   }
// #endif 
}
