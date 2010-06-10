/*
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet.Messaging;
using Brunet.Util;
using Mono.Security.X509;
using System;
using System.IO;
using System.Security.Cryptography;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Security {
  /// <summary>A revocation message creator and parser.</summary>
  /// <remarks>Stores the user's name, a current time stamp, and a random
  /// number hashed together using SHA1 and signed by an RSA key.</remarks>
  public class UserRevocationMessage : ICopyable {
    public readonly string Username;
    public readonly byte[] Hash;
    public readonly byte[] Signature;
    protected readonly MemBlock _data;

    /// <summary>Create a new revocation message.</summary>
    public UserRevocationMessage(RSACryptoServiceProvider private_key, string username)
    {
      Username = username;
      int signature_length = private_key.KeySize / 8;
      byte[] data = null;

      using(MemoryStream ms = new MemoryStream()) {
        AdrConverter.Serialize(Username, ms);
        Random rand = new Random();
        NumberSerializer.WriteInt(rand.Next(), ms);
        NumberSerializer.WriteLong(DateTime.UtcNow.Ticks, ms);
        data = new byte[ms.Length + signature_length];
        ms.Position = 0;
        ms.Read(data, 0, (int) ms.Length);
      }

      SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider();
      Hash = sha1.ComputeHash(data, 0, data.Length - signature_length);
      Signature = private_key.SignHash(Hash, CryptoConfig.MapNameToOID("SHA1"));
      Signature.CopyTo(data, data.Length - signature_length);
      _data = MemBlock.Reference(data);
    }

    /// <summary>Parse a revocation message.</summary>
    public UserRevocationMessage(Certificate cacert, MemBlock data)
    {
      _data = data;

      int pos = 0;
      int length = 0;

      Username = AdrConverter.Deserialize(data, pos, out length) as string;
      pos += length;
      // Random number to reduce likelihood of malicious duplication of messages
      NumberSerializer.ReadInt(data, pos);
      pos += 4;
      // Verify that there is a date contained therein, perhaps we should verify the time
      new DateTime(NumberSerializer.ReadLong(data, pos));
      pos += 8;
      Signature = new byte[data.Length - pos];
      data.Slice(pos).CopyTo(Signature, 0);

      // hash the data
      SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider();
      Hash = sha1.ComputeHash(data, 0, data.Length - Signature.Length);

      if(!cacert.PublicKey.VerifyHash(Hash,
            CryptoConfig.MapNameToOID("SHA1"), Signature))
      {
        throw new Exception("Invalid UserRevocationMessage signature");
      }
    }

    public int CopyTo(byte[] dst, int offset)
    {
      return _data.CopyTo(dst, offset);
    }

    public int Length { get { return _data.Length; } }
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class UserRevocationTest {
    [Test]
    public void TestParse()
    {
      RSACryptoServiceProvider key = new RSACryptoServiceProvider();
      CertificateMaker cm = new CertificateMaker("United States", "UFL",
          "ACIS", "David Wolinsky", "davidiw@ufl.edu", key,
          "brunet:node:abcdefghijklmnopqrs");
      Certificate cert = cm.Sign(cm, key);
      UserRevocationMessage urm = new UserRevocationMessage(key, "David Wolinsky");
      UserRevocationMessage urm0 = new UserRevocationMessage(cert, MemBlock.Copy(urm));
      Assert.AreEqual(urm.Signature, urm0.Signature, "Signature");
      Assert.AreEqual(urm.Username, urm0.Username, "Username");
      Assert.AreEqual(urm.Hash, urm0.Hash, "Hash");
    }
  }
#endif
}
