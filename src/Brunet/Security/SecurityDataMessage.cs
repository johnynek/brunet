/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using Brunet.Util;
using System;
using System.Security.Cryptography;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Security {
  /// <summary>Parses and creates SecurityDataMessages.  In general this class
  /// is not thread-safe.</summary>
  /// <remarks>The format for said packets is:
  /// [security params][epoch][seqid][encrypted [data length][data][signature]]
  /// </remarks>
  public class SecurityDataMessage: DataPacket {
    protected int _spi;
    ///<summary></summary>
    public int SPI {
      get {
        return _spi;
      }
      set {
        if(value != _spi) {
          _update_icpacket = true;
          _spi = value;
        }
      }
    }

    protected int _epoch;
    ///<summary>The epoch of the SH who created this packet.</summary>
    public int Epoch {
      get {
        return _epoch;
      }
      set {
        if(value != _epoch) {
          _update_icpacket = true;
          _epoch = value;
        }
      }
    }

    protected int _seqid;
    ///<summary>The sequence ID.</summary>
    public int Seqid {
      get {
        return _seqid;
      }
      set {
        if(value != _seqid) {
          _update_icpacket = true;
          _seqid = value;
        }
      }
    }

    protected MemBlock _data;
    ///<summary>User data of the packet.</summary>
    public MemBlock Data {
      get {
        return _data;
      }
      set {
        if(value != _data) {
          _update_icpacket = true;
          _data = value;
        }
      }
    }

    protected MemBlock _signature;
    public MemBlock Signature {
      get {
        return _signature;
      }
      set {
        if(value != _signature) {
          _update_icpacket = true;
          _signature = value;
        }
      }
    }

    protected MemBlock _encrypted_data;

    public SecurityDataMessage(MemBlock data): base(data) {
      try {
        int pos = 0;
        _spi = NumberSerializer.ReadInt(data, pos);
        pos += 4;
        _epoch = NumberSerializer.ReadInt(data, pos);
        pos += 4;
        _seqid = NumberSerializer.ReadInt(data, pos);
        pos += 4;
        _encrypted_data = data.Slice(pos);
      } catch {
        throw new Exception("Invalid SecurityDataMessage");
      }
      _update_icpacket = false;
    }

    public SecurityDataMessage(): base() {
      _update_icpacket = true;
    }

    protected override void UpdatePacket() {
      try {
        byte[] b = new byte[4 + 4 + 4];
        int pos = 0;
        NumberSerializer.WriteInt(_spi, b, pos);
        pos += 4;
        NumberSerializer.WriteInt(_epoch, b, pos);
        pos += 4;
        NumberSerializer.WriteInt(_seqid, b, pos);
        MemBlock header = MemBlock.Reference(b);
        _icpacket = new CopyList(header, _encrypted_data);
      } catch(Exception e) {
        throw new Exception("Missing data to build packet!");
      }
      base.UpdatePacket();
    }

    protected byte[] _unsigned_data {
      get {
        byte[] b = new byte[4 + 4 + 4 + _data.Length];
        try {
          int pos = 0;
          NumberSerializer.WriteInt(_spi, b, pos);
          pos += 4;
          NumberSerializer.WriteInt(_epoch, b, pos);
          pos += 4;
          NumberSerializer.WriteInt(_seqid, b, pos);
          pos += 4;
          _data.CopyTo(b, pos);
        } catch {
          throw new Exception("Missing data for sign packet!");
        }

        return b;
      }
    }
        
    ///<summary>Verifies the packet given a Hash algorithm.</summary>
    public bool Verify(HashAlgorithm hash) {
      MemBlock hash_data = MemBlock.Reference(hash.ComputeHash(_unsigned_data));
      return _signature.Equals(hash_data);
    }

    ///<summary>Signs a packet given a Hash algorithm.</summary>
    public void Sign(HashAlgorithm hash) {
      _signature = MemBlock.Reference(hash.ComputeHash(_unsigned_data));
      _update_icpacket = true;
    }

    ///<summary>Encrypts the packet given a SymmetricEncryption.</summary>
    public void Encrypt(SymmetricEncryption se) {
      byte[] to_encrypt = new byte[4 + _data.Length + _signature.Length];
      int pos = 0;
      NumberSerializer.WriteInt(_data.Length, to_encrypt, pos);
      pos += 4;
      _data.CopyTo(to_encrypt, pos);
      pos += _data.Length;
      _signature.CopyTo(to_encrypt, pos);
      _encrypted_data = se.EncryptData(to_encrypt);
      _update_icpacket = true;
    }

    ///<summary>Decrypts the packet given a SymmetricEncryption returning true
    ///if it was able to decrypt it.</summary>
    public bool Decrypt(SymmetricEncryption se) {
      byte[] decrypted = se.DecryptData(_encrypted_data);
      int pos = 0;
      int data_len = NumberSerializer.ReadInt(decrypted, pos);
      pos += 4;
      _data = MemBlock.Reference(decrypted, pos, data_len);
      pos += data_len;
      _signature = MemBlock.Reference(decrypted, pos, decrypted.Length - pos);
      return true;
    }
  }

#if BRUNET_NUNIT
  /// [security params][epoch][seqid][encrypted [data length][data][signature]]
  [TestFixture]
  public class SecurityDataMessageTest {
    // build one from scratch
    // parse it using SDM
    // cause a rebuild and test the data block again by parsing it using SDM
    [Test]
    public void Incoming() {
      SymmetricAlgorithm sa = new RijndaelManaged();
      SymmetricEncryption se = new SymmetricEncryption(sa);
      HashAlgorithm hash = new SHA1CryptoServiceProvider();

      int spi = 12345;
      int epoch = 67890;
      int seqid = 1222;

      Random rand = new Random();
      byte[] data = new byte[144];
      rand.NextBytes(data);
      MemBlock mdata = MemBlock.Reference(data);

      byte[] to_sign = new byte[12 + data.Length];
      int pos = 0;
      NumberSerializer.WriteInt(spi, to_sign, pos);
      pos += 4;
      NumberSerializer.WriteInt(epoch, to_sign, pos);
      pos += 4;
      NumberSerializer.WriteInt(seqid, to_sign, pos);
      pos += 4;
      data.CopyTo(to_sign, pos);

      byte[] signature = hash.ComputeHash(to_sign);
      byte[] to_encrypt = new byte[4 + data.Length + signature.Length];
      pos = 0;
      NumberSerializer.WriteInt(data.Length, to_encrypt, pos);
      pos += 4;
      data.CopyTo(to_encrypt, pos);
      pos += data.Length;
      signature.CopyTo(to_encrypt, pos);

      byte[] encrypted = se.EncryptData(to_encrypt);
      byte[] packet = new byte[12 + encrypted.Length];
      pos = 0;
      NumberSerializer.WriteInt(spi, packet, pos);
      pos += 4;
      NumberSerializer.WriteInt(epoch, packet, pos);
      pos += 4;
      NumberSerializer.WriteInt(seqid, packet, pos);
      pos += 4;
      encrypted.CopyTo(packet, pos);

      MemBlock mpacket = MemBlock.Reference(packet);

      // check 
      SecurityDataMessage sdm_e = new SecurityDataMessage(packet);
      sdm_e.Decrypt(se);
      Assert.AreEqual(spi, sdm_e.SPI, "SPI");
      Assert.AreEqual(epoch, sdm_e.Epoch, "Epoch");
      Assert.AreEqual(seqid, sdm_e.Seqid, "Seqid");
      Assert.AreEqual(mdata, sdm_e.Data, "Data");
      Assert.IsTrue(sdm_e.Verify(hash), "Signature");
      Assert.AreEqual(mpacket, sdm_e.Packet, "Packet");

      SecurityDataMessage sdm_d = new SecurityDataMessage();
      sdm_d.SPI = spi;
      sdm_d.Epoch = epoch;
      sdm_d.Seqid = seqid;
      sdm_d.Data = data;
      sdm_d.Sign(hash);
      sdm_d.Encrypt(se);
      sdm_e = new SecurityDataMessage(sdm_d.Packet);
      sdm_e.Decrypt(se);

      Assert.AreEqual(spi, sdm_e.SPI, "SPI");
      Assert.AreEqual(epoch, sdm_e.Epoch, "Epoch");
      Assert.AreEqual(seqid, sdm_e.Seqid, "Seqid");
      Assert.AreEqual(mdata, sdm_e.Data, "Data");
      Assert.IsTrue(sdm_e.Verify(hash), "Signature");
      Assert.AreEqual(sdm_d.Packet, sdm_e.Packet, "Packet");
    }
  }
#endif
}
