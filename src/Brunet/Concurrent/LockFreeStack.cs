using System.Threading;
using Brunet.Collections;
#if BRUNET_NUNIT
using System;
using System.Collections;
using NUnit.Framework;
#endif

namespace Brunet.Concurrent {

/** A thread-safe stack class
 * This is probably the simplest example of a transactional
 * style of thread-safe programming.
 */
public class LockFreeStack<T> {

  protected ImmutableList<T> _state;
  public ImmutableList<T> State {
    get { return _state; }
  }

  public LockFreeStack() {
    _state = ImmutableList<T>.Empty;
  }

  /** Push a value and return the state of the stack AFTER
   * 
   */
  public ImmutableList<T> Push(T val) {
    ImmutableList<T> old_state = _state;
    ImmutableList<T> state;
    ImmutableList<T> new_state;
    do {
      state = old_state;
      new_state = new ImmutableList<T>(val, state);
      old_state = Interlocked.CompareExchange<ImmutableList<T>>(ref _state, new_state, state);
    } while(old_state != state);
    return new_state;
  }

  /** try to Pop and return the state BEFORE the pop
   * @param state the state BEFORE the Pop
   * @return true if the Pop was successful
   */
  public bool TryPop(out ImmutableList<T> state) {
    ImmutableList<T> old_state = _state;
    ImmutableList<T> new_state;
    do {
      state = old_state;
      if( ImmutableList<T>.Empty == state ) {
        return false;
      }
      new_state = state.Tail;
      old_state = Interlocked.CompareExchange<ImmutableList<T>>(ref _state, new_state, state);
    } while(old_state != state);
    return true;
  }
}
#if BRUNET_NUNIT
[TestFixture]
public class LFStackTester {

  [Test]
  public void Test() {
    var rand = new Random();
    int TEST_CASES = 10000;
    var lfs = new LockFreeStack<int>();
    var good_stack = new Stack(); //.Net stack
    for(int i = 0; i < TEST_CASES; i++) {
      if( 0 == rand.Next(2) ) {
        //Push:
        int val = rand.Next();
        good_stack.Push(val);
        var state = lfs.Push(val);
        Assert.AreEqual(state.Count, good_stack.Count, "Count is the same");
      }
      else {
        //TryPop
        if( good_stack.Count > 0 ) {
          int good_val = (int)good_stack.Pop();
          ImmutableList<int> state;
          bool couldpop = lfs.TryPop(out state);
          Assert.IsTrue(couldpop, "Pop test: success");
          Assert.AreEqual(good_val, state.Head, "Pop test: equality");
          Assert.AreEqual(good_stack.Count, state.Count - 1, "Pop test: count");
          Assert.AreEqual(good_stack.Count, lfs.State.Count, "Pop test: .State count");
        }
        else {
          ImmutableList<int> state;
          Assert.IsFalse(lfs.TryPop(out state), "Empty TryPop test");
          Assert.AreEqual(state, ImmutableList<int>.Empty, "Empty test is ImmutableList<int>.Empty");
          Assert.AreEqual(0, lfs.State.Count, "Empty TryPop test: .State count");
        }
      }
    }
  }

}
#endif

}
