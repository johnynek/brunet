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
