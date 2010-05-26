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
using Brunet.Concurrent;
using Brunet.Messaging;
using Brunet.Util;

using Mono.Security.X509;
using Mono.Security.X509.Extensions;
using Mono.Security.Cryptography;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;

#if BRUNET_NUNIT
using Brunet.Messaging.Mock;
using NUnit.Framework;
#endif

namespace Brunet.Security.PeerSec {
  /// <summary>Because X509Certificates provided by Mono do not have value based
  /// hashcodes, we had to implement this special class that compares the Raw
  /// Data of a certificate.  This class is thread-safe.</summary>
  public class WriteOnceX509 {
    protected X509Certificate _cert;
    ///<summary></summary>
    public X509Certificate Value {
      get {
        return _cert;
      }
      set {
        X509Certificate cert = Interlocked.CompareExchange(ref _cert, value, null);
        if(cert != null) {
          MemBlock cert0 = MemBlock.Reference(cert.RawData);
          MemBlock cert1 = MemBlock.Reference(value.RawData);
          if(!cert0.Equals(cert1)) {
            throw new Exception("Value already set!");
          }
        }
      }
    }
  }

  /// <summary>This is the brains of the security system.  Each SecurityAssociation
  /// represents a Secure connection via an ISender, such that two different
  /// ISenders would need their own PeerSecAssociation.</summary>
  public class PeerSecAssociation: SecurityAssociation {
    /// <summary>When the SH of an SA has reached its half-life it requires an
    /// update, but the SA cannot do that so he would relay that message to a
    /// listener of this event.</summary>
    public event EventHandler RequestUpdate;

    ///<summary>We are either Active or Updating.</summary>
    public bool Active { get { return _active; } }
    protected bool _active;
    ///<summary>The last time the SH was updated.</summary>
    public DateTime LastUpdate { get { return _last_update; } }
    protected DateTime _last_update;
    ///<summary>Our SPI for this SA.</summary>
    public int SPI { get { return _spi; } }
    protected int _spi;
    ///<summary>The Epoch of our SH.</summary>
    public int CurrentEpoch { get { return _current_epoch; } }
    protected int _current_epoch;

    protected DateTime _last_called_request_update;
    protected SecurityHandler _current_sh;
    protected DiffieHellman _dh;
    protected int _last_epoch;
    protected int _called_enable;
    protected WriteOnceX509 _remote_cert;
    protected WriteOnceX509 _local_cert;

    ///<summary>Local half of the DHE</summary>
    public MemBlock LDHE {
      get {
        lock(_sync) {
          if(_ldhe == null) {
            if(_dh != null) {
              _dh.Clear();
            }
            _dh = new DiffieHellmanManaged();
            _ldhe = _dh.CreateKeyExchange();
          }
        }
        return MemBlock.Reference(_ldhe);
      }
    }
    protected byte[]  _ldhe;

    ///<summary>True if we have successfully verified the hash.</summary>
    public bool HashVerified { get { return _hash_verified; } }
    protected bool _hash_verified;

    ///<summary>Remote half of the DHE</summary>
    public WriteOnceIdempotent<MemBlock> RDHE;
    public WriteOnceIdempotent<MemBlock> RemoteCookie;
    public WriteOnceIdempotent<MemBlock> DHEWithCertificateAndCAsOutHash;
    public WriteOnceIdempotent<MemBlock> DHEWithCertificateAndCAsInHash;
    public WriteOnceIdempotent<MemBlock> DHEWithCertificateHash;

    override public X509Certificate LocalCertificate {
      get {
        return _local_cert.Value;
      }
      set {
        _local_cert.Value = value;
      }
    }

    override public X509Certificate RemoteCertificate {
      get {
        return _remote_cert.Value;
      }
      set {
        _remote_cert.Value = value;
      }
    }

    public PeerSecAssociation(ISender sender, CertificateHandler ch, int spi) :
      base(sender, ch)
    {
      _closed = 0;
      _active = false;
      _spi = spi;
      _last_update = DateTime.MinValue;
      _state = States.Waiting;
      Reset();
    }

    ///<summary>This method listens for the SH to request an update and passes
    ///the message to the RequestUpdate event.</summary>
    protected void UpdateSH(object o, EventArgs ea) {
      if(RequestUpdate != null) {
        lock(_sync) {
          if(_last_called_request_update.AddMilliseconds(TIMEOUT * 2) < DateTime.UtcNow) {
            return;
          }
          _last_called_request_update = DateTime.UtcNow;
        }
        RequestUpdate(this, null);
      }
    }

