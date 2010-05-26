/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2008 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using Brunet.Concurrent;
using Brunet.Util;
using System;
using System.Threading;

#if BRUNET_NUNIT
using NUnit.Framework;
using System.Security.Cryptography;
#endif

namespace Brunet.Messaging.Mock {
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
