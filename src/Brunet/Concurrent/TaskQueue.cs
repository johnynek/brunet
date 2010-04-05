/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2006-2008 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using Brunet.Concurrent;

namespace Brunet.Concurrent {

/**
 * Since interfaces are much faster than Delegates in .Net,
 * we use this for asynchronous code that might be run later.
 */
public interface IAction {
  void Start();
}

/** an IAction that does nothing.
 * This is a Singleton
 */
public class NullAction : IAction {
  static readonly NullAction _instance = new NullAction();
  static NullAction() { }
  public static NullAction Instance { get { return _instance; } }
  /** Private constructor to guarantee Singleton
   */
  private NullAction() { }
  public void Start() { }
}

/**
 * This class represents objects that work to complete a single
 * Task.  When the they are done, they fire a FinishEvent.
 */
abstract public class TaskWorker : IAction {
  
  protected TaskWorker()
  {
    _finish_event = new FireOnceEvent();
  }
  /**
   * This object MUST correctly implement GetHashCode and Equals
   */
  abstract public object Task { get; }
  
  private FireOnceEvent _finish_event;
  /**
   * This is fired when the TaskWorker is finished,
   * it doesn't mean it was successful, it just means
   * it has stopped
   */
  public event EventHandler FinishEvent {
    add { _finish_event.Add(value); }
    remove { _finish_event.Remove(value); }
  }
  /**
   * Is true if the TaskWorker is finished
   */
  virtual public bool IsFinished { get { return _finish_event.HasFired; } }

  /**
   * Subclasses call this to fire the finish event
   * @return true if this is the first time this method is called
   */
  protected bool FireFinished() {
    return _finish_event.Fire(this, null);
  }

  /**
   * This method tells the TaskWorked to start working
   */
  abstract public void Start();
}

/**
 * We commonly need to wait at least some period of time and then do
 * something else.
 *
 * This does not include a timer.  It checks to see if it should finish
 * when the method CheckTime is called.
 */
public class WaitTaskWorker : TaskWorker {

  public readonly object State;
  
  protected readonly object _sync;
  protected bool _finish_is_set;
  protected DateTime _finish_time;
  protected TimeSpan _interval;

  //Each wait is a unique task.
  public override object Task { get { return this; } }

  public WaitTaskWorker(TimeSpan min_wait_interval, object state) {
    _sync = new object();
    _finish_is_set = false;
    State = state;
  }

  public WaitTaskWorker(DateTime finish_after_utc_time, object state) {
    _sync = null;
    _finish_is_set = true;
    State = state;
  }

  /**
   * Checks DateTime.UtcNow to see if it is time to finish
   */
  public void CheckTime(object o, System.EventArgs args) {
    if( _finish_is_set && (DateTime.UtcNow > _finish_time) ) {
      FireFinished();
    }
  }

  /**
   * If we are waiting for an interval, set the finishing time,
   * otherwise, do nothing.
   */
  public override void Start() {
    if( _sync != null ) {
      lock( _sync ) {
        if( !_finish_is_set ) {
          _finish_is_set = true;
          _finish_time = DateTime.UtcNow + _interval; 
        }
      }
    }
  }
}

/**
 * Manages the tasks for a particular node.
 * It makes sure that for each Node there is at most
 * one TaskWorker per task.
 */
public class TaskQueue {

  /**
   * Here is the list workers
   */
  protected readonly Hashtable _task_to_workers;
  protected readonly object _sync;

  /**
   * When the TaskQueue completely empties,
   * this event is fired, every time.
   */
  public event EventHandler EmptyEvent;

  //if the queue can start workers (added by Arijit Ganguly)
  protected int _is_active;
  public bool IsActive {
    set {
      System.Threading.Interlocked.Exchange(ref _is_active, value ? 1 : 0);
    }
  }
  
  protected int _worker_count;
  public int WorkerCount {
    get {
      lock ( _sync ) {
        return _worker_count;
      }
    }
  }

  public TaskQueue() {
    _task_to_workers = new Hashtable();
    _sync = new object();
    _worker_count = 0;
    //is active by default
    IsActive = true;
  }

  public void Enqueue(TaskWorker new_worker)
  {
    bool start = false;
    lock( _sync ) {
      Queue work_queue = (Queue)_task_to_workers[ new_worker.Task ];
      if( work_queue == null ) {
        //This is a new task:
        work_queue = new Queue();
        _task_to_workers[ new_worker.Task ] = work_queue;
        //Start the job!
        start = true;
      }
      //In any case, add the worker:
      new_worker.FinishEvent += this.TaskEndHandler;
      work_queue.Enqueue(new_worker);
      _worker_count++;
    }
    /*
     * Get to work!
     */
    if( start  && (1 == _is_active)) {
      Start(new_worker);
    }
  }
  
  /**
   * @return true if there is at least one TaskWorker for this task.
   */
  public bool HasTask(object task) {
    lock( _sync ) {
      return _task_to_workers.ContainsKey(task);
    }
  }
  /**
   * If you want to control if new TaskWorkers are started in some
   * other thread, or event loop, you can override this method
   */
  protected virtual void Start(TaskWorker tw) {
    tw.Start();
  }
  /**
   * When a TaskWorker completes, we remove it from the queue and
   * start the next in that task queue
   */
  protected void TaskEndHandler(object worker, EventArgs args)
  {
    TaskWorker new_worker = null;
    EventHandler eh = null;
    lock( _sync ) {
      TaskWorker this_worker = (TaskWorker)worker;   
      object task = this_worker.Task;
      Queue work_queue = (Queue)_task_to_workers[task];
      if( work_queue != null ) {
        work_queue.Dequeue();
        if( work_queue.Count > 0 ) {
          //Now the new job is at the head of the queue:
          new_worker = (TaskWorker)work_queue.Peek();
        }
        else {
          /*
           * There are no more elements in the queue, forget it:
           * If we leave a 0 length queue, this would be a memory
           * leak
           */
          _task_to_workers.Remove(task);
        }
        _worker_count--;
        if (_worker_count == 0) {
          eh = EmptyEvent;
        }
      }
      else {
        //This TaskEndHandler has been called more than once clearly.
        Console.Error.WriteLine("ERROR: {0} called TaskEndHandler but no queue for this task: {1}",
                                worker, task);
      }
    }
    if( new_worker != null && (1 == _is_active)) {
      //You start me up!
      Start(new_worker);
    }
    if( eh != null ) { eh(this, EventArgs.Empty); }
  }
}

}
