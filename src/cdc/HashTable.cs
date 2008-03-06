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
using System;


/** A namespace for Concurrent Distributed Collection classes
 * This is a namespace that holds distributed collections such as Distributed
 * HashTable, Queue, List, Set, etc...
 */
namespace Brunet.Cdc
{

  /** A Concurrent Hashtable base class
   * Defines the basic interface for a Concurrent HashTable, on which
   * most of our other primitives are built.
   */
  abstract public class HashTable {
    /** Begin a Compare-and-Swap operation
     * @param key the key to change
     * @param new_value the value to replace if the old value is given
     * @param old_value the value we are checking for (may be null)
     * @param cb the delegate to call on completion
     * Note the swap succeeds if and only if the return value
     * of EndCompareSwap == old_value
     */
    abstract public IAsyncResult BeginCompareSwap(MemBlock key, MemBlock new_value,
                                                 MemBlock old_value, AsyncCallback cb,
                                                 object state);

    /** Begin a Read of a Key
     * @param key the key to read
     * @param result_cb the delegate to call when the read completes
     */
    abstract public IAsyncResult BeginRead(MemBlock key, AsyncCallback result_cb,
                                           object state);
    /** Swap the value held at key, and return the old value
     * @param key the key to change the value of
     * @param new_value the new value to set to
     * @param cb the delegate to call at completion
     */
    abstract public IAsyncResult BeginSwap(MemBlock key, MemBlock new_value, AsyncCallback cb,
                                           object state);
    /** create a new random key
     * Some protocols need to be able to create random keys.  These should
     * be very (VERY) unlikely to collide globally.
     */
    abstract public MemBlock CreateRandomKey();
    /** End of a Compare-and-Swap
     * @return the value before the the BeginCompareSwap was called.
     * Note the swap succeeds if and only if the return value == old_value
     */
    abstract public MemBlock EndCompareSwap(IAsyncResult r);
    /** End a Read of a key
     * @returns null if there is no such key in the HashTable
     * @throw Exception if there is a timeout.
     */
    abstract public MemBlock EndRead(IAsyncResult r);
    /** End a Swap
     * @return the old value held by the key
     */
    abstract public MemBlock EndSwap(IAsyncResult r);

    /** Synchronous interface
     * the default implementation uses the asynchronous implementation
     */
    virtual public MemBlock CompareSwap(MemBlock key, MemBlock new_value, MemBlock old_value) {
      IAsyncResult r = BeginCompareSwap(key, new_value, old_value, this.DoNothing, null);
      r.AsyncWaitHandle.WaitOne();
      return EndCompareSwap(r);
    }

    /** Synchronous interface
     * the default implementation uses the asynchronous implementation
     */
    virtual public MemBlock Read(MemBlock key) {
      IAsyncResult r = BeginRead(key, this.DoNothing, null);
      r.AsyncWaitHandle.WaitOne();
      return EndRead(r);
    }

    /** Synchronous interface
     * the default implementation uses the asynchronous implementation
     */
    virtual public MemBlock Swap(MemBlock key, MemBlock new_value) {
      IAsyncResult r = BeginSwap(key, new_value, this.DoNothing, null);
      r.AsyncWaitHandle.WaitOne();
      return EndSwap(r);
    }
    private void DoNothing(IAsyncResult r) { }

  }

  /** A simple local implementation of HashTable.
   * Implements the Cdc.HashTable interface
   */
#if BRUNET_NUNIT
  [TestFixture]
#endif
  public class LocalHashTable : HashTable {
    
    /** Objects returned by the asynchronous operations
     */
    protected class LhtAsResult : IAsyncResult {
      protected readonly object _state;
      public object AsyncState { get { return _state; } }
      
      protected readonly System.Threading.WaitHandle _wh;
      public System.Threading.WaitHandle AsyncWaitHandle { get { return _wh; } }

      //Since this is local, all these complete synchronously
      public bool CompletedSynchronously { get { return true; } }
      //By the time we return this object, it is completed.
      public bool IsCompleted { get { return true; } }

      public readonly MemBlock Result;

      public LhtAsResult(object state, MemBlock res) {
        _state = state; 
        //We start off signalled, and stay there.
        _wh = new System.Threading.ManualResetEvent(true);
        Result = res;
      }
    }
    protected readonly System.Collections.Hashtable _ht;
    protected readonly object _sync;
    protected readonly Random _rand;

    /** Creates a Concurrent Local HashTable
     */
    public LocalHashTable() {
      _ht = new System.Collections.Hashtable();
      _sync = new object();
      _rand = new Random();
    }
    
    /** Begin a Compare-and-Swap operation
     * @param key the key to change
     * @param new_value the value to replace if the old value is given
     * @param old_value the value we are checking for (may be null)
     * @param cb the delegate to call on completion
     * Note the swap succeeds if and only if the return value
     * of EndCompareSwap == old_value
     */
    override public IAsyncResult BeginCompareSwap(MemBlock key, MemBlock new_value,
                                                 MemBlock old_value, AsyncCallback cb,
                                                 object state) {
      MemBlock old_v = null;
      lock( _sync ) {
        old_v = _ht[key] as MemBlock;
        if ( (old_v == null) && (old_value == null)) {
          //Looks good:
          _ht[key] = new_value; 
        }
        else if( old_v.Equals(old_value) ) {
          //Use Equals method to check for equality
          _ht[key] = new_value;
        }
      }
      IAsyncResult r = new LhtAsResult(state, old_v);
      cb(r);
      return r;
    }

