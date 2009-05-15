/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Mono.Security.X509;
using Mono.Security.X509.Extensions;
using Mono.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;

#if BRUNET_NUNIT
using Brunet.Mock;
using NUnit.Framework;
#endif

namespace Brunet {
  /// <summary>Because X509Certificates provided by Mono do not have value based
  /// hashcodes, we had to implement this special class that compares the Raw
  /// Data of a certificate.</summary>
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
  /// ISenders would need their own SecurityAssociation.</summary>
  public class SecurityAssociation: SimpleSource, IDataHandler, ISender {
    ///<summary>The different states for an SA to be in.</summary>
    public enum SAState {
      ///<summary>The SA is forming a connection but has not done so yet.</summary>
      Waiting,
      ///<summary>The SA has formed a connection and it is active.</summary>
      Active,
      ///<summary>The SA has formed a connection and is updating it.</summary>
      Updating,
      ///<summary>The SA has been closed and is no longer active.</summary>
      Closed
    }

    ///<summary>The state of the SA.</summary>
    public SAState State {
      get {
        return _state;
      }
      set {
        bool update = false;
        lock(_sync) {
          if(value == _state) {
            return;
          }

          if(value == SAState.Active) {
            _active = true;
            if(_state != SAState.Updating) {
              update = true;
            }
          } else if(value == SAState.Closed) {
            _active = false;
            update = true;
          }
          _state = value;
        }
        ProtocolLog.WriteIf(ProtocolLog.Security, "Setting " + ToString() + " : " + value);
        if(update && StateChange != null) {
          StateChange(this, null);
        }
      }
    }
    
    protected SAState _state;

    /// <summary>When the SH of an SA has reached its half-life it requires an
    /// update, but the SA cannot do that so he would relay that message to a
    /// listener of this event.</summary>
    public event EventHandler RequestUpdate;
    ///<summary>This event is called when we change amongst waiting, active, and
    ///closed.</summary>
    public event EventHandler StateChange;

    ///<summary>We are either Active or Updating.</summary>
    public bool Active { get { return _active; } }
    protected bool _active;
    ///<summary>We are closed.</summary>
    public bool Closed { get { return _closed == 1; } }
    protected int _closed;
    ///<summary>The last time the SH was updated.</summary>
    public DateTime LastUpdate { get { return _last_update; } }
    protected DateTime _last_update;
    ///<summary>The insecure sender we send over.</summary>
    public ISender Sender { get { return _sender; } }
    protected ISender _sender;
    ///<summary>Our SPI for this SA.</summary>
    public int SPI { get { return _spi; } }
    protected int _spi;
    ///<summary>The Epoch of our SH.</summary>
    public int CurrentEpoch { get { return _current_epoch; } }
    protected int _current_epoch;

#if BRUNET_NUNIT
    // This may need to be tweaked for slower machines
    public static readonly int TIMEOUT = 500;
#else
    public static readonly int TIMEOUT = 60000;
#endif

    ///<summary>This should be called before processing any messages, all
    ///security authorization attempts should be completed by TIMEOUT.</summary>
    public bool TimedOut {
      get {
        return _last_update.AddMilliseconds(TIMEOUT) < DateTime.UtcNow;
      }
    }

    ///<summary>If we don't receive a packet soon enough, this will be false
    ///which allows the SA to be reset.</summary>
    protected bool _incoming;
    protected DateTime _last_called_request_update;
    protected SecurityHandler _current_sh;
    protected DiffieHellman _dh;
    protected bool _running;
    protected int _last_epoch;
    protected int _called_enable;

