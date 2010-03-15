/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
