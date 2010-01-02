using System;
using System.Collections;
using System.Collections.Generic;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Util {

public class ImmutableDictionary<K,V> : IDictionary<K,V>
                                        where K : System.IComparable<K> {
  public readonly K Key;
  public readonly V Value;
  public readonly ImmutableDictionary<K,V> LTDict;
  public readonly ImmutableDictionary<K,V> GTDict;
  protected readonly int _count;

  public static readonly ImmutableDictionary<K,V> Empty = new ImmutableDictionary<K,V>();

  ///////////////////////
  // Constructors 
  //////////////////////

  protected ImmutableDictionary() {
    Key = default(K);
    Value = default(V);
    LTDict = null;
    GTDict = null; 
    _count = 0;
  }

  protected ImmutableDictionary(K key, V val, ImmutableDictionary<K,V> lt, ImmutableDictionary<K,V> gt) {
    Key = key;
    Value = val;
    LTDict = lt;
    GTDict = gt;
    _count = 1 + LTDict._count + GTDict._count;
  } 
  /** Create a Dictionary with just one pair
   */
  public ImmutableDictionary(K key, V val) {
    Key = key;
    Value = val;
    LTDict = Empty;
    GTDict = Empty;
    _count = 1;
  }
  
  public ImmutableDictionary(ICollection<KeyValuePair<K,V>> kvs) :
    this(new List<KeyValuePair<K,V>>(kvs), 0, kvs.Count, true) {
  }

  /** Create a dictionary from a sorted list over the given ranges
   * Creates a balanced tree.
   */
  protected ImmutableDictionary(List<KeyValuePair<K,V>> kvs, int start,
                                int upbound, bool sort) {
    if( sort ) {
      kvs.Sort(this.CompareKV);
    }
    int count = upbound - start;
    if( count == 0 ) {
      //Can't handle this case
      throw new Exception("Can't create an Empty ImmutableDictionary this way, use Empty");
    }
    int mid = start + (count / 2);
    Key = kvs[mid].Key;
    Value = kvs[mid].Value;
    LTDict = (mid > start) ? new ImmutableDictionary<K,V>(kvs, start, mid, false) : Empty;
    GTDict = (upbound > (mid + 1)) ?
              new ImmutableDictionary<K,V>(kvs, mid+1, upbound, false)
              : Empty;
    _count = count;
  }

  ///////////////////////
  // Inner-classes
  //////////////////////
  
  protected class MaxToMinEnumerable<K1,V1> : IEnumerable,
                                              IEnumerable<KeyValuePair<K1,V1>>
                                              where K1 : IComparable<K1> {
    protected readonly ImmutableDictionary<K1,V1> _dict;
    public MaxToMinEnumerable(ImmutableDictionary<K1,V1> dict) {
      _dict = dict;
    }
    public IEnumerator<KeyValuePair<K1,V1>> GetEnumerator() {
      return _dict.GetMaxToMinEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() {
      return _dict.GetMaxToMinEnumerator();
    }
  }

  ///////////////////////
  // Properties
  //////////////////////

  public int Count {
    get {
      return _count;
    }
  }

  public int Depth {
    get {
      if( this == Empty ) {
        return 0;
      }
      else {
        return 1 + Math.Max(LTDict.Depth, GTDict.Depth);
      }
    }
  }

  public bool IsEmpty { get { return this == Empty; } }

  public bool IsReadOnly { get { return true; } }

  public V this[K key] {
    get {
      var node = GetKey(key);
      if(node != Empty) {
        return node.Value;
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

  /** Return the subtree with the max value at the root, or Empty if Empty
   */
  public ImmutableDictionary<K,V> Max {
    get {
      if( this == Empty ) { return Empty; }
      var dict = this;
      var next = dict.GTDict;
      while(next != Empty) {
        dict = next;
        next = dict.GTDict;
      }
      return dict;
    }
  }

  public IEnumerable<KeyValuePair<K,V>> MaxToMin {
    get {
      return new MaxToMinEnumerable<K,V>(this);
    }
  }

  /** Return the subtree with the min value at the root, or Empty if Empty
   */
  public ImmutableDictionary<K,V> Min {
    get {
      if( this == Empty ) { return Empty; }
      var dict = this;
      var next = dict.LTDict;
      while(next != Empty) {
        dict = next;
        next = dict.LTDict;
      }
      return dict;
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
    var node = this.GetKey(kv.Key);
    return (node != Empty) && object.Equals(node.Value, kv.Value);
  }

  public bool ContainsKey(K key) {
    return GetKey(key) != Empty;
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

  /** Compare a KeyValuePair based only on keys
   */
  protected int CompareKV(KeyValuePair<K,V> kv0, KeyValuePair<K,V> kv1) {
    return kv0.Key.CompareTo(kv1.Key);
  }

  /** Enumerate from smallest to largest key
   */
  public IEnumerator<KeyValuePair<K,V>> GetEnumerator() {
    var to_visit = new Stack<ImmutableDictionary<K,V>>();
    to_visit.Push(this);
    while(to_visit.Count > 0) {
      var this_d = to_visit.Pop();
      if( this_d == Empty ) { continue; }
      if( this_d.LTDict == Empty ) {
        //This is the next smallest value in the Dict:
        yield return new KeyValuePair<K,V>(this_d.Key, this_d.Value);
        to_visit.Push(this_d.GTDict);
      }
      else {
        //Break it up
        to_visit.Push(this_d.GTDict);
        to_visit.Push(new ImmutableDictionary<K,V>(this_d.Key, this_d.Value));
        to_visit.Push(this_d.LTDict);
      }
    }
  }
  IEnumerator IEnumerable.GetEnumerator() {
    return this.GetEnumerator();
  }
  
  /** Enumerate from largest to smallest key
   */
  public IEnumerator<KeyValuePair<K,V>> GetMaxToMinEnumerator() {
    var to_visit = new Stack<ImmutableDictionary<K,V>>();
    to_visit.Push(this);
    while(to_visit.Count > 0) {
      var this_d = to_visit.Pop();
      if( this_d == Empty ) { continue; }
      if( this_d.GTDict == Empty ) {
        //This is the next biggest value in the Dict:
        yield return new KeyValuePair<K,V>(this_d.Key, this_d.Value);
        to_visit.Push(this_d.LTDict);
      }
      else {
        //Break it up
        to_visit.Push(this_d.LTDict);
        to_visit.Push(new ImmutableDictionary<K,V>(this_d.Key, this_d.Value));
        to_visit.Push(this_d.GTDict);
      }
    }
  }

  /** Return the sub-tree with the given key as the root or Empty
   */
  public ImmutableDictionary<K,V> GetKey(K key) {
    var dict = this;
    while(dict != Empty) {
      int comp = dict.Key.CompareTo(key);
      if( comp < 0 ) {
        dict = dict.GTDict;
      }
      else if( comp > 0 ) {
        dict = dict.LTDict;
      }
      else {
        //Awesome:
        return dict;
      }
    }
    return Empty;
  }

  /** Return a new tree with the key-value pair inserted
   * If the key is already present, it replaces the value
   * This operation is O(Log N) where N is the number of keys
   */
  public ImmutableDictionary<K,V> InsertIntoNew(K key, V val) {
    if( this == Empty ) {
      return new ImmutableDictionary<K,V>(key, val);
    }
    K newk = Key;
    V newv = Value;
    ImmutableDictionary<K,V> newlt = LTDict;
    ImmutableDictionary<K,V> newgt = GTDict;
    
    int comp = Key.CompareTo(key);
    if( comp < 0 ) {
      //Let the GTDict put it in:
      newgt = GTDict.InsertIntoNew(key, val);
    }
    else if( comp > 0 ) {
      //Let the LTDict put it in:
      newlt = LTDict.InsertIntoNew(key, val);
    }
    else {
      //Replace the current value:
      newk = key;
      newv = val;
    }
    return new ImmutableDictionary<K,V>(newk, newv, newlt, newgt);
  }

  /** Merge two Dictionaries into one.
   */
  public static ImmutableDictionary<K,V> Merge(ImmutableDictionary<K,V> one,
                                               ImmutableDictionary<K,V> two) {
    if( one == Empty ) {
      return two; 
    }
    if( two == Empty ) {
      return one;
    }
    //Neither are Empty
    //Merge from two into one:
    if( two._count > one._count ) {
      //Swap them so the sub-merge is on the smaller:
      var temp = two;
      two = one;
      one = temp;
    }
    var two_m = Merge(two.LTDict, two.GTDict);
    var one_m = one.InsertIntoNew(two.Key, two.Value);
    return Merge(one_m, two_m); 
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
   * if the key is not found, old_node is Empty, else old_node is the Dict
   * with matching Key
   */
  public ImmutableDictionary<K,V> RemoveFromNew(K key, out ImmutableDictionary<K,V> old_node) {
    if( this == Empty ) {
      old_node = Empty;
      return Empty;
    }
    int comp = Key.CompareTo(key);
    if( comp < 0 ) {
      var newgt = GTDict.RemoveFromNew(key, out old_node);
      if( old_node == Empty ) {
        //Not found, so nothing changed
        return this;
      }
      return new ImmutableDictionary<K,V>(Key, Value, LTDict, newgt);
    }
    else if( comp > 0 ) {
      var newlt = LTDict.RemoveFromNew(key, out old_node);
      if( old_node == Empty ) {
        //Not found, so nothing changed
        return this;
      }
      return new ImmutableDictionary<K,V>(Key, Value, newlt, GTDict);
    }
    else {
      //found it
      old_node = this;
      return RemoveRoot();
    }
  }

  public ImmutableDictionary<K,V> RemoveMax(out ImmutableDictionary<K,V> max)
  {
    if(this == Empty) {
      max = Empty;
      return Empty;
    }
    if( GTDict == Empty ) {
      //We are the max:
      max = this;
      return LTDict;
    }
    else {
      //Go down:
      var newgt = GTDict.RemoveMax(out max);
      return new ImmutableDictionary<K,V>(Key, Value, LTDict, newgt);
    }
  }

  public ImmutableDictionary<K,V> RemoveMin(out ImmutableDictionary<K,V> min)
  {
    if(this == Empty) {
      min = Empty;
      return Empty;
    }
    if( LTDict == Empty ) {
      //We are the minimum:
      min = this;
      return GTDict;
    }
    else {
      //Go down:
      var newlt = LTDict.RemoveMin(out min);
      return new ImmutableDictionary<K,V>(Key, Value, newlt, GTDict);
    }
  }

  /** Return a new dict with the root key-value pair removed
   */
  public ImmutableDictionary<K,V> RemoveRoot() {
    if( this == Empty ) {
      return this;
    }
    if( LTDict == Empty ) {
      return GTDict;
    }
    if( GTDict == Empty ) {
      return LTDict;
    }
    //Neither are empty:
    if( LTDict._count < GTDict._count ) {
      //LTDict has fewer, so promote from GTDict to minimize depth
      ImmutableDictionary<K,V> min;
      var newgt = GTDict.RemoveMin(out min);
      return new ImmutableDictionary<K,V>(min.Key, min.Value, LTDict, newgt);
    }
    else {
      ImmutableDictionary<K,V> max;
      var newlt = LTDict.RemoveMax(out max);
      return new ImmutableDictionary<K,V>(max.Key, max.Value, newlt, GTDict);
    }
  }

  public bool TryGetValue(K key, out V val) {
    var dict = GetKey(key);
    val = dict.Value;
    return dict != Empty;
  }
}

#if BRUNET_NUNIT

[TestFixture]
public class ImDictTest {
  [Test]
  public void Test() {
    var r = new System.Random();
    var dict = ImmutableDictionary<int, int>.Empty;
    var good_d = new Dictionary<int, int>();
    Assert.IsTrue(dict.IsEmpty, "IsEmpty test");
    Assert.AreEqual(0, dict.Count, "Initially zero");
    for(int i = 0 ; i < 10000; i++) {
      int k = r.Next();
      int v = r.Next();
      good_d.Add(k,v);
      dict = dict.InsertIntoNew(k,v);
      Assert.AreEqual(good_d.Count, dict.Count, "Equal Count");
    }
    //Check that all are in there:
    foreach(var kv in good_d) {
      int val;
      bool has_it = dict.TryGetValue(kv.Key, out val);
      Assert.IsTrue(dict.ContainsKey(kv.Key), "ContainsKey test");
      Assert.IsTrue(has_it, "TryGetValue return test");
      Assert.AreEqual(val, kv.Value, "TryGetValue value test");
    }
    var dict2 = new ImmutableDictionary<int,int>(dict);
    //Check that all are in dict2:
    Assert.AreEqual(dict2.Count, good_d.Count, "dict2 equal size");
    foreach(var kv in good_d) {
      int val;
      bool has_it = dict2.TryGetValue(kv.Key, out val);
      Assert.IsTrue(dict2.ContainsKey(kv.Key), "ContainsKey test2");
      Assert.IsTrue(has_it, "TryGetValue return test2");
      Assert.AreEqual(val, kv.Value, "TryGetValue value test2");
    }
    //Enumeration testing:
    var d_key_list = new List<int>();
    foreach(var kv in dict) {
      d_key_list.Add(kv.Key);
    }
    for(int i = 0; i < d_key_list.Count - 1; i++) {
      Assert.IsTrue(d_key_list[i].CompareTo(d_key_list[i+1]) < 0, "Sorted enumeration");
    }
    //Max-to-min Enumeration testing:
    var d_key_list2 = new List<int>();
    foreach(var kv in dict.MaxToMin) {
      d_key_list2.Add(kv.Key);
    }
    for(int i = 0; i < d_key_list2.Count - 1; i++) {
      Assert.IsTrue(d_key_list2[i].CompareTo(d_key_list2[i+1]) > 0, "Sorted enumeration 2");
    }
    //Remove everything:
    var all_keys = new List<int>(good_d.Keys);
    foreach(var k in all_keys) {
      good_d.Remove(k);
      ImmutableDictionary<int,int> old;
      dict = dict.RemoveFromNew(k, out old);
      Assert.AreEqual(good_d.Count, dict.Count, "Remove count test");
      Assert.IsFalse(old.IsEmpty, "Old is not empty test");
    }
  }
}

#endif

}
