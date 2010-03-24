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
using System.Threading;

#if BRUNET_NUNIT
using NUnit.Framework;
using System.Security.Cryptography;
#endif

namespace Brunet.Mock {
  /// <summary>This class provides a MockSender object for a multiple clients
  /// and a single end point, which is the Receiver provided in the
  /// constructor.</summary>
  /// <remarks>A user must supply the return path and state to use.</remarks>
  public class ThreadedMockSender: MockSender {
    BlockingQueue _queue;

    public ThreadedMockSender(ISender ReturnPath, object State,
        IDataHandler Receiver, int RemoveNPTypes) :
      this(ReturnPath, State, Receiver, RemoveNPTypes, 0)
    {
    }

    public ThreadedMockSender(ISender ReturnPath, object State,
        IDataHandler Receiver, int RemoveNPTypes, double drop_rate) :
      base(ReturnPath, State, Receiver, RemoveNPTypes, drop_rate)
    {
      _queue = new BlockingQueue();
      Thread runner = new Thread(ThreadRun);
      runner.IsBackground = true;
      runner.Start();
    }

    public override void Send(ICopyable data) {
      _queue.Enqueue(data);
    }

    protected void ThreadRun()
    {
      while(true) {
        ICopyable data = _queue.Dequeue() as ICopyable;
        try {
          base.Send(data);
        } catch(Exception e) {
          Console.WriteLine(e);
        }
      }
    }

    public override string ToString() {
      return ("ThreadedMockSender: " + this.GetHashCode());
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class TMSTest {
    [Test]
    public void NoPType() {
      MockDataHandler idh = new MockDataHandler();
      MockSender mspr = new ThreadedMockSender(null, null, idh, 0);
      byte[] data = new byte[1024];
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      rng.GetBytes(data);
      MemBlock mdata = MemBlock.Reference(data);
      object lr = idh.LastReceived;
      mspr.Send(mdata);
      while(lr == idh.LastReceived) {
        Thread.Sleep(0);
      }
      Assert.AreEqual(mdata, idh.LastReceived, "No PType MockSender");
    }

    [Test]
    public void PType() {
      MockDataHandler idh = new MockDataHandler();
      MockSender mspr = new ThreadedMockSender(null, null, idh, 1);
      byte[] data = new byte[1024];
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      rng.GetBytes(data);
      MemBlock mdata = MemBlock.Reference(data);
      object lr = idh.LastReceived;
      mspr.Send(new CopyList(new PType("LONG"), mdata));
      while(lr == idh.LastReceived) {
        Thread.Sleep(0);
      }

      Assert.AreEqual(mdata, idh.LastReceived, "PType MockSender");
    }
  }
#endif
}
