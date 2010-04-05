/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System;

namespace Brunet.Connections {

/**
 * In the linking process between two nodes, classes which implement
 * this interface can hold a lock in the ConnectionTable.
 * @see ConnectionTable
 * @see ConnectionTable.Lock
 */
public interface ILinkLocker {

  /**
   * In the link process, sometimes locks may be transfered from
   * one object holding the lock to another.  This is the code
   * called by ConnectionTable when someone tries to lock an already
   * locked address.  If this returns true, the previous holder must
   * account for the fact that it no longer holds the lock.
   */
  bool AllowLockTransfer(Address a, string contype, ILinkLocker new_locker);
  Object TargetLock {
    get;
    set;
  }
}

}
