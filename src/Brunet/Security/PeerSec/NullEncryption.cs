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
      BlockSizeValue = 8;
      FeedbackSizeValue = 0;

      LegalKeySizesValue = new KeySizes[1];
      LegalKeySizesValue[0] = new KeySizes(0, 0, 0);

      LegalBlockSizesValue = new KeySizes[1];
      LegalBlockSizesValue[0] = new KeySizes(8, 8, 0);
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

