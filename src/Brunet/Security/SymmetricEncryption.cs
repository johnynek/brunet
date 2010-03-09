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

// Portions of this code are borrowed from:
//
// Mono.Security.Cryptography.SymmetricTransform implementation
//
// Authors:
//	Thomas Neidhart (tome@sbox.tugraz.at)
//	Sebastien Pouliot <sebastien@ximian.com>
//
// Portions (C) 2002, 2003 Motus Technologies Inc. (http://www.motus.com)
// Copyright (C) 2004-2008 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using Brunet;
using System;
using System.Security.Cryptography;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Security {
  /// <summary>This wraps SymmetricAlgorithm to be better supported for lossy
  /// communication.  This class is not thread-safe.</summary>
  public class SymmetricEncryption {
    SymmetricAlgorithm _sa;
    RandomNumberGenerator rng;
    Brunet.Collections.Cache _decryptors;
    ICryptoTransform _enc;
    byte[] _enc_iv;
    byte[] _temp;
    public readonly int BlockSizeByte;

    /// <summary>Creates a new SymmetricEncryption handler for the passed in
    /// SymmetricAlgorithm.</summary>
    public SymmetricEncryption(SymmetricAlgorithm Algorithm)
    {
      if(Algorithm.Mode != CipherMode.CBC) {
        throw new Exception("SymmetricEncryption requires the symmetric algorithm to use CBC.");
      }

      rng = new RNGCryptoServiceProvider();
      _sa = Algorithm;
      // We take care of PKCS7 padding here due to issues in the underlying implementations...
      _sa.Padding = PaddingMode.None;
      BlockSizeByte = _sa.BlockSize / 8;
      // Match the same size as our Window size...
      _decryptors = new Brunet.Collections.Cache(64);
      _enc_iv = new byte[BlockSizeByte];
      rng.GetBytes(_enc_iv);
      _enc = _sa.CreateEncryptor(_sa.Key, _enc_iv);
      _temp = new byte[BlockSizeByte];
    }

    /// <summary>Takes in an unencrypted byte array and returns the encrypted
    /// form.</summary>
    /// <param name="UnencryptedData">The byte array to encrypt.</param>
    /// <returns>The encrypted data.</returns> */
    public byte[] EncryptData(byte[] UnencryptedData)
    {
      int count = UnencryptedData.Length;
      int full = (count / BlockSizeByte) * BlockSizeByte;
      int rem = count - full;
      int total = full + BlockSizeByte;

      byte[] output = new byte[BlockSizeByte + total];
      _enc_iv.CopyTo(output, 0);
      _enc.TransformBlock(UnencryptedData, 0, full, output, BlockSizeByte);

      Buffer.BlockCopy(UnencryptedData, full, _temp, 0, rem);
      byte padding = (byte) (BlockSizeByte - rem);
      for(int i = rem; i < BlockSizeByte; i++) {
        _temp[i] = padding;
      }

      _enc.TransformBlock(_temp, 0, BlockSizeByte, output, output.Length - BlockSizeByte);
      _enc_iv.CopyTo(output, 0);
      Buffer.BlockCopy(output, output.Length - BlockSizeByte, _enc_iv, 0, BlockSizeByte);

      return output;
    }

    /// <summary>Takes in a byte array and returns the data in a unencrypted
    /// format.</summary>
    /// <param name="EncryptedData">The data to decrypt.</param>
    /// <returns>The unencrypted data.</param> */
    public byte[] DecryptData(byte[] EncryptedData)
    {
      Brunet.Util.MemBlock iv_ref = Brunet.Util.MemBlock.Reference(EncryptedData, 0, BlockSizeByte);
      ICryptoTransform dec = (ICryptoTransform) _decryptors[iv_ref];
      if(dec == null) {
        byte[] iv = new byte[BlockSizeByte];
        iv_ref.CopyTo(iv, 0);
        dec = _sa.CreateDecryptor(_sa.Key, iv);
      } else {
        _decryptors.Remove(iv_ref);
      }

      int count = EncryptedData.Length - BlockSizeByte;
      iv_ref = Brunet.Util.MemBlock.Reference(EncryptedData, count, BlockSizeByte);
      _decryptors[iv_ref] = dec;


      if((count % BlockSizeByte) > 0 || count == 0) {
        throw new CryptographicException("Invalid input block size.");
      }

      byte[] output = new byte[count];
      dec.TransformBlock(EncryptedData, BlockSizeByte, count, output, 0);

      byte padding = output[count - 1];
      int length = count - padding;
      for(int i = length; i < count; i++) {
        if(output[i] != padding) {
          throw new CryptographicException(String.Format("Bad padding at position {0}.", i));
        }
      }

      byte[] res = new byte[length];
      Buffer.BlockCopy(output, 0, res, 0, length);
      return res;
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
        byte[] data = new byte[1010];
        RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        rng.GetBytes(data);
        byte[] encd = enc.EncryptData(data);
        if(i % 12 == 0) {
          continue;
        }
        byte[] decd = enc.DecryptData(encd);
        Brunet.Util.MemBlock mdecd = Brunet.Util.MemBlock.Reference(decd);
        Brunet.Util.MemBlock mdata = Brunet.Util.MemBlock.Reference(data);
        Assert.AreEqual(mdecd, mdata, "TDESEncryption: " + i);
      }
    }
  }
#endif
}
