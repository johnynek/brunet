/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin, <boykin@pobox.com>  University of Florida

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
using System.Text;

namespace Brunet
{

  /**
   * Holds connection messages which are sent
   * only between adjacent hosts.  When a
   * ConnectionMessage is routed, it is 
   * embedded into an AHPacket.
   *
   * @see ConnectionMessage
   * @see ConnectionMessageParser
   */
  public class ConnectionPacket : Packet
  {
    //This holds the entire binary represenation of the packet :
    protected MemBlock  _buffer;

    public override int Length { get { return _buffer.Length; } }
    public override int PayloadLength { get { return (_buffer.Length - 1); } }
    public override Packet.ProtType type {
      get { return Packet.ProtType.Connection; }
    }
    /**
     * This is the prefered way to access the payload since
     * it does not require any copy operation
     */
    public override MemoryStream PayloadStream {
      get {
        //Starting at the first byte, return a stream
        return Payload.ToMemoryStream();
      }
    }
    public override MemBlock Payload {
      get {
        return _buffer.Slice(1);
      }
    }
    /**
     * @throws ArgumentException if this is not a ConnectionPacket
     */
    public ConnectionPacket(byte[] binary, int off, int len) : this(MemBlock.Copy(binary, off,len))
    { }
    /**
     * @throws ArgumentException if this is not a ConnectionPacket
     */
    public ConnectionPacket(MemBlock mb)
    {
      if (mb[0] != (byte)Packet.ProtType.Connection ) {
        throw new System.
        ArgumentException("Packet is not a ConnectionPacket");
      }
      _buffer = mb;
    }

    public ConnectionPacket(byte[] binary):this(binary, 0,
            binary.Length)
    {

    }

    override public int CopyTo(byte[] dest, int offset)
    {
      return _buffer.CopyTo(dest, offset);
    }

    override public string ToString()
    {
      StringWriter sw = new StringWriter();
      sw.WriteLine("Packet Protocol :  " + this.type.ToString());
      sw.WriteLine("Payload : ");
      System.Text.Encoding e = new System.Text.ASCIIEncoding();
      byte[] buffer = new byte[ _buffer.Length ];
      _buffer.CopyTo(buffer, 0);
      sw.WriteLine(e.GetString(buffer));
      sw.WriteLine();
      return sw.ToString();
    }
  }

}
