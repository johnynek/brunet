/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using Brunet;
using Brunet.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

#if NUNIT
using NUnit.Framework;
#endif

namespace NetworkPackets.Dns {
  /**
  <summary>A response type is all the other blocks of data in a Dns packet
  after the question, they can be RR, AR, and NS types based upon the RData
  payload.</summary>
  <remarks>
  <para>Represents a Response to a Dns Query.</para>
  <code>
  1  1  1  1  1  1
  0  1  2  3  4  5  6  7  8  9  0  1  2  3  4  5
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                                               |
  /                                               /
  /                      Name                     /
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                      Type                     |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                     Class                     |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                      Ttl                      |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                   RdLength                    |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--|
  /                     RData                     /
  /                                               /
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  </code>
  </remarks>
  */
  public class Response: DataPacket {
    /// <summary>the name rdata resolves as a string.</summary>
    public readonly String Name;
    /// <summary>the name rdata resolves as a memblock.</summary>
    public readonly MemBlock NameBlob;
    /// <summary>type of response</summary>
    public readonly DnsPacket.Types Type;
    /// <summary>type of network</summary>
    public readonly DnsPacket.Classes Class;
    /// <summary>Cache flush, not used for Dns only MDns</summary>
    public readonly bool CacheFlush;
    /// <summary>cache time to live for the response</summary>
    public readonly int Ttl;
    /// <summary>the length of the rdata</summary>
    public readonly short RdLength;
    /// <summary>string representation of the RData</summary>
    public readonly String RData;
    /// <summary>MemBlock representation of the RData</summary>
    public readonly MemBlock RDataBlob;

    /**
    <summary>Creates a response from the parameter fields with RData being
    a memory chunk.  This is for MDns which supports caching</summary>
    <param name="Name">The name resolved.</param>
    <param name="Type">The query type.</param>
    <param name="Class">The network type.</param>
    <param name="CacheFlush">Flush the dns cache in the client.</param>
    <param name="Ttl">How long to hold the result in the local dns cache.</param>
    <param name="RData">RData in String format.</param>
    */
    public Response(string name, DnsPacket.Types type, DnsPacket.Classes class_type,
                    bool cache_flush, int ttl, String rdata) {
      Name = name;
      Class = class_type;
      Ttl = ttl;
      Type = type;
      CacheFlush = cache_flush;
      RData = rdata;

      if(Type == DnsPacket.Types.A || Type == DnsPacket.Types.AAAA) {
        NameBlob = DnsPacket.HostnameStringToMemBlock(Name);
        RDataBlob = DnsPacket.IPStringToMemBlock(RData);
      }
      else if(Type == DnsPacket.Types.Ptr) {
        if(DnsPacket.StringIsIP(Name)) {
          NameBlob = DnsPacket.PtrStringToMemBlock(Name);
        }
        else {
          NameBlob = DnsPacket.HostnameStringToMemBlock(Name);
        }
        RDataBlob = DnsPacket.HostnameStringToMemBlock(RData);
      }
      else {
        throw new Exception("Invalid Query Type: " + Type + "!");
      }

      RdLength = (short) RDataBlob.Length;
      // 2 for Type + 2 for Class + 4 for Ttl + 2 for RdLength
      byte[] data = new byte[10];
      int idx = 0;
      data[idx++] = (byte) ((((int) Type) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) Type) & 0xFF);

      byte cf = 0x80;
      if(!CacheFlush) {
        cf = 0x00;
      }

      data[idx++] = (byte) (((((int) Class) >> 8) & 0x7F) | cf);
      data[idx++] = (byte) (((int) Class) & 0xFF);
      data[idx++] = (byte) ((Ttl >> 24) & 0xFF);
      data[idx++] = (byte) ((Ttl >> 16) & 0xFF);
      data[idx++] = (byte) ((Ttl >> 8) & 0xFF);
      data[idx++] = (byte) (Ttl & 0xFF);
      data[idx++] = (byte) ((RdLength >> 8) & 0xFF);
      data[idx] = (byte) (RdLength & 0xFF);

      _icpacket = new CopyList(NameBlob, MemBlock.Reference(data), RDataBlob);
    }

    /**
    <summary>Creates a response from the parameter fields with RData being
    a memory chunk.  This is for regular dns which has no notion of caching.
    </summary>
    <param name="Name">The name resolved.</param>
    <param name="Type">The query type.</param>
    <param name="Class">The network type.</param>
    <param name="CacheFlush">Flush the dns cache in the client.</param>
    <param name="Ttl">How long to hold the result in the local dns cache.</param>
    <param name="RData">RData in String format.</param>
    */
    public Response(String Name, DnsPacket.Types Type, DnsPacket.Classes Class,
      int Ttl, String RData): this(Name, Type, Class, false, Ttl, RData) {}