    ///<summary>Local half of the DHE</summary>
    public MemBlock LDHE {
      get {
        if(_ldhe == null) {
          if(_dh != null) {
            _dh.Clear();
          }
          _dh = new DiffieHellmanManaged();
          _ldhe = _dh.CreateKeyExchange();
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
    public WriteOnceX509 LocalCertificate;
    public WriteOnceX509 RemoteCertificate;
    public WriteOnceIdempotent<MemBlock> DHEWithCertificateAndCAsOutHash;
    public WriteOnceIdempotent<MemBlock> DHEWithCertificateAndCAsInHash;
    public WriteOnceIdempotent<MemBlock> DHEWithCertificateHash;

    public SecurityAssociation(ISender sender, int spi) {
      _closed = 0;
      _active = false;
      _sender = sender;
      _spi = spi;
      _last_update = DateTime.MinValue;
      _state = SAState.Waiting;
      Reset();
    }

    ///<summary>Closes the SecurityAssociation if this has been called two
    ///successive times without an incoming or outgoing message passing over
    ///this SA or we are inactive and nothing has happened in the equivalent of
    ///5 timeouts.</summary>
    public bool GarbageCollect() {
      bool close = false;
      lock(_sync) {
        if(_closed == 1) {
          close = true;
        } if(!_running) {
          close = true;
#if BRUNET_NUNIT
        } else if(!_active) {
#else
        } else if(!_active && _last_update.AddSeconds(TIMEOUT * 5) < DateTime.UtcNow) {
#endif
          close = true;
        }
        _incoming = false;
        _running = false;
      }

      if(close) {
        Close("Garbage collect");
      }
      return close;
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
        if(_incoming && _last_update != DateTime.MinValue &&
            (_last_update.AddMilliseconds(TIMEOUT * 2) > DateTime.UtcNow ||
            _closed == 1))
        {
          return false;
        }
        
        _last_update = DateTime.UtcNow;
        _last_called_request_update = DateTime.UtcNow;
      }

      LocalCertificate = new WriteOnceX509();
      RemoteCertificate = new WriteOnceX509();
      DHEWithCertificateAndCAsInHash = new WriteOnceIdempotent<MemBlock>();
      DHEWithCertificateAndCAsOutHash = new WriteOnceIdempotent<MemBlock>();
      DHEWithCertificateHash = new WriteOnceIdempotent<MemBlock>();
      RDHE = new WriteOnceIdempotent<MemBlock>();
      RemoteCookie = new WriteOnceIdempotent<MemBlock>();
      _ldhe = null;
      _hash_verified = false;
      _called_enable = 0;
      _incoming = true;
      _running = true;
      if(State == SAState.Active) {
        State = SAState.Updating;
      }

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
      if(Interlocked.Exchange(ref _called_enable, 1) == 1) {
        return;
      } else if(_closed == 1) {
        throw new Exception("Cannot enable a closed SA!");
      } else if(_ldhe == null) {
        throw new Exception("Local DHE not set.");
      } else if(RDHE.Value == null) {
        throw new Exception("Remote DHE not set.");
      } else if(!_hash_verified) {
        throw new Exception("Hash is not verified!");
      } else if(TimedOut) {
        throw new Exception("Timed out on this one!");
      }

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
      // All finished set the state (which will probably fire an event)
      State = SAState.Active;
    }

    ///<summary>All incoming data filters through here.</summary>
    public void HandleData(MemBlock data, ISender return_path, object state) {
      if(!_active) {
        if(_closed == 0) {
          UpdateSH(null, null);
        }
        return;
      }

      SecurityDataMessage sdm = new SecurityDataMessage(data);
      if(sdm.SPI != _spi) {
        throw new Exception("Invalid SPI!");
      }

      SecurityHandler sh = _current_sh;
      try {
        // try to decrypt the data
        sh.DecryptAndVerify(sdm);
      } catch {
        // Maybe this is just a late arriving packet, if it is, we'll just ignore it
        if(sdm.Epoch == _last_epoch) {
          return;
        // maybe the current_sh got updated, let's try again
        } else if(_current_sh != sh) {
          _current_sh.DecryptAndVerify(sdm);
        // maybe its none of the above, let's throw it away!
        } else {
          throw;
        }
      }

      // Hand it to our subscriber
      if(_sub != null) {
        _sub.Handle(sdm.Data, this);
      }
      _incoming = true;
      _running = true;
    }

    ///<summary>All outgoing data filters through here.</summary>
    public void Send(ICopyable data) {
      if(!_active) {
        if(_closed == 1) {
          throw new SendException(false, "SA closed, unable to send!");
        }
        UpdateSH(null, null);
        return;
      }

      // prepare the packet
      SecurityDataMessage sdm = new SecurityDataMessage();
      sdm.SPI = _spi;
      sdm.Data = data as MemBlock;
      if(sdm.Data == null) {
        byte[] b = new byte[data.Length];
        data.CopyTo(b, 0);
        sdm.Data = MemBlock.Reference(b);
      }

      // Encrypt it!
      SecurityHandler sh = _current_sh;
      try {
        sh.SignAndEncrypt(sdm);
      } catch {
        if(sh != _current_sh) {
          _current_sh.SignAndEncrypt(sdm);
        } else {
          throw;
        }
      }

      // Prepare for sending and send over the underlying ISender!
      data = new CopyList(SecurityOverlord.Security, SecurityOverlord.SecureData, sdm.ICPacket);
      try {
        _sender.Send(data);
        _running = true;
      } catch (Exception e) {
        Close("Failed on sending");
        throw new SendException(false, "Failed on sending closing...", e);
      }
    }

    ///<summary>Given a string, this looks inside the certificates SANE to see
    ///if the string is present.  This isn't inefficient as it looks, there
    ///tends to be no entries at most of those places, so this usually has
    ///runtime of 1.</summary>
    public bool VerifyCertificateBySubjectAltName(string name) {
      foreach(X509Extension ext in RemoteCertificate.Value.Extensions) {
        if(!ext.Oid.Equals("2.5.29.17")) {
          continue;
        }
        SubjectAltNameExtension sane = new SubjectAltNameExtension(ext);
        foreach(string aname in sane.RFC822) {
          if(name.Equals(aname)) {
            return true;
          }
        }
        foreach(string aname in sane.DNSNames) {
          if(name.Equals(aname)) {
            return true;
          }
        }
        foreach(string aname in sane.IPAddresses) {
          if(name.Equals(aname)) {
            return true;
          }
        }
        foreach(string aname in sane.UniformResourceIdentifiers) {
          if(name.Equals(aname)) {
            return true;
          }
        }
      }
      return false;
    }

    /// <summary>For some reason we failed during a security exchange.  Since
    /// we only close in the case of Waiting and not Update, we don't call Close
    /// we call this.</summary>
    public void Failure() {
      bool close = false;
      lock(_sync) {
        if(State == SAState.Updating) {
          State = SAState.Active;
        } else if(State != SAState.Active) {
          close = true;
        }
      }
      if(close) {
        Close("Failed, probably due to timeout");
      }
    }

    public string ToUri() {
      throw new NotImplementedException();
    }

    public override string ToString() {
      return "SecurityAssociation for " + Sender + " SPI: " + SPI;
    }

    ///<summary>This closes the SA and cleans up its state.</summary>
    public void Close(string reason) {
      ProtocolLog.WriteIf(ProtocolLog.Security, ToString() + " closing because " + reason);
      if(Interlocked.Exchange(ref _closed, 1) == 1) {
        return;
      }
      State = SAState.Closed;

      SecurityHandler sh = _current_sh;
      if(sh != null) {
        sh.Close();
      }
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class SecurityAssociationTest {
    public SecurityAssociation.SAState state;

    public void StateChange(Object o, EventArgs ea) {
      SecurityAssociation sa = o as SecurityAssociation;
      state = sa.State;
    }

    [Test]
    public void Test() {
      int spi = SecurityPolicy.DefaultSPI;
      MockSender sender1 = new MockSender(null, null, null, 2);
      MockSender sender2 = new MockSender(null, null, null, 2);
      SecurityAssociation sa1 = new SecurityAssociation(sender1, spi); 
      sa1.StateChange += StateChange;
      sender2.Receiver = sa1;
      MockDataHandler mdh1 = new MockDataHandler();
      sa1.Subscribe(mdh1, null);
      SecurityAssociation sa2 = new SecurityAssociation(sender2, spi); 
      sender1.Receiver = sa2;
      MockDataHandler mdh2 = new MockDataHandler();
      sa2.Subscribe(mdh2, null);

      byte[] b = null;
      Random rand = new Random();
      MemBlock mb = null;

      int current_epoch = sa1.CurrentEpoch;
      for(int i = 0; i < 5; i++) {
        Thread.Sleep(SecurityAssociation.TIMEOUT * 2 + 5);
        Setup(ref sa1, ref sa2);

        b = new byte[128];
        rand.NextBytes(b);
        mb = MemBlock.Reference(b);
        sa1.Send(mb);
        Assert.IsTrue(mdh2.Contains(mb), "Contains" + i);
        Assert.AreEqual(state, sa1.State, "State == Active" + i);
        Assert.IsFalse(current_epoch == sa1.CurrentEpoch, "Current epoch " + i);
        current_epoch = sa1.CurrentEpoch;
      }

      sa1.GarbageCollect();
      sa1.GarbageCollect();
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
      SecurityAssociation sa1 = new SecurityAssociation(sender1, spi); 
      sa1.StateChange += StateChange;
      sender2.Receiver = sa1;
      MockDataHandler mdh1 = new MockDataHandler();
      sa1.Subscribe(mdh1, null);
      SecurityAssociation sa2 = new SecurityAssociation(sender2, spi); 
      sender1.Receiver = sa2;
      MockDataHandler mdh2 = new MockDataHandler();
      sa2.Subscribe(mdh2, null);

      Setup(ref sa1, ref sa2);

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
          Thread.Sleep(SecurityAssociation.TIMEOUT * 2 + 5);
          Setup(ref sa1, ref sa2);
        } else {
          if(i % 20 == 1 && i != 1) {
            Assert.IsFalse(current_epoch == sa1.CurrentEpoch, "Current epoch " + i);
            current_epoch = sa1.CurrentEpoch;
          }
          Assert.AreEqual(current_epoch, sa1.CurrentEpoch, "Current epoch " + i);
        }
      }
    }

    protected void Setup(ref SecurityAssociation sa1, ref SecurityAssociation sa2) {
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
