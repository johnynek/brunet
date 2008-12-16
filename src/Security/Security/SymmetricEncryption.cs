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
using System.Security.Cryptography;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet {
  /// <summary>This wraps SymmetricAlgorithm to be better supported for lossy
  /// communication.</summary>
  public class SymmetricEncryption {
    SymmetricAlgorithm _sa;
    RandomNumberGenerator rng;
    Cache _decryptors;
    ICryptoTransform _enc;
    byte[] _enc_iv;

    /// <summary>Creates a new SymmetricEncryption handler for the passed in
    /// SymmetricAlgorithm.</summary>
    public SymmetricEncryption(SymmetricAlgorithm Algorithm)
    {
      if(Algorithm.Mode != CipherMode.CBC) {
        throw new Exception("SymmetricEncryption requires the symmetric algorithm to use CBC.");
      }

      rng = new RNGCryptoServiceProvider();
      _sa = Algorithm;
      // Match the same size as our Window size...
      _decryptors = new Cache(64);
      _enc_iv = new byte[_sa.BlockSize / 8];
      rng.GetBytes(_enc_iv);
      _enc = _sa.CreateEncryptor(_sa.Key, _enc_iv);
    }

    /// <summary>Takes in an unencrypted byte array and returns the encrypted
    /// form.</summary>
    /// <param name="UnencryptedData">The byte array to encrypt.</param>
    /// <returns>The encrypted data.</returns> */
    public byte[] EncryptData(byte[] UnencryptedData)
    {
      byte[] enc_data = _enc.TransformFinalBlock(UnencryptedData, 0, 
                                                UnencryptedData.Length);
      byte[] data = new byte[_enc_iv.Length + enc_data.Length];
      _enc_iv.CopyTo(data, 0);
      enc_data.CopyTo(data, _enc_iv.Length);
      Array.Copy(enc_data, enc_data.Length - _enc_iv.Length, _enc_iv, 0, _enc_iv.Length);
      return data;
    }

    /// <summary>Takes in a byte array and returns the data in a unencrypted
    /// format.</summary>
    /// <param name="EncryptedData">The data to decrypt.</param>
    /// <returns>The unencrypted data.</param> */
    public byte[] DecryptData(byte[] EncryptedData)
    {
      byte[] iv = new byte[_sa.BlockSize / 8];
      Array.Copy(EncryptedData, 0, iv, 0, iv.Length);
      MemBlock iv_ref = MemBlock.Reference(iv);
      ICryptoTransform dec = (ICryptoTransform) _decryptors[iv_ref];
      if(dec == null) {
        dec = _sa.CreateDecryptor(_sa.Key, iv);
      } else {
        _decryptors.Remove(iv_ref);
      }

      byte[] new_iv = new byte[iv.Length];
      Array.Copy(EncryptedData, EncryptedData.Length - iv.Length, new_iv, 0, iv.Length);
      iv_ref = MemBlock.Reference(new_iv);
      _decryptors[iv_ref] = dec;

      return dec.TransformFinalBlock(EncryptedData, iv.Length,
                                     EncryptedData.Length - iv.Length);
    }

    /// <summary>When we're done using this, it is safest to clear all security
    /// bits from memory.  That's what this does.</summary>
    public void Clear() {
      _sa.Clear();
    }
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class SymmetricEncryptionUnitTests {
    [Test]
    public void TDES()
    {
      SymmetricEncryption enc = new SymmetricEncryption(new TripleDESCryptoServiceProvider());
      for(int i = 1000; i < 1025; i ++) {
        byte[] data = new byte[1024];
        RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        rng.GetBytes(data);
        byte[] encd = enc.EncryptData(data);
        byte[] decd = enc.DecryptData(encd);
        MemBlock mdecd = MemBlock.Reference(decd);
        MemBlock mdata = MemBlock.Reference(data);
        Assert.AreEqual(mdecd, mdata, "TDESEncryption: " + i);
      }
    }
  }
#endif
}