    /**
    <summary>Creates a response given the entire packet.</summary>
    <remarks>The entire packet must be given, because some name servers take
    advantage of pointers to reduce their size.</remarks>
    <param name="Data">The entire Dns packet.</param>
    <param name="Start">The starting position of the Response.</param>
    */
    public Response(MemBlock Data, int Start) {
      int idx = 0;
      NameBlob = DnsPacket.RetrieveBlob(Data, Start, out idx);

      int type = (Data[idx++] << 8) + Data[idx++];
      Type = (DnsPacket.Types) type;

      CacheFlush = ((Data[idx] & 0x80) == 0x80) ? true : false;
      int rclass = ((Data[idx++] << 8) & 0x7F) + Data[idx++];
      Class = (DnsPacket.Classes) rclass;

      Ttl = (Data[idx++] << 24);
      Ttl |= (Data[idx++] << 16);
      Ttl |= (Data[idx++] << 8);
      Ttl |= (Data[idx++]);

      RdLength = (short) ((Data[idx++] << 8) + Data[idx++]);
      RDataBlob = Data.Slice(idx, RdLength);

      if(Type == DnsPacket.Types.Ptr) {
        try {
          Name = DnsPacket.PtrMemBlockToString(NameBlob);
        }
        catch {
          Name = DnsPacket.HostnameMemBlockToString(NameBlob);
        }
        int End = 0;
        RDataBlob = DnsPacket.RetrieveBlob(Data, idx, out End);
        RData = DnsPacket.HostnameMemBlockToString(RDataBlob);
      }
      else if(Type == DnsPacket.Types.A) {
        Name = DnsPacket.HostnameMemBlockToString(NameBlob);
        RData = DnsPacket.IPMemBlockToString(RDataBlob);
      }
      _icpacket = _packet = Data.Slice(Start, idx + RdLength - Start);
    }
  }

#if NUNIT
  [TestFixture]
  public class DnsResponseTest {
    [Test]
    public void DnsPtrTest() {
      String Name = "64.233.169.104";
      DnsPacket.Types Type = DnsPacket.Types.Ptr;
      DnsPacket.Classes Class = DnsPacket.Classes.IN;
      int Ttl = 30;
      String RData = "yo-in-f104.google.com";
      Response rp = new Response(Name, Type, Class, Ttl, RData);

      MemBlock ptrm = MemBlock.Reference(new byte[] {0x03, 0x31, 0x30, 0x34,
        0x03, 0x31, 0x36, 0x39, 0x03, 0x32, 0x33, 0x33, 0x02, 0x36, 0x34, 0x07,
        0x69, 0x6e, 0x2d, 0x61, 0x64, 0x64, 0x72, 0x04, 0x61, 0x72, 0x70, 0x61,
        0x00, 0x00, 0x0c, 0x00, 0x01, 0x00, 0x00, 0x00, 0x1e, 0x00, 0x17, 0x0a,
        0x79, 0x6f, 0x2d, 0x69, 0x6e, 0x2d, 0x66, 0x31, 0x30, 0x34, 0x06, 0x67,
        0x6f, 0x6f, 0x67, 0x6c, 0x65, 0x03, 0x63, 0x6f, 0x6d, 0x00});
      Response rm = new Response(ptrm, 0);

      Assert.AreEqual(rp.Packet, ptrm, "Packet");
      Assert.AreEqual(rm.Name, Name, "Name");
      Assert.AreEqual(rm.Type, Type, "Type");
      Assert.AreEqual(rm.Class, Class, "Class");
      Assert.AreEqual(rm.Ttl, Ttl, "Ttl");
      Assert.AreEqual(rm.RData, RData, "RData");
    }

    [Test]
    public void DnsATest() {
      String Name = "www.cnn.com";
      DnsPacket.Types Type = DnsPacket.Types.A;
      DnsPacket.Classes Class = DnsPacket.Classes.IN;
      int Ttl = 2;
      String RData = "64.236.91.24";
      Response rp = new Response(Name, Type, Class, Ttl, RData);

      MemBlock am = MemBlock.Reference(new byte[] {0x03, 0x77, 0x77, 0x77,
        0x03, 0x63, 0x6e, 0x6e, 0x03, 0x63, 0x6f, 0x6d, 0x00, 0x00, 0x01, 0x00,
        0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x04, 0x40, 0xec, 0x5b, 0x18});
      Response rm = new Response(am, 0);

      Assert.AreEqual(rp.Packet, am, "Packet");
      Assert.AreEqual(rm.Name, Name, "Name");
      Assert.AreEqual(rm.Type, Type, "Type");
      Assert.AreEqual(rm.Class, Class, "Class");
      Assert.AreEqual(rm.Ttl, Ttl, "Ttl");
      Assert.AreEqual(rm.RData, RData, "RData");
    }

    [Test]
    public void MDnsATest() {
      String Name = "david-laptop.local";
      DnsPacket.Types Type = DnsPacket.Types.A;
      bool CacheFlush = true;
      DnsPacket.Classes Class = DnsPacket.Classes.IN;
      int Ttl = 120;
      String RData = "10.227.56.136";
      Response rp = new Response(Name, Type, Class, CacheFlush, Ttl, RData);

      MemBlock am = MemBlock.Reference(new byte[] {0x0c, 0x64, 0x61, 0x76,
        0x69, 0x64, 0x2d, 0x6c, 0x61, 0x70, 0x74, 0x6f, 0x70, 0x05, 0x6c, 0x6f,
        0x63, 0x61, 0x6c, 0x00, 0x00, 0x01, 0x80, 0x01, 0x00, 0x00, 0x00, 0x78,
        0x00, 0x04, 0x0a, 0xe3, 0x38, 0x88});

      Response rm = new Response(am, 0);

      Assert.AreEqual(rp.Packet, am, "Packet");
      Assert.AreEqual(rm.Name, Name, "Name");
      Assert.AreEqual(rm.Type, Type, "Type");
      Assert.AreEqual(rm.Class, Class, "Class");
      Assert.AreEqual(rm.CacheFlush, CacheFlush, "CacheFlush");
      Assert.AreEqual(rm.Ttl, Ttl, "Ttl");
      Assert.AreEqual(rm.RData, RData, "RData");
    }
  }
#endif
}
