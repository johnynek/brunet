/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

#if NUNIT
using NUnit.Framework;
#endif

namespace NetworkPackets.DNS {
  /**
  <summary>A response type is all the other blocks of data in a DNS packet
  after the question, they can be RR, AR, and NS types based upon the RDATA
  payload.</summary>
  <remarks>
  <para>Represents a Response to a DNS Query.</para>
  <code>
  1  1  1  1  1  1
  0  1  2  3  4  5  6  7  8  9  0  1  2  3  4  5
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                                               |
  /                                               /
  /                      NAME                     /
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                      TYPE                     |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                     CLASS                     |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                      TTL                      |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                   RDLENGTH                    |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--|
  /                     RDATA                     /
  /                                               /
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  </code>
  </remarks>
  */
  public class Response: DataPacket {
    /// <summary>the name rdata resolves as a string.</summary>
    public readonly String NAME;
    /// <summary>the name rdata resolves as a memblock.</summary>
    public readonly MemBlock NAME_BLOB;
    /// <summary>type of response</summary>
    public readonly DNSPacket.TYPES TYPE;
    /// <summary>type of network</summary>
    public readonly DNSPacket.CLASSES CLASS;
    /// <summary>cache time to live for the response</summary>
    public readonly int TTL;
    /// <summary>the length of the rdata</summary>
    public readonly short RDLENGTH;
    /// <summary>string representation of the RDATA</summary>
    public readonly String RDATA;
    /// <summary>MemBlock representation of the RDATA</summary>
    public readonly MemBlock RDATA_BLOB;

    /**
    <summary>Creates a response from the parameter fields with RDATA being
    a memory chunk.</summary>
    <param name="NAME">The name resolved.</param>
    <param name="TYPE">The query type.</param>
    <param name="CLASS">The network type.</param>
    <param name="TTL">How long to hold the result in the local dns cache.</param>
    <param name="RDATA">RDATA in String format.</param>
    */
    public Response(String NAME, DNSPacket.TYPES TYPE,
                       DNSPacket.CLASSES CLASS, int TTL, String RDATA) {
      this.NAME = NAME;
      this.CLASS = CLASS;
      this.TTL = TTL;

      this.TYPE = TYPE;
      this.CLASS = CLASS;
      this.RDATA = RDATA;

      if(TYPE == DNSPacket.TYPES.A) {
        NAME_BLOB = DNSPacket.HostnameStringToMemBlock(NAME);
        RDATA_BLOB = DNSPacket.IPStringToMemBlock(RDATA);
      }
      else if(TYPE == DNSPacket.TYPES.PTR) {
        NAME_BLOB = DNSPacket.PtrStringToMemBlock(NAME);
        RDATA_BLOB = DNSPacket.HostnameStringToMemBlock(RDATA);
      }
      else {
        throw new Exception("Invalid Query TYPE: " + TYPE + "!");
      }

      RDLENGTH = (short) RDATA_BLOB.Length;

      // 2 for TYPE + 2 for CLASS + 4 for TTL + 2 for RDLENGTH
      byte[] data = new byte[10];
      int idx = 0;
      data[idx++] = (byte) ((((int) TYPE) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) TYPE) & 0xFF);
      data[idx++] = (byte) ((((int) CLASS) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) CLASS) & 0xFF);
      data[idx++] = (byte) ((TTL >> 24) & 0xFF);
      data[idx++] = (byte) ((TTL >> 16) & 0xFF);
      data[idx++] = (byte) ((TTL >> 8) & 0xFF);
      data[idx++] = (byte) (TTL & 0xFF);
      data[idx++] = (byte) ((RDLENGTH >> 8) & 0xFF);
      data[idx] = (byte) (RDLENGTH & 0xFF);

