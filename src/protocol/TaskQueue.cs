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

  public TaskQueue() {
    _task_to_workers = new Hashtable();
    _sync = new object();
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
    }
    /*
     * Get to work!
     */
    if( start ) {
      new_worker.Start();
    }
  }
  
  /**
   * When a TaskWorker completes, we remove it from the queue and
   * start the next in that task queue
   */
  protected void TaskEndHandler(object worker, EventArgs args)
  {
    TaskWorker this_worker = (TaskWorker)worker;   
    TaskWorker new_worker = null;
    lock( _sync ) {
      Queue work_queue = (Queue)_task_to_workers[this_worker.Task];
      work_queue.Dequeue();
      if( work_queue.Count > 0 ) {
        //Now the new job is at the head of the queue:
        new_worker = (TaskWorker)work_queue.Peek();
      }
    }
    if( new_worker != null ) {
      //You start me up!
      new_worker.Start();
    }
  }
}

}
