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
using System;

/**
\namespace NetworkPackets
\brief Defines Packet, Packet with a Payload, Ethernet, IP, and UDP Packets.
*/
namespace NetworkPackets {
  /**
  <summary>Provides an abstraction to sue a generic packet idea, that is you
  can use the ICPacket portion to make a large packet and just copy the final 
  object to a byte array in the end rather then at each stage.  When Packet
  is accessed and is undefined, it will perform the copy automatically for 
  you from ICPacket to Packet.</summary>
  */
  public abstract class DataPacket {
    /// <summary>The packet in ICopyable format.</summary>
    protected ICopyable _icpacket;
    /// <summary>The packet in ICopyable format.</summary>
    public ICopyable ICPacket { get { return _icpacket; } }

    /// <summary>The packet in MemBlock format</summary>
    protected MemBlock _packet;
    /// <summary>The packet in ICopyable format.  Creates the _packet if it
    /// does not already exist.</summary>
    public MemBlock Packet {
      get {
        if(_packet == null) {
          if(_icpacket is MemBlock) {
            _packet = (MemBlock) _icpacket;
          }
          else {
            byte[] tmp = new byte[_icpacket.Length];
            _icpacket.CopyTo(tmp, 0);
            _packet = MemBlock.Reference(tmp);
          }
        }
        return _packet;
      }
    }
  }

  /**
  <summary>Similar to DataPacket but also provides a(n) (IC)Payload for packet
  types that have a header and a body, as Ethernet and IP Packets do.</summary>
  */
  public abstract class NetworkPacket: DataPacket {
    /// <summary>The payload in ICopyable format.</summary>
    protected ICopyable _icpayload;
    /// <summary>The payload in ICopyable format.</summary>
    public ICopyable ICPayload { get { return _icpayload; } }
    /// <summary>The packet in MemBlock format</summary>
    protected MemBlock _payload;
    /// <summary>The packet in ICopyable format.  Creates the _packet if it
    /// does not already exist.</summary>
    public MemBlock Payload {
      get {
        if(_payload == null) {
          if(_icpayload is MemBlock) {
            _payload = (MemBlock) _icpayload;
          }
          else {
            byte[] tmp = new byte[_icpayload.Length];
            _icpayload.CopyTo(tmp, 0);
            _payload = MemBlock.Reference(tmp);
          }
        }
        return _payload;
      }
    }
  }
}