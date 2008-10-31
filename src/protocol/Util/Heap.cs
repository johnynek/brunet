/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2008 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

using System.Collections;
using System.Collections.Generic;

namespace Brunet.Util {

/**
 * Implementation of a (binary) min-heap datastructure
 */
public class Heap<T> : IEnumerable<T> {

  protected class HeapNode<R> {
    public HeapNode<R> Parent;
    public HeapNode<R> Child0;
    public HeapNode<R> Child1;
    public int Children;

    public R Value;
  }

  protected HeapNode<T> _root;
  readonly protected IComparer<T> _comp;

  public Heap(IComparer<T> comp) {
    _comp = comp;
  }

  public Heap() : this(Comparer<T>.Default) { }

  /** the number of values in the heap */
  public int Count {
    get {
      if( _root == null ) {
        return 0;
      }
      else {
        return _root.Children + 1;
      }
    }
  }

  /** Insert a new value into the min heap
   */
  public void Add(T newval) {
    HeapNode<T> newnode = new HeapNode<T>();
    newnode.Value = newval;
    if( _root == null ) {
      _root = newnode;
    }
    else {
      InsertAt(_root, newnode);
    }
  }

  /** Remove all items from the heap
   */
  public void Clear() {
    _root = null;  
  }

  IEnumerator IEnumerable.GetEnumerator() {
    return GetEnumerator();
  }
  /** Implement IEnumerable<T>
   * Returns all the values in no particular order.
   */
  public IEnumerator<T> GetEnumerator() {
    /*
     * We do a depth first search
     */
    HeapNode<T> current = _root;
    HeapNode<T> prev = _root.Parent;
    while( current != null ) {
      if( prev == current.Parent ) {
        //We are going down:
        if( current.Child0 != null ) {
          //Go to the child0:
          prev = current;
          current = current.Child0;
        }
        else if( current.Child1 != null ) {
          prev = current;
          current = current.Child1;
        }
        else {
          //This is dead-end:
          yield return current.Value;
          prev = current;
          current = current.Parent;
        }
      }
      else {
        //We are coming up from a child:
        if( prev == current.Child0 ) {
          if( current.Child1 != null ) {
            prev = current;
            current = current.Child1; 
          }
          else {
            //No child1, so go back up:
            yield return current.Value;
            prev = current;
            current = current.Parent;
          }
        }
        else {
          //we are coming up from Child1:
          yield return current.Value;
          prev = current;
          current = current.Parent;
        }
      }
    }
  }

  /** get the minimum value without changing the heap
   * @throws System.InvalidOperationException if the Heap is empty
   */
  public T Peek() {
    if( _root == null ) {
      throw new System.InvalidOperationException("Heap is empty");
    }
    else {
      return _root.Value;
    }
  }

  /** remove the minimum value from the heap
   * @throws System.InvalidOperationException if the Heap is empty
   */
  public T Pop() {
    if( _root == null ) {
      throw new System.InvalidOperationException("Heap is empty");
    }
    else {
      T val = _root.Value;
      _root = ReplaceValue(_root);
      return val;
    }
  }

  // Non-Public methods:

  protected void InsertAt(HeapNode<T> parent, HeapNode<T> new_node) {
    if( parent.Child0 == null ) {
      new_node.Parent = parent;
      parent.Child0 = new_node;
      FixChildrenCount(parent);
      FixHeapProperty(new_node);
    }
    else if( parent.Child1 == null ) {
      new_node.Parent = parent;
      parent.Child1 = new_node;
      FixChildrenCount(parent);
      FixHeapProperty(new_node);
    }
    else {
      //Both children exist, go to the one with the minimum children:
      if( parent.Child0.Children < parent.Child1.Children) {
        InsertAt(parent.Child0, new_node);
      }
      else {
        InsertAt(parent.Child1, new_node);
      }
    }
  }

  protected void FixChildrenCount(HeapNode<T> node) {
    if( node == null ) { return; }
    int c0 = node.Child0 != null ? node.Child0.Children + 1 : 0;
    int c1 = node.Child1 != null ? node.Child1.Children + 1 : 0;
    node.Children = c0 + c1;
    FixChildrenCount(node.Parent);
  }

