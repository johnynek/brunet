/*
Copyright (C) 2007  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System.Threading;
#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Concurrent
{
/**
 * This handles making EventHandler delegates that can
 * only be fired once.
 */
#if BRUNET_NUNIT
[TestFixture]
#endif
public class FireOnceEvent {

  private class State {
    public bool HasFired;
    public EventHandler EH;
  }

  private State _state;
  
  ///Once this is true, it will always be true
  public bool HasFired { get { return _state.HasFired; } }

  public FireOnceEvent() {
    _state = new State();
    _state.HasFired = false;
    _state.EH = null;
  }

  public void Add(EventHandler eh) {
    State old_s;
    State new_s = new State();
    new_s.HasFired = false;
    do {
      old_s = _state;
      if( old_s.HasFired ) {
        throw new Exception("Already fired");
      }
      new_s.EH = (EventHandler)Delegate.Combine(old_s.EH, eh);
    } while(old_s != Interlocked.CompareExchange<State>(ref _state, new_s, old_s));
  }
  public void Remove(EventHandler eh) {
    State old_s;
    State new_s = new State();
    do {
      old_s = _state;
      new_s.HasFired = old_s.HasFired;
      new_s.EH = (EventHandler)Delegate.Remove(old_s.EH, eh);
    } while(old_s != Interlocked.CompareExchange<State>(ref _state, new_s, old_s));
  }

  /**
   * @return true if we actually fire.
   */
  public bool Fire(object o, System.EventArgs args) {
    State new_s = new State();
    new_s.HasFired = true;
    new_s.EH = null;
    State old_s = Interlocked.Exchange(ref _state, new_s);
    if( false == old_s.HasFired) {
      //We are firing for the first time
      if( old_s.EH != null ) {
        old_s.EH(o, args);
      }
      return true;
    }
    else {
      //We have already fired
      return false;
    }
  }
#if BRUNET_NUNIT
  [Test]
  public void Test0() {
    FireOnceEvent feo = new FireOnceEvent();
    int[] fired = new int[1];
    fired[0] = 0;
    feo.Add( delegate(object o, EventArgs args) { fired[0] = fired[0] + 1; });
    Assert.IsTrue( feo.Fire(null, null), "First fire test" );
    Assert.IsFalse( feo.Fire(null, null), "Second fire test" );
    Assert.IsFalse( feo.Fire(null, null), "Second fire test" );
    Assert.IsFalse( feo.Fire(null, null), "Second fire test" );
    Assert.AreEqual(fired[0], 1, "Fire event test 2");
  }
#endif
}

}
