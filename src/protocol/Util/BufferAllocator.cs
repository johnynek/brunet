/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

namespace Brunet {

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

  protected int _max_size;
  protected int _allocation_size;

  protected byte[] _buf;
  public byte[] Buffer { 
    get { return _buf; }
  }

  protected int _offset;
  public int Offset { get { return _offset; } }

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

  public BufferAllocator(int max_size) : this(max_size, 2.0) { }

  public void AdvanceBuffer(int count) {
    _offset += count;
    if( _offset + _max_size > _buf.Length ) {
      //We can't fit another read:
      _buf = new byte[ _allocation_size ];
      _offset = 0;
    }
  }

}

}
