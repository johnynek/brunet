/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Threading;
using System.Collections.Generic;
using System;

#if BRUNET_NUNIT
using NUnit.Framework;
using System.Collections;
#endif

namespace Brunet.Util {
  /// <summary>A very simple timer, single-threaded blocking timer, inspired
  /// by Mono's System.Threading.Timer. </summary>
  public class SimpleTimer : IDisposable, IComparable<SimpleTimer> {
    protected readonly WaitCallback _callback;
    protected readonly object _state;
    /// <summary>How often the timer will be called.</summary>
    public int Period { get { return _period_ms; } }
    protected readonly int _period_ms;
    protected bool _disposed;
    protected long _next_run;

    protected readonly static object _sync;
    protected readonly static Heap<SimpleTimer> _timers;
    protected static int _running;
    protected readonly static AutoResetEvent _re;
    protected readonly static Thread _thread;

    static SimpleTimer()
    {
      _sync = new object();
      _timers = new Heap<SimpleTimer>();
#if !BRUNET_SIMULATOR
      _re = new AutoResetEvent(false);
      _thread = new Thread(TimerThread);
      _thread.Start();
#endif
    }

#if BRUNET_SIMULATOR
    public static long Minimum {
      get {
        lock(_sync) {
          if(_timers.Empty) {
            return long.MaxValue;
          } else {
            return _timers.Peek()._next_run;
          }
        }
      }
    }
#endif

    /// <summary>Process all applicable events.  Called by TimerThread for
    /// non-simulation and directly for Simulation of real time.</summary>
#if BRUNET_SIMULATOR
    public static long Run()
#else
    protected static long Run()
#endif
    {
      long min_next_run = long.MaxValue;
      long ticks = DateTime.UtcNow.Ticks;
      SimpleTimer timer = null;
      _running = 0;
      while(true) {
        bool dispose = false;
        lock(_sync) {
          if(_timers.Empty) {
            break;
          }

          timer = _timers.Peek();
          if(timer._next_run > ticks) {
            min_next_run = timer._next_run;
            break;
          }

          _timers.Pop();
          if(timer._disposed) {
            continue;
          }
          if(timer._period_ms > 0 && timer._period_ms != Timeout.Infinite) {
            timer.Update(timer._period_ms, timer._period_ms);
          } else {
            dispose = true;
          }
        }

        try {
          timer._callback(timer._state);
        } catch (Exception e) {
          ProtocolLog.WriteIf(ProtocolLog.Exceptions, e.ToString());
        }

        if(dispose) {
          timer.Dispose();
        }
      }

      return min_next_run;
    }

    /// <summary>Wait for the next event.</summary>
    protected static void TimerThread() {
      while(true) {
        long next_run = Run();
        if(next_run == long.MaxValue) {
          _re.WaitOne(Timeout.Infinite, true);
        } else {
          long diff = next_run - DateTime.UtcNow.Ticks;
          if(diff <= 0) {
            continue;
          }
          _re.WaitOne((int) (diff / TimeSpan.TicksPerMillisecond), true);
        }
      }
    }

    /// <summary>Creates a new timer.</summary>
    public SimpleTimer(WaitCallback callback, object state, int dueTime, int period)
    {
      if(dueTime < -1) {
        throw new ArgumentOutOfRangeException("dueTime");
      } else if(period < -1) {
        throw new ArgumentOutOfRangeException("period");
      }

      _callback = callback;
      _state = state;
      _period_ms = period;

      Update(dueTime, period);
    }

    /// <summary>Updates a timer for its first or next run.</summary>
    protected void Update(int dueTime, int period)
    {
      long now = DateTime.UtcNow.Ticks;
      if(dueTime == Timeout.Infinite) {
        throw new Exception("There must be a due time!");
      } else {
        _next_run = dueTime * TimeSpan.TicksPerMillisecond + now;
      }

      bool first = false;
      lock(_sync) {
        first = _timers.Add(this);
      }
      // If we're in the simulator, we don't use the AutoResetEvent
#if !BRUNET_SIMULATOR
      if(first) {
        _re.Set();
      }
#endif
    }

    /// <summary>Don't call the event, I'm done.</summary>
    public void Dispose()
    {
      _disposed = true;
    }

    public int CompareTo(SimpleTimer t) {
      lock(_sync) {
        return this._next_run.CompareTo(t._next_run);
      }
    }
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class TimerTest {
    protected ArrayList _order = new ArrayList();
    protected Hashtable _hash = new Hashtable();
    protected object _sync = new object();
    
    public void Callback(object state) {
      lock(_sync) {
        _order.Add(state);
      }
    }

    public void PeriodCallback(object state) {
      SimpleTimer t = state as SimpleTimer;
      int calls = (int) _hash[t];
      lock(_sync) {
        _hash[t] = ++calls;
      }
      if(calls == 5) {
        t.Dispose();
      }
    }

    [Test]
    public void TestDipose() {
      _order = new ArrayList();
      for(int i = 0; i < 100; i++) {
        SimpleTimer t = new SimpleTimer(Callback, i, 100 + i, 0);
        if(i % 2 == 0) {
          t.Dispose();
        }
      }

      Thread.Sleep(500);
      foreach(int val in _order) {
        Assert.IsTrue(val % 2 == 1, "Even value got in...");
      }
      Assert.AreEqual(_order.Count, 50, "Should be 50 in _order");
    }

    [Test]
    public void TestLargeNumberWithPeriod() {
      _hash = new Hashtable();
      _order = new ArrayList();
      for(int i = 0; i < 10000; i++) {
        new SimpleTimer(Callback, i, 200, 0);
      }

      for(int i = 0; i < 5; i++) {
        SimpleTimer t = null;
        t = new SimpleTimer(PeriodCallback, t, 50, 50);
      }

      Thread.Sleep(500);

      Assert.AreEqual(_order.Count, 10000, "Should be 10000");
      foreach(int val in _hash.Values) {
        Assert.AreEqual(val, 5, "Should be 5");
      }
    }
  }
#endif
}

