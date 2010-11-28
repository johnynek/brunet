/*
Copyright (C) 2010  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet.Collections;
using Brunet.Util;
using System;
using System.Collections;
using System.Collections.Generic;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Messaging {
  /// <summary>Many mediums do not provide means for peers to establish an
  /// identifier pair.  This class adds a source/remote port abstraction to a
  /// data channel.  This class is thread-safe.</summary>
  public class IdentifierTable : IEnumerable<IIdentifierPair> {
    protected UidGenerator<IIdentifierPair> _local_to_idpair;
    public int Count { get { return _local_to_idpair.Count; } }

    public IdentifierTable() : this(true)
    {
    }

    public IdentifierTable(bool non_neg)
    {
#if BRUNET_SIMULATOR
      Random rand = Node.SimulatorRandom;
#else
      Random rand = new Random();
#endif
      _local_to_idpair = new UidGenerator<IIdentifierPair>(rand, non_neg);
    }

    /// <summary>A new identifier pair, may be incoming or outgoing.</summary>
    public int Add(IIdentifierPair idpair)
    {
      int local = 0;
      lock(_local_to_idpair) {
        local = _local_to_idpair.GenerateID(idpair);
      }

      try {
        idpair.LocalID = local;
      } catch {
        // Another idpairtext added the local id first...
        lock(_local_to_idpair) {
          _local_to_idpair.TryTake(local, out idpair);
        }
      }

      return idpair.LocalID;
    }

    /// <summary>Remove the identifier pair specified by the local id.</summary>
    public bool Remove(int local_id)
    {
      lock(_local_to_idpair) {
        IIdentifierPair nil;
        return _local_to_idpair.TryTake(local_id, out nil);
      }
    }

    /// <summary>Return the IDs and the payload given a packet.</summary>
    public bool Parse(MemBlock packet, out MemBlock payload, out int local_id,
        out int remote_id)
    {
      if(packet.Length < 8) {
        throw new Exception("Data is not a complete datagram, less than 8 bytes: " + packet.Length);
      }

      remote_id = NumberSerializer.ReadInt(packet, IdentifierPair.SOURCE_OFFSET);
      local_id = NumberSerializer.ReadInt(packet, IdentifierPair.DESTINATION_OFFSET);
      payload = packet.Slice(8);
      return true;
    }

    /// <summary>Return the identifier pair given a local and remote id.</summary>
    public bool TryGet(int local_id, int remote_id,
        out IIdentifierPair idpair)
    {
      lock(_local_to_idpair) {
        if(!_local_to_idpair.TryGet(local_id, out idpair)) {
          return false;
        }
      }

      int c_rem_id = idpair.RemoteID;

      if(local_id != idpair.LocalID) {
        throw new Exception("Invalid local id");
      } else if(remote_id != c_rem_id) {
        if(c_rem_id != IdentifierPair.DEFAULT_ID) {
          throw new Exception("Invalid remote id (expected, got): " +
              c_rem_id + ", " + remote_id);
        }
        idpair.RemoteID = remote_id;
      }
      return true;
    }

    /// <summary>Assuing the identifier pair is already known, this idpairnects
    /// the Parse and TryGet methods.</summary>
    public bool Verify(IIdentifierPair idpair, MemBlock packet, out MemBlock payload)
    {
      int local_id, remote_id;
      Parse(packet, out payload, out local_id, out remote_id);
      IIdentifierPair real_idpair;
      TryGet(local_id, remote_id, out real_idpair);
      if(idpair != real_idpair) {
        throw new Exception("IdentifierPair mismatch");
      }
      return true;
    }

    /// <summary>Returns a thread-safe enumerator.</summary>
    IEnumerator IEnumerable.GetEnumerator(){
      return GetEnumerator();
    }

    /// <summary>Returns a thread-safe enumerator.</summary>
    public IEnumerator<IIdentifierPair> GetEnumerator()
    {
      List<IIdentifierPair> cps = null;
      lock(_local_to_idpair) {
        cps = new List<IIdentifierPair>(_local_to_idpair);
      }

      foreach(IIdentifierPair cp in cps) {
        yield return cp;
      }
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class IdentifierTableTest {
    [Test]
    public void Test()
    {
      MemBlock payload;
      int local_id, remote_id;
      IdentifierTable it_0 = new IdentifierTable();
      IIdentifierPair idpair_0 = new IdentifierPair();
      IdentifierTable it_1 = new IdentifierTable();
      IIdentifierPair idpair_1 = new IdentifierPair();

      it_0.Add(idpair_0);
      it_1.Parse(idpair_0.Header, out payload, out local_id, out remote_id);
      Assert.AreEqual(local_id, 0, "Inbound local_id");
      Assert.AreNotEqual(remote_id, 0, "Inbound remote_id");
      idpair_1.RemoteID = remote_id;
      it_1.Add(idpair_1);

      it_0.Parse(idpair_1.Header, out payload, out local_id, out remote_id);
      Assert.IsTrue(it_0.Verify(idpair_0, idpair_1.Header, out payload), "Return");
      Assert.IsTrue(it_1.Verify(idpair_1, idpair_0.Header, out payload), "Inbound 2");
      Assert.IsTrue(it_0.Verify(idpair_0, idpair_1.Header, out payload), "Return 2");
      Assert.AreEqual(idpair_0.LocalID, idpair_1.RemoteID, "idpair_0.LocalID");
      Assert.AreEqual(idpair_1.LocalID, idpair_0.RemoteID, "idpair_1.LocalID");
      Assert.AreNotEqual(0, idpair_0.LocalID, "idpair_0.LocalID != 0");
      Assert.AreNotEqual(0, idpair_1.LocalID, "idpair_1.LocalID != 0");

      IIdentifierPair idpair_2 = new IdentifierPair();
      idpair_2.RemoteID = idpair_0.LocalID;
      idpair_2.LocalID = 1;
      try {
        Assert.IsFalse(it_0.Verify(idpair_0, idpair_2.Header, out payload), "Malicious packet");
      } catch { }

      IIdentifierPair idpair_3 = new IdentifierPair();
      try {
        Assert.IsFalse(it_0.Verify(idpair_3, idpair_3.Header, out payload), "Broken packet");
      } catch { }
    }
  }
#endif
}
