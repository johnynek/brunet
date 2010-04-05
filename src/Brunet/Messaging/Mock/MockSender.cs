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
using System;

#if BRUNET_NUNIT
using NUnit.Framework;
using System.Security.Cryptography;
#endif

namespace Brunet.Messaging.Mock {
  /// <summary>This class provides a MockSender object for a multiple clients
  /// and a single end point, which is the Receiver provided in the
  /// constructor.</summary>
  /// <remarks>A user must supply the return path and state to use.</remarks>
  public class MockSender: ISender {
    public IDataHandler Receiver;
    public ISender ReturnPath;
    public object State;
    int _remove_n_ptypes;

    public MockSender(ISender ReturnPath, object State, IDataHandler Receiver,
        int RemoveNPTypes)
    {
      this.ReturnPath = ReturnPath;
      this.State = State;
      this.Receiver = Receiver;
      _remove_n_ptypes = RemoveNPTypes;
    }

    public void Send(ICopyable Data) {
      byte[] data = new byte[Data.Length];
      Data.CopyTo(data, 0);
      MemBlock mdata = MemBlock.Reference(data);
      for(int i = 0; i < _remove_n_ptypes; i++) {
        MemBlock payload = mdata;
        PType.Parse(mdata, out payload);
        mdata = payload;
      }
      Receiver.HandleData(mdata, ReturnPath, State);
    }

    public String ToUri() {
      throw new NotImplementedException();
    }
    
    public override string ToString() {
      return ("MockSender: " + this.GetHashCode());
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class MSTest {
    [Test]
    public void NoPType() {
      MockDataHandler idh = new MockDataHandler();
      MockSender mspr = new MockSender(null, null, idh, 0);
      byte[] data = new byte[1024];
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      rng.GetBytes(data);
      MemBlock mdata = MemBlock.Reference(data);
      mspr.Send(mdata);
      Assert.AreEqual(mdata, idh.LastReceived, "No PType MockSender");
    }

    [Test]
    public void PType() {
      MockDataHandler idh = new MockDataHandler();
      MockSender mspr = new MockSender(null, null, idh, 1);
      byte[] data = new byte[1024];
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      rng.GetBytes(data);
      MemBlock mdata = MemBlock.Reference(data);
      mspr.Send(new CopyList(new PType("LONG"), mdata));
      Assert.AreEqual(mdata, idh.LastReceived, "PType MockSender");
    }
  }
#endif
}
