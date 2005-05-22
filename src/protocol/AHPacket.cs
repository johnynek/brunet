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

    /** This is the largest positive short */
    public static readonly short MaxTtl = (short) 32767;


    protected byte[] _payload;

    /**
     * @param s Stream to read the AHPacket from
     * @param length the lenght of the packet
     */
    public AHPacket(Stream s, int length)
    {
      if( s.Length < length ) {
        throw new ArgumentException("Cannot read AHPacket from Stream");
      }
      byte[] buf = new byte[length];
      s.Read(buf, 0, length);
      int offset = 0; 
      //Now this is exactly the same code as below:
      int off = offset;
      if (buf[offset] != (byte)Packet.ProtType.AH ) {
        throw new System.
        ArgumentException("Packet is not an AHPacket: " + buf[offset].ToString());
      }
      offset += 1;
      _hops = NumberSerializer.ReadShort(buf, offset);
      offset += 2;
      _ttl = NumberSerializer.ReadShort(buf, offset);
      offset += 2;
      _source = AddressParser.Parse(buf, offset);
      offset += 20;
      _destination = AddressParser.Parse(buf, offset);
      offset += 20;
      _options = (ushort)NumberSerializer.ReadShort(buf, offset);
      offset += 2;
      int len = 0;
      _pt = NumberSerializer.ReadString(buf, offset, out len);
      offset += len;
      int headersize = offset - off;
      int payload_len = length - headersize;
      _payload = new byte[payload_len];
      Array.Copy(buf, offset, _payload, 0, payload_len);
    }
    public AHPacket(byte[] buf, int offset, int length)
    {
      int off = offset;
      if (buf[offset] != (byte)Packet.ProtType.AH ) {
        throw new System.
        ArgumentException("Packet is not an AHPacket");
      }
      offset += 1;
      _hops = NumberSerializer.ReadShort(buf, offset);
      offset += 2;
      _ttl = NumberSerializer.ReadShort(buf, offset);
      offset += 2;
      _source = AddressParser.Parse(buf, offset);
      offset += 20;
      _destination = AddressParser.Parse(buf, offset);
      offset += 20;
      _options = (ushort)NumberSerializer.ReadShort(buf, offset);
      offset += 2;
      int len = 0;
      _pt = NumberSerializer.ReadString(buf, offset, out len);
      offset += len;
      int headersize = offset - off;
      int payload_len = length - headersize;
      _payload = new byte[payload_len];
      Array.Copy(buf, offset, _payload, 0, payload_len);
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
                    string payload_prot,
                    byte[] payload, int off, int len) :
	                    this(hops, ttl, source, destination,
                                 AHOptions.AddClassDefault,
                                 payload_prot, payload, off,len)
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
		    ushort options,
                    string payload_prot,
                    byte[] payload, int off, int len)
    {
      _hops = hops;
      _ttl = ttl;
      _source = source;
      _destination = destination;
      if( options == AHOptions.AddClassDefault ) {
        _options = GetDefaultOption( _destination );
      }
      else {
        _options = options;
      }
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
		    ushort options,
                    string payload_prot,
                    byte[] payload) : this(hops, ttl, source, destination, options,
                                               payload_prot, payload, 0,
                                           payload.Length) {

    }
    /**
     * Same as similar constructor with offset and len, only
     * we assume the entired buffer is the payload
     */
    public AHPacket(short hops,
                    short ttl,
                    Address source,
                    Address destination,
                    string payload_prot,
                    byte[] payload) : this(hops, ttl, source, destination,
                                               AHOptions.AddClassDefault,
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
		    ushort options,
                    AHPacket p) {
      _hops = hops;
      _ttl = ttl;
      _source = source;
      _destination = destination;
      if( options == AHOptions.AddClassDefault ) {
        _options = GetDefaultOption( _destination );
      }
      else {
        _options = options;
      }
      _pt = p._pt;
      _payload = p._payload;
    }

    public override ProtType type { get { return Packet.ProtType.AH; } }
    public override int Length { get { return HeaderSize + _payload.Length; } }
    public override int PayloadLength { get { return _payload.Length; } }
    
    /**
     * The number of bytes in the header, including the type 0x02 
     * Since the payload type is a string, this is a variable.
     */
    public int HeaderSize {
      get {
        return 47 + NumberSerializer.GetByteCount(_pt);
      }
    }

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

    protected ushort _options;
    /**
     * This is a 16 bit field that describe routing and delivery
     * options
     */
    public ushort Options { get { return _options; } }
    
    protected string _pt;
    public string PayloadType { get { return _pt; } }

    /**
     * This is the prefered way to access the payload
     * Does not require any copy operation
     */
    public override MemoryStream PayloadStream {
      get {
        //Return a read-only MemoryStream of the Payload
        //return new MemoryStream(_payload, false);
	return GetPayloadStream(0);
      }
    }

    public override void CopyTo(byte[] dest, int off)
    {
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
      NumberSerializer.WriteShort((short)_options, dest, off);
      off += 2;
      off += NumberSerializer.WriteString(_pt, dest, off);
      Array.Copy(_payload, 0, dest, off, PayloadLength);
      off += PayloadLength;
    }

    /**
     * @param offset the offset into the payload to start the stream
     */
    virtual public MemoryStream GetPayloadStream(int offset) {
      return new MemoryStream(_payload, offset, _payload.Length - offset, false);
    }

    /**
     * Inner class to represent the options
     */
    public class AHOptions {
      //These are delivery options controlling when the packet is delivered locally
      public static readonly ushort AddClassDefault = 0;
      /**
       * Only the very last node to see the packet gets it delivered in this
       * case.  It may be when TTL==HOPs, or it my be the last in some route.
       */
      public static readonly ushort Last = 1;
      public static readonly ushort Path = 2;
      /**
       * This delivers the packet to the nearest nodes in the network to
       * the destination.  More than one node may get the packet, but certainly
       * the closest two should get the packet.
       */
      public static readonly ushort Nearest = 3;
      /**
       * Only a node with an address that exactly matches the destination should
       * get the packet
       */
      public static readonly ushort Exact = 4;
    }
    
    /**
     * Inner class which is just a namespace for the protocols
     * This is just for convenience.  You can ignore this if you like.
     * When using one of these protocols, it is smart to use this class
     * so the compiler can catch typos (which it can't do with strings).
     */
    public class Protocol 
    {
      public static readonly string Connection = "c";
      public static readonly string Forwarding = "f";
      public static readonly string Echo = "e";
      public static readonly string Tftp = "tftp";
      public static readonly string Chat = "chat";
      public static readonly string IP = "i";
      public static readonly string ReqRep = "r";
    }
    static protected ushort GetDefaultOption(Address dest) {
        //This is the default option:
        ushort my_opts = AHOptions.Last;
        if( dest is DirectionalAddress ) {
          my_opts = AHOptions.Last;
        }
        else if( dest is RwtaAddress ) {
          my_opts = AHOptions.Last;
        }
        else if( dest is UnstructuredAddress ) {
          my_opts = AHOptions.Path;
        }
        else if( dest is StructuredAddress ) {
          my_opts = AHOptions.Nearest;
        }
        return my_opts;
    }
    /**
     * The options flag has 16 bits.  Different parts
     * are used for different things and some can be combined.
     * Note, for a given 16 bits, more than one option may 
     * match (for instance, flags)
     *
     * @param opt the option you want to test for a match for
     * @return true if this packet has opt set.
     */
    public bool HasOption(ushort opt) {
      ushort my_opts = _options;
      if( my_opts == AHOptions.AddClassDefault ) {
        my_opts = GetDefaultOption( Destination );
      }
      //Console.WriteLine("Options: {0}, my_opt: {1}, opt: {2}", _options, my_opts, opt);
      return (opt == my_opts);
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
                           _options,
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
      sw.WriteLine("Payload Protocol: " + this.PayloadType);
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
