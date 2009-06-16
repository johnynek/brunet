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

namespace Brunet.Security {
  /// <summary>Provides a Null SymmetricAlgorithm, its blazing fast!  Typically,
  /// these classes are not thread-safe, but since the block size is 1 and there
  /// is no history, it is thread-safe.</summary>
  public class NullEncryption : SymmetricAlgorithm {
    public NullEncryption()
    {
      KeySizeValue = 0;
      BlockSizeValue = 1;
      FeedbackSizeValue = 0;

      LegalKeySizesValue = new KeySizes[1];
      LegalKeySizesValue[0] = new KeySizes(0, 0, 0);

      LegalBlockSizesValue = new KeySizes[1];
      LegalBlockSizesValue[0] = new KeySizes(1, 1, 0);
    }

    public override void GenerateIV()
    {
    }

    public override void GenerateKey()
    {
    }

    public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[] rgbIV) {
      return new NullTransform();
    }

    public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[] rgbIV) {
      return new NullTransform();
    }
  }

  public sealed class NullTransform : ICryptoTransform
  {
    public int BlockSizeValue { get { return 1; } }
    public bool CanTransformMultipleBlocks { get { return true; } }
    public bool CanReuseTransform { get { return true; } }
    public int InputBlockSize { get { return 1; } }
    public int OutputBlockSize { get { return 1; } }

    public void Clear()
    {
    }

    public void Reset()
    {
    }

    public void Dispose()
    {
    }

    public int TransformBlock(byte[] inputBuffer, int inputOffset,
        int inputCount, byte[] outputBuffer, int outputOffset)
    {
      Array.Copy(inputBuffer, inputOffset, outputBuffer, outputOffset, inputCount);
      return inputCount;
    }

    public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset,
        int inputCount)
    {
      if(inputOffset == 0 && inputCount == inputBuffer.Length)
      {
        return inputBuffer;
      }

      byte[] output = new byte[inputCount];
      Array.Copy(inputBuffer, inputOffset, output, 0, inputCount);
      return output;
    }
  }

#if BRUNET_NUNIT
#endif
}

