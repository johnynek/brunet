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

using Brunet;
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
