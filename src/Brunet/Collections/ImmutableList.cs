using System.Collections;
using System.Collections.Generic;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Collections {

/** Simple immutable singly linked list class
 * Pushing (Inserting at location 0) and getting from index 0 (Head)
 * is O(1) for this class.  All other operations are O(N).
 * The point of this class is to have an immutable List which
 * can share significant memory with other instances.  If you
 * Add to the head (PushIntoNew) the entire Tail of the new
 * list is reused.
 * 
 * Using this class as a stack is another efficient use of this
 * class.
 */
public class ImmutableList<T> : IList<T> {

  ///O(N) operation, avoid if you can. Prefer to enumerate with foreach
  public int Count {
    get {
      var list = this;
      var tail = list.Tail;
      int count = 0;
      while(tail != null) {
        count++;
        list = tail;
        tail = list.Tail;
      }
      return count;
    }
  }
  ///@return this == Empty
  public bool IsEmpty {
    get {
      return this == Empty;
    }
  }
  /** For IList<T> always true */
  public bool IsReadOnly { get { return true; } }

  public readonly T Head;
  public readonly ImmutableList<T> Tail;
  //This represents the empty list:
  public static readonly ImmutableList<T> Empty = new ImmutableList<T>();

  /** return items from this list in the same order as GetEnumerator
   * if idx == 0, return Head, else Tail[idx - 1]
   * this is an O(Count) time operation.
   */
  public T this[int idx] {
    get {
      if (idx < 0) {
        throw new System.ArgumentOutOfRangeException("index", idx, "cannot be negative");
      }
      var l = this;
      while(true) {
        if( l == Empty ) {
          throw new System.ArgumentOutOfRangeException("index", idx, "index >= Count");
        }
        if( idx == 0 ) {
          return l.Head; 
        }
        else {
          --idx;
          l = l.Tail;
        }
      }
    }
    set {
      throw new System.NotSupportedException();
    }
  }

  protected ImmutableList() {
    Head = default(T);
    Tail = null;
  }

  /** create a list with one item
   */
  public ImmutableList(T val) : this(val, Empty) {

  }

  /** push a value onto an existing list at pos == 0
   * @param val the new value
   * @param list the existing list (won't change in size)
   */
  public ImmutableList(T val, ImmutableList<T> list) {
    Head = val;
    Tail = list;
  }

 // //////////
 // Methods
 // //////////

  public void Add(T item) {
    throw new System.NotSupportedException();
  }

  public void Clear() {
    throw new System.NotSupportedException();
  }

  public bool Contains(T item) {
    return IndexOf(item) != -1;
  }

  public void CopyTo(T[] dest, int idx) {
    foreach(T item in this) {
      dest[idx] = item;
      idx += 1;
    }
  }

  public override bool Equals(object o) {
    ImmutableList<T> other = o as ImmutableList<T>;
    var list = this;
    while( other != null && list != null ) { 
      if( object.ReferenceEquals(other, list) ) {
        return true;
      }
      //check the head:
      bool head_eq = list.Head != null ? 
                       list.Head.Equals(other.Head)
                       : other.Head == null;
      if( !head_eq ) {
        return false;
      }
      //Check the rest of the list:
      other = other.Tail;
      list = list.Tail;
    }
    return false;
  }

  public IEnumerator<T> GetEnumerator() {
    var list = this;
    var tail = list.Tail;
    while(tail != null) {
      yield return list.Head;
      list = tail;
      tail = list.Tail;
    }
  }

  IEnumerator IEnumerable.GetEnumerator() {
    return this.GetEnumerator();
  }

  /// if Head is not null, Head is hashcode, else Tail if not null, else 0
  public override int GetHashCode() {
    var list = this;
    while(list.Tail != null) {
      var h = list.Head;
      if (h != null) {
        return h.GetHashCode();
      }
      list = list.Tail;
    }
    return 0;
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

  
  public ImmutableList<T> InsertIntoNew(int idx, T val) {
    var headlist = Empty;
    var taillist = this;
    while(idx > 0) {
      if( taillist == Empty ) {
        throw new System.ArgumentOutOfRangeException("index to large");
      }
      headlist = headlist.PushIntoNew(taillist.Head);
      taillist = taillist.Tail;
      idx -= 1;
    }
    taillist = taillist.PushIntoNew(val);
    //Now put all the head elements back:
    while(headlist != Empty) {
      taillist = taillist.PushIntoNew(headlist.Head);
      headlist = headlist.Tail;
    }
    return taillist;
  }

  /** Treat the list like a stack and push a new value to index==0
   * same as new ImmutableList<T>(val, this)
   */
  public ImmutableList<T> PushIntoNew(T val) {
    return new ImmutableList<T>(val, this);
  }

  public bool Remove(T val) {
    throw new System.NotSupportedException();
  }
  /** if item not in list, return this, else remove first occurence
   */
  public ImmutableList<T> RemoveFromNew(T val) {
    var headlist = Empty;
    var taillist = this;
    bool found = false;
    bool notnull = val != null;
    while(taillist != Empty) {
      var item = taillist.Head;
      taillist = taillist.Tail;
      bool eq = notnull ? item.Equals(val) : val == null;
      if( !eq ) {
        headlist = headlist.PushIntoNew(item);
      }
      else {
        //Skip this guy, and break out.
        found = true;
        break;
      }
    }
    if(found) {
      //Now put all the head elements back:
      while(headlist != Empty) {
        taillist = taillist.PushIntoNew(headlist.Head);
        headlist = headlist.Tail;
      }
      return taillist;
    }
    else {
      //The val was not in the list
      return this;
    }
  }

  public void RemoveAt(int idx) {
    throw new System.NotSupportedException();
  }

  public ImmutableList<T> RemoveAtFromNew(int idx) {
    var headlist = Empty;
    var taillist = this;
    while(taillist != Empty) {
      var val = taillist.Head;
      taillist = taillist.Tail;
      if(idx == 0) {
        //this one doesn't get saved, stop here:
        break;
      }
      idx -= 1;
      headlist = headlist.PushIntoNew(val);
    }
    if( idx != 0 ) {
      throw new System.ArgumentOutOfRangeException("index too large too small");
    }
    //Push
    while(headlist != Empty) {
      taillist = taillist.PushIntoNew(headlist.Head);
      headlist = headlist.Tail;
    }
    return taillist;
  }

  public static ImmutableList<T> Reverse(IEnumerable<T> l) {
    var list = Empty;
    foreach(T val in l) {
      list = new ImmutableList<T>(val, list);
    }
    return list;
  }

}

#if BRUNET_NUNIT
[TestFixture]
public class ImmutListTest {
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
    var intlist = ImmutableList<int>.Empty;
    Assert.AreEqual(0, intlist.Count, "Empty count test");
    for(int i = MAX_VAL; i >= 0; i--) {
      intlist = new ImmutableList<int>(i, intlist);
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
    var revil = ImmutableList<int>.Reverse(intlist);
    for(int i = 0; i <= MAX_VAL; i++) {
      var val = revil[MAX_VAL - i];
      Assert.AreEqual(i, val, "Reversed worked");
      Assert.AreEqual(i, intlist.IndexOf(val), "IndexOf works");
      Assert.AreEqual(-1, revil.IndexOf(val + MAX_VAL + 1), "IndexOf -1 on absent");
    }
  }
  [Test]
  public void InRemTest() {
    int MAX = 100;
    List<int> good = new List<int>();
    var test = ImmutableList<int>.Empty;
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
    var test2 = ImmutableList<int>.Reverse(test);
    test2 = ImmutableList<int>.Reverse(test2);
    //test2 and test1 should be equal.
    Assert.AreEqual(test, test2, "Equality all distinct");
    //Make a shared tail:
    Assert.AreEqual(test.PushIntoNew(0), test.PushIntoNew(0), "Equality tail equality");
  }

}

#endif

}
