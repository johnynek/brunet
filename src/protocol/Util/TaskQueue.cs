/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2006  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System.Collections;

namespace Brunet {

/**
 * This class represents objects that work to complete a single
 * Task.  When the they are done, they fire a FinishEvent.
 */
abstract public class TaskWorker {

  /**
   * This object MUST correctly implement GetHashCode and Equals
   */
  abstract public object Task { get; }
  /**
   * This is fired when the TaskWorker is finished,
   * it doesn't mean it was successful, it just means
   * it has stopped
   */
  public event EventHandler FinishEvent;

  /**
   * Is true if the TaskWorker is finished
   */
  abstract public bool IsFinished { get; }

  /**
   * Subclasses call this to fire the finish event
   */
  protected void FireFinished() {
    if( FinishEvent != null ) {
      FinishEvent(this, EventArgs.Empty);
      //Make sure we only fire once:
      FinishEvent = null;
    }
  }

  /**
   * This method tells the TaskWorked to start working
   */
  abstract public void Start();
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
  protected Hashtable _task_to_workers;
  protected object _sync;

  /**
   * When the TaskQueue completely empties,
   * this event is fired, every time.
   */
  public event EventHandler EmptyEvent;

  protected int _worker_count;
  //if the queue can start workers (added by Arijit Ganguly)
  protected bool _is_active;
  public bool IsActive {
    set {
      _is_active = value;
    }
  }
  
  public int WorkerCount {
    get {
      lock( _sync ) {
        return _worker_count;
      }
    }
  }

  public TaskQueue() {
    _task_to_workers = new Hashtable();
    _sync = new object();
    _worker_count = 0;
    //is active by default
    _is_active = true;
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
    if( start  && _is_active) {//added the _is_active check (Arijit Ganguly)
      new_worker.Start();
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
   * When a TaskWorker completes, we remove it from the queue and
   * start the next in that task queue
   */
  protected void TaskEndHandler(object worker, EventArgs args)
  {
    TaskWorker new_worker = null;
    bool fire_empty;
    lock( _sync ) {
      TaskWorker this_worker = (TaskWorker)worker;   
      object task = this_worker.Task;
      Queue work_queue = (Queue)_task_to_workers[task];
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
      fire_empty = (_worker_count == 0);
    }
    if( new_worker != null && _is_active) {//added the _is_active check (Arijit Ganguly)
      //You start me up!
      new_worker.Start();
    }
    if( fire_empty && (EmptyEvent != null) ) {
      EmptyEvent(this, EventArgs.Empty);
    }
  }
}

}