      _icpacket = new CopyList(NAME_BLOB, MemBlock.Reference(data), RDATA_BLOB);
    }

    /**
    <summary>Creates a response from the parameter fields with RDATA being
    a memory chunk.</summary>
    <param name="NAME_BLOB">The name to resolve.</param>
    <param name="TYPE">The query type.</param>
    <param name="CLASS">The network type.</param>
    <param name="TTL">How long to hold the result in the local dns cache.</param>
    <param name="RDATA_BLOB">RDATA in byte format.</param>
     */
    public Response(MemBlock NAME_BLOB, DNSPacket.TYPES TYPE,
                       DNSPacket.CLASSES CLASS, int TTL, MemBlock RDATA_BLOB) {
      this.NAME_BLOB = NAME_BLOB;
      this.CLASS = CLASS;
      this.TTL = TTL;

      this.TYPE = TYPE;
      this.CLASS = CLASS;
      this.RDATA_BLOB = RDATA_BLOB;

      RDLENGTH = (short) RDATA_BLOB.Length;

      // 2 for TYPE + 2 for CLASS + 4 for TTL + 2 for RDLENGTH
      byte[] data = new byte[10];
      int idx = 0;
      data[idx++] = (byte) ((((int) TYPE) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) TYPE) & 0xFF);
      data[idx++] = (byte) ((((int) CLASS) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) CLASS) & 0xFF);
      data[idx++] = (byte) ((TTL >> 24) & 0xFF);
      data[idx++] = (byte) ((TTL >> 16) & 0xFF);
      data[idx++] = (byte) ((TTL >> 8) & 0xFF);
      data[idx++] = (byte) (TTL & 0xFF);
      data[idx++] = (byte) ((RDLENGTH >> 8) & 0xFF);
      data[idx] = (byte) (RDLENGTH & 0xFF);

      _icpacket = new CopyList(NAME_BLOB, MemBlock.Reference(data), RDATA_BLOB);
    }

    /**
    <summary>Creates a response from the parameter fields with RDATA being
    a memory chunk.</summary>
    <param name="NAME">The name to resolve.</param>
    <param name="TYPE">The query type.</param>
    <param name="CLASS">The network type.</param>
    <param name="TTL">How long to hold the result in the local dns cache.</param>
    <param name="RDATA_BLOB">RDATA in byte format.</param>
     */
    public Response(String NAME, DNSPacket.TYPES TYPE,
                       DNSPacket.CLASSES CLASS, int TTL, MemBlock RDATA_BLOB) {
      this.NAME = NAME;
      this.CLASS = CLASS;
      this.TTL = TTL;

      this.TYPE = TYPE;
      this.CLASS = CLASS;
      this.RDATA_BLOB = RDATA_BLOB;

      if(TYPE == DNSPacket.TYPES.A) {
        NAME_BLOB = DNSPacket.HostnameStringToMemBlock(NAME);
      }
      else if(TYPE == DNSPacket.TYPES.PTR) {
        NAME_BLOB = DNSPacket.PtrStringToMemBlock(NAME);
      }
      else {
        throw new Exception("Invalid QTYPE: " + TYPE + "!");
      }

      RDLENGTH = (short) RDATA_BLOB.Length;

      // 2 for TYPE + 2 for CLASS + 4 for TTL + 2 for RDLENGTH
      byte[] data = new byte[10];
      int idx = 0;
      data[idx++] = (byte) ((((int) TYPE) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) TYPE) & 0xFF);
      data[idx++] = (byte) ((((int) CLASS) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) CLASS) & 0xFF);
      data[idx++] = (byte) ((TTL >> 24) & 0xFF);
      data[idx++] = (byte) ((TTL >> 16) & 0xFF);
      data[idx++] = (byte) ((TTL >> 8) & 0xFF);
      data[idx++] = (byte) (TTL & 0xFF);
      data[idx++] = (byte) ((RDLENGTH >> 8) & 0xFF);
      data[idx] = (byte) (RDLENGTH & 0xFF);

      _icpacket = new CopyList(NAME_BLOB, MemBlock.Reference(data), RDATA_BLOB);
    }

    /**
    <summary>Creates a response given the entire packet.</summary>
    <remarks>The entire packet must be given, because some name servers take
    advantage of pointers to reduce their size.</remarks>
    <param name="Data">The entire DNS packet.</param>
    <param name="Start">The starting position of the Response.</param>
    */
    public Response(MemBlock Data, int Start) {
      // Is this a Pointer?
      int idx = Start;
      int offset = Start;
      if(0xC0 == (Data[Start] | 0xC0)) {
        offset = (Data[idx++] & 0x3F << 8);
        offset |= Data[idx++];
      }

      int pos = offset;
      while(Data[pos] != 0) {
        pos++;
      }

      NAME_BLOB = Data.Slice(offset, ++pos - offset);
      if(offset == Start) {
        idx += pos;
      }

      int type = (Data[idx++] << 8) + Data[idx++];
      TYPE = (DNSPacket.TYPES) type;

      int rclass = (Data[idx++] << 8) + Data[idx++];
      CLASS = (DNSPacket.CLASSES) rclass;

      TTL = (Data[idx++] << 24);
      TTL |= (Data[idx++] << 16);
      TTL |= (Data[idx++] << 8);
      TTL |= (Data[idx++]);

      RDLENGTH = (short) ((Data[idx++] << 8) + Data[idx++]);
      RDATA_BLOB = Data.Slice(idx, RDLENGTH);

      if(TYPE == DNSPacket.TYPES.PTR) {
        NAME = DNSPacket.PtrMemBlockToString(NAME_BLOB);
        RDATA = DNSPacket.HostnameMemBlockToString(RDATA_BLOB);
      }
      else if(TYPE == DNSPacket.TYPES.A) {
        NAME = DNSPacket.HostnameMemBlockToString(NAME_BLOB);
        RDATA = DNSPacket.IPMemBlockToString(RDATA_BLOB);
      }
      _icpacket = _packet = Data.Slice(Start, idx + RDLENGTH - Start);
    }
  }