    ///<summary>This is called when we want to reset the state of the SA after
    ///an equivalent time of two timeouts has occurred.</summary>
    public bool Reset() {
      lock(_sync) {
        if(_running || _closed == 1)
        {
          return false;
        }
        
        _last_update = DateTime.UtcNow;
        _last_called_request_update = DateTime.UtcNow;

        _local_cert = new WriteOnceX509();
        _remote_cert = new WriteOnceX509();
        DHEWithCertificateAndCAsInHash = new WriteOnceIdempotent<MemBlock>();
        DHEWithCertificateAndCAsOutHash = new WriteOnceIdempotent<MemBlock>();
        DHEWithCertificateHash = new WriteOnceIdempotent<MemBlock>();
        RDHE = new WriteOnceIdempotent<MemBlock>();
        RemoteCookie = new WriteOnceIdempotent<MemBlock>();
        _ldhe = null;
        _hash_verified = false;
        _called_enable = 0;
        _running = true;
      }

      UpdateState(States.Active, States.Updating);
      return true;
    }

    ///<summary>Verifies the hash to the DHEWithCertificateAndCAsOutHash.</summary>
    public bool VerifyResponse(MemBlock hash) {
      if(DHEWithCertificateAndCAsOutHash.Value == null) {
        throw new Exception("Hash not set!");
      } else if(!DHEWithCertificateAndCAsOutHash.Value.Equals(hash)) {
        throw new Exception("Hash does not match!");
      }
      _hash_verified = true;
      return hash.Equals(DHEWithCertificateAndCAsOutHash.Value);
    }

    ///<summary>Verifies the hash with the DHEWithCertificateHash.</summary>
    public bool VerifyRequest(MemBlock hash) {
      if(DHEWithCertificateHash.Value == null) {
        throw new Exception("Hash not set!");
      } else if(!DHEWithCertificateHash.Value.Equals(hash)) {
        throw new Exception("Hash does not match!");
      }
      _hash_verified = true;
      return hash.Equals(DHEWithCertificateHash.Value);
    }

    ///<summary>Enables the SA if it has been properly setup.</summary>
    public void Enable() {
      // If both parties setup simultaneous SAs we could end up calling this
      // twice, once as a client and the other as server, this way we only
      // go through the whole process once.
      lock(_sync) {
        if(_called_enable == 1) {
          return;
        } else if(_closed == 1) {
          throw new Exception("Cannot enable a closed SA!");
        } else if(_ldhe == null) {
          throw new Exception("Local DHE not set.");
        } else if(RDHE.Value == null) {
          throw new Exception("Remote DHE not set.");
        } else if(!_hash_verified) {
          throw new Exception("Hash is not verified!");
        }
        _called_enable = 1;

        // Deriving the DHE exchange and determing the order of keys
        // Specifically, we need up to 4 keys for the sender/receiver encryption/
        // authentication codes.  So to determine the order, we say whomever has
        // the smallest gets the first set of keys.
        byte[] rdhe = (byte[]) RDHE.Value;
        RDHE = null;
        byte[] key = _dh.DecryptKeyExchange(rdhe);
        _dh.Clear();
        _dh = null;
        int i = 0;
        while(i < _ldhe.Length && _ldhe[i] == rdhe[i]) i++;
        bool same = i == _ldhe.Length;
        bool first = !same && (_ldhe[i] < rdhe[i]);
        _ldhe = null;
        // Gathering our security parameter objects
        SecurityPolicy sp = SecurityPolicy.GetPolicy(_spi);
        SymmetricAlgorithm in_sa = sp.CreateSymmetricAlgorithm();
        HashAlgorithm in_ha = sp.CreateHashAlgorithm();
        SymmetricAlgorithm out_sa = sp.CreateSymmetricAlgorithm();
        HashAlgorithm out_ha = sp.CreateHashAlgorithm();

        // Generating the total key length 
        int key_length = key.Length + 2 + (in_sa.KeySize / 8 + in_sa.BlockSize / 8) * 2;
        KeyedHashAlgorithm in_kha = in_ha as KeyedHashAlgorithm;
        KeyedHashAlgorithm out_kha = out_ha as KeyedHashAlgorithm;
        if(in_kha != null) {
          key_length += (in_kha.HashSize / 8) * 2;
        }

        // Generating a key by repeatedly hashing the DHE value and the key so far
        SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider();
        int usable_key_offset = key.Length;
        while(key.Length < key_length) {
          byte[] hash = sha1.ComputeHash(key);
          byte[] tmp_key = new byte[hash.Length + key.Length];
          key.CopyTo(tmp_key, 0);
          hash.CopyTo(tmp_key, key.Length);
          key = tmp_key;
        }

        // Like a sub-session ID (see DTLS)
        short epoch = (short) ((key[usable_key_offset] << 8) + key[usable_key_offset + 1]);
        usable_key_offset += 2;

        byte[] key0 = new byte[in_sa.KeySize / 8];
        Array.Copy(key, usable_key_offset, key0, 0, key0.Length);
        usable_key_offset += key0.Length;

        byte[] key1 = new byte[in_sa.KeySize / 8];
        Array.Copy(key, usable_key_offset, key1, 0, key1.Length);
        usable_key_offset += key1.Length;

        // Same may occur if we are forming a session with ourselves!
        if(same) {
          in_sa.Key = key0;
          out_sa.Key = key0;
        } else if(first) {
          in_sa.Key = key0;
          out_sa.Key = key1;
        } else {
          out_sa.Key = key0;
          in_sa.Key = key1;
        }

        if(in_kha != null) {
          byte[] hkey0 = new byte[in_kha.HashSize / 8];
          Array.Copy(key, usable_key_offset, hkey0, 0, hkey0.Length);
          usable_key_offset += hkey0.Length;

          byte[] hkey1 = new byte[in_kha.HashSize / 8];
          Array.Copy(key, usable_key_offset, hkey1, 0, hkey1.Length);
          usable_key_offset += hkey1.Length;

          if(same) {
            in_kha.Key = hkey0;
            out_kha.Key = hkey0;
          } else if(first) {
            in_kha.Key = hkey0;
            out_kha.Key = hkey1;
          } else {
            out_kha.Key = hkey0;
            in_kha.Key = hkey1;
          }
        }

        SecurityHandler sh = new SecurityHandler(in_sa, out_sa, in_ha, out_ha, epoch);
        sh.Update += UpdateSH;
        SecurityHandler to_close = _current_sh;
        if(to_close != null) {
          to_close.Close();
        }
        _current_sh = sh;
        _last_epoch = _current_epoch;
        _current_epoch = epoch;
      }
      // All finished set the state (which will probably fire an event)
      UpdateState(States.Active);
      _active = _state == States.Active;
    }

