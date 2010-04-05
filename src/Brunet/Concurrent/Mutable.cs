/*
Copyright (C) 2010  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

using Brunet.Collections;
using System.Threading;

namespace Brunet.Concurrent
{
/**
 * This class gives us thread-safe, lock-free mutable variables.
 * T is the immutable state object.  R is the side-result that
 * is returned as the Second part of a Pair when changing state
 */
public class Mutable<T> where T : class {
  /*
   * Interfaces are faster than delegate, for performance critical code,
   * implement this
   */
  public interface Updater<R> {
    /* 
     * given the old state, compute a new state
     * and return side information R
     * This may be called more than once before
     * it succeeds, so this should be a purely functional
     * operation (no IO).
     */
    Pair<T,R> ComputeNewStateSide(T oldstate);
  }
  public interface Updater {
    /* 
     * given the old state, compute a new state
     * This may be called more than once before
     * it succeeds, so this should be a purely functional
     * operation (no IO).
     */
    T ComputeNewState(T oldstate);
  }

  protected T _state;

  public T State {
    get { return _state; }
  }

  public Mutable(T initial) {
    _state = initial;
  }

  public T Exchange(T newval) {
    return Interlocked.Exchange<T>(ref _state, newval);
  }

  /** Update and return old and new state
   * NO GUARANTEE THAT update_meth IS ONLY CALLED ONCE!!!!
   * update_meth should return a new state based on the old state
   */
  public Pair<T,T> Update(System.Converter<T,T> update_meth) {
    T old_state = _state;
    T state;
    T new_state;
    do {
      state = old_state;
      new_state = update_meth(state);
      old_state = Interlocked.CompareExchange<T>(ref _state, new_state, state);
    } while(old_state != state);
    return new Pair<T,T>(state, new_state);
  }
  /** Update and return old, new and side result
   * NO GUARANTEE THAT update_meth IS ONLY CALLED ONCE!!!!
   * update_meth should return a pair that has the new state, and a side
   * result
   * the returned value gives the old state, new state and that side value
   */
  public Triple<T,T,R> Update<R>(System.Converter<T,Pair<T,R>> update_meth) {
    T old_state = _state;
    T state;
    Pair<T,R> res;
    do {
      state = old_state;
      res = update_meth(state);
      old_state = Interlocked.CompareExchange(ref _state, res.First, state);
    } while(old_state != state);
    return new Triple<T,T,R>(state, res.First, res.Second);
  }
  /** Update and return old and new state
   * this is faster than the delegate based approach
   * NO GUARANTEE THAT update_meth IS ONLY CALLED ONCE!!!!
   * update_meth should return a new state based on the old state
   */
  public Pair<T,T> Update(Mutable<T>.Updater update_meth) {
    T old_state = _state;
    T state;
    T new_state;
    do {
      state = old_state;
      new_state = update_meth.ComputeNewState(state);
      old_state = Interlocked.CompareExchange<T>(ref _state, new_state, state);
    } while(old_state != state);
    return new Pair<T,T>(state, new_state);
  }
  /** Update and return old, new and side result
   * this is faster than the delegate based approach
   * NO GUARANTEE THAT update_meth IS ONLY CALLED ONCE!!!!
   * update_meth should return a pair that has the new state, and a side
   * result
   * the returned value gives the old state, new state and that side value
   */
  public Triple<T,T,R> Update<R>(Mutable<T>.Updater<R> update_meth) {
    T old_state = _state;
    T state;
    Pair<T,R> res;
    do {
      state = old_state;
      res = update_meth.ComputeNewStateSide(state);
      old_state = Interlocked.CompareExchange(ref _state, res.First, state);
    } while(old_state != state);
    return new Triple<T,T,R>(state, res.First, res.Second);
  }
}

#if BRUNET_NUNIT
#endif
}
