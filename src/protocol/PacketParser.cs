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
 * Brunet.AHPacket;
 * Brunet.ConnectionPacket;
 * Brunet.Packet;
 * Brunet.ParseException
 */

namespace Brunet
{

  /**
   * When a Packet comes in, the PacketParser reads
   * the type and then creates a Packet of that type
   * and returns it.  This allows us to use the RTTI
   * (run time type information) which .Net provides.
   */

  public class PacketParser
  {

    public static Packet Parse(byte[] p)
    {
      return Parse(p, 0, p.Length);
    }
    public static Packet Parse(byte[] p, int length)
    {
      return Parse(p, 0, length);
    }
    public static Packet Parse(byte[] binpack, int off, int length)
    {
      Packet.ProtType ptype;
      try {
        ptype = (Packet.ProtType) binpack[off];
      }
      catch(System.Exception ex) {
        throw new ParseException("Unrecognized Packet Type", ex);
      }

      Packet p;

      switch (ptype) {
      case Packet.ProtType.Connection:
        p = new ConnectionPacket(binpack, off, length);
        break;
      case Packet.ProtType.AH:
        p = new AHPacket(binpack, off, length);
        break;
      case Packet.ProtType.Direct:
	//the new packet type that we have added into the system
	p = new DirectPacket(binpack, off, length);
	break;
      default:
        throw new ParseException("Unrecognized Packet Type");
      }
      return p;
    }
  }

}
