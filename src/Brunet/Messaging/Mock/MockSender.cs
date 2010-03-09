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
