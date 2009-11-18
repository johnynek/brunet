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
 * This class gives us write once variables.
 * For value types, it uses boxing, which may
 * be bad for performance.
 *
 * It is thread safe.
 */
public class WriteOnce<T> {

  protected object _value;
  protected readonly static object UNSET = new object();
  
  /**
   * Get the value stored in this write once.  It is null
   * until it has been set.  Once set, future sets will throw
   * an exception.
   */
  public T Value {
    get {
      T val;
      if( TryGet(out val) ) {
        return val;
      }
      else {
        //@todo I think we should throw an exception here:
        return default(T);
      }
    }
    set {
      if (false == TrySet(value)) {
        throw new System.Exception(
                    System.String.Format("Value already set: {0}", _value));
      }
    }
  }

  public bool IsSet { get { return _value != UNSET; } }

  public WriteOnce() {
    _value = UNSET;
  }

  public override string ToString() {
    return  (_value != UNSET) ? _value.ToString() : System.String.Empty;
  }

  public bool TryGet(out T val) {
    bool result = _value != UNSET;
    if( result ) {
      val = (T)_value;
    }
    else {
      val = default(T);
    }
    return result;
  }

  /** Try to set the value.
   * @return true if the value was set
   */
  public bool TrySet(T val) {
    object old = System.Threading.Interlocked.CompareExchange(ref _value, val, UNSET);
    return ( old == UNSET );
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
    Assert.IsNull(wos2.Value, "Value set to null test");
    wos2.Value = wos2;
    Assert.AreEqual(wos2.Value, wos2, "Value set non-null test");

    //Try it with an int:
    WriteOnce<int> woi = new WriteOnce<int>();
    int iv;
    Assert.IsFalse(woi.TryGet(out iv), "int is unset");
    woi.Value = 2;
    Assert.IsTrue(woi.TryGet(out iv), "int is set");
    Assert.AreEqual(iv, 2, "tryget works");
    Assert.AreEqual(woi.Value, 2, "int is set");
    //Second set should fail:
    try {
      woi.Value = 3;
      Assert.IsTrue(false, "second int set test");
    }
    catch {
      Assert.IsTrue(true, "second int set test passed");
    }
  }
}

#endif
}
