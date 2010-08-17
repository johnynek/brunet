/*
This program is part of BruNet, a library for the creation of efficient overlay networks.
Copyright (C) 2010 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System;
using System.Collections;

using Brunet.Util;

namespace Brunet.Connections {
  public sealed class ConnectionLockManager {
// ////////////
// Member variables
// ////////////
    private readonly object _sync;
    private ConnectionTableState _cts;
    private readonly Hashtable _address_locks;
// ////////////
// Properties 
// ////////////

// ////////////
// Constructors
// ////////////
    public ConnectionLockManager(ConnectionTable tab) {
      _sync = new object();
      _address_locks = new Hashtable();
      _cts = tab.State;
      //Watch for new Connections or lost connections
      tab.ConnectionEvent += UpdateState;
      tab.DisconnectionEvent += UpdateState;
    }
// ////////////
// Methods 
// ////////////

    /**
     * @param a the Address to lock
     * @param t the type of connection
     * @param locker the object wishing to hold the lock
     *
     * We use this to make sure that two linkers are not
     * working on the same address for the same connection type
     *
     * @throws ConnectionExistsException if there is already a connection to this address
     * @throws CTLockException if we cannot get the lock
     * @throws CTLockException if lockedvar is not null or a, when called. 
     */
    public void Lock(Address a, string t, ILinkLocker locker)
    {
 #if false
      if( null == a ) { return; }
      ConnectionType mt = Connection.StringToMainType(t);
      lock( _sync ) {
        if( locker.TargetLock != null && (false == a.Equals(locker.TargetLock)) ) {
          //We only overwrite the locker.TargetLock() if it is null:
          throw new CTLockException(
                  String.Format("locker.TargetLock() not null, set to: {0}", locker.TargetLock));
        }
        var list = _cts.GetConnections(mt);
        if( list.Contains(a) ) {
          /**
           * We already have a connection of this type to this node.
           */
          throw new ConnectionExistsException(list[a]);
        }
        Hashtable locks = (Hashtable)_address_locks[mt];
        if( locks == null ) {
          locks = new Hashtable();
          _address_locks[mt] = locks;
        }
        ILinkLocker old_locker = (ILinkLocker)locks[a];
        if( null == old_locker ) {
          locks[a] = locker;
          locker.TargetLock = a;
          if(ProtocolLog.ConnectionTableLocks.Enabled) {
            ProtocolLog.Write(ProtocolLog.ConnectionTableLocks,
              String.Format("locker: {0} Unlocking: {1}", locker, a));
          }
        }
        else if (old_locker == locker) {
          //This guy already holds the lock
          locker.TargetLock = a;
        }
        else if ( old_locker.AllowLockTransfer(a,t,locker) ) {
        //See if we can transfer the lock:
          locks[a] = locker;
          locker.TargetLock = a;
          //Make sure the lock is null
          old_locker.TargetLock = null;
        }
        else {
          if(ProtocolLog.ConnectionTableLocks.Enabled) {
            ProtocolLog.Write(ProtocolLog.ConnectionTableLocks,
              String.Format("{0} tried to lock {1}, but {2} holds the lock",
                locker, a, locks[a]));
          }
          throw new CTLockException(
                      String.Format(
                        "Lock on {0} cannot be transferred from {1} to {2}",
                        a, old_locker, locker));
        }
      }
 #endif
    }

    /**
     * We use this to make sure that two linkers are not
     * working on the same address
     * @param t the type of connection.
     * @param locker the object which holds the lock.
     * @throw Exception if the lock is not held by locker
     */
    public void Unlock(string t, ILinkLocker locker) {
 #if false
      ConnectionType mt = Connection.StringToMainType(t);
      lock( _sync ) {
        if( locker.TargetLock != null ) {
          Hashtable locks = (Hashtable)_address_locks[mt];
          if(ProtocolLog.ConnectionTableLocks.Enabled) {
            ProtocolLog.Write(ProtocolLog.ConnectionTableLocks,
              String.Format("Unlocking {0}", locker.TargetLock));
          }

          object real_locker = locks[locker.TargetLock];
          if(null == real_locker) {
            string err = String.Format(locker + " tried to unlock "
                         + locker.TargetLock + " but no such lock" );
            if(ProtocolLog.ConnectionTableLocks.Enabled) {
              ProtocolLog.Write(ProtocolLog.ConnectionTableLocks, err);
            }
            throw new Exception(err);
          }
          else if(real_locker != locker) {
            string err = String.Format(locker +
                " tried to unlock " + locker.TargetLock + " but not the owner");
            if(ProtocolLog.ConnectionTableLocks.Enabled) {
              ProtocolLog.Write(ProtocolLog.ConnectionTableLocks, err);
            }
            throw new Exception(err);
          }

          locks.Remove(locker.TargetLock);
          locker.TargetLock = null;
        }
      }
#endif
    }

    private void UpdateState(object tab, EventArgs args) {
      var cea = (ConnectionEventArgs)args;
      lock( _sync ) { _cts = cea.NewState; }
    }
  }

#if false
  public void LockTest() {
      byte[]  abuf = new byte[20];
      Address a = new AHAddress(abuf);

      ConnectionTable tab = new ConnectionTable();
      TestLinkLocker lt = new TestLinkLocker(true);
      TestLinkLocker lf = new TestLinkLocker(false);

      //Get a lock on a.
      tab.Lock(a, "structured.near", lt);
      Assert.AreEqual(a, lt.TargetLock, "lt has lock");
      tab.Unlock("structured.near", lt);
      Assert.IsNull(lt.TargetLock, "Unlock nulling test");
      //Unlock null should be fine:
      tab.Unlock("structured.near", lt);
      Assert.IsNull(lt.TargetLock, "Unlock nulling test");
      //We can't unlock if we don't have the lock:
      lt.TargetLock = a;

      try {
        tab.Unlock("structured.near", lt);
        Assert.IsFalse(true, "We were able to unlock an address incorrectly");
      } catch { }
      //Get a lock and transfer:
      tab.Lock(a, "structured.near", lt);
      Assert.AreEqual(a, lt.TargetLock, "lt has lock");
      tab.Lock(a, "structured.near", lf);
      Assert.IsTrue(lf.TargetLock == a, "Lock was transferred to lf");
      //lt.TargetLock should be null;
      Assert.IsNull(lt.TargetLock, "lock was transfered and we are null");

      //Now, lt should not be able to get the lock:
      try {
        tab.Lock(a, "structured.near", lt);
        Assert.IsFalse(true, "We somehow got the lock");
      }
      catch { }
      Assert.IsNull(lt.TargetLock, "lt shouldn't hold the lock");
      Assert.AreEqual(lf.TargetLock, a, "lf still holds the lock");
      //Now let's unlock:
      tab.Unlock("structured.near", lf);
      //lf.TargetLock should be null;
      Assert.IsNull(lf.TargetLock, "lock was transfered and we are null");
    }
#endif

}
