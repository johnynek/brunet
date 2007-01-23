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
    protected MemBlock _buffer;
    
    //the higher level protocol for the payload
    protected string _pt;
    protected int _type_length;
    public string PayloadType {
      get {
        InitType();
        return _pt;
      }
    }

    /**
     * Constructor
     * @param protocol for the payload
     * @payload packet payload
     */
    public DirectPacket(string payload_prot, byte[] payload) {
      _pt = payload_prot;
      _type_length = NumberSerializer.GetByteCount(_pt);
      byte[] buf = new byte[ 1 + _type_length + payload.Length ];
      buf[0] = (byte) Packet.ProtType.Direct;
      NumberSerializer.WriteString(_pt, buf, 1);
      System.Array.Copy(payload, 0, buf, _type_length + 1, payload.Length);
      _buffer = MemBlock.Reference(buf, 0, buf.Length);
    }
    /**
     * Constructor used by PacketParser to build the packet.
     */
    public DirectPacket(byte[] buf, int offset, int length)
     : this(MemBlock.Copy(buf, offset, length))
    {
    
    }
    public DirectPacket(MemBlock buf) {
      if (buf[0] != (byte) Packet.ProtType.Direct) {
        throw new System.ArgumentException("Packet is not a direct packet");
      }
      _buffer = buf;
    }
    /**
     * The header is a shrinked header, with 1 byte identifying the 
     * "direct" type and the rest being the "string-type" protocol
     */
    public int HeaderSize {
      get {
        InitType();
	return 1 + _type_length;
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
	return _buffer.Length;
      }
    }
    public override int PayloadLength {
      get {
	return _buffer.Length - HeaderSize;
      }
    }
    override public MemBlock  Payload {
      get {
	return _buffer.Slice(HeaderSize);
      }
    }
    protected void InitType() {
      if( _pt == null ) {
        //Lazily get the type
        _type_length = _buffer.Slice(1).IndexOf(0);
        _pt = _buffer.Slice(1, _type_length).GetString(System.Text.Encoding.UTF8);
        _type_length = _type_length + 1;
      }
    }

    public override MemoryStream PayloadStream {
      get {
	return GetPayloadStream(0);
      }
    }
    public override void CopyTo(byte[] dest, int off) {
      _buffer.CopyTo(dest, off);
    }

    /**
     * @param offset the offset into the payload to start the stream
     */
    virtual public MemoryStream GetPayloadStream(int offset) {
      return _buffer.Slice(HeaderSize + offset).ToMemoryStream();
    }
  }
}
