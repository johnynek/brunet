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

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet
{
/**
 * This class gives us write once reference types.
 * It is thread safe.
 */
public class WriteOnce<T> {

  protected object _value;
  
  /**
   * Get the value stored in this write once.  It is null
   * until it has been set.  Once set, future sets will throw
   * an exception.
   */
  public T Value {
    get {
      return (T)_value;
    }
    set {
      if (false == TrySet(value)) {
        throw new System.Exception(
                    System.String.Format("Value already set: {0}", _value));
      }
    }
  }

  public WriteOnce() {
    _value = null;
  }

  public override string ToString() {
    return  (_value != null) ? _value.ToString() : System.String.Empty;
  }

  /** Try to set the value.
   * @return true if the value was set
   */
  public bool TrySet(T val) {
    object old = System.Threading.Interlocked.CompareExchange(ref _value, val, null);
    return ( old == null );
  }
    
}

#if BRUNET_NUNIT
[TestFixture]
public class WriteOnceTest {
  [Test]
  public void Test0() {
    WriteOnce<string> wos = new WriteOnce<string>();
    Assert.IsNull(wos.Value, "initial value is null");
    string s = "this is the new value";
    wos.Value = s;
    Assert.AreEqual(wos.Value, s, "First set test");
    try {
      wos.Value = "different";
      Assert.IsTrue(false, "Second set test");
    }
    catch {
      Assert.IsTrue(true, "Got exception on second set");
    }
    try {
      wos.Value = null;
      Assert.IsTrue(false, "Null set test");
    }
    catch {
      Assert.IsTrue(true, "Null set test (exception case)");
    }
    WriteOnce<object> wos2 = new WriteOnce<object>();
    wos2.Value = null;
    Assert.IsNull(wos2.Value, "Value set to null test");
    wos2.Value = wos2;
    Assert.AreEqual(wos2.Value, wos2, "Value set non-null test");
  }
}

#endif
}
