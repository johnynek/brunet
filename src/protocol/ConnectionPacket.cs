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

/*
 * Dependencies : 
 * Brunet.Packet
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
    protected byte[]  _buffer;
    protected int _len;

    public override int Length { get { return _len; } }
    public override int PayloadLength { get { return (_len - 1); } }
    public override Packet.ProtType type {
      get { return Packet.ProtType.Connection; }
    }
    /**
     * This is the prefered way to access the payload since
     * it does not require any copy operation
     */
    public override MemoryStream PayloadStream {
      get {
        //Return a read-only MemoryStream
        return new MemoryStream(_buffer, 1, PayloadLength, false);
      }
    }
    /**
     * @throws ArgumentException if this is not a ConnectionPacket
     */
    public ConnectionPacket(byte[] binary, int off, int len)
    {
      if (binary[off] != (byte)Packet.ProtType.Connection ) {
        throw new System.
        ArgumentException("Packet is not a ConnectionPacket");
      }
      _buffer = new byte[len];
      Array.Copy(binary, off, _buffer, 0, len);
      _len = len;
    }

    public ConnectionPacket(byte[] binary):this(binary, 0,
            binary.Length)
    {

    }

    override public void CopyTo(byte[] dest, int offset)
    {
      Array.Copy(_buffer, 0, dest, offset, Length);
    }

    override public string ToString()
    {
      StringWriter sw = new StringWriter();
      sw.WriteLine("Packet Protocol :  " + this.type.ToString());
      sw.WriteLine("Payload : ");
      System.Text.Encoding e = new System.Text.ASCIIEncoding();
      sw.WriteLine(e.GetString(_buffer));
      sw.WriteLine();
      return sw.ToString();
    }
  }

}
