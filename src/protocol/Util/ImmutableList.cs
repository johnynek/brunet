using System.Threading;
using System.Collections;
using System.Collections.Generic;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Util {

/** Simple immutable singly linked list class
 */
public class ImmutableList<T> : IEnumerable<T> {

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
      Assert.AreEqual(i, revil[MAX_VAL - i], "Reversed worked");
    }
  }

}

#endif

}
