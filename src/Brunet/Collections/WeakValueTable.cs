using System;
using System.Collections.Generic;
#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Collections {

/** Keeps a weakreference to the value, strong to the key
 * WARNING!!! If the value is reachable from the key (i.e.
 * the key has some reference to the value, or an object that...)
 * the whole point of this class is defeated.  THINK CAREFULLY
 * ABOUT YOUR KEY TYPES.
 */
public class WeakValueTable<K,V> where V : class {

  // ////
  // Member variables
  // ////

  protected Dictionary<K,WeakReference> _tab;
  //Clean every time the size hits a multiple of this:
  private const int CLEAN_INTERVAL = 256;
  
  // ////
  // Constructors
  // ////
  
  public WeakValueTable() {
    _tab = new Dictionary<K,WeakReference>();
  } 
  // ////
  // Properties
  // ////

  // ////
  // Methods
  // ////

  /**
   * Adds a new value
   * @throw ArgumentException if key already exists
   * @throw if val is null, this doesn't make sense
   */
  public void Add(K key, V val) {
    if( val == null ) {
      throw new ArgumentNullException("Value cannot be null");
    }
    //Check if the old value exists and is dead:
    WeakReference exist;
    if( _tab.TryGetValue(key, out exist) ) {
      if( false == exist.IsAlive ) {
        //Reuse the old weakreference:
        exist.Target = val;
      }
      else {
        throw new ArgumentException(String.Format("Key {0} already present", key));
      }
    }
    else {
      //This is a new guy:
      _tab.Add(key, new WeakReference(val));
    }
    if( _tab.Count % CLEAN_INTERVAL == (CLEAN_INTERVAL - 1) ) {
      Clean();
    }
  }
  protected void Clean() {
    var to_rem = new List<K>();
    foreach(var pair in _tab) {
      if( false == pair.Value.IsAlive ) {
        to_rem.Add(pair.Key);
      }
    }
    foreach(var key in to_rem) {
      _tab.Remove(key);
    }
  }
  public void Clear() {
    _tab.Clear();
  }
  /** Returns null if not in the table
   * reading is thread-safe
   */
  public V GetValue(K key) {
    WeakReference w_val;
    V val = null;
    if( _tab.TryGetValue(key, out w_val) ) {
      val = w_val.Target as V;
      //If it is null, it is dead, and we could
      //remove, but that would break the thread-safety
      //of this method, since we'd have to use non-thread-safe
      //_tab.Remove.  Just wait till next cleanup
    }
    return val;
  }
  /** return if this key was present before the call
   */
  public bool Remove(K key) {
    bool ret = _tab.Remove(key); 
    if( _tab.Count % CLEAN_INTERVAL == (CLEAN_INTERVAL - 1) ) {
      Clean();
    }
    return ret;
  }

  /** If the key is present or not make this value the new one
   */
  public void Replace(K key, V val) {
    if( val == null ) {
      throw new ArgumentNullException("Value cannot be null");
    }
    _tab[key] = new WeakReference(val);
    if( _tab.Count % CLEAN_INTERVAL == (CLEAN_INTERVAL - 1) ) {
      Clean();
    }
  }
}
#if BRUNET_NUNIT
// /////////
// Test methods
// /////////
[TestFixture]
public class WeakValTest {

  [Test]
  public void TestContainment() {
    Random r = new Random();
    const int test_cases = 1000;
    var good = new Dictionary<int, object>();
    var test_tab = new WeakValueTable<int, object>();
    for(int i = 0; i < test_cases; i++) {
      //this is likely to have several collisions, which we want to test:
      var key = r.Next(test_cases/2);
      var val = new object();
      object existing;
      bool ingood = good.TryGetValue(key, out existing);
      object existing_test = test_tab.GetValue(key);
      Assert.AreEqual(ingood, existing_test != null, "Containment");
      if(ingood) {
        Assert.AreEqual(existing, existing_test, "Contained Value");
        test_tab.Replace(key, val);
      }
      else {
        test_tab.Add(key, val);
      }
      good[key] = val;
    }
    foreach(var kvp in good) {
      Assert.AreEqual(kvp.Value, test_tab.GetValue(kvp.Key), "Test recall");
    }
    test_tab.Clear();
    foreach(var kvp in good) {
      Assert.IsNull(test_tab.GetValue(kvp.Key), "Clear test");
    }
  }

}

#endif

}
