/*
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System;

namespace Brunet
{
/**
 * QueueWithTimeout makes sure that queues do not become so long such that
 * the data being processed is never valid and valid data becomes invalid
 * by the time it is processed.  Heuristics should be used to set a timeout
 */
  public class QueueWithTimeout: Queue {
    public QueueWithTimeout() {
      _timeout = 60;
      _timeout_queue = new Queue();
    }

    public QueueWithTimeout(int timeout) {
      _timeout_queue = new Queue();
      _timeout = timeout;
    }

    int _timeout;
    Queue _timeout_queue;

    protected void Check(bool leave_one) {
      // For Dequeue and Peek if we don't leave one, we'll get an undesired exception
      int minimum = leave_one ? 1 : 0;
      if(base.Count == 0)
        return;
        // This is only works when _timeout is > 0 and clears
      int count = 0;
      while(base.Count > minimum && _timeout < (DateTime.UtcNow - (DateTime) _timeout_queue.Peek()).TotalSeconds) {
        base.Dequeue();
        _timeout_queue.Dequeue();
      }
    }

    public override object Dequeue() {
      Check(true);
      _timeout_queue.Dequeue();
      return base.Dequeue();
    }

    public override void Enqueue(object o) {
      Check(false);
      _timeout_queue.Enqueue(DateTime.UtcNow);
      base.Enqueue(o);
    }

    public override object Peek() {
      Check(true);
      return base.Peek();
    }
  }
}