    ///<summary>All incoming data filters through here.</summary>
    override protected bool HandleIncoming(MemBlock data, out MemBlock app_data)
    {
      if(!_active) {
        if(_closed == 0) {
          UpdateSH(null, null);
        }
        app_data = null;
        return false;
      }

      SecurityDataMessage sdm = new SecurityDataMessage(data);
      if(sdm.SPI != _spi) {
        throw new Exception("Invalid SPI!");
      }

      try {
        // try to decrypt the data
        lock(_sync) {
          _current_sh.DecryptAndVerify(sdm);
        }
      } catch {
        // Maybe this is just a late arriving packet, if it is, we'll just ignore it
        if(sdm.Epoch == _last_epoch) {
          app_data = null;
          return false;
        // bad packet, let's throw it away!
        } else {
          throw;
        }
      }

      _running = true;
      app_data = sdm.Data;
      return true;
    }

    ///<summary>All outgoing data filters through here.</summary>
    override protected bool HandleOutgoing(ICopyable app_data, out ICopyable data)
    {
      if(!_active) {
        if(_closed == 1) {
          throw new SendException(false, "SA closed, unable to send!");
        }
        UpdateSH(null, null);
        data = null;
        return false;
      }

      // prepare the packet
      SecurityDataMessage sdm = new SecurityDataMessage();
      sdm.SPI = _spi;
      sdm.Data = app_data as MemBlock;
      if(sdm.Data == null) {
        byte[] b = new byte[app_data.Length];
        app_data.CopyTo(b, 0);
        sdm.Data = MemBlock.Reference(b);
      }

      // Encrypt it!
      lock(_sync) {
        _current_sh.SignAndEncrypt(sdm);
      }

      data = new CopyList(PeerSecOverlord.Security, PeerSecOverlord.SecureData, sdm.ICPacket);
      return true;
    }

    /// <summary>For some reason we failed during a security exchange.  Since
    /// we only close in the case of Waiting and not Update, we don't call Close
    /// we call this.</summary>
    public void Failure()
    {
      bool close = false;
      lock(_sync) {
        if(State == States.Updating) {
          UpdateState(States.Updating, States.Active);
        }

        if(State != States.Active) {
          close = true;
        }
      }
      if(close) {
        Close("Failed, probably due to timeout");
      }
    }

    public override string ToString() 
    {
      return "SecurityAssociation for " + Sender + " SPI: " + SPI;
    }

    ///<summary>This closes the SA and cleans up its state.</summary>
    override public bool Close(string reason) {
      if(!base.Close(reason)) {
        return false;
      }

      SecurityHandler sh = _current_sh;
      if(sh != null) {
        sh.Close();
      }
      return true;
    }
  }
#if BRUNET_NUNIT
//  [TestFixture]
  public class PeerSecAssociationTest {
    public SecurityAssociation.States state;

