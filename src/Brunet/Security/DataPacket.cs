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
using Brunet.Util;

namespace Brunet.Security {
  /// <summary>Just an abstract packet class.  This is thread-safe.</summary>
  public abstract class DataPacket {
    /// <summary>The packet in ICopyable format.</summary>
    protected ICopyable _icpacket;
    /// <summary>The packet in ICopyable format.</summary>
    public ICopyable ICPacket {
      get {
        if(_update_packet) {
          UpdatePacket();
        }
        return _icpacket;
      }
    }

    /// <summary>The packet in MemBlock format</summary>
    protected MemBlock _packet;
    /// <summary>The packet in ICopyable format.  Creates the _packet if it
    /// does not already exist.</summary>
    public MemBlock Packet {
      get {
        if(_update_packet || _update_icpacket) {
          if(ICPacket is MemBlock) {
            _packet = (MemBlock) ICPacket;
          } else {
            _packet = MemBlock.Copy(ICPacket);
          }
          _update_packet = false;
        }
        return _packet;
      }
    }

    protected bool _update_icpacket;
    protected bool _update_packet;

    protected virtual void UpdatePacket() {
      _update_icpacket = false;
    }

    public DataPacket() {
      _update_icpacket = true;
      _update_packet = true;
    }

    public DataPacket(ICopyable packet) {
      _update_icpacket = false;
      _update_packet = false;

      _icpacket = packet;
      _packet = packet as MemBlock;
      if(_packet == null) {
        _update_packet = true;
      }
    }
  }
}
