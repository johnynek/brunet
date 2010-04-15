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
using System;
using System.Security.Cryptography;
using System.Threading;
#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Security {
  ///<summary>Provides encryption, decryption, and authentication through
  /// a single object.  The format for packets is 2 bytes for index, 2 bytes for
  /// sequence id, data, and signature, with data, and signature being
  /// encrypted.  This class is not thread-safe.</summary> 
  /// @todo Need to not take in duplicates within the sliding Window! Read Appendix
  /// C of RFC2401*/
  public class SecurityHandler {
    /// <summary>Used for verifying.</summary>
    protected HashAlgorithm _incoming_auth;
    /// <summary>Used for signing.</summary>
    protected HashAlgorithm _outgoing_auth;
    /// <summary>Used for encrypting.</summary>
    protected SymmetricEncryption _encryptor;
    /// <summary>Used for decrypting.</summary>
    protected SymmetricEncryption _decryptor;
    /// <summary>We may have delayed packets related to different SHs so we use
    /// the Epoch to differentiate.</summary>
    protected readonly short Epoch;
    protected int _last_incoming_seqid;
    protected int _last_outgoing_seqid;
    /// <summary>This is the recommended Window size used by DTLS</summary>
    public const int WINDOW_SIZE = 64;
    public const int HEADER_LENGTH = 6;
#if BRUNET_NUNIT
    public const int HALF_LIFE = 20;
#else
//    public const int HALF_LIFE = Int32.MaxValue / 2;
    public const int HALF_LIFE = 1000;
#endif
    /// <summary></summary>
    protected Object _sync;
    /// <summary>The creation time for this SecurityHandler.</summary>
    public readonly DateTime StartTime;
    /// <summary>The enables the use of a sliding window, where slow arriving
    /// packets are rejected, if they occur outside the windows time frame.</summary>
    public bool UseWindow;
    protected bool _closed;
    public bool Closed { get { return _closed; } }
    public int LastIncomingSeqid { get { return _last_incoming_seqid; } }
    public int LastOutgoingSeqid { get { return _last_outgoing_seqid; } }
    /// <summary>This is called whenever the half-life of the SH has been reached
    /// and every 1000 packets afterwards.</summary>
    public event EventHandler Update;

    ///  <summary>Creates a new SecurityHandler.  The algorithm used is to
    /// sign the data, append that to the end, and then encrypt the data.</summary>
    /// <param name="in_sa">Used for decrypting.</param>
    /// <param name="out_sa">Used for encrypting.</param>
    /// <param name="in_ha">Used for verifying.</param>
    /// <param name="out_ha">Used for signing.</param>
    /// <param name="epoch">A number to uniquely identify this SH.</param> */
    public SecurityHandler(SymmetricAlgorithm in_sa, SymmetricAlgorithm out_sa,
        HashAlgorithm in_ha, HashAlgorithm out_ha, short epoch)
    {
      _sync = new Object();
      StartTime = DateTime.UtcNow;
      _incoming_auth = in_ha;
      _outgoing_auth = out_ha;
      _encryptor = new SymmetricEncryption(in_sa);
      _decryptor = new SymmetricEncryption(out_sa);
      Epoch = epoch;
      _last_outgoing_seqid = 0;
      _last_incoming_seqid = 0;
      UseWindow = true;
    }

    /// <summary>First signs the data and then encrypts it.</summary>
    /// <param name="UnecryptedData">The data to sign and encrypt.</param>
    /// <returns>The signed and encrypted data.</returns>
    public void SignAndEncrypt(SecurityDataMessage sdm) {
      if(_closed) {
        throw new Exception("SecurityHandler: closed");
      }
      // Get the sequence id and increment the counter
      int seqid = ++_last_outgoing_seqid;
      // We ask for an update at the half life and every 1000 packets thereafter
      if(seqid == HALF_LIFE || (seqid > HALF_LIFE && seqid % 1000 == 0)) {
        if(Update != null) {
          Update(Epoch, EventArgs.Empty);
        }
      }

      sdm.Seqid = seqid;
      sdm.Epoch = Epoch;
      sdm.Sign(_outgoing_auth);
      sdm.Encrypt(_encryptor);
    }

    /// <summary>Decrypts the data and then verifys it.</summary>
    /// <param name="EncryptedData">The data to decrypt and verify.</param>
    /// <returns>The verified and decrypted data.</returns>
    public void DecryptAndVerify(SecurityDataMessage sdm) {
      if(_closed) {
        throw new Exception("SecurityHandler: closed");
      } else if(sdm.Epoch != Epoch) {
        throw new Exception(String.Format("Wrong index {0}, it should be {1}.",
              sdm.Epoch, Epoch));
      }

      int seqid = sdm.Seqid;

      // Verify the seqid
      // If greater than current, new seqid and allow packet
      // If less than current but within window, allow packet
      // Else throw exception
      if(UseWindow) {
        if(seqid == Int32.MaxValue) {
          Close();
          throw new Exception("Maximum amount of packets sent over SecurityHandler.");
        } else if(seqid + WINDOW_SIZE < _last_incoming_seqid) {
          throw new Exception(String.Format("Invalid seqid: {0}, current seqid: {1}, window: {2}.",
                              seqid, _last_incoming_seqid, WINDOW_SIZE));
        }
      }

      sdm.Decrypt(_decryptor);
      if(!sdm.Verify(_incoming_auth)) {
        throw new Exception("Invalid signature");
      }

      if(seqid > _last_incoming_seqid) {
        _last_incoming_seqid = seqid;
      }
    }

    /// <summary>When this is done being used, this should be closed for
    /// security purposes.</summary>
    public void Close() {
      if(_closed) {
        return;
      }
      _closed = true;
      _encryptor.Clear();
      _decryptor.Clear();
      _outgoing_auth.Clear();
      _incoming_auth.Clear();
    }
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class SecurityHandlerUnitTests {
    protected bool _update_set = false;

    protected void UpdateHandler(object o, EventArgs ea) {
      _update_set = true;
    }

/*
    [Test]
    public void Null()
    {
      SymmetricAlgorithm enc = new NullEncryption();
      HashAlgorithm auth = new NullHash();
      SecurityHandler sh = new SecurityHandler(enc, enc, auth, auth, 0);
      int window_size = SecurityHandler.WINDOW_SIZE;
      byte[] data = new byte[1024];
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      rng.GetBytes(data);
      SecureDataMessage sdm = new SecurityDataMessage();
      sdm.SPI = 1;
      sdm.Version = 0;
      sdm.Data = MemBlock.Reference(data);

      sh.SignAndEncrypt(sdm);
      // the other node is last received 0... we send -1 the window size
      int index = 1;
      NumberSerializer.WriteInt(index, encd, 2);
      byte[] decd = sh.DecryptAndVerify(encd);
      MemBlock mdecd = MemBlock.Reference(decd);
      MemBlock mdata = MemBlock.Reference(data);
      Assert.AreEqual(mdecd, mdata, "SecurityHandler:Null index = 1");

      index = Int32.MaxValue / 4;
      NumberSerializer.WriteInt(index, encd, 2);
      decd = sh.DecryptAndVerify(encd);
      mdecd = MemBlock.Reference(decd);
      Assert.AreEqual(mdecd, mdata, "SecurityHandler:Null index = big number");

      NumberSerializer.WriteInt(index - window_size, encd, 2);
      decd = sh.DecryptAndVerify(encd);
      mdecd = MemBlock.Reference(decd);
      Assert.AreEqual(mdecd, mdata, "SecurityHandler:Null index - WINDOW_SIZE");

      NumberSerializer.WriteInt(index - window_size - 1, encd, 2);
      try {
        mdecd = null;
        decd = sh.DecryptAndVerify(encd);
        mdecd = MemBlock.Reference(decd);
      } catch {}
      Assert.IsTrue(!mdata.Equals(mdecd), "SecurityHandler:Null index - WINDOW_SIZE - 1");

      NumberSerializer.WriteInt(Int32.MaxValue, encd, 2);
      try {
        mdecd = null;
        decd = sh.DecryptAndVerify(encd);
        mdecd = MemBlock.Reference(decd);
      } catch {}
      Assert.IsTrue(!mdata.Equals(mdecd), "SecurityHandler:Null index - WINDOW_SIZE - 1");
    }
*/

    [Test]
    public void TDES()
    {
      SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider(); 
      SymmetricAlgorithm sa = new TripleDESCryptoServiceProvider();
      SecurityHandler sh = new SecurityHandler(sa, sa, sha1, sha1, 0);
      byte[] data = new byte[1024];
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      rng.GetBytes(data);
      SecurityDataMessage sdm = new SecurityDataMessage();
      sdm.SPI = 5;
      sdm.Data = Brunet.Util.MemBlock.Reference(data);
      sh.SignAndEncrypt(sdm);
      SecurityDataMessage sdm_d = new SecurityDataMessage(sdm.Packet);
      sh.DecryptAndVerify(sdm_d);
      Assert.AreEqual(sdm.Data, sdm.Data, "SecurityHandler");
    }
  }
#endif
}
