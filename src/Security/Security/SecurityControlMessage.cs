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

using System;
using System.Collections.Generic;
using System.Security.Cryptography;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet {
  /// <summary>Parses and creates SecurityControlMessages</summary>
  /// <remarks>The format for said packets is:
  /// [version][security params][message type][cookie local][cookie remote]
  /// [dhe length][dhe][cert length][cert][cas length][cas][signature]
  /// version, type, data length are integers
  /// the cookie and id sizes are static in a given environment
  /// the signature is static based upon the signature type
  /// </remarks>
  public class SecurityControlMessage {
    public static readonly int CookieLength = 20;
    public static readonly int CALength = 20;
    public static readonly MemBlock EmptyCookie;

    static SecurityControlMessage()
    {
      byte[] empty_cookie = new byte[CookieLength];
      for(int i = 0; i < CookieLength; i++) {
        empty_cookie[i] = 0;
      }
      EmptyCookie = MemBlock.Reference(empty_cookie);
    }

    ///<summary>The purpose for the SecurityControlMessage</summary>
    public enum MessageType {
      Cookie,
      CookieResponse,
      DHEWithCertificate,
      DHEWithCertificateAndCAs,
      Confirm,
      NoSuchSA
    }

    protected int _version;
    public int Version {
      get {
        return _version;
      }
      set {
        if(value != _version) {
          _update_packet = true;
          _version = value;
        }
      }
    }

    protected int _spi;
    ///<summary>Security Parameter Index proposed.</summary>
    public int SPI {
      get {
        return _spi;
      }
      set {
        if(value != _spi) {
          _update_packet = true;
          _spi = value;
        }
      }
    }

    protected MessageType _type;
    public MessageType Type {
      get {
        return _type;
      }
      set {
        if(value != _type) {
          _update_packet = true;
          _type = value;
        }
      }
    }

    protected MemBlock _local_cookie;
    public MemBlock LocalCookie {
      get {
        return _local_cookie;
      }
      set {
        if(value != _local_cookie) {
          _update_packet = true;
          _local_cookie = value;
        }
      }
    }

    protected MemBlock _remote_cookie;
    public MemBlock RemoteCookie {
      get {
        return _remote_cookie;
      }
      set {
        if(value != _remote_cookie) {
          _update_packet = true;
          _remote_cookie = value;
        }
      }
    }

    protected List<MemBlock> _cas;
    ///<summary>List of CA serial numbers.</summary>
    public List<MemBlock> CAs {
      get {
        return _cas;
      }
      set {
        _update_packet = true;
        _cas = value;
      }
    }

    protected MemBlock _certificate;
    public MemBlock Certificate {
      get {
        return _certificate;
      }
      set {
        if(value != _certificate) {
          _update_packet = true;
          _certificate = value;
        }
      }
    }

    protected MemBlock _dhe;
    public MemBlock DHE {
      get {
        return _dhe;
      }
      set {
        if(value != _dhe) {
          _update_packet = true;
          _dhe = value;
        }
      }
    }

    protected MemBlock _hash;
    public MemBlock Hash {
      get {
        return _hash;
      }
      set {
        if(value != _hash) {
          _update_packet = true;
          _hash = value;
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
          _update_packet = true;
          _signature = value;
        }
      }
    }

    protected bool _update_packet;
    protected MemBlock _packet;
    ///<summary>MemBlock equivalent of the packet.</summary>
    public MemBlock Packet {
      get {
        if(_update_packet) {
          UpdatePacket();
          _update_packet = false;
        }
        return _packet;
      }
    }

    ///<summary>Everything but the signature from the packet.</summary>
    public MemBlock UnsignedData {
      get {
        if(_signature == null) {
          Signature = MemBlock.Reference(new byte[0]);
        }
        return Packet.Slice(0, Packet.Length - Signature.Length);
      }
    }

    public SecurityControlMessage(MemBlock data) {
      try {
        int pos = 0;
        _version = NumberSerializer.ReadInt(data, 0);
        pos += 4; 
        _spi = NumberSerializer.ReadInt(data, pos);
        pos += 4;
        _type = (MessageType) NumberSerializer.ReadInt(data, pos);
        pos += 4;
        _local_cookie = data.Slice(pos, CookieLength);
        pos += CookieLength;
        _remote_cookie = data.Slice(pos, CookieLength);
        pos += CookieLength;

        bool read_cas = false;
        bool read_dhe = false;
        bool read_cert = false;
        bool read_hash = false;

        switch(_type) {
          case MessageType.CookieResponse:
            read_cas = true;
            break;
          case MessageType.DHEWithCertificate:
            read_cert = true;
            read_dhe = true;
            break;
          case MessageType.DHEWithCertificateAndCAs:
            read_cas = true;
            read_dhe = true;
            read_cert = true;
            break;
          case MessageType.Confirm:
            read_hash = true;
            break;
        }

        if(read_dhe) {
          int dhe_length = NumberSerializer.ReadInt(data, pos);
          pos += 4;
          _dhe = data.Slice(pos, dhe_length);
          pos += dhe_length;
        }

        if(read_cert) {
          int cert_length = NumberSerializer.ReadInt(data, pos);
          pos +=4;
          _certificate = data.Slice(pos, cert_length);
          pos += cert_length;
        }

        if(read_cas) {
          int cas_length = NumberSerializer.ReadInt(data, pos);
          pos += 4;
          _cas = new List<MemBlock>();
          int end = pos + cas_length;
          while(pos < end) {
            _cas.Add(data.Slice(pos, CALength));
            pos += CALength;
          }
        }

        if(read_hash) {
          int hash_length = NumberSerializer.ReadInt(data, pos);
          pos +=4;
          _hash = data.Slice(pos, hash_length);
          pos += hash_length;
        }

        _signature = data.Slice(pos);
      } catch {
        throw new Exception("Invalid SecurityControlMessage");
      }
      _update_packet = false;
      _packet = data;
    }

    public SecurityControlMessage() {
      _update_packet = true;
    }

    ///<summary>Writes the packet.</summary>
    protected void UpdatePacket() {
      try { 
        int data_length = 0;
        bool write_cas = false;
        bool write_dhe = false;
        bool write_cert = false;
        bool write_hash = false;
        bool require_signature = true;

        switch(_type) {
          case MessageType.NoSuchSA:
            require_signature = false;
            break;
          case MessageType.Cookie:
            require_signature = false;
            break;
          case MessageType.CookieResponse:
            require_signature = false;
            data_length = 4 + _cas.Count * CALength;
            write_cas = true;
            break;
          case MessageType.DHEWithCertificate:
            data_length = 4 + _dhe.Length + 4 + _certificate.Length;
            write_cert = true;
            write_dhe = true;
            break;
          case MessageType.DHEWithCertificateAndCAs:
            data_length = 4 + _dhe.Length + 4 + _certificate.Length + 4 + _cas.Count * CALength;
            write_cas = true;
            write_dhe = true;
            write_cert = true;
            break;
          case MessageType.Confirm:
            data_length = 4 + _hash.Length;
            write_hash = true;
            break;
        }

        int length = 4 + 4 + 4 + 2 * CookieLength + data_length;
        if(require_signature) {
          length += _signature.Length;
        }
        byte[] b = new byte[length];
        int pos = 0;
        NumberSerializer.WriteInt(_version, b, pos);
        pos += 4;
        NumberSerializer.WriteInt(_spi, b, pos);
        pos += 4;
        NumberSerializer.WriteInt((int) _type, b, pos);
        pos += 4;
        if(_local_cookie == null) {
          EmptyCookie.CopyTo(b, pos);
        } else {
          _local_cookie.CopyTo(b, pos);
        }
        pos += CookieLength;
        if(_remote_cookie == null) {
          EmptyCookie.CopyTo(b, pos);
        } else {
          _remote_cookie.CopyTo(b, pos);
        }
        pos += CookieLength;
        
        if(write_dhe) {
          NumberSerializer.WriteInt(_dhe.Length, b, pos);
          pos += 4;
          _dhe.CopyTo(b, pos);
          pos += _dhe.Length;
        }

        if(write_cert) {
          NumberSerializer.WriteInt(_certificate.Length, b, pos);
          pos += 4;
          _certificate.CopyTo(b, pos);
          pos += _certificate.Length;
        }

        if(write_cas) {
          NumberSerializer.WriteInt(_cas.Count * CALength, b, pos);
          pos += 4;
          foreach(MemBlock ca in _cas) {
            ca.CopyTo(b, pos);
            pos += CALength;
          }
        }

        if(write_hash) {
          NumberSerializer.WriteInt(_hash.Length, b, pos);
          pos += 4;
          _hash.CopyTo(b, pos);
          pos += _hash.Length;
        }

        if(require_signature) {
          _signature.CopyTo(b, pos);
        }
        _packet = MemBlock.Reference(b);
      } catch(Exception e) {
        throw new Exception("Missing data for SecurityControlMessage!");
      }
      _update_packet = false;
    }

    ///<summary>Verifies the packet given a RSA key and a Hash algorithm.</summary>
    public bool Verify(RSACryptoServiceProvider rsa, HashAlgorithm hash) {
      byte[] unsigned_data = (byte[]) UnsignedData;
      byte[] signature = (byte[]) Signature;
      return rsa.VerifyData(unsigned_data, hash, signature);
    }

    ///<summary>Signs a packet given a RSA key and a Hash algorithm.</summary>
    public void Sign(RSACryptoServiceProvider rsa, HashAlgorithm hash) {
      byte[] unsigned_data = (byte[]) UnsignedData;
      byte[] signature = rsa.SignData(unsigned_data, hash);
      Signature = MemBlock.Reference(signature); 
    }
  }

#if BRUNET_NUNIT
  /// [version][type][id local][id remote][cookie local][cookie remote][data length][data][signature]
  [TestFixture]
  public class SecurityControlMessageTest {
    // build one from scratch
    // parse it using SCM
    // cause a rebuild and test the data block again by parsing it using SCM
    [Test]
    public void Incoming() {
      Random rand = new Random();
      byte[] local_cookie = new byte[SecurityControlMessage.CookieLength];
      byte[] remote_cookie = new byte[SecurityControlMessage.CookieLength];
      byte[] dhe = new byte[144];
      byte[] cas = new byte[120];
      byte[] cert = new byte[100];
      RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
      HashAlgorithm hash = new SHA1CryptoServiceProvider();

      rand.NextBytes(local_cookie);
      rand.NextBytes(remote_cookie);
      rand.NextBytes(dhe);
      rand.NextBytes(cas);
      rand.NextBytes(cert);

      MemBlock mlocal_cookie = MemBlock.Reference(local_cookie);
      MemBlock mremote_cookie = MemBlock.Reference(remote_cookie);
      MemBlock mdhe = MemBlock.Reference(dhe);
      MemBlock mcert = MemBlock.Reference(cert);
      MemBlock mcas = MemBlock.Reference(cas);
      List<MemBlock> lcas = new List<MemBlock>();
      for(int i = 0; i < cas.Length; i+= SecurityControlMessage.CALength) {
        lcas.Add(MemBlock.Reference(mcas.Slice(i, SecurityControlMessage.CALength)));
      }

      int length = 4 + 4 + 4 + 2 * SecurityControlMessage.CookieLength +
        4 + cas.Length + 4 + dhe.Length +
        4 + cert.Length;

      byte[] b = new byte[length];
      int pos = 0;
      NumberSerializer.WriteInt(5, b, pos);
      pos += 4;
      NumberSerializer.WriteInt(12345, b, pos);
      pos += 4;
      NumberSerializer.WriteInt((int) SecurityControlMessage.MessageType.DHEWithCertificateAndCAs, b, pos);
      pos += 4;
      local_cookie.CopyTo(b, pos);
      pos += SecurityControlMessage.CookieLength;
      remote_cookie.CopyTo(b, pos);
      pos += SecurityControlMessage.CookieLength;

      NumberSerializer.WriteInt(dhe.Length, b, pos);
      pos += 4;
      dhe.CopyTo(b, pos);
      pos += dhe.Length;

      NumberSerializer.WriteInt(cert.Length, b, pos);
      pos += 4;
      cert.CopyTo(b, pos);
      pos += cert.Length;

      NumberSerializer.WriteInt(cas.Length, b, pos);
      pos += 4;
      mcas.CopyTo(b, pos);
      pos += cas.Length;

      byte[] signature = rsa.SignData(b, hash);
      byte[] nb = new byte[b.Length + signature.Length];
      b.CopyTo(nb, 0);
      signature.CopyTo(nb, b.Length);
      MemBlock packet = MemBlock.Reference(nb);

      // check 
      SecurityControlMessage scm = new SecurityControlMessage(packet);
      Assert.AreEqual(5, scm.Version, "Version");
      Assert.AreEqual(12345, scm.SPI, "SPI");
      Assert.AreEqual(SecurityControlMessage.MessageType.DHEWithCertificateAndCAs, scm.Type, "Type");
      Assert.AreEqual(mlocal_cookie, scm.LocalCookie, "LocalCookie");
      Assert.AreEqual(mremote_cookie, scm.RemoteCookie, "RemoteCookie");
      Assert.AreEqual(mdhe, scm.DHE, "DHE");
      Assert.AreEqual(mcert, scm.Certificate, "Certificate");
      int contains = 0;
      foreach(MemBlock ca in scm.CAs) {
        if(scm.CAs.Contains(ca)) {
          contains++;
        }
      }
      Assert.AreEqual(contains, lcas.Count, "Contains CAs");
      Assert.IsTrue(scm.Verify(rsa, hash), "Signature");
      Assert.AreEqual(packet, scm.Packet, "Packet");

      // change a few things around and check again!
      scm.Version = 0;
      SecurityControlMessage scm1 = new SecurityControlMessage(scm.Packet);
      scm1.Sign(rsa, hash);
      Assert.AreEqual(scm1.Version, scm.Version, "Version 1");
      Assert.AreEqual(scm1.SPI, scm.SPI, "SPI 1");
      Assert.AreEqual(scm1.Type, scm.Type, "Type 1");
      Assert.AreEqual(scm1.LocalCookie, scm.LocalCookie, "LocalCookie 1");
      Assert.AreEqual(scm1.RemoteCookie, scm.RemoteCookie, "RemoteCookie 1");
      Assert.AreEqual(mdhe, scm.DHE, "DHE 1");
      Assert.AreEqual(mcert, scm.Certificate, "Certificate 1");
      contains = 0;
      foreach(MemBlock ca in scm.CAs) {
        if(scm.CAs.Contains(ca)) {
          contains++;
        }
      }
      Assert.AreEqual(contains, lcas.Count, "Contains CAs 1");
      Assert.IsTrue(scm1.Signature != scm.Signature, "Signature 1");
      Assert.AreEqual(scm1.Packet.Slice(4, scm1.Signature.Length),
          scm.Packet.Slice(4, scm.Signature.Length), "Packet 1");
    }
  }
#endif
}
