/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

namespace Brunet.Util {

/**
 * We often need a buffer whose maximum size is known,
 * but the actual used bytes is not know (reading some
 * data from the network for instance).
 *
 * This class allocates a larger than neccesary fixed size
 * buffer.  The idea is to reduce copies, allocation and
 * garbage collection.
 */
public class BufferAllocator {

  protected readonly int _max_size;
  protected readonly int _allocation_size;

  protected byte[] _buf;
  public byte[] Buffer { 
    get { return _buf; }
  }

  protected int _offset;
  public int Offset { get { return _offset; } }
  /**
   * The capacity of the current Buffer
   */
  public int Capacity {
    get {
      return _buf.Length - _offset;
    }
  }
  /**
   * @param max_size the maximum size of a buffer you are going to read
   * @param overhead maximum overhead to use
   */
  public BufferAllocator(int max_size, double overhead) {
    _max_size = max_size;
    _allocation_size = (int)((1.0 + overhead) * max_size);
    _buf = new byte[ _allocation_size ];
    _offset = 0;
  }

  /**
   * Makes a BufferAllocator with overhead = 1.0.  In the worst case
   * this wastes 50% of the memory it uses, but it allocates smaller
   * blocks (which are less likely to stay in scope longer).
   */
  public BufferAllocator(int max_size) : this(max_size, 1.0) { }

  public void AdvanceBuffer(int count) {
    if( count < 0 ) { throw new System.ArgumentOutOfRangeException("count", count,
                                       "Count must be non-negative"); }
    _offset += count;
    if( _offset + _max_size > _buf.Length ) {
      //We can't fit another read:
      _buf = new byte[ _allocation_size ];
      _offset = 0;
    }
  }

}

}
