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
#if BRUNET_NUNIT
using NUnit.Framework;
using System.Security.Cryptography;
#endif

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


    protected MemBlock _buffer;
    /**
     * @param s Stream to read the AHPacket from
     * @param length the lenght of the packet
     */
    public AHPacket(Stream s, int length)
    {
      if( s.Length < length ) {
        throw new ArgumentException("Cannot read AHPacket from Stream");
      }
      byte[] buffer = new byte[length];
      s.Read(buffer, 0, length);
      int offset = 0; 
      //Now this is exactly the same code as below:
      if (buffer[offset] != (byte)Packet.ProtType.AH ) {
        throw new System.
        ArgumentException("Packet is not an AHPacket: " + buffer[offset].ToString());
      }
      offset += 1;
      _hops = NumberSerializer.ReadShort(buffer, offset);
      offset += 2;
      _ttl = NumberSerializer.ReadShort(buffer, offset);
      offset += 2;
      //Skip the addresses
      offset += 40;
      _options = (ushort)NumberSerializer.ReadShort(buffer, offset);
      _buffer = MemBlock.Reference(buffer, 0, length);
    }
    public AHPacket(byte[] buf, int off, int length) : this(MemBlock.Copy(buf, off, length))
    { }

    public AHPacket(MemBlock buf)
    {
      //Now this is exactly the same code as below:
      if (buf[0] != (byte)Packet.ProtType.AH ) {
        throw new System.
        ArgumentException("Packet is not an AHPacket: " + buf[0].ToString());
      }
      _buffer = buf;
      int offset = 0; 
      offset += 1;
      _hops = NumberSerializer.ReadShort(_buffer, offset);
      offset += 2;
      _ttl = NumberSerializer.ReadShort(_buffer, offset);
      offset += 2;
      //Skip the addresses
      offset += 40;
      _options = (ushort)NumberSerializer.ReadShort(_buffer, offset);
    }

    public AHPacket(byte[] buf, int length):this(buf, 0, length)
    {
    }

    public AHPacket(byte[] buf):this(buf, 0, buf.Length)
    {
    }

    /*
     * This is a constructor that allows a Packets to share
     * buffers.  The only field that is allowed to be incorrect
     * is the hops field (the only field that changes as a packet
     * moves).  All the rest must be the same as what was deserialized
     * from the buffer (otherwise you'll have problems).
     */
    private AHPacket() { }

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
     * @param poff Offset to the zeroth byte of payload
     * @param len Length of the payload
     */
    public AHPacket(short hops,
                    short ttl,
                    Address source,
                    Address destination,
		    ushort options,
                    string payload_prot,
                    byte[] payload, int poff, int len)
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
      _type_length = NumberSerializer.GetByteCount(_pt);
      int total_size = 47 + _type_length + len;
      byte[] buffer = new byte[ total_size ]; 
      int off = 0;
      buffer[off] = (byte)Packet.ProtType.AH;
      off += 1;
      NumberSerializer.WriteShort(_hops, buffer, off);
      off += 2;
      NumberSerializer.WriteShort(_ttl, buffer, off);
      off += 2;
      _source.CopyTo(buffer, off);
      off += 20;
      _destination.CopyTo(buffer, off);
      off += 20;
      NumberSerializer.WriteShort((short)_options, buffer, off);
      off += 2;
      off += NumberSerializer.WriteString(_pt, buffer, off);
      Array.Copy(payload, poff, buffer, off, len);
      _buffer = MemBlock.Reference(buffer, 0, total_size);
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
    public override ProtType type { get { return Packet.ProtType.AH; } }
    public override int Length { get { return _buffer.Length; } }
    public override int PayloadLength {
      get {
        return _buffer.Length - this.HeaderSize;
      }
    }
    
    /**
     * The number of bytes in the header, including the type 0x02 
     * Since the payload type is a string, this is a variable.
     */
    public int HeaderSize {
      get {
        if( _type_length < 0 ) {
          //We have not initalized it yet
          /*
           * We take the slice starting with position 47, and we search for
           * the first null.  Then, since the type length includes the null,
           * we add 1 to it.
           */
          _type_length = _buffer.Slice(47).IndexOf(0);
          _pt = _buffer.Slice(47, _type_length).GetString(System.Text.Encoding.UTF8);
          _type_length = _type_length + 1;
        }
        return 47 + _type_length;
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
    public Address Source {
      get {
        if( _source == null ) {
          _source = AddressParser.Parse( _buffer.Slice(5, Address.MemSize) );
        }
        return _source;
      }
    }

    protected Address _destination;
    /**
     * The destination of this packet
     */
    public Address Destination {
      get {
        if( _destination == null ) {
          _destination = AddressParser.Parse( _buffer.Slice(25, Address.MemSize) );
        }
        return _destination;
      }
    }

    protected ushort _options;
    /**
     * This is a 16 bit field that describe routing and delivery
     * options
     */
    public ushort Options { get { return _options; } }
    
    protected int _type_length = -1;
    protected string _pt;
    public string PayloadType {
      get {
        if( _pt == null ) {
          _type_length = _buffer.Slice(47).IndexOf(0);
          _pt = _buffer.Slice(47, _type_length).GetString(System.Text.Encoding.UTF8);
          _type_length = _type_length + 1;
        }
        return _pt;
      }
   }

    /**
     * This is the prefered way to access the payload
     * Does not require any copy operation
     */
    public override MemoryStream PayloadStream {
      get {
        //Return a read-only MemoryStream of the Payload
        //return new MemoryStream(_payload, false);
	return GetPayloadStream( 0 );
      }
    }

    public override MemBlock Payload {
      get {
        //Get everything after the header
        return _buffer.Slice( HeaderSize );
      }
    }

    public override int CopyTo(byte[] dest, int off)
    {
      _buffer.CopyTo(dest, off);
      //Hops is the only field that can be out of sync:
      NumberSerializer.WriteShort(_hops, dest, off + 1);
      return _buffer.Length;
    }

    /**
     * @param offset the offset into the payload to start the stream
     */
    virtual public MemoryStream GetPayloadStream(int offset) {
      int buf_off = this.HeaderSize + offset;
      int len = this.PayloadLength - offset;
      MemBlock payload_offset = _buffer.Slice( buf_off, len);
      return payload_offset.ToMemoryStream();
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
       * This mode assumes the Greedy routing mode for structured addresses
       */
      public static readonly ushort Last = 1;
      /*
       * This mode assumes the Annealing routing mode for structured addresses
       */
      public static readonly ushort Path = 2;
      /**
       * This uses the greedy routing algorithm.  The packet always
       * gets closer to the destination until it can get no closer,
       * as is delivered to that node.
       */
      public static readonly ushort Greedy = 3;
      /**
       * This mode allows the packet to "go uphill" for one step.
       * However, every local minimum will get the packet.  So,
       * more than one node may receive the packet.
       * This mode is slightly fault tolerant.
       */
      public static readonly ushort Annealing = 4;
      /**
       * Only a node with an address that exactly matches the destination should
       * get the packet
       */
      public static readonly ushort Exact = 5;
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
      //for tunnel edges
      public static readonly string Tunneling = "t";
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
        else if( dest is StructuredAddress ) {
          my_opts = AHOptions.Annealing;
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
      //Console.Error.WriteLine("Options: {0}, my_opt: {1}, opt: {2}", _options, my_opts, opt);
      return (opt == my_opts);
    }

    /**
     * @returns a new AHPacket with the hops field incremented
     */
    public AHPacket IncrementHops()
    {
      AHPacket p = new AHPacket();
      p._buffer = _buffer;
      
      p._hops = (short)(_hops + 1);
      p._ttl = _ttl;
      p._source = _source;
      p._destination = _destination;
      p._options = _options;
      p._pt = _pt;
      return p;
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
      sw.WriteLine(e.GetString( this.PayloadStream.ToArray() ));
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

#if BRUNET_NUNIT
  /**
   * This is an NUnit test class
   */
  [TestFixture]
  public class AHPacketTester {
  
    public void AssertEqualAHPackets(AHPacket p1, AHPacket p2) {
      Assert.AreEqual(p1.Hops, p2.Hops, "Hops");
      Assert.AreEqual(p1.Ttl, p2.Ttl, "Ttl");
      Assert.AreEqual(p1.Source, p2.Source, "Source");
      Assert.AreEqual(p1.Destination, p2.Destination, "Dest");
      Assert.AreEqual(p1.Options, p2.Options, "Options");
      Assert.AreEqual(p1.PayloadType, p2.PayloadType, "Payload Type");
      byte[] payload1 = p1.PayloadStream.ToArray();
      byte[] payload2 = p2.PayloadStream.ToArray();
      Assert.AreEqual(payload1.Length, payload2.Length, "Payload length");
      for(int i = 0; i < payload1.Length; i++) {
        Assert.AreEqual(payload1[i], payload2[i], "Payload[" + i.ToString() + "]");
      }
      Assert.AreEqual(p1.Length, p2.Length, "Total Length");
    }
    public AHPacket RoundTrip(AHPacket p) {
        byte[] binary_packet = new byte[ p.Length ];
        p.CopyTo( binary_packet, 0);
        return new AHPacket(MemBlock.Reference(binary_packet));
    }
    [Test]
    public void Test() {
      /*
       * Make some random packets and see if they round trip properly.
       */
      RandomNumberGenerator rng = new RNGCryptoServiceProvider();
      Random simple_rng = new Random();
      for(int i = 0; i < 100; i++) {
        AHAddress source = new AHAddress(rng);
        AHAddress dest = new AHAddress(rng);
        short ttl = (short)simple_rng.Next(Int16.MaxValue);
        short hops = (short)simple_rng.Next(Int16.MaxValue);
        ushort options = (ushort)simple_rng.Next(UInt16.MaxValue);
        byte[] bin_prot = new byte[ simple_rng.Next(4) ];
        simple_rng.NextBytes(bin_prot);
        string random_prot = Base32.Encode( bin_prot );
        byte[] payload = new byte[ simple_rng.Next(1024) ];
        AHPacket p = new AHPacket(hops, ttl, source, dest,
                                  options, random_prot, payload);
        AssertEqualAHPackets(p, RoundTrip(p) );
        AHPacket phops = new AHPacket((short)(hops + 1), ttl, source, dest,
                                  options, random_prot, payload);
        AHPacket inc_hops = p.IncrementHops();
        AssertEqualAHPackets(phops, inc_hops );
        //Round trip them all:
        AssertEqualAHPackets(p, RoundTrip(p));
        AssertEqualAHPackets(inc_hops, RoundTrip(inc_hops));
        AssertEqualAHPackets(phops, RoundTrip(phops));
      }
    }
  }
#endif

}
