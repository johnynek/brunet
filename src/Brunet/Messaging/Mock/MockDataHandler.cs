/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2008 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using System;
using System.Collections;

#if BRUNET_NUNIT
using NUnit.Framework;
using System.Security.Cryptography;
#endif

namespace Brunet.Messaging.Mock {
  /// <summary>This class provides a MockDataHandler object that keeps a hash
  /// table containing all the data received and an event to notify when an
  /// HandleData has been called.</summary>
  public class MockDataHandler: IDataHandler {
    Hashtable _ht;
    ArrayList _order;
    MemBlock _last_received;
    public MemBlock LastReceived { get { return _last_received; } }
    public event EventHandler HandleDataCallback;

    public MockDataHandler() {
      _ht = new Hashtable();
      _order = new ArrayList();
    }

    public void HandleData(MemBlock payload, ISender return_path, object state) {
      _last_received = payload;
      _ht[payload] = return_path;
      _order.Add(payload);
      if(HandleDataCallback != null) {
        HandleDataCallback(payload, null);
      }
    }

    public bool Contains(object o) {
      return _ht.Contains(o);
    }

    public ISender Value(object o) {
      return _ht[o] as ISender;
    }

    public int Position(object o) {
      return _order.IndexOf(o);
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class MDHTest {
    int _count = 0;
    protected void Callback(object o, EventArgs ea) {
      _count++;
    }

    [Test]
    public void test() {
      MockDataHandler mdh = new MockDataHandler();
      mdh.HandleDataCallback += Callback;
      ISender sender = new MockSender(null, null, mdh, 0);
      byte[][] b = new byte[10][];
      MemBlock[] mb = new MemBlock[10];
      Random rand = new Random();
      for(int i = 0; i < 10; i++) {
        b[i] = new byte[128];
        rand.NextBytes(b[i]);
        mb[i] = MemBlock.Reference(b[i]);
        sender.Send(mb[i]);
      }

      for(int i = 0; i < 10; i++) {
        Assert.AreEqual(i, mdh.Position(mb[i]), "Position " + i);
        Assert.IsTrue(mdh.Contains(mb[i]), "Contains " + i);
      }

      Assert.AreEqual(_count, 10, "Count");
    }
  }
#endif
}
