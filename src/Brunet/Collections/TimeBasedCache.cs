/*
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet.Util;
using System;
using System.Collections.Generic;
using System.Threading;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Collections {
  /// <summary>A thread-safe Cache mechanism that limits entry count not by
  /// amount of entries, but the time an entry has been in the Cache.</summary>
  public class TimeBasedCache<K, V> {
    /// <summary> An evicted entry.</summary>
    public class EvictionArgs : EventArgs {
      public readonly K Key;
      public readonly V Value;
      public EvictionArgs(K key, V value)
      {
        Key = key;
        Value = value;
      }
    }

    /// <summary>Time between moving cache entries to high levels and eviction.</summary>
    public readonly int CleanupTime;
    /// <summary>A lock synchronizer for the hashtables and cache.</summary>
    protected readonly ReaderWriterLock _sync;
    /// <summary>Active contents.</summary>
    protected Dictionary<K, V> _first;
    /// <summary>Not so active contents.</summary>
    protected Dictionary<K, V> _second;
    /// <summary>Timer that handles the garbage collection of mappings.</summary>
    protected readonly FuzzyEvent _fe;
    /// <summary>Turn off the cache.</summary>
    protected int _stopped;
    public event EventHandler<EvictionArgs> EvictionHandler;

    public TimeBasedCache(int timer)
    {
      CleanupTime = timer;
      _sync = new ReaderWriterLock();
      _first = new Dictionary<K, V>();
      _second = new Dictionary<K, V>();
      _fe = Brunet.Util.FuzzyTimer.Instance.DoEvery(Recycle, CleanupTime, CleanupTime / 10);
      _stopped = 0;
    }

#if BRUNET_NUNIT
    public void Recycle(DateTime now)
#else
    protected void Recycle(DateTime now)
#endif
    {
      if(_stopped == 1) {
        return;
      }

      Dictionary<K, V> removed = null;
      _sync.AcquireWriterLock(-1);
      removed = _second;
      _second = _first;
      _first = new Dictionary<K, V>();
      _sync.ReleaseWriterLock();

      var eh = EvictionHandler;
      if(eh == null) {
        return;
      }

      foreach(KeyValuePair<K, V> kvp in removed) {
        eh(this, new EvictionArgs(kvp.Key, kvp.Value));
      }
    }

    /// <summary>Adds the value into the cache, returning false if the value
    /// is already in the cache but promotes the data into _first if in
    /// _second.</summary>
    public bool Update(K key, V value)
    {
      if(_stopped == 1) {
        throw new Exception("Stopped!");
      }

      bool rv = true;

      _sync.AcquireReaderLock(-1);
      try {
        if(_first.ContainsKey(key)) {
          return false;
        }
      } finally {
        _sync.ReleaseReaderLock();
      }


      _sync.AcquireWriterLock(-1);
      try {
        if(_second.ContainsKey(key)) {
          _second.Remove(key);
          rv = false;
        }

        _first[key] = value;
      } finally {
        _sync.ReleaseWriterLock();
      }
      return rv;
    }

    public bool TryGetValue(K key, out V value)
    {
      bool update;
      return TryGetValue(key, out value, out update);
    }

    public bool TryGetValue(K key, out V value, out bool update)
    {
      if(_stopped == 1) {
        throw new Exception("Stopped!");
      }

      update = false;
      bool rv = false;
      _sync.AcquireReaderLock(-1);
      try {
        if(_first.TryGetValue(key, out value)) {
          rv = true;
        } else if(_second.TryGetValue(key, out value)) {
          rv = true;
          update = true;
        }
      } finally {
        _sync.ReleaseReaderLock();
      }
      return rv;
    }

    /// <summary> Must stop the Recycling thread.</summary>
    public void Stop()
    {
      if(System.Threading.Interlocked.Exchange(ref _stopped, 1) == 0) {
        return;
      }

      _fe.TryCancel();
    }
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class TestTimeBasedCache {
    protected TimeBasedCache<int, int>.EvictionArgs _last_ea = null;

    protected void HandleEviction(object sender, TimeBasedCache<int, int>.EvictionArgs ea)
    {
      _last_ea = ea;
    }

    [Test]
    public void Test()
    {
      TimeBasedCache<int, int> tbc = new TimeBasedCache<int, int>(600000);
      tbc.EvictionHandler += HandleEviction;
      int val = 0;
      bool update = false;
      Assert.IsFalse(tbc.TryGetValue(10, out val, out update), "Empty");
      Assert.IsFalse(update, "Empty -- update");
      tbc.Update(10, 5);
      Assert.IsTrue(tbc.TryGetValue(10, out val, out update), "First");
      Assert.IsFalse(update, "First -- update");
      Assert.AreEqual(5, val);
      tbc.Recycle(DateTime.UtcNow);
      Assert.IsTrue(tbc.TryGetValue(10, out val, out update), "Second");
      Assert.IsTrue(update, "Second -- update");
      Assert.AreEqual(5, val);
      tbc.Recycle(DateTime.UtcNow);
      Assert.IsFalse(tbc.TryGetValue(10, out val, out update), "Recycled");
      Assert.IsFalse(update, "Recycled -- update");
      Assert.AreEqual(10, _last_ea.Key, "Key");
      Assert.AreEqual(5, _last_ea.Value, "Value");
      tbc.Stop();
    }
  }
#endif
}
