/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com> University of Florida

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

using Brunet;
using System;
using System.Threading;
using System.Collections.Generic;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet
{
  /**
   *  This class implements an event scheduler. It is a singleton
   *  class and hence only one instance is allowed to exists
   *  in a process.
   */
  
  public class TaskScheduler {
    protected static TaskScheduler _scheduler = null;
    protected static readonly object _class_lock = new object();

    protected BlockingQueue _in_queue;
    protected LinkedList<BrunetTask> _task_queue;
    protected Thread _thread;
    protected long _now_ticks;
    protected bool _finished;

    protected TaskScheduler() {
      _in_queue = new BlockingQueue();
      _task_queue = new LinkedList<BrunetTask>();
      _thread = new Thread(this.Run);
      _finished = false;
    }
    
    public static TaskScheduler GetInstance() {
      lock(_class_lock) {
	if (_scheduler == null) {
	  _scheduler = new TaskScheduler();
	}
	return _scheduler;
      }
    } 

    public void Start() {
      _thread.Start();
    }
    
    public void Stop() {
      _finished = true;
      _in_queue.Enqueue(null);//unblocks the thread
    }

    public void Schedule(BrunetTask task) {
      _in_queue.Enqueue(task);
    }

    protected void Run() {
      while(!_finished) {
	//
	// Find out the next closest event in the sorted list.
	//
	BrunetTask task = null;
	bool fire = false;
	if (_task_queue.First == null) {
	  task = (BrunetTask) _in_queue.Dequeue(); 
	} else {
	  _now_ticks = DateTime.UtcNow.Ticks;
	  LinkedListNode<BrunetTask> next_node = _task_queue.First;
	  if (next_node.Value.Instant > _now_ticks) {
	    int milliseconds = (int) ((next_node.Value.Instant - _now_ticks)/10000.0);
	    task = (BrunetTask) _in_queue.Dequeue(milliseconds,
						  out fire);
	  } else {
	    fire = true;
	  }
	}

	if (fire) {
	  //
	  // Time to fire the next event in the queue.
	  //
	  
	  task = _task_queue.First.Value;
	  _task_queue.RemoveFirst();
	  task.Fire();
	} else {
	  //
	  // Add the new task to the sorted list.
	  //
	  if (task != null) {
	    AddTask(task);
	  }
	}
      }
    }
    
    protected void AddTask(BrunetTask task) {
      //
      // Classical linked list insertion.
      // 
      LinkedListNode<BrunetTask> current = _task_queue.Last;
      while (current != null) {
	if (task.Instant > current.Value.Instant) {
	  _task_queue.AddAfter(current, task);
	  break;
	}
	current = current.Previous;
      }

      if (current == null) {
	_task_queue.AddFirst(task);
      }
    }
  }

  abstract public class BrunetTask {
    protected long _instant;
    public long Instant {
      get {
	return _instant;
      }
    }
    public BrunetTask(long instant) {
      _instant = instant;
    }

    //
    // Should be a non-blocking call.
    //
    abstract public void Fire();
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class SchedulerTester {  
    public class TestTask: BrunetTask {
      public static int Counter = 0;
      public static LinkedList<int> Values = new LinkedList<int>();
      protected int _value;
      public TestTask(int x, long instant):base(instant) 
      {
	_value = x;
      }
      public override void Fire() {
	Interlocked.Increment(ref Counter);
	Values.AddFirst(_value);
      }
    }
    [Test]
    public void TestScheduler() {
      TaskScheduler scheduler = TaskScheduler.GetInstance();
      Assert.IsTrue(scheduler != null);
      scheduler.Start();
      TestTask.Counter = 0;
      long now_ticks = DateTime.UtcNow.Ticks;
      for (int i = 0; i < 100; i++) {
	BrunetTask task = new TestTask(i, now_ticks + i*5*10000);
	scheduler.Schedule(task);
      }
      Thread.Sleep(1500);
      Assert.AreEqual(100, TestTask.Counter);
      scheduler.Stop();
      
      LinkedListNode<int> current  = TestTask.Values.Last;
      int expected_value = 0;
      while (current != null) {
	//Console.WriteLine(current.Value);
	Assert.AreEqual(current.Value, expected_value);
	expected_value++;
	current = current.Previous;
      }
    }
  }
#endif
}
