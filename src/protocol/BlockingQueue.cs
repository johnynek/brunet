/*
Copyright (C) 2005  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

using System.Collections;
using System.Threading;
using System;

//#define DEBUG

namespace Brunet
{

/**
 * This class offers a means to pass objects in a queue
 * between threads (or in the same thread).  The Dequeue
 * method will block until there is something in the Queue
 */
public class BlockingQueue : Queue {
  
  public BlockingQueue() {
    _are = new AutoResetEvent(false); 
    _closed = false;
    _exception = null;
  }
 
  protected AutoResetEvent _are;
 
  protected Exception _exception;
  protected bool _closed;

  public bool Closed { get { lock ( this ) { return _closed; } } }
  
  //Make sure we lock before getting the Count
  public override int Count { get { lock( this ) { return base.Count; } } }
 
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
   * Once this method is called,
   * all future Dequeue's will throw exceptions
   */
  public void Close() {
    lock( this ) {
      _closed = true;
    }
    //Wake up any blocking threads:
#if DEBUG
    System.Console.WriteLine("Close set");
#endif
    _are.Set();
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
    lock( this ) {
      //Make sure we don't have any old signals waiting...
      _are.Reset();
      if( _exception != null ) { throw _exception; }
      if( Count > 0 ) {
#if DEBUG
	System.Console.WriteLine("Not empty");
#endif
        val = base.Dequeue();
      }
      else if ( _closed ) {
        //We are closed and empty, no need to wait:
#if DEBUG
	System.Console.WriteLine("Closed Queue");
#endif
        val = base.Dequeue();
      }
    }
    if( val != null ) {
      timedout = false;
      return val;
    }
    bool got_set = _are.WaitOne(millisec, false);
    lock( this ) {
      if( _exception != null ) { throw _exception; }
      if( got_set ) {
#if DEBUG
	System.Console.WriteLine("Got set: count {0}", Count);
#endif
        val = base.Dequeue();
        timedout = false;
      }
      else {
        //We timed out
        timedout = true;
        val = null;
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
    lock( this ) {
      if( _exception != null ) { throw _exception; }
      if( Count > 0 ) {
        val = base.Peek();
      }
      else if ( _closed ) {
        //We are closed and empty, no need to wait:
        val = base.Peek();
      }
    }
    if( val != null ) {
      timedout = false;
      return val;
    }
    bool got_set = _are.WaitOne(millisec, false);
    lock( this ) {
      if( _exception != null ) { throw _exception; }
      if( got_set ) {
        val = base.Peek();
        timedout = false;
      }
      else {
        //We timed out
        timedout = true;
        val = null;
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
    }
    //Wake up any waiting threads
#if DEBUG
    System.Console.WriteLine("Enqueue set: count {0}", Count);
#endif
    _are.Set();
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
#if DEBUG
    System.Console.WriteLine("Exception set: ex {0}", x);
#endif
    _are.Set();
  }

#if false
  public void Test()
  {
    string line;
    do {
      line = System.Console.ReadLine();
      Enqueue(line);
    }  while( line != "q" );
  }
  
  public static void Main(string[] args)
  {
    Console.WriteLine("About to make BlockingQueue");
    BlockingQueue tq = new BlockingQueue();
    Console.WriteLine("Made Queue");
    
    Thread t = new Thread(new ThreadStart( tq.Test ));

    t.Start();
    Console.WriteLine("Started Thread");
    string line;
    do {
      Console.WriteLine("About to Dequeue");
      line = (string)tq.Dequeue();
      System.Console.WriteLine(line);
    } while( line != "q" );
    tq.Close();
    
  }
#endif
}

}