#if NUNIT
  [TestFixture]
  public class DNSResponseTest {
    [Test]
    public void DNSPtrTest() {
      String NAME = "64.233.169.104";
      DNSPacket.TYPES TYPE = DNSPacket.TYPES.PTR;
      DNSPacket.CLASSES CLASS = DNSPacket.CLASSES.IN;
      int TTL = 30;
      String RDATA = "yo-in-f104.google.com";
      Response rp = new Response(NAME, TYPE, CLASS, TTL, RDATA);

      MemBlock ptrm = MemBlock.Reference(new byte[] {0x03, 0x31, 0x30, 0x34,
        0x03, 0x31, 0x36, 0x39, 0x03, 0x32, 0x33, 0x33, 0x02, 0x36, 0x34, 0x07,
        0x69, 0x6e, 0x2d, 0x61, 0x64, 0x64, 0x72, 0x04, 0x61, 0x72, 0x70, 0x61,
        0x00, 0x00, 0x0c, 0x00, 0x01, 0x00, 0x00, 0x00, 0x1e, 0x00, 0x17, 0x0a,
        0x79, 0x6f, 0x2d, 0x69, 0x6e, 0x2d, 0x66, 0x31, 0x30, 0x34, 0x06, 0x67,
        0x6f, 0x6f, 0x67, 0x6c, 0x65, 0x03, 0x63, 0x6f, 0x6d, 0x00});
      Response rm = new Response(ptrm, 0);

      Assert.AreEqual(rp.Packet, ptrm, "Packet");
      Assert.AreEqual(rm.NAME, NAME, "NAME");
      Assert.AreEqual(rm.TYPE, TYPE, "TYPE");
      Assert.AreEqual(rm.CLASS, CLASS, "CLASS");
      Assert.AreEqual(rm.TTL, TTL, "TTL");
      Assert.AreEqual(rm.RDATA, RDATA, "RDATA");
    }

    [Test]
    public void DNSATest() {
      String NAME = "www.cnn.com";
      DNSPacket.TYPES TYPE = DNSPacket.TYPES.A;
      DNSPacket.CLASSES CLASS = DNSPacket.CLASSES.IN;
      int TTL = 2;
      String RDATA = "64.236.91.24";
      Response rp = new Response(NAME, TYPE, CLASS, TTL, RDATA);

      MemBlock am = MemBlock.Reference(new byte[] {0x03, 0x77, 0x77, 0x77,
        0x03, 0x63, 0x6e, 0x6e, 0x03, 0x63, 0x6f, 0x6d, 0x00, 0x00, 0x01, 0x00,
        0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x04, 0x40, 0xec, 0x5b, 0x18});
      Response rm = new Response(am, 0);

      Assert.AreEqual(rp.Packet, am, "Packet");
      Assert.AreEqual(rm.NAME, NAME, "NAME");
      Assert.AreEqual(rm.TYPE, TYPE, "TYPE");
      Assert.AreEqual(rm.CLASS, CLASS, "CLASS");
      Assert.AreEqual(rm.TTL, TTL, "TTL");
      Assert.AreEqual(rm.RDATA, RDATA, "RDATA");
    }

    [Test]
    public void DNSBlobTest0() {
      String NAME = "www.cnn.com";
      DNSPacket.TYPES TYPE = DNSPacket.TYPES.A;
      DNSPacket.CLASSES CLASS = DNSPacket.CLASSES.IN;
      int TTL = 2;
      MemBlock RDATA_BLOB = MemBlock.Reference(new byte[] {0x40, 0xec, 0x5b, 0x18});
      Response rp = new Response(NAME, TYPE, CLASS, TTL, RDATA_BLOB);

      MemBlock am = MemBlock.Reference(new byte[] {0x03, 0x77, 0x77, 0x77,
        0x03, 0x63, 0x6e, 0x6e, 0x03, 0x63, 0x6f, 0x6d, 0x00, 0x00, 0x01, 0x00,
        0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x04, 0x40, 0xec, 0x5b, 0x18});
      Response rm = new Response(am, 0);

      Assert.AreEqual(rp.Packet, am, "Packet");
      Assert.AreEqual(rm.NAME, NAME, "NAME");
      Assert.AreEqual(rm.TYPE, TYPE, "TYPE");
      Assert.AreEqual(rm.CLASS, CLASS, "CLASS");
      Assert.AreEqual(rm.TTL, TTL, "TTL");
      Assert.AreEqual(rm.RDATA_BLOB, RDATA_BLOB, "RDATA");
    }

    [Test]
    public void DNSBlobTest1() {
      MemBlock NAME_BLOB = MemBlock.Reference(new byte[] {0x03, 0x31, 0x30,
        0x34, 0x03, 0x31, 0x36, 0x39, 0x03, 0x32, 0x33, 0x33, 0x02, 0x36, 0x34,
        0x07, 0x69, 0x6e, 0x2d, 0x61, 0x64, 0x64, 0x72, 0x04, 0x61, 0x72, 0x70,
        0x61, 0x00});
      DNSPacket.TYPES TYPE = DNSPacket.TYPES.PTR;
      DNSPacket.CLASSES CLASS = DNSPacket.CLASSES.IN;
      int TTL = 30;
      MemBlock RDATA_BLOB = MemBlock.Reference(new byte[] {0x0a, 0x79, 0x6f,
        0x2d, 0x69, 0x6e, 0x2d, 0x66, 0x31, 0x30, 0x34, 0x06, 0x67, 0x6f, 0x6f,
        0x67, 0x6c, 0x65, 0x03, 0x63, 0x6f, 0x6d, 0x00});
      Response rp = new Response(NAME_BLOB, TYPE, CLASS, TTL, RDATA_BLOB);

      MemBlock ptrm = MemBlock.Reference(new byte[] {0x03, 0x31, 0x30, 0x34,
        0x03, 0x31, 0x36, 0x39, 0x03, 0x32, 0x33, 0x33, 0x02, 0x36, 0x34, 0x07,
        0x69, 0x6e, 0x2d, 0x61, 0x64, 0x64, 0x72, 0x04, 0x61, 0x72, 0x70, 0x61,
        0x00, 0x00, 0x0c, 0x00, 0x01, 0x00, 0x00, 0x00, 0x1e, 0x00, 0x17, 0x0a,
        0x79, 0x6f, 0x2d, 0x69, 0x6e, 0x2d, 0x66, 0x31, 0x30, 0x34, 0x06, 0x67,
        0x6f, 0x6f, 0x67, 0x6c, 0x65, 0x03, 0x63, 0x6f, 0x6d, 0x00});
      Response rm = new Response(ptrm, 0);

      Assert.AreEqual(rp.Packet, ptrm, "Packet");
      Assert.AreEqual(rm.NAME_BLOB, NAME_BLOB, "NAME");
      Assert.AreEqual(rm.TYPE, TYPE, "TYPE");
      Assert.AreEqual(rm.CLASS, CLASS, "CLASS");
      Assert.AreEqual(rm.TTL, TTL, "TTL");
      Assert.AreEqual(rm.RDATA_BLOB, RDATA_BLOB, "RDATA");
    }
  }
#endif
}