    /** Begin a Read of a Key
     * @param key the key to read
     * @param result_cb the delegate to call when the read completes
     */
    override public IAsyncResult BeginRead(MemBlock key, AsyncCallback cb,
                                           object state) {
      MemBlock old_v = null;
      lock( _sync ) {
        //We have to hold the lock to be consistent with CompareSwap and Swap
        old_v = _ht[key] as MemBlock;
      }
      IAsyncResult r = new LhtAsResult(state, old_v);
      cb(r);
      return r;
    }
    /** Swap the value held at key, and return the old value
     * @param key the key to change the value of
     * @param new_value the new value to set to
     * @param cb the delegate to call at completion
     */
    override public IAsyncResult BeginSwap(MemBlock key, MemBlock new_value, AsyncCallback cb,
                                           object state) {
      
      MemBlock old_v = null;
      lock( _sync ) {
        old_v = _ht[key] as MemBlock;
        _ht[key] = new_value;
      }
      IAsyncResult r = new LhtAsResult(state, old_v);
      cb(r);
      return r;
    }
    /** return a new random key
     * @todo depending on how this class is used, we need to improve the
     * quality of rng.
     */
    override public MemBlock CreateRandomKey() {
      /*
       * As long as we use System.Random, there is
       * no use in putting more than 4 bytes, since
       * there are only 4 bytes of randomness in System.Random,
       * so, given 4 bytes, you can compute all the future
       * values.
       */
      byte[] key_buf = new byte[4];
      _rand.NextBytes(key_buf);
      return MemBlock.Reference(key_buf);
    }
    /** End of a Compare-and-Swap
     * @return the value before the the BeginCompareSwap was called.
     * Note the swap succeeds if and only if the return value == old_value
     */
    override public MemBlock EndCompareSwap(IAsyncResult r) {
      LhtAsResult res = (LhtAsResult)r;
      return res.Result;
    }
    /** End a Read of a key
     * @returns null if there is no such key in the HashTable
     * @throw Exception if there is a timeout.
     */
    override public MemBlock EndRead(IAsyncResult r) {
      LhtAsResult res = (LhtAsResult)r;
      return res.Result;
    }
    /** End a Swap
     * @return the old value held by the key
     */
    override public MemBlock EndSwap(IAsyncResult r) {
      LhtAsResult res = (LhtAsResult)r;
      return res.Result;
    }
#if BRUNET_NUNIT
    /*
     * Here are the NUnit tests
     */
    [Test]
    public void TestRecall() {
      Random r = new Random();
      System.Collections.Hashtable ht = new System.Collections.Hashtable();
      for(int i = 0; i < 128; i++) {
        byte[] key_buf = new byte[ r.Next(1024) ];
        r.NextBytes(key_buf);
        MemBlock key = MemBlock.Reference(key_buf);
        byte[] val_buf = new byte[ r.Next(1024) ];
        r.NextBytes(val_buf);
        MemBlock val = MemBlock.Reference(val_buf);
        
        MemBlock old_v = Swap(key, val);
        ht[key] = val;
        Assert.IsNull(old_v, "old value is null"); 
      }

      foreach(System.Collections.DictionaryEntry de in ht) {
        MemBlock recall_v = Read((MemBlock)de.Key);
        Assert.AreEqual(recall_v, de.Value, "check recall");
      }
    }
    /*
     * Test the CompareSwap feature
     */
    [Test]
    public void TestCAS() {
      Random r = new Random();
      System.Collections.Hashtable ht = new System.Collections.Hashtable();
      for(int i = 0; i < 128; i++) {
        byte[] key_buf = new byte[ r.Next(1024) ];
        r.NextBytes(key_buf);
        MemBlock key = MemBlock.Reference(key_buf);
        byte[] val_buf = new byte[ r.Next(1024) ];
        r.NextBytes(val_buf);
        MemBlock val = MemBlock.Reference(val_buf);
        
        MemBlock old_v = CompareSwap(key, val, null);
        ht[key] = val;
        Assert.IsNull(old_v, "old value is null"); 
        //Try it again, make sure it doesn't work:
        Assert.IsNotNull(CompareSwap(key, val, null), "old value is not null");
        //Try it again with a different value:
        MemBlock other_v = MemBlock.Concat(key, val);
        Assert.AreEqual(val, CompareSwap(key, other_v, other_v), "update failed");
        MemBlock current = Read(key);
        Assert.AreEqual(val, current, "still not updated");
        Assert.AreNotEqual(other_v, current, "make sure update didn't work");
        //Now do a real update:
        Assert.AreEqual(val, CompareSwap(key, other_v, val), "first update");
        Assert.AreEqual(other_v, Read(key), "update worked");
        ht[key] = other_v; 
      }

      foreach(System.Collections.DictionaryEntry de in ht) {
        MemBlock recall_v = Read((MemBlock)de.Key);
        Assert.AreEqual(recall_v, de.Value, "check recall");
      }
    }
#endif 
  }

}
