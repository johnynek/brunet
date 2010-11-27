// Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida
// For license, see the file LICENSE in the root directory of this software.

using System;
using Brunet.Util;

namespace Brunet.Simulator.Tasks {
  abstract public class Task {
    /// <summary>Is the task complete?</summary>
    public bool Done { get { return _done; } }
    protected bool _done;
    /// <summary>Time taken to complete the Crawl.</summary>
    public TimeSpan TimeTaken { get { return _time_taken; } }
    protected TimeSpan _time_taken;

    protected readonly EventHandler _finished;
    protected DateTime _start;

    /// <summary>Create a new task.  finished is called when the task
    /// completes.</summary>
    public Task(EventHandler finished)
    {
      _done = false;
      _finished = finished;
    }

    virtual public void Start()
    {
      _start = DateTime.UtcNow;
    }

    virtual protected void Finished()
    {
      _time_taken = DateTime.UtcNow - _start;
      _done = true;
      if(_finished != null) {
        _finished(this, EventArgs.Empty);
      }
    }

    virtual public void Run(int time)
    {
      DateTime end = DateTime.UtcNow.AddSeconds(time);
      while(!_done && DateTime.UtcNow < end) {
        SimpleTimer.RunStep();
      }
    }
  }
}
