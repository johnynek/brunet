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
      default:
        throw new ParseException("Unrecognized Packet Type");
      }
      return p;
    }
  }

}
