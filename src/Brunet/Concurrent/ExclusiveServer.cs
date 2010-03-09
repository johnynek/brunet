/*
Copyright (C) 2010  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

using Brunet.Util;
using Brunet.Collections;

namespace Brunet.Concurrent
{
/**
 * This class gives you a model where at most k threads are working
 * on customers of type C.  When the customer j <= k arrives, the thread
 * that adds the customer is used to do the work.  When customer
 * j > k arrives, the customer is added to a queue.  There is
 * no guarantee that jobs are done in order (in fact, now a stack
 * rather than a queue is used internally).
 */

public abstract class ExclusiveServer<C> {

  /** Immutable State for the ExclusiveServer
   * This state encapsulates the state machine for ExclusiveServer
   * this is for use with Mutable
   */
  protected class SQState<c> {
    public readonly int MaxServers;
    public readonly int CustomersInServiceCount;
    public readonly ImmutableList<c> CustomersWaiting;

    public SQState(int maxservers) : this(maxservers, 0,
                                             ImmutableList<c>.Empty) {
    }

    protected SQState(int servs, int count, ImmutableList<c> waiting) {
      MaxServers = servs;
      CustomersInServiceCount = count;
      CustomersWaiting = waiting;
    }

    public SQState<c> AddCustomer(c newcust) {
      if( CustomersInServiceCount < MaxServers ) {
        return new SQState<c>(MaxServers,
                           CustomersInServiceCount + 1,
                           CustomersWaiting);
      }
      else {
        return new SQState<c>(MaxServers,
                           CustomersInServiceCount,
                           CustomersWaiting.PushIntoNew(newcust));

      }
    }

    public SQState<c> FinishCustomer() {
      if( !CustomersWaiting.IsEmpty ) {
        return new SQState<c>(MaxServers,
                         CustomersInServiceCount,
                         CustomersWaiting.Tail);
      }
      else {
        //There is no one else to serve:
        return new SQState<c>(MaxServers,
                         CustomersInServiceCount - 1,
                         ImmutableList<c>.Empty);
      }
    }
  }

  protected class AddCust<c> : Mutable<SQState<c>>.Updater {
    protected readonly c NewCust;
    public AddCust(c newCust) { NewCust = newCust; }
    public SQState<c> ComputeNewState(SQState<c> old_state) {
      return old_state.AddCustomer(NewCust);
    }
  }
  protected class FinishCust<c> : Mutable<SQState<c>>.Updater {
    public SQState<c> ComputeNewState(SQState<c> old_state) {
      return old_state.FinishCustomer();
    }
  }

  private readonly Mutable<SQState<C>> _state;
  private static readonly FinishCust<C> FINISH_UPDATE = new FinishCust<C>();

  public ExclusiveServer(int max_servers) {
    _state = new Mutable<SQState<C>>(new SQState<C>(max_servers));
  }
  public ExclusiveServer() : this(1) { }

  public void Add(C customer) {
    var res = _state.Update(new AddCust<C>(customer));
    var old_s = res.First;
    var new_s = res.Second;
    if(old_s.CustomersInServiceCount < new_s.CustomersInServiceCount) {
      //We have to do work now:
      C to_serve = customer;
      Pair<SQState<C>,SQState<C>> sres = null;
      ImmutableList<C> waiting;
      do {
        Serve(to_serve);
        sres = _state.Update(FINISH_UPDATE);
        //Get out the customer waiting BEFORE we finished:
        waiting = sres.First.CustomersWaiting;
        to_serve = waiting.Head; 
      } while(!waiting.IsEmpty);
    }
    else {
      //Someone else is going to do the work!
    }
  }

  //This should NEVER throw an exception!
  abstract protected void Serve(C customer);

}

#if BRUNET_NUNIT
[TestFixture]
public class ExTest : ExclusiveServer<int> {
  public int total;
  public ExTest() : base() {
    total = 0;
  }
  override protected void Serve(int val) {
    /*
     * Here is a non-thread-safe algorithm.
     * if two threads run concurrently, it will
     * not work correctly, but since ExclusiveServer
     * should prevent that, everything should be okay.
     */
     int current = System.Threading.Interlocked.Exchange(ref total, 1000);
     int old = System.Threading.Interlocked.Exchange(ref total, current + val);
     Assert.AreEqual(1000, old, "Got -1 back");
  }

  [Test]
  public void BasicTest() {
    var serv = new ExTest();
    var total = 0;
    for(int i = 0; i < 100; i++) {
      serv.Add(i);
      total += i;
    }
    Assert.AreEqual(serv.total, total, "single threaded correct");
  }
  [Test]
  public void ThreadedTest() {
    const int MAX = 1000000;
    const int THREADS = 10;
    var serv = new ExTest();
    System.Threading.ThreadStart t_d = delegate() {
      for(int i = 0; i < 2 * MAX; i++) {
        if( i % 2 == 0 ) {        
          serv.Add(-1);
        }
        else {
          serv.Add(1);
        }
      }
    };
    var threads = new System.Collections.Generic.List<System.Threading.Thread>();
    for(int i = 0; i < THREADS; i++) {
      threads.Add(new System.Threading.Thread(t_d));
    }
    foreach(var t in threads) {
      t.Start();
    }
    foreach(var t in threads) {
      t.Join();
    }
    Assert.AreEqual(0, serv.total, "Threaded exclusive test");
  }

}
#endif

}
