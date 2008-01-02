/*
Copyright (C) 2007  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet
{
/**
 * This handles making EventHandler delegates that can
 * only be fired once.
 */
#if BRUNET_NUNIT
[TestFixture]
#endif
public class FireOnceEvent {

  private readonly object _sync;
  private EventHandler _eh;
  private bool _have_fired;

  public FireOnceEvent() : this(null) { }

  /**
   * @param sync the object to use to lock
   */
  public FireOnceEvent(object sync) {
    if( sync == null ) {
      _sync = new object();
    }
    else {
      _sync = sync;
    }
    _have_fired = false;
  } 

  public void Add(EventHandler eh) {
    lock( _sync ) {
      if( _have_fired ) {
        throw new Exception("Already fired");
      }
      _eh = (EventHandler)Delegate.Combine(_eh, eh);
    } 
  }
  public void Remove(EventHandler eh) {
    lock( _sync ) {
      _eh = (EventHandler)Delegate.Remove(_eh, eh);
    }
  }

  /**
   * @return true if we actually fire.
   */
  public bool Fire(object o, System.EventArgs args) {
    EventHandler eh = null;
    bool fire = false;
    lock( _sync ) {
      if( !_have_fired ) {
        _have_fired = true;
        fire = true;
        eh = _eh;
        _eh = null;
      }
    }
    if( eh != null ) {
      eh(o, args);
    }
    return fire;
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