  protected void FixHeapProperty(HeapNode<T> child) {
    HeapNode<T> parent = child.Parent;
    if( parent == null ) { return; }
    if( _comp.Compare(child.Value, parent.Value) < 0 ) {
      /*
       * The Parent should be (and was) smaller than all other children, but this
       * child is even smaller, so just swap it, and check above
       */
      T val = parent.Value;
      parent.Value = child.Value;
      child.Value = val;
      //Now fix the parent:
      FixHeapProperty(parent);
    }
  }

  /** replace the value with the smaller of the two childen
   * @return the node which is the "new parent" 
   */
  protected HeapNode<T> ReplaceValue(HeapNode<T> parent) {
    bool c0 = parent.Child0 != null;
    bool c1 = parent.Child1 != null;
    if( c0 && c1 ) {
      //Both not null, take the min:
      HeapNode<T> minchild = null;
      if( _comp.Compare(parent.Child0.Value, parent.Child1.Value) < 0 ) {
        minchild = parent.Child0;
      }
      else {
        minchild = parent.Child1;
      }
      parent.Value = minchild.Value;
      /*
       * Note, the heap property is still true because we promoted
       * a value that was not greater than the other.
       *
       * Also, we did not explicity change the children count.
       */
      ReplaceValue(minchild);
      return parent;
    }
    else {
      //At least one child is null, we can delete parent now
      HeapNode<T> gp = parent.Parent;
      HeapNode<T> new_parent = null;
      bool gp_c0 = gp != null ? gp.Child0 == parent : false;
      bool gp_c1 = gp != null ? gp.Child1 == parent : false;
      if( c0 ) {
        //c0 not null, but c1 is:
        parent.Child0.Parent = gp;
        new_parent = parent.Child0;
      }
      else if( c1 ) {
        //c1 not null, but c0 is:
        parent.Child1.Parent = gp;
        new_parent = parent.Child1;
      }
      if( gp_c0 ) {
        gp.Child0 = new_parent;
      }
      if( gp_c1 ) {
        gp.Child1 = new_parent;
      }
      //gp just lost a child, so we need to fix the child count above.
      HeapNode<T> current_p = gp;
      while( current_p != null ) {
        current_p.Children -= 1;
        current_p = current_p.Parent;
      }
      /*
       * Note, the heap property is still valid, we just removed
       * a child
       */
      return new_parent;
    }
  }
}

#if BRUNET_NUNIT
[TestFixture]
public class HeapTester {
  [Test]
  public void BasicTest() {
    Heap<int> int_heap = new Heap<int>();
    List<int> sort_list = new List<int>();
    System.Random r = new System.Random();
    int test_count = 1000;
    for(int i = 0; i < test_count; i++ ) {
      int rval = r.Next();
      int_heap.Add(rval);
      sort_list.Add(rval);
      sort_list.Sort();
      Assert.AreEqual(sort_list[0], int_heap.Peek(), "Min value");
      Assert.AreEqual(sort_list.Count, int_heap.Count, "Count value");
    }
    //now, sort_list should still be sorted:
    for(int i = 0; i < test_count; i++) {
      Assert.AreEqual(sort_list[i], int_heap.Pop(), "min-pop");
      Assert.AreEqual(999 - i, int_heap.Count, "Count after pop");
    }
    int_heap.Clear();
    Assert.AreEqual(0, int_heap.Count, "Heap Clearing");
    //Make sure enumeration works:
    for(int i = 0; i < test_count; i++) {
      int rval = r.Next();
      int_heap.Add(rval);
    }
    Heap<int> heap_2 = new Heap<int>();
    foreach(int hval in int_heap) {
      heap_2.Add(hval);
    }
    Assert.AreEqual(int_heap.Count, heap_2.Count, "Second heap same size");
    for(int i = 0; i < test_count; i++) {
      Assert.AreEqual(int_heap.Pop(), heap_2.Pop(), "Pop equivalence");
    }
  }
}
#endif

 
}
