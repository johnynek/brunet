using System;
using System.Collections;
using System.Collections.Generic;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Collections {

/** Read-only immutable hashtable for fast lookup (infrequent writes)
 * 
 * To modify, use the InsertIntoNew and RemoveFromNew methods
 * which return a new instance by allocating a new internal array
 * this operation is O(N), where N is the number of items 
 * so this is an efficient way to make changes without having
 * to copy the entire data structure.
 * 
 * Clearly this is a thread-safe class (because it is read-only),
 * but note: if the K or V types are not immutable, you could have
 * a problem: someone could modify the object without changing the 
 * dictionary and not only would the Dictionary be incorrectly ordered
 * you could have race conditions.  It is required that you only use
 * immutable key types in the dictionary, and only thread-safe if
 * both the keys and values are immutable.
 */

public class ImmutableHashtable<K,V> : IDictionary<K,V> {

  /** This is the only way to represent an Empty Dictionary
   */
  public static readonly ImmutableHashtable<K,V> Empty = new ImmutableHashtable<K,V>();

  protected readonly Triple<K,V,int>[] _table;
  protected readonly int _count;

  ///////////////////////
  // Constructors 
  //////////////////////

  /** Only used to create the Empty dictionary
   */
  protected ImmutableHashtable() {
    _table = new Triple<K,V,int>[1];
    _count = 0;
  }

  protected ImmutableHashtable(Triple<K,V,int>[] tab, int count) {
    _table = tab;
    _count = count;
  } 
  /** Create a Dictionary with just one pair
   */
  public ImmutableHashtable(K key, V val) {
    _table = new Triple<K,V,int>[1];
    _table[0] = new Triple<K,V,int>(key,val,key.GetHashCode());
    _count = 1;  
  }
  
  /** Create a dictionary from an existing ICollection (including
   * IDictionaries)
   */
  public ImmutableHashtable(ICollection<KeyValuePair<K,V>> kvs) {
    _count = kvs.Count;
    int length = 1;
    while( length < _count ) {
      length = length * 2;
    }
    _table = new Triple<K,V,int>[ length ];
    int mask = length - 1; 
    foreach(var kv in kvs) {
      var item = new Triple<K,V,int>(kv.Key, kv.Value,kv.Key.GetHashCode());
      int itidx = IdxInTab(_table, item.First, item.Third, mask);
      _table[itidx] = item;
    } 
  }


  ///////////////////////
  // Inner-classes
  //////////////////////
  

  ///////////////////////
  // Properties
  //////////////////////

  public int Count {
    get {
      return _count;
    }
  }

  public bool IsEmpty { get { return (_count == 0); } }

  public bool IsReadOnly { get { return true; } }

  public V this[K key] {
    get {
      int idx;
      if( TryGetIdx(key, key.GetHashCode(), out idx) ) {
        return _table[idx].Second;
      }
      else {
        throw new KeyNotFoundException(String.Format("Key: {0}", key));
      }
    }
    set {
      throw new NotSupportedException();
    }
  }

  public ICollection<K> Keys {
    get {
      ///@todo optimize this
      /*
       * we could make "Key-adapter" ICollection which
       * could basically be a thin wrapper around this Dictionary
       * so this operation is basically free
       */
      List<K> keys = new List<K>();
      foreach(var kv in this) {
        keys.Add(kv.Key);
      }
      return keys;
    }
  }

  public ICollection<V> Values {
    get {
      ///@todo optimize this
      List<V> vals = new List<V>();
      foreach(var kv in this) {
        vals.Add(kv.Value);
      }
      return vals;
    }
  }

  //////////////////////
  // Methods
  //////////////////////

  /** For IDictionary
   */
  public void Add(K key, V val) {
    throw new NotSupportedException();
  }
  /** For ICollection
   */
  public void Add(KeyValuePair<K,V> kv) {
    throw new NotSupportedException();
  }

  /** For IDictionary
   */
  public void Clear() {
    throw new NotSupportedException();
  }

  public bool Contains(KeyValuePair<K,V> kv) {
    int idx;
    if( TryGetIdx(kv.Key, kv.Key.GetHashCode(), out idx) ) {
      return object.Equals(_table[idx].Second, kv.Value);
    }
    else {
      return false;
    }
  }

  public bool ContainsKey(K key) {
    int idx;
    return TryGetIdx(key, key.GetHashCode(), out idx);
  }

  /** For ICollection
   */
  public void CopyTo(KeyValuePair<K,V>[] dest, int off) {
    int item = off;
    foreach(var kv in this) {
      dest[item] = kv;
      item += 1;
    }
  }

  /** Enumerate from smallest to largest key
   */
  public IEnumerator<KeyValuePair<K,V>> GetEnumerator() {
    foreach(var pair in _table) {
      if(null != pair) {
        yield return new KeyValuePair<K,V>(pair.First, pair.Second);
      }
    }
  }
  IEnumerator IEnumerable.GetEnumerator() {
    return this.GetEnumerator();
  }
  

  /** Return the index in the table of a given key 
   */
  protected bool TryGetIdx(K key, int hc, out int idx) {
    int length = _table.Length;
    int mask = length - 1;
    idx = IdxInTab(_table, key, hc, mask);
    if( idx < 0 ) {
      return false;
    }
    else {
      return null != _table[idx];
    }
  }
 
  protected static int IdxInTab(Triple<K,V,int>[] tab, K key, int hc, int mask) {
    int idx = hc & mask;
    for(int i = 0; i <= mask; i++) {
      int pos = idx ^ i;
      var this_it = tab[pos];
      if( null == this_it ) {
        return pos;
      }
      else if( (this_it.Third == hc) && (this_it.First.Equals(key)) ) {
        //This is a repeated key:
        return pos;
      }
    }
    return -1;
  }
  /** Return a new tree with the key-value pair inserted
   * If the key is already present, it replaces the value
   * This operation is O(N) where N is the number of keys
   */
  public ImmutableHashtable<K,V> InsertIntoNew(K key, V val) {
    if( _count == 0 ) {
      return new ImmutableHashtable<K,V>(key, val);
    }
    int hc = key.GetHashCode();
    int length = _table.Length;
    int mask = length - 1;
    int count;
    
    var new_item = new Triple<K,V,int>(key,val,hc);
    Triple<K,V,int>[] new_table;
    int idx = hc & mask;
    if( null == _table[idx] ) {
      //We keep the same mask no matter what if there is no collision
      new_table = new Triple<K,V,int>[length];
      System.Array.Copy(_table, 0, new_table, 0, length);
      new_table[idx] = new_item;
      count = _count + 1;
    }
    else if( 3 * _count < 2 * length ) {
      //Don't rehash if we are not too full
      idx = IdxInTab(_table, key, hc, mask);
      //If _table[idx] is empty, we are increasing the count
      count = (null == _table[idx]) ? _count + 1 : _count;
      new_table = new Triple<K,V,int>[length];
      System.Array.Copy(_table, 0, new_table, 0, length);
      new_table[idx] = new_item;
    }
    else {
      //Check to see if this is a new or existing item:
      count = TryGetIdx(key, hc, out idx) ? _count : _count + 1;
      //Rehash
      new_table = new Triple<K,V,int>[ 2 * length ];
      mask = (2 * length) - 1;
      //Put the new one in:
      idx = IdxInTab(new_table, key, hc, mask);
      new_table[idx] = new_item;
      //Done with new_item, now look at old items:
      for(int i = 0; i < length; i++) {
        var item = _table[i];
        if( null != item ) {
          idx = IdxInTab(new_table, item.First, item.Third, mask);
          new_table[idx] = item;
        }
      }
    }
    return new ImmutableHashtable<K,V>(new_table, count);
  }

  /** For IDictionary
   */
  public bool Remove(K key) {
    throw new NotSupportedException();
  }
  /** For ICollection
   */
  public bool Remove(KeyValuePair<K,V> kv) {
    throw new NotSupportedException();
  }
                                                 
  /** Try to remove the key, and return the resulting Dict
   * if the key is not found return this
   */
  public ImmutableHashtable<K,V> RemoveFromNew(K key) {
    if( false == ContainsKey(key) ) {
      return this;
    }
    int length = _table.Length;
    Triple<K,V,int>[] new_table;
    int mask;
    if( 4 * _count < length ) {
      //Count is getting kind of small:
      new_table = new Triple<K,V,int>[ length / 2];
      mask = (length / 2) - 1;
    }
    else {
      //Keep the same size:
      new_table = new Triple<K,V,int>[ length ];
      mask = length - 1;
    }
    /*
     * If we don't rehash, we have to check that
     * by nulling out a particular entry, we don't hide collisions with
     * that entry.  I don't see an efficient way to do that, so, we are
     * rehashing the whole table:
     */
    int count = _count;
    //Rehash:
    int hc = key.GetHashCode();
    for(int i = 0; i < length; i++) {
      var item = _table[i];
      if( null != item) {
        if ((hc != item.Third) || (false == key.Equals(item.First))) {
          //They are different, do the cheap check first
          int itidx = IdxInTab(new_table, item.First, item.Third, mask);
          new_table[itidx] = item;
        }
        else {
          count -= 1;
        }
      }
    }
    return new ImmutableHashtable<K,V>(new_table, count);
  }

  public bool TryGetValue(K key, out V val) {
    int idx;
    if( TryGetIdx(key, key.GetHashCode(), out idx) ) {
      val = _table[idx].Second;
      return true;
    }
    else {
      val = default(V);
      return false;
    }
  }
}

#if BRUNET_NUNIT

[TestFixture]
public class ImHashTest {
  public void AssertEqualDict<K,V>(IDictionary<K,V> d1, IDictionary<K,V> d2) {
    Assert.AreEqual(d1.Count, d2.Count, "Equal Count");
    foreach(var kv in d1) {
      V val;
      bool has_it = d2.TryGetValue(kv.Key, out val);
      Assert.IsTrue(d2.ContainsKey(kv.Key), "ContainsKey test");
      Assert.IsTrue(has_it, "TryGetValue return test");
      Assert.AreEqual(val, kv.Value, "TryGetValue value test");
    }
  }
  [Test]
  public void Test() {
    var r = new System.Random();
    var dict = ImmutableHashtable<int, int>.Empty;
    var good_d = new Dictionary<int, int>();
    Assert.IsTrue(dict.IsEmpty, "IsEmpty test");
    Assert.AreEqual(0, dict.Count, "Initially zero");
    for(int i = 0 ; i < 1000; i++) {
      int k = r.Next();
      int v = r.Next();
      var ndict = dict.InsertIntoNew(k,v);
      //Insertion didn't change the old dict
      AssertEqualDict<int,int>(dict, good_d);
      AssertEqualDict<int,int>(good_d, dict);
      dict = ndict;
      good_d.Add(k,v);
      Assert.AreEqual(good_d.Count, dict.Count, "Equal Count");
      AssertEqualDict<int,int>(good_d, dict);
      AssertEqualDict<int,int>(dict, good_d);
    }
    //Check that all are in there:
    var dict2 = new ImmutableHashtable<int,int>(dict);
    AssertEqualDict<int,int>(dict, dict2);
    AssertEqualDict<int,int>(good_d, dict2);
    AssertEqualDict<int,int>(dict2, good_d);

    //Make sure that non-present keys fail:
    for(int i = 0; i < 1000; i++) {
      //Generate a random key:
      int k = r.Next();
      bool ispresent = good_d.ContainsKey(k);
      Assert.AreEqual(ispresent, dict.ContainsKey(k),
                      "ContainsKey test");
      if( !ispresent ) {
        int val;
        Assert.IsFalse(dict.TryGetValue(k, out val),
                       "TryGetValue fails on bad key");
      }
    }
    //Remove everything:
    var all_keys = new List<int>(good_d.Keys);
    foreach(var k in all_keys) {
      var val = good_d[k];
      var ndict = dict.RemoveFromNew(k);
      //Make sure removal didn't change dict
      AssertEqualDict<int,int>(dict, good_d);
      AssertEqualDict<int,int>(good_d, dict);
      good_d.Remove(k);
      Assert.AreEqual(good_d.Count, ndict.Count, "Remove count test");
      AssertEqualDict<int,int>(ndict, good_d);
      AssertEqualDict<int,int>(good_d, ndict);
      //RemoveRoot depends on the above
      Assert.IsFalse(ndict.ContainsKey(k));
      dict = ndict;
    }
  }
}

#endif

}
