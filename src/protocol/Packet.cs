using System.IO;

namespace Brunet
{

  /**
   * Base class for all Packets.  All Packets are IMMUTABLE objects.
   * Once set, they cannot be changed.  This is to make them
   * thread-safe.  Also, there is no need to Clone them, since
   * copy-by-reference is fine due to immutability
   */

  abstract public class Packet
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
             * Table of Brunet sub-protocols.  Currently, only two
             * are defined
             */
public enum ProtType:byte
            {
              Connection = 1,
              AH = 2
            }
            /**
             * Copy the binary representation of the Packet into
             * destination starting at offset
             * @param destination the byte array to copy the packet into
             * @param offset the offset of the array to start at
             * @return the number of bytes written into the array
             */
            abstract public void CopyTo(byte[] destination, int offset);
  }

}
