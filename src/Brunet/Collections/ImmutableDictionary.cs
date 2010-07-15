using System;
using System.Collections;
using System.Collections.Generic;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Collections {

/** Read-only immutable data structure for IComparable Keys
 * Implemented as a readonly binary AVL tree, so most operations
 * have 1.44 Log C complexity where C is the count.
 *
 * http://en.wikipedia.org/wiki/AVL_tree
  
 * To modify, use the InsertIntoNew and RemoveFromNew methods
 * which return a new instance with minimal changes (about Log C),
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

public class ImmutableDictionary<K,V> : IDictionary<K,V>
                                        where K : System.IComparable<K> {
  public readonly K Key;
  public readonly V Value;
  public readonly ImmutableDictionary<K,V> LTDict;
  public readonly ImmutableDictionary<K,V> GTDict;
  protected readonly int _count;
  protected readonly int _depth;

  /** This is the only way to represent an Empty Dictionary
   */
  public static readonly ImmutableDictionary<K,V> Empty = new ImmutableDictionary<K,V>();

  ///////////////////////
  // Constructors 
  //////////////////////

  /** Only used to create the Empty dictionary
   */
  protected ImmutableDictionary() {
    Key = default(K);
    Value = default(V);
    LTDict = null;
    GTDict = null; 
    _count = 0;
    _depth = 0;
  }

  protected ImmutableDictionary(K key, V val, ImmutableDictionary<K,V> lt, ImmutableDictionary<K,V> gt) {
    Key = key;
    Value = val;
    LTDict = lt;
    GTDict = gt;
    _count = 1 + LTDict._count + GTDict._count;
    _depth = 1 + Math.Max(LTDict._depth, GTDict._depth);
  } 
  /** Create a Dictionary with just one pair
   */
  public ImmutableDictionary(K key, V val) : this(key, val, Empty, Empty) {
  
  }
  
  /** Create a dictionary from an existing ICollection (including
   * IDictionaries)
   */
  public ImmutableDictionary(ICollection<KeyValuePair<K,V>> kvs) :
    this(new List<KeyValuePair<K,V>>(kvs), 0, kvs.Count, true) {
  }

  /** Create a dictionary from a sorted list over the given ranges
   * Creates a balanced tree.
   */
  protected ImmutableDictionary(List<KeyValuePair<K,V>> kvs, int start,
                                int upbound, bool sort) {
    int count = upbound - start;
    if( count == 0 ) {
      //Can't handle this case
      throw new Exception("Can't create an Empty ImmutableDictionary this way, use Empty");
    }
    if( sort ) {
      kvs.Sort(this.CompareKV);
    }
    int mid = start + (count / 2);
    Key = kvs[mid].Key;
    Value = kvs[mid].Value;
    LTDict = (mid > start) ? new ImmutableDictionary<K,V>(kvs, start, mid, false) : Empty;
    GTDict = (upbound > (mid + 1)) ?
              new ImmutableDictionary<K,V>(kvs, mid+1, upbound, false)
              : Empty;
    _count = count;
    _depth = 1 + Math.Max(LTDict._depth, GTDict._depth);
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

  protected int Balance {
    get {
      if( this == Empty ) { return 0; }
      return LTDict._depth - GTDict._depth;
    }
  }

  public int Count {
    get {
      return _count;
    }
  }

  public int Depth {
    get {
      return _depth;
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
  
  override public bool Equals(object o) {
    if( object.ReferenceEquals(this, o) ) { return true; }
    var other = o as ImmutableDictionary<K,V>;
    if( other != null ) {
      //Equivalent must have same count:
      if( other._count != this._count ) { return false; }
      //Now go element by element:
      bool all_equal = true;
      //Enumeration goes in a sorted order:
      var this_enum = this.GetEnumerator();
      var o_enum = other.GetEnumerator();
      while(all_equal) {
        this_enum.MoveNext();
        //Since we have the same count, this must return same as above
        //Both are finished, but were equal to this point:
        if( false == o_enum.MoveNext() ) { return true; }
        var tkv = this_enum.Current;
        var okv = o_enum.Current;
        all_equal = tkv.Key.Equals(okv.Key) &&
                    //Handle case of null values:
                    (null != tkv.Value ? tkv.Value.Equals(okv.Value)
                                       : null == okv.Value);
      }
      return all_equal;
    }
    else {
      return false;
    }
  }

  /** Fix the root balance if LTDict and GTDict have good balance
   * Used to keep the depth less than 1.44 log_2 N (AVL tree)
   */
  protected ImmutableDictionary<K,V> FixRootBalance() {
    int bal = Balance;
    if( Math.Abs(bal) < 2 ) {
      return this; 
    }
    else if( bal == 2 ) {
      if( LTDict.Balance == 1 || LTDict.Balance == 0) {
        //Easy case:
        return this.RotateToGT();
      }
      else if( LTDict.Balance == -1 ) {
        //Rotate LTDict:
        var newlt = LTDict.RotateToLT();
        var newroot = new ImmutableDictionary<K,V>(Key, Value, newlt, GTDict);
        return newroot.RotateToGT();
      }
      else {
        throw new Exception(String.Format("LTDict too unbalanced: {0}", LTDict.Balance));
      }
    }
    else if( bal == -2 ) {
      if( GTDict.Balance == -1 || GTDict.Balance == 0 ) {
        //Easy case:
        return this.RotateToLT();
      }
      else if( GTDict.Balance == 1 ) {
        //Rotate GTDict:
        var newgt = GTDict.RotateToGT();
        var newroot = new ImmutableDictionary<K,V>(Key, Value, LTDict, newgt);
        return newroot.RotateToLT();
      }
      else {
        throw new Exception(String.Format("LTDict too unbalanced: {0}", LTDict.Balance));
      }

    }
    else {
      //In this case we can show: |bal| > 2
      //if( Math.Abs(bal) > 2 ) {
      throw new Exception(String.Format("Tree too out of balance: {0}",
                          Balance));
    }
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
  /** XOR min key and value pair hashcodes.  Cost Log N
   */
  override public int GetHashCode() {
    var imd = this.Min;
    if( imd != Empty ) {
      return imd.Key.GetHashCode() ^ (imd.Value != null ? imd.Value.GetHashCode() : 0);
    }
    else {
      return 0;
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
    var newroot = new ImmutableDictionary<K,V>(newk, newv, newlt, newgt);
    return newroot.FixRootBalance();
  }

  /** Merge two Dictionaries into one.
   */
  public static ImmutableDictionary<K,V> Merge(ImmutableDictionary<K,V> one,
                                               ImmutableDictionary<K,V> two) {
    if( two._count > one._count ) {
      //Swap them so the sub-merge is on the smaller:
      var temp = two;
      two = one;
      one = temp;
    }
    ImmutableDictionary<K,V> min;
    /*
     * A nice recursive algorithm is just return Merge,
     * rather than loop, but I'm afraid O(N) recursions
     * will cause .Net to explode EVEN THOUGH IT IS TAIL
     * RECURSION!  (they should use tailcall).
     */
    while(two._count > 0) {
      two = two.RemoveMin(out min);
      one = one.InsertIntoNew(min.Key, min.Value);
    }
    return one;
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
      var newroot = new ImmutableDictionary<K,V>(Key, Value, LTDict, newgt);
      return newroot.FixRootBalance();
    }
    else if( comp > 0 ) {
      var newlt = LTDict.RemoveFromNew(key, out old_node);
      if( old_node == Empty ) {
        //Not found, so nothing changed
        return this;
      }
      var newroot = new ImmutableDictionary<K,V>(Key, Value, newlt, GTDict);
      return newroot.FixRootBalance();
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
      var newroot = new ImmutableDictionary<K,V>(Key, Value, LTDict, newgt);
      return newroot.FixRootBalance();
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
      var newroot = new ImmutableDictionary<K,V>(Key, Value, newlt, GTDict);
      return newroot.FixRootBalance();
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
      var newroot = new ImmutableDictionary<K,V>(min.Key, min.Value, LTDict, newgt);
      return newroot.FixRootBalance();
    }
    else {
      ImmutableDictionary<K,V> max;
      var newlt = LTDict.RemoveMax(out max);
      var newroot = new ImmutableDictionary<K,V>(max.Key, max.Value, newlt, GTDict);
      return newroot.FixRootBalance();
    }
  }
  /** Move the Root into the GTDict and promote LTDict node up
   * If LTDict is empty, this operation returns this
   */
  public ImmutableDictionary<K,V> RotateToGT() {
    if (LTDict == Empty || this == Empty) {
      return this;
    }
    var gLT = LTDict.LTDict;
    var gGT = LTDict.GTDict;
    var newgt = new ImmutableDictionary<K,V>(Key, Value, gGT, GTDict);
    return new ImmutableDictionary<K,V>(LTDict.Key, LTDict.Value, gLT, newgt);
  }
  /** Move the Root into the LTDict and promote GTDict node up
   * If GTDict is empty, this operation returns this
   */
  public ImmutableDictionary<K,V> RotateToLT() {
    if (GTDict == Empty || this == Empty) {
      return this;
    }
    var gLT = GTDict.LTDict;
    var gGT = GTDict.GTDict;
    var newlt = new ImmutableDictionary<K,V>(Key, Value, LTDict, gLT);
    return new ImmutableDictionary<K,V>(GTDict.Key, GTDict.Value, newlt, gGT);
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
  public void AssertDepthGood<K,V>(ImmutableDictionary<K,V> d) where K : IComparable<K> {
      double maxdepth = 1.0 + 1.45 * Math.Log((double)d.Count)/Math.Log(2.0);
      Assert.IsTrue(d.Depth <= maxdepth,
       String.Format("Depth is too large: AVL Tree: {0} > {1}",
       d.Depth, maxdepth));
      
      double mindepth = Math.Log((double)(d.Count+1))/Math.Log(2.0);
      Assert.IsTrue(d.Depth >= mindepth,
       String.Format("Depth is too small: AVL Tree: {0} < {1}",
       d.Depth, mindepth));
  }
  [Test]
  public void EqualsTest() {
    var r = new System.Random();
    var dict = ImmutableDictionary<int,int>.Empty;
    var good_d = new Dictionary<int, int>();
    for(int i = 0; i < 100; i++) {
      int k = r.Next();
      int v = r.Next();
      var new_dict = dict.InsertIntoNew(k,v);
      good_d[k] = v;
      Assert.AreEqual(new_dict, new ImmutableDictionary<int,int>(good_d), "Equality with good");
      Assert.AreEqual(new_dict, new_dict, "Self equality (new)");
      Assert.AreEqual(dict, dict, "Self equality");
      if( dict.ContainsKey(k) == false ) {
        Assert.AreNotEqual(new_dict, dict, "new is different");
        int k2;
        do {
          k2 = r.Next();
        }
        while(new_dict.ContainsKey(k2));
        dict = dict.InsertIntoNew(k2, k2);
        //These have the same count, but should be different, harder case:
        Assert.AreNotEqual(new_dict, dict, "Hard non-equality");
      }
      dict = new_dict;
    }
  }
  [Test]
  public void Test() {
    var r = new System.Random();
    var dict = ImmutableDictionary<int, int>.Empty;
    var good_d = new Dictionary<int, int>();
    Assert.IsTrue(dict.IsEmpty, "IsEmpty test");
    Assert.AreEqual(0, dict.Count, "Initially zero");
    for(int i = 0 ; i < 1000; i++) {
      int k = r.Next();
      int v = r.Next();
      good_d.Add(k,v);
      dict = dict.InsertIntoNew(k,v);
      Assert.AreEqual(good_d.Count, dict.Count, "Equal Count");
      AssertEqualDict<int,int>(dict, dict.RotateToGT());
      AssertEqualDict<int,int>(dict, dict.RotateToLT());
      AssertDepthGood<int,int>(dict);
    }
    //Do an inorder add to really tax the balancing:
    var depthtest = ImmutableDictionary<int,int>.Empty;
    foreach(var kv in dict) {
      depthtest = depthtest.InsertIntoNew(kv.Key, kv.Value);
      AssertDepthGood<int,int>(depthtest);
    }
    AssertEqualDict<int,int>(good_d, dict);
    AssertEqualDict<int,int>(dict, good_d);
    //Check that all are in there:
    var dict2 = new ImmutableDictionary<int,int>(dict);
    AssertDepthGood<int,int>(dict2);
    AssertEqualDict<int,int>(dict, dict2);
    AssertEqualDict<int,int>(good_d, dict2);
    AssertEqualDict<int,int>(dict2, good_d);

    //Make sure that non-present keys fail:
    for(int i = 0; i < 10000; i++) {
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
      var val = good_d[k];
      good_d.Remove(k);
      ImmutableDictionary<int,int> old;
      var ndict = dict.RemoveFromNew(k, out old);
      Assert.AreEqual(old.Key, k, "Removed key is correct");
      Assert.AreEqual(old.Value, val, "Removed val is correct");
      Assert.AreEqual(good_d.Count, ndict.Count, "Remove count test");
      Assert.IsFalse(old.IsEmpty, "Old is not empty test");
      //Test RemoveRoot, RemoveMin, RemoveMax:
      var max = dict.Max;
      ImmutableDictionary<int,int> max2;
      Assert.IsFalse(dict.RemoveMax(out max2).ContainsKey(max.Key),
                     "RemoveMax removal test");
      Assert.IsTrue(max == max2, "RemoveMax out parameter test");
      var min = dict.Min;
      ImmutableDictionary<int,int> min2;
      Assert.IsFalse(dict.RemoveMin(out min2).ContainsKey(min.Key),
                     "RemoveMin removal test");
      Assert.IsTrue(min == min2, "RemoveMin out parameter test");
      //RemoveRoot depends on the above
      Assert.IsFalse(dict.RemoveRoot().ContainsKey(dict.Key),
                     "RemoveRoot works");
      dict = ndict;
    }
  }
}

#endif

}
