/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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
using System.IO;

namespace Brunet {
  /** This is a single-hop packet class. When the source and destination
   *  are just a hop away, why incur the burden of putting the > 40 byte
   *  Brunet header.
   */
  public class DirectPacket: Packet {
    //the payload contained inside the packet
    protected byte[] _payload;
    
    //the higher level protocol for the payload
    protected string _pt;
    public string PayloadType { get { return _pt; } }

    /**
     * Constructor
     * @param protocol for the payload
     * @payload packet payload
     */
    public DirectPacket(string payload_prot, byte[] payload) {
      _pt = payload_prot;
      _payload = payload;
    }
    /**
     * Constructor used by PacketParser to build the packet.
     */
    public DirectPacket(byte[] buf, int offset, int length) {
      int off = offset;
      if (buf[offset] != (byte) Packet.ProtType.Direct) {
        throw new System.ArgumentException("Packet is not a direct packet");
      }
      offset += 1;
      int len = 0;
      _pt = NumberSerializer.ReadString(buf, offset, out len);
      offset += len;
      int headersize = offset - off;
      int payload_len = length - headersize;
      _payload = new byte[payload_len];
      Array.Copy(buf, offset, _payload, 0, payload_len);

    }
    /**
     * The header is a shrinked header, with 1 byte identifying the 
     * "direct" type and the rest being the "string-type" protocol
     */
    public int HeaderSize {
      get {
	return 1 + NumberSerializer.GetByteCount(_pt);
      }
    }
    /**
     * The new protocol type "Direct".
     */
    public override Packet.ProtType type {
      get {
	return Packet.ProtType.Direct;
      }
    }
    public override int Length {
      get {
	return HeaderSize + _payload.Length;
      }
    }
    public override int PayloadLength {
      get {
	return _payload.Length;
      }
    }
    public byte[] Payload {
      get {
	return _payload;
      }
    }
    public override MemoryStream PayloadStream {
      get {
        //Return a read-only MemoryStream of the Payload
        //return new MemoryStream(_payload, false);
	return GetPayloadStream(0);
      }
    }
    public override void CopyTo(byte[] dest, int off) {
      //1 byte for packet type - direct
      dest[off] = (byte)Packet.ProtType.Direct;
      off += 1;
      //protocol type which is a string
      off += NumberSerializer.WriteString(_pt, dest, off);
      //copying off the payload
      Array.Copy(_payload, 0, dest, off, PayloadLength);
      off += PayloadLength;
    }

    /**
     * @param offset the offset into the payload to start the stream
     */
    virtual public MemoryStream GetPayloadStream(int offset) {
      return new MemoryStream(_payload, offset, _payload.Length - offset, false);
    }
  }
}