    public void StateChange(SecurityAssociation sa, SecurityAssociation.States st)
    {
      state = st;
    }

    [Test]
    public void Test() {
      int spi = SecurityPolicy.DefaultSPI;
      MockSender sender1 = new MockSender(null, null, null, 2);
      MockSender sender2 = new MockSender(null, null, null, 2);
      PeerSecAssociation sa1 = new PeerSecAssociation(sender1, null, spi); 
      sa1.StateChangeEvent += StateChange;
      sender2.Receiver = sa1;
      MockDataHandler mdh1 = new MockDataHandler();
      sa1.Subscribe(mdh1, null);
      PeerSecAssociation sa2 = new PeerSecAssociation(sender2, null, spi); 
      sender1.Receiver = sa2;
      MockDataHandler mdh2 = new MockDataHandler();
      sa2.Subscribe(mdh2, null);

      byte[] b = null;
      Random rand = new Random();
      MemBlock mb = null;

      int current_epoch = sa1.CurrentEpoch;
      for(int i = 0; i < 5; i++) {
        Setup(sa1, sa2);

        b = new byte[128];
        rand.NextBytes(b);
        mb = MemBlock.Reference(b);
        sa1.Send(mb);

        Assert.IsTrue(mdh2.Contains(mb), "Contains" + i);
        Assert.AreEqual(state, sa1.State, "State == Active" + i);
        Assert.IsFalse(current_epoch == sa1.CurrentEpoch, "Current epoch " + i);
        current_epoch = sa1.CurrentEpoch;
      }

      sa1.CheckState();
      sa1.CheckState();
      b = new byte[128];
      rand.NextBytes(b);
      mb = MemBlock.Reference(b);
      try {
        sa1.Send(mb);
      } catch {}
      Assert.IsTrue(!mdh2.Contains(mb), "Failed!");
      Assert.AreEqual(state, sa1.State, "State == Failed");
    }

    public void Callback(object o, EventArgs ea) {
      callback_count++;
    }

    protected int callback_count;

    [Test]
    public void SHUpdateTest() {
      callback_count = 0;
      int spi = SecurityPolicy.DefaultSPI;
      MockSender sender1 = new MockSender(null, null, null, 2);
      MockSender sender2 = new MockSender(null, null, null, 2);
      PeerSecAssociation sa1 = new PeerSecAssociation(sender1, null, spi); 
      sa1.StateChangeEvent += StateChange;
      sender2.Receiver = sa1;
      MockDataHandler mdh1 = new MockDataHandler();
      sa1.Subscribe(mdh1, null);
      PeerSecAssociation sa2 = new PeerSecAssociation(sender2, null, spi); 
      sender1.Receiver = sa2;
      MockDataHandler mdh2 = new MockDataHandler();
      sa2.Subscribe(mdh2, null);

      Setup(sa1, sa2);

      sa1.RequestUpdate += Callback;
      sa2.RequestUpdate += Callback;

      byte[] b = null;
      Random rand = new Random();
      MemBlock mb = null;

      int current_epoch = sa1.CurrentEpoch;
      for(int i = 0; i < 80; i++) {
        b = new byte[128];
        rand.NextBytes(b);
        mb = MemBlock.Reference(b);
        sa1.Send(mb);
        Assert.IsTrue(mdh2.Contains(mb), "Contains" + i);
        if(i % 20 == 0 && i != 0) {
          Assert.AreEqual(callback_count, 1, "Callback count " + i);
          callback_count = 0;
          Setup(sa1, sa2);
        } else {
          if(i % 20 == 1 && i != 1) {
            Assert.IsFalse(current_epoch == sa1.CurrentEpoch, "Current epoch " + i);
            current_epoch = sa1.CurrentEpoch;
          }
          Assert.AreEqual(current_epoch, sa1.CurrentEpoch, "Current epoch " + i);
        }
      }
    }

    protected void Setup(PeerSecAssociation sa1, PeerSecAssociation sa2) {
      sa1.Reset();
      sa2.Reset();
      sa1.RDHE.Value = sa2.LDHE;
      sa2.RDHE.Value = sa1.LDHE;

      Random rand = new Random();
      byte[] b = new byte[128];
      rand.NextBytes(b);
      MemBlock mb = MemBlock.Reference(b);

      sa1.DHEWithCertificateAndCAsOutHash.Value = mb;
      sa1.VerifyResponse(mb);

      b = new byte[128];
      rand.NextBytes(b);
      mb = MemBlock.Reference(b);

      sa2.DHEWithCertificateHash.Value = mb;
      sa2.VerifyRequest(mb);

      sa1.Enable();
      sa2.Enable();
      // This is just for kicks
      sa1.Enable();
    }
  }
#endif
}
