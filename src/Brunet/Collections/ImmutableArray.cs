using System;
using System.Collections;
using System.Collections.Generic;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Collections {

/** Simple immutable array class, O(1) read, O(N) write
 * Useful in cases where writing is very rare, but reading is common
 * ImmutableArray<byte> is very similar to a MemBlock.
 */
public class ImmutableArray<T> : IList<T> {

  public int Count {
    get {
      return _length;
    }
  }
  /** For IList<T> always true */
  public bool IsReadOnly { get { return true; } }

  protected readonly T[] _data;
  protected readonly int _offset;
  protected readonly int _length;
  //This represents the empty list:
  public static readonly ImmutableArray<T> Empty = new ImmutableArray<T>();

  /**
   * this is an O(1) time operation.
   */
  public T this[int idx] {
    get {
      if (idx < 0) {
        throw new System.ArgumentOutOfRangeException("index", idx, "cannot be negative");
      }
      if (idx >= _length) {
        throw new System.ArgumentOutOfRangeException("index", idx,
                  String.Format("must be less than length: {0}",_length));
      }
      return _data[idx + _offset];
    }
    set {
      throw new System.NotSupportedException();
    }
  }

  protected ImmutableArray() {
    _data = new T[0]; //Make sure this is not null
    _offset = 0;
    _length = 0;
  }

  public ImmutableArray(params T[] vals) {
    _data = vals;
    _offset = 0;
    _length = vals.Length;
  }
  protected ImmutableArray(T[] array, int offset, int length) {
    if( offset < 0 ) {
      throw new System.ArgumentOutOfRangeException("offset", offset, "cannot be negative");
    }
    if( length + offset > array.Length ) {
      throw new System.ArgumentOutOfRangeException("length", length,
          String.Format("length({0}) + offset({1}) past array.Length({2})",
          length, offset, array.Length));
    }
    _data = array;
    _offset = offset;
    _length = length; 
  }
 // ///////
 // Factory methods
 // /////
  public static ImmutableArray<T> Copy(T[] array, int offset, int length) {
    T[] data = new T[length];
    Array.Copy(array, offset, data, 0, length);
    return new ImmutableArray<T>(data, 0, length);
  }
  public static ImmutableArray<T> Copy(T[] array) {
    return Copy(array, 0, array.Length);
  }
  public static ImmutableArray<T> Copy(IEnumerable<T> data) {
    return Empty.AddRangeIntoNew(data);
  }
  public static ImmutableArray<T> Reference(T[] array, int offset, int length) {
    return new ImmutableArray<T>(array, offset, length);
  }
  public static ImmutableArray<T> Reference(T[] array) {
    return new ImmutableArray<T>(array, 0, array.Length);
  } 

 // //////////
 // Methods
 // //////////

  public void Add(T item) {
    throw new System.NotSupportedException();
  }
  public ImmutableArray<T> AddIntoNew(T item) {
    return InsertIntoNew(_length, item);
  }
  public ImmutableArray<T> AddRangeIntoNew(IEnumerable<T> data) {
    ICollection<T> c_data = data as ICollection<T>;
    int count;
    if( c_data == null ) {
      c_data = new List<T>(data);
    }
    count = c_data.Count;
    int new_length = _length + count;
    T[] new_data = new T[new_length];
    Array.Copy(_data, _offset, new_data, 0, _length);
    c_data.CopyTo(new_data, _length);
    return new ImmutableArray<T>(new_data, 0, new_length);
  }

  public void Clear() {
    throw new System.NotSupportedException();
  }

  public bool Contains(T item) {
    return IndexOf(item) != -1;
  }

  public void CopyTo(T[] dest, int idx) {
    Array.Copy(_data, _offset, dest, idx, _length);
  }

  public override bool Equals(object o) {
    if( object.ReferenceEquals(this, o) ) {
      return true;
    }
    ImmutableArray<T> other = o as ImmutableArray<T>;
    if( null == other ) { return false; }
    if( other._length != this._length ) { return false; }
    if( (other._data == this._data )
        && (other._offset == this._offset) ) {
      //Must be equal in this case:
      return true;
    }
    for(int i = 0; i < _length; i++) {
      T item = _data[_offset + i];
      T o_item = other._data[ other._offset + i];
      bool same = null != item ? item.Equals(o_item) : null == o_item;
      if(!same) { return false; }
    }
    return true; 
  }

  public IEnumerator<T> GetEnumerator() {
    for(int i = _offset; i < (_offset + _length); i++) {
      yield return _data[i];
    }
  }

  IEnumerator IEnumerable.GetEnumerator() {
    return this.GetEnumerator();
  }

  /// if Head is not null, Head is hashcode, else Tail if not null, else 0
  public override int GetHashCode() {
    for(int i = _offset; i < (_offset + _length); i++) {
      T item = _data[i];
      if( null != item ) { return _length ^ item.GetHashCode(); }
    }
    //All are null, return length 
    return _length;
  }

  public int IndexOf(T item) {
    int idx = 0;
    bool notnull = item != null;
    foreach(T val in this) {
      bool eq = notnull ? item.Equals(val) : val == null;
      if(eq) {
        return idx;
      }
      idx += 1;
    }
    return -1;
  }

  public void Insert(int idx, T val) {
    throw new System.NotSupportedException();
  }

  
  /** Just like Insert, but returns a new Array
   */
  public ImmutableArray<T> InsertIntoNew(int idx, T val) {
    int new_length = _length + 1;
    T[] new_data = new T[new_length];
    if( (idx >= 0) && (idx <= _length)) {
      Array.Copy(_data, _offset, new_data, 0, idx);
      new_data[idx] = val;
      Array.Copy(_data, _offset + idx, new_data, idx + 1, _length - idx);
      return new ImmutableArray<T>(new_data, 0, new_length);
    }
    else {
      throw new System.ArgumentOutOfRangeException("idx", idx,
                String.Format("Must be between [0,{1}]",_length));
    }
  }

  /** Treat the list like a stack and push a new value to index==0
   */
  public ImmutableArray<T> PushIntoNew(T val) {
    return InsertIntoNew(0,val);
  }

  public bool Remove(T val) {
    throw new System.NotSupportedException();
  }
  /** if item not in list, return this, else remove first occurence
   */
  public ImmutableArray<T> RemoveFromNew(T val) {
    int idx = IndexOf(val);
    if( idx < 0 ) { return this; }
    else {
      return RemoveAtFromNew(idx);
    }
  }

  public void RemoveAt(int idx) {
    throw new System.NotSupportedException();
  }

  public ImmutableArray<T> RemoveAtFromNew(int idx) {
    if( idx < 0 || idx >= _length) {
      throw new System.ArgumentOutOfRangeException("idx", idx,
                String.Format("< 0 or >= {0}",_length));
    }
    int new_length = _length - 1;
    int rest = (_length - 1) - idx;
    T[] new_data = new T[new_length];
    Array.Copy(_data, _offset, new_data, 0, idx);
    //Skip over the idx element
    Array.Copy(_data, _offset + idx + 1, new_data, idx, rest);
    return new ImmutableArray<T>(new_data, 0, new_length);
  }
  /** replace a value 
   */
  public ImmutableArray<T> ReplaceIntoNew(int idx, T val) {
    T[] new_data = new T[_length];
    Array.Copy(_data, _offset, new_data, 0, _length);
    new_data[idx] = val;
    return new ImmutableArray<T>(new_data, 0, _length);
  } 

}

#if BRUNET_NUNIT
[TestFixture]
public class ImmutArrayTest {
  public void ListEqual<T>(IList<T> l1, IList<T> l2) {
    var e1 = l1.GetEnumerator();
    var e2 = l2.GetEnumerator();
    bool cont1 = e1.MoveNext();
    bool cont2 = e2.MoveNext();
    Assert.AreEqual(cont1, cont2, "first move");
    while(cont1) {
      Assert.AreEqual(e1.Current, e2.Current, "List equality");
      cont1 = e1.MoveNext();
      cont2 = e2.MoveNext();
      Assert.AreEqual(cont1, cont2, "move");
    }
  }
  [Test]
  public void Test() {
    int MAX_VAL = 40;
    var intlist = ImmutableArray<int>.Empty;
    Assert.AreEqual(0, intlist.Count, "Empty count test");
    for(int i = MAX_VAL; i >= 0; i--) {
      intlist = intlist.PushIntoNew(i);
      Assert.AreEqual(intlist.Count, MAX_VAL - i + 1, "Count works");
    }
    for(int i = 0; i <= MAX_VAL; i++) {
      Assert.AreEqual(i, intlist[i], "Index getter");
    }
    int k = 0;
    foreach(int j in intlist) {
      Assert.AreEqual(k,j,"Foreach test");
      k++;
    }
    Assert.AreEqual(k, MAX_VAL + 1, "foreach complete enumeration");
  }
  [Test]
  public void InRemTest() {
    int MAX = 100;
    List<int> good = new List<int>();
    var test = ImmutableArray<int>.Empty;
    var r = new System.Random();
    for(int i = 0; i < MAX; i++) {
      int v = r.Next();
      int idx = r.Next(good.Count + 1);
      good.Insert(idx, v);
      test = test.InsertIntoNew(idx, v);
    }
    //Now test:
    ListEqual(good, test); 
    for(int i = 0; i < MAX; i++) {
      int idx = r.Next(good.Count);
      Assert.AreEqual(good[idx], test[idx], "index equality");
    }
    //RemoveAt:
    var rgood = new List<int>(good);
    var save_test = test;
    for(int i = 0; i < good.Count; i++) {
      int idx = r.Next(rgood.Count);
      rgood.RemoveAt(idx);
      test = test.RemoveAtFromNew(idx);
      ListEqual(rgood, test);
    }
    test = save_test;
    //Remove:
    rgood = new List<int>(good);
    save_test = test;
    for(int i = 0; i < good.Count; i++) {
      int idx = r.Next(rgood.Count);
      var item = rgood[idx];
      rgood.Remove(item);
      test = test.RemoveFromNew(item);
      ListEqual(rgood, test);
    }
    test = save_test;
    //Check equality:
    var test2 = ImmutableArray<int>.Copy(test);
    //test2 and test should be equal.
    Assert.AreEqual(test, test2, "Equality all distinct");
    //Test constructor:
    var inited = new ImmutableArray<int>(1,2,3,4);
    Assert.AreEqual(inited[0], 1, "Testing params constructor");
    Assert.AreEqual(inited[1], 2, "Testing params constructor");
    Assert.AreEqual(inited[2], 3, "Testing params constructor");
    Assert.AreEqual(inited[3], 4, "Testing params constructor");
    Assert.AreEqual(inited.Count, 4, "Testing params constructor"); 
  }

}

#endif

}
