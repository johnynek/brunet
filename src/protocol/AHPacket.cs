/*
 * Dependencies : 
 * Brunet.Address;
 * Brunet.AddressParser;
 * Brunet.Packet;
 * Brunet.NumberSerializer;
 */

using System;
using System.IO;

namespace Brunet
{

 /**
  * Type of Packet which is routed over the virtual Brunet
  * network.  These packets can hold a general payload and
  * can be routed to their destination only using the header.
  */

  public class AHPacket : Packet
  {

  /** The number of bytes in the header, including the type 0x02 */
    public static readonly int HeaderSize = 46;
  /** This is the largest positive short */
    public static readonly short MaxTtl = (short) 32767;


    protected byte[] _payload;
	
    /**
     * @param s Stream to read the AHPacket from
     * @param length the lenght of the packet
     */
    public AHPacket(Stream s, int length)
    {
      if( s.Length < length || length < HeaderSize ) {
        throw new ArgumentException("Cannot read AHPacket from Stream");
      }
      byte[] header = new byte[HeaderSize];
      s.Read(header, 0, HeaderSize);
      if( header[0] != (byte)Packet.ProtType.AH ) {
        throw new System.
          ArgumentException("Packet is not an AHPacket");
      } 
      _hops = NumberSerializer.ReadShort(header, 1);
      _ttl = NumberSerializer.ReadShort(header, 3);
      _source = AddressParser.Parse(header, 5);
      _destination = AddressParser.Parse(header, 25);
      _pt = (Protocol) header[45];
      _payload = new byte[length - HeaderSize];
      s.Read(_payload, 0, _payload.Length);
    }
    public AHPacket(byte[] buf, int offset, int length)
    {
      if (buf[offset] != (byte)Packet.ProtType.AH ) {
        throw new System.
          ArgumentException("Packet is not an AHPacket");
      }
      _hops = NumberSerializer.ReadShort(buf, offset + 1);
      _ttl = NumberSerializer.ReadShort(buf, offset + 3);
      _source = AddressParser.Parse(buf, offset + 5);
      _destination = AddressParser.Parse(buf, offset + 25);
      _pt = (Protocol) buf[offset + 45];
      _payload = new byte[length - HeaderSize];
      Array.Copy(buf, offset + HeaderSize,
		 _payload, 0, length - HeaderSize);
    }
    
    public AHPacket(byte[] buf, int length):this(buf, 0, length)
    {
    }

    public AHPacket(byte[] buf):this(buf, 0, buf.Length)
    {
    }

    /**
     * @param hops Hops for this packet
     * @param ttl TTL for this packet
     * @param source Source Address for this packet
     * @param destination Destination Address for this packet
     * @param payload_prot AHPacket.Protocol of the Payload
     * @param payload buffer holding the payload
     * @param off Offset to the zeroth byte of payload
     * @param len Length of the payload
     */
    public AHPacket(short hops,
                    short ttl,
                    Address source,
                    Address destination,
                    Protocol payload_prot,
		    byte[] payload, int off, int len)
    {
      _hops = hops;
      _ttl = ttl;
      _source = source;
      _destination = destination;
      _pt = payload_prot;
      _payload = new byte[len];
      Array.Copy(payload, off, _payload, 0, len);
    }
    /**
     * Same as similar constructor with offset and len, only
     * we assume the entired buffer is the payload
     */
    public AHPacket(short hops,
                    short ttl,
                    Address source,
                    Address destination,
                    Protocol payload_prot,
		    byte[] payload) : this(hops, ttl, source, destination,
			                   payload_prot, payload, 0,
					   payload.Length) {
    
    }
    /**
     * Makes a new packet with a new header but the same payload
     * without copying the Payload, just a reference to the payload
     */
    public AHPacket(short hops,
                    short ttl,
                    Address source,
                    Address destination,
                    AHPacket p) {
      _hops = hops;
      _ttl = ttl;
      _source = source;
      _destination = destination;
      _pt = p._pt;
      _payload = p._payload;
    }
    
    public override ProtType type { get { return Packet.ProtType.AH; } }
    public override int Length { get { return HeaderSize + _payload.Length; } }
    public override int PayloadLength { get { return _payload.Length; } }
    
    protected short _hops;
  /**
   * The number of edges this packet has crossed
   */
    public short Hops { get { return _hops; } }
  
    protected short _ttl;
  /**
   * The maximum number of edges this packet may cross
   */
    public short Ttl { get { return _ttl; } }

    protected Address _source;
  /**
   * The source of this packet
   * The source should only be an AHAddress
   */
    public Address Source { get { return _source; } }
    
    protected Address _destination;
  /**
   * The destination of this packet
   */
    public Address Destination { get { return _destination; } }

    protected Protocol _pt;
    public Protocol PayloadType { get { return _pt; } }

    /**
     * This is the prefered way to access the payload
     * Does not require any copy operation
     */
    public override MemoryStream PayloadStream {
      get {
	//Return a read-only MemoryStream of the Payload
        return new MemoryStream(_payload, false);
      }
    }

    public override void CopyTo(byte[] dest, int off)
    {
      int start_off = off;
      dest[off] = (byte)Packet.ProtType.AH;
      off += 1;
      NumberSerializer.WriteShort(_hops, dest, off);
      off += 2;
      NumberSerializer.WriteShort(_ttl, dest, off);
      off += 2;
      _source.CopyTo(dest, off);
      off += 20;
      _destination.CopyTo(dest, off);
      off += 20;
      dest[off] = (byte)_pt;
      off += 1;
      Array.Copy(_payload, 0, dest, off, PayloadLength);
      off += PayloadLength;
    }
    
    public enum Protocol:byte
    {
      Deflate = 1,
      Connection = 2,
      Forwarding = 3,
      Echo = 4,
      Tftp = 5
    }

    /**
     * @returns a new AHPacket with the hops field incremented
     */
    public AHPacket IncrementHops()
    {
      return new AHPacket( (short)(_hops + 1),
		           _ttl,
			   _source,
			   _destination,
			   this );
    }
    
    override public string ToString()
    {
      StringWriter sw = new StringWriter();
      sw.WriteLine("Packet Protocol: " + this.type.ToString());
      sw.WriteLine("Hops: " + this.Hops);
      sw.WriteLine("Ttl: " + this.Ttl);
      sw.WriteLine("Source: " + this.Source.ToString());
      sw.WriteLine("Destination: " + this.Destination.ToString());
      sw.WriteLine("Payload Protocol: " +
                   this.PayloadType.ToString());
      sw.WriteLine("Payload Length: {0}", this.PayloadLength);
      sw.WriteLine("Payload: ");
      System.Text.Encoding e = new System.Text.UTF8Encoding();
      //this is the original line that Oscar had
#if true
      sw.WriteLine(e.GetString(_payload));
#else
      // the following is a hack for debugging purposes
      String pl = e.GetString(_payload);
      int i = pl.IndexOf('<');
      String printable = pl.Substring(i,160);
      sw.WriteLine( printable );
#endif
      sw.WriteLine();
      return sw.ToString();
    }
  }

}
