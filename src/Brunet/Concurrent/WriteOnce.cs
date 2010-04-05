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

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Concurrent
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
