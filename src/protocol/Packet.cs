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

using System.IO;

namespace Brunet
{

  /**
   * Base class for all Packets.  All Packets are IMMUTABLE objects.
   * Once set, they cannot be changed.  This is to make them
   * thread-safe.  Also, there is no need to Clone them, since
   * copy-by-reference is fine due to immutability
   */

  abstract public class Packet : Brunet.ICopyable
  {
    /**
     * Maximum size of a packet, this is the largest number
     * that can be represented by a signed short
     */
    public static readonly int MaxLength = 32767;

    public abstract int Length { get; }

    public abstract ProtType type
    {
      get;
    }
    
    public abstract int PayloadLength { get; }
    /**
     * @returns a System.IO.MemoryStream object which
     * can only be read from that holds the Payload
     * This does not require a copy operation
     */
    public abstract MemoryStream PayloadStream { get; }
   
    /**
     * Get a MemBlock of the Payload.
     */
    public abstract MemBlock Payload { get; }
    /**
     * Table of Brunet sub-protocols.  Currently, only two
     * are defined
     */
    public enum ProtType:byte
    {
      Connection = 1,
	AH = 2,
	Direct = 3
      //added the new packet type, which doesn't have a Brunet header
      //useful only when sourse and destination are direcly connected
    }
    /**
     * Copy the binary representation of the Packet into
     * destination starting at offset
     * @param destination the byte array to copy the packet into
     * @param offset the offset of the array to start at
     * @return the number of bytes written into the array
     */
    abstract public int CopyTo(byte[] destination, int offset);
  }
}
