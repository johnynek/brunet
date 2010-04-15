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

/**
\namespace NetworkPackets::nsD
\brief Defines Dns Packets.
*/
namespace NetworkPackets.Dns {
  /**
  <summary>Supports the parsing of DNS Packets.</summary>
  <remarks><para>This is a very naive implementation and lacks support for
  services other than address lookup (TYPE=A) and pointer look up (TYPE=PTR).
  Because I haven't found a service that used inverse querying for name look
  up, only pointer look up is implemented.</para>

  <para>Exceptions will not occur when parsing byte arrays, only when
  attempting to create from scratch new packets with unsupported TYPES.</para>

  <code>
  A DNS packet ...
  1  1  1  1  1  1
  0  1  2  3  4  5  6  7  8  9  0  1  2  3  4  5
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                      ID                       |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |QR|   Opcode  |AA|TC|RD|RA|   Z    |   RCODE   |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    QDCOUNT                    |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    ANCOUNT                    |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    NSCOUNT                    |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    ARCOUNT                    |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                                               |
  /                    QUERYS                     /
  /                                               /
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                                               |
  /                   RESPONSES                   /
  /                                               /
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  </code>
  <list type="table">
    <listheader>
      <term>Field</term>
      <description>Description</description>
    </listheader>
    <item>
      <term>ID</term>
      <description>identification - client generated, do not change</description>
    </item>
    <item>
      <term>QR</term>
      <description>query / reply, client sends 0, server replies 1</description>
    </item>
    <item>
      <term>Opcode</term>
      <description>0 for query, 1 inverse query</description>
    </item>
    <item>
      <term>AA</term>
      <description>Authoritative answer - True when there is a mapping</description>
    </item>
    <item>
      <term>TC</term>
      <description>Truncation - ignored - 0</description>
    </item>
    <item>
      <term>RD</term>
      <description>Recursion desired</description>
    </item>
    <item>
      <term>RA</term>
      <description>Recursion availabled</description>
    </item>
    <item>
      <term>Z</term><description>Reserved - must be 0</description>
    </item>
    <item>
      <term>RCODE</term>
      <description>ignored, stands for error code - 0</description>
    </item>
    <item>
      <term>QDCOUNT</term>
      <description>questions - should be 1</description>
    </item>
    <item>
      <term>ANCOUNT</term>
      <description>answers - should be 0 until we answer!</description>
    </item>
    <item>
      <term>NSCOUNT</term>
      <description>name server records - somewhat supported, but I can't
        find a reason why it needs to be so I've left in ZoneAuthority code
        in case it is ever needed!</description>
    </item>
    <item>
      <term>ARCOUNT</term>
      <description>additional records - unsupported</description>
    </item>
  </list>
  </remarks>
  */
  public class DNSPacket: DataPacket {
    /// <summary>the standard ptr suffix</summary>
    public const String INADDR_ARPA = ".in-addr.arpa";
  /// <summary>DNS Query / Response / Record types</summary>
    public enum TYPES {
    /// <summary>Host address(name)</summary>
      A = 1,
    /// <summary>zone authority</summary>
      SOA = 6,
    /// <summary>domain name pointer (ip address)</summary>
      PTR = 12
    };
  /// <summary>supported network classes</summary>
    public enum CLASSES {
    /// <summary>The Internet</summary>
      IN = 1
    };
    /// <summary>Unique packet ID</summary>
    public readonly short ID;
    /// <summary>Query if true, otherwise a response</summary>
    public readonly bool QUERY;
    /// <summary>0 = Query, 1 = Inverse Query, 2 = Status</summary>
    public readonly byte OPCODE;
    /// <summary>Authoritative answer (if you have a resolution, set)</summary>
    public readonly bool AA;
    public readonly bool RD;
    public readonly bool RA;
    /// <summary>list of Questions</summary>
    public readonly Question[] Questions;
    /// <summary>list of Answers</summary>
    public readonly Response[] Answers;
    public readonly Response[] Authority;
    public readonly Response[] Additional;

    /**
    <summary>Creates a DNS packet from the parameters provided.</summary>
    <param name="ID">A unique ID for the packet, responses should be the same
    as the query</param>
    <param name="QUERY">True if a query, false if a response</param>
    <param name="OPCODE">0 = Query, which is the only supported parsing method
    </param>
    <param name="AA">Authoritative Answer, true if there is a resolution for
    the lookup.</param>
    <param name="Questions">A list of Questions.</param>
    <param name="Answers">A list of Answers.</param>
    */
    public DNSPacket(short ID, bool QUERY, byte OPCODE, bool AA, bool RA,
                     bool RD, Question[] Questions, Response[] Answers,
                     Response[] Authority, Response[] Additional) {
      byte[] header = new byte[12];

      this.ID = ID;
      header[0] = (byte) ((ID >> 8) & 0xFF);
      header[1] = (byte) (ID & 0xFF);

      this.QUERY = QUERY;
      if(!QUERY) {
        header[2] |= 0x80;
      }

      this.OPCODE = OPCODE;
      header[2] |= (byte) (OPCODE << 3);

      this.AA = AA;
      if(AA) {
        header[2] |= 0x4;
      }
      this.RD = RD;
      if(RD) {
        header[2] |= 0x1;
      }
      this.RA = RA;
      if(RA) {
        header[3] |= 0x80;
      }

      if(Questions != null) {
        this.Questions = Questions;
        header[4] = (byte) ((Questions.Length >> 8) & 0xFF);
        header[5] = (byte) (Questions.Length  & 0xFF);
      }
      else {
        this.Questions = new Question[0];
        header[4] = 0;
        header[5] = 0;
      }

      if(Answers != null) {
        this.Answers = Answers;
        header[6] = (byte) ((Answers.Length >> 8) & 0xFF);
        header[7] = (byte) (Answers.Length  & 0xFF);
      }
      else {
        this.Answers = new Response[0];
        header[6] = 0;
        header[7] = 0;
      }

      if(Authority != null) {
        this.Authority = Authority;
        header[8] = (byte) ((Authority.Length >> 8) & 0xFF);
        header[9] = (byte) (Authority.Length  & 0xFF);
      }
      else {
        this.Authority = new Response[0];
        header[8] = 0;
        header[9] = 0;
      }

      if(Additional != null) {
        this.Additional = Additional;
        header[10] = (byte) ((Additional.Length >> 8) & 0xFF);
        header[11] = (byte) (Additional.Length  & 0xFF);
      }
      else {
        this.Additional = new Response[0];
        header[10] = 0;
        header[11] = 0;
      }

      _icpacket = MemBlock.Reference(header);

      for(int i = 0; i < this.Questions.Length; i++) {
        _icpacket = new CopyList(_icpacket, Questions[i].ICPacket);
      }
      for(int i = 0; i < this.Answers.Length; i++) {
        _icpacket = new CopyList(_icpacket, Answers[i].ICPacket);
      }
      for(int i = 0; i < this.Authority.Length; i++) {
        _icpacket = new CopyList(_icpacket, Authority[i].ICPacket);
      }
      for(int i = 0; i < this.Additional.Length; i++) {
        _icpacket = new CopyList(_icpacket, Additional[i].ICPacket);
      }
    }

    /**
    <summary>Parses a MemBlock as a DNSPacket.</summary>
    <param name="Packet">The payload containing hte DNS Packet in byte format.
    </param>
    */
    public DNSPacket(MemBlock Packet) {
      ID = (short) ((Packet[0] << 8) + Packet[1]);
      QUERY = (bool) (((Packet[2] & 0x80) >> 7) == 0);
      OPCODE = (byte) ((Packet[2] & 0x78) >> 3);

      if((Packet[2] & 0x4) == 0x4) {
        AA = true;
      }
      else {
        AA = false;
      }

      if((Packet[2] & 0x1) == 0x1) {
        RD = true;
      }
      else {
        RD = false;
      }

      if((Packet[3] & 0x80) == 0x80) {
        RA = true;
      }
      else {
        RA = false;
      }

      int qdcount = (Packet[4] << 8) + Packet[5];
      int ancount = (Packet[6] << 8) + Packet[7];
      int nscount = (Packet[8] << 8) + Packet[9];
      int arcount = (Packet[10] << 8) + Packet[11];
      int idx = 12;

      Questions = new Question[qdcount];
      for(int i = 0; i < qdcount; i++) {
        Questions[i] = new Question(Packet, idx);
        idx += Questions[i].Packet.Length;
      }

      Answers = new Response[ancount];
      for(int i = 0; i < ancount; i++) {
        Answers[i] = new Response(Packet, idx);
        idx += Answers[i].Packet.Length;
      }

      Authority = new Response[nscount];
      for(int i = 0; i < nscount; i++) {
        Authority[i] = new Response(Packet, idx);
        idx += Authority[i].Packet.Length;
      }

      Additional = new Response[arcount];
      for(int i = 0; i < arcount; i++) {
        Additional[i] = new Response(Packet, idx);
        idx += Additional[i].Packet.Length;
      }

      _icpacket = _packet = Packet;
    }

    /**
    <summary>Given a DNSPacket, it will generate a failure message so that
    the local resolver can move on to the next nameserver without timeouting
    on the this one.</summary>
    <param name="Packet">The base packet to translate into a failed response
    </param>
    */
    public static MemBlock BuildFailedReplyPacket(DNSPacket Packet) {
      byte[] res = new byte[Packet.Packet.Length];
      Packet.Packet.CopyTo(res, 0);
      res[3] |= 5;
      res[2] |= 0x80;
      return MemBlock.Reference(res);
    }

    public static bool StringIsIP(String IP) {
      bool is_ip = false;
      try {
        IPAddress.Parse(IP);
        is_ip = true;
      }
      catch {}
      return is_ip;
    }

    /**
    <summary>Takes in a memblock containing dns ptr data ...
    d.c.b.a.in-addr.arpa ... and returns the IP Address as a string.</summary>
    <param name="ptr">The block containing the dns ptr data.</param>
    <returns>The IP Address as a string - a.b.c.d.</returns>
    */
    public static String PtrMemBlockToString(MemBlock ptr) {
      String name = HostnameMemBlockToString(ptr);
      String[] res = name.Split('.');
      name = String.Empty;
        /* The last 2 parts are the pointer IN-ADDR.ARPA, the rest is 
        * reverse notation, we don't bother the user with this.
        */
      for(int i = res.Length - 3; i > 0; i--) {
        try {
          Byte.Parse(res[i]);
        }
        catch {
          throw new Exception("Invalid IP PTR");
        }
        name += res[i] + ".";
      }
      name += res[0];
      return name;
    }

    /**
    <summary>Takes in an IP Address in dns format and returns a string.  The
    format is abcd (byte[] {a, b, c, d}.</summary>
    <param name="ip">a memblock containing abcd.</param>
    <returns>String IP a.b.c.d</returns>
    */
    public static String IPMemBlockToString(MemBlock ip) {
      if(ip.Length != 4 && ip.Length != 6) {
        throw new Exception("Invalid IP");
      }
      String res = ip[0].ToString();
      for(int i = 1; i < ip.Length; i++) {
        res += "." + ip[i].ToString();
      }
      return res;
    }

    /**
    <summary>Takes in a memblock containing a dns formatted hostname string and
    converts it into a String.</summary>
    <param name="name">The memblock containing the dns formated hostname.
    </param>
    <returns>The hostname in a properly formatted string.</returns>
    */
    public static String HostnameMemBlockToString(MemBlock name) {
      String names = String.Empty;
      int idx = 0;
      while(name[idx] != 0) {
        byte length = name[idx++];
        for(int i = 0; i < length; i++) {
          names += (char) name[idx++];
        }
        if(name[idx] != 0) {
          names  += ".";
        }
      }
      return names;
    }

    /**
    <summary>Takes in an IP Address string and returns the dns ptr formatted
    memblock containing d.c.b.a.in-addr.arpa.</summary>
    <param name="ptr">An IP Address in the format a.b.c.d.</param>
    <returns>MemBlock containing d.c.b.a.in-addr.arpa.</returns>
    */
    public static MemBlock PtrStringToMemBlock(String ptr) {
      String[] res = ptr.Split('.');
      String name = String.Empty;
      for(int i = res.Length - 1; i > 0; i--) {
        name += res[i] + ".";
      }
      name += res[0] + INADDR_ARPA;
      return HostnameStringToMemBlock(name);
    }

    /**
    <summary>Takes in an ip string such as a.b.c.d and returns a MemBlock
    containing the IP [a, b, c, d].</summary>
    <param name="ip">The IP in a string to convert.</param>
    <returns>The MemBlock version of the IP Address.</returns>
    */
    public static MemBlock IPStringToMemBlock(String ip) {
      string []bytes = ip.Split('.');
      if(bytes.Length != 4 && bytes.Length != 6) {
        throw new Exception("Invalid IP");
      }
      byte[] ipb = new byte[bytes.Length];
      for(int i = 0; i < ipb.Length; i++) {
        ipb[i] = Byte.Parse(bytes[i]);
      }
      return MemBlock.Reference(ipb);
    }

    /**
    <summary>Given a NAME as a string converts it into bytes given the type
    of query.</summary>
    <param name="name">The name to convert (and resolve).</param>
    <param name="TYPE">The type of response packet.</param>
    */
    public static MemBlock HostnameStringToMemBlock(String name) {
      String[] pieces = name.Split('.');
      // First Length + Data + 0 (1 + name.Length + 1)
      byte[] nameb = new byte[name.Length + 2];
      int pos = 0;
      for(int idx = 0; idx < pieces.Length; idx++) {
        nameb[pos++] = (byte) pieces[idx].Length;
        for(int jdx = 0; jdx < pieces[idx].Length; jdx++) {
          nameb[pos++] = (byte) pieces[idx][jdx];
        }
      }
      nameb[pos] = 0;
      return MemBlock.Reference(nameb);
    }

    /**
    <summary>A blob is a fully resolved name.  DNS uses pointers to reduce
    memory consumption in packets, this can traverse all pointers and return a
    complete name.  The blob starts a Start and Ends at End.  This is used so
    that the parsing program knows where to continue reading data from.
    </summary>
    <param name="Data">The entire packet to grab the blob from.</param>
    <param name="Start">The beginning of the blob.</param>
    <param name="End">Returned to the user and notes where the blob ends.
    </param>
    <returns>The fully resolved blob as a memblock.</returns>
    */
    public static MemBlock RetrieveBlob(MemBlock Data, int Start,
                                        out int End) {
      int pos = Start, idx = 0;
      End = Start;
      byte [] blob = new byte[256];
      int length = 0;
      bool first = true;
      while(Data[pos] != 0) {
        if((Data[pos] & 0xF0) == 0xC0) {
          int offset = (Data[pos++] & 0x3F) << 8;
          offset |= Data[pos];
          if(first) {
            End = pos + 1;
            first = false;
          }
          pos = offset;
        }
        else {
          blob[idx++] = Data[pos++];
          length++;
        }
      }

      // Get the last 0
      blob[idx] = Data[pos++];
      if(first) {
        End = pos;
      }
      return MemBlock.Reference(blob, 0, length + 1);
    }
  }

#if NUNIT
  [TestFixture]
  public class DNSPacketTest {
    [Test]
    public void TestHostname() {
      String hostname = "yo-in-f104.google.com";
      MemBlock hostnamem = MemBlock.Reference(new byte[] {0x0a, 0x79, 0x6f,
        0x2d, 0x69, 0x6e, 0x2d, 0x66, 0x31, 0x30, 0x34, 0x06, 0x67, 0x6f, 0x6f,
        0x67, 0x6c, 0x65, 0x03, 0x63, 0x6f, 0x6d, 0x00});

      Assert.AreEqual(hostname, DNSPacket.HostnameMemBlockToString(hostnamem),
                      "HostnameMemBlockToString");
      Assert.AreEqual(hostnamem, DNSPacket.HostnameStringToMemBlock(hostname),
                      "HostnameStringToMemBlock");
      Assert.AreEqual(hostname, DNSPacket.HostnameMemBlockToString(
                      DNSPacket.HostnameStringToMemBlock(hostname)),
                      "Hostname String dual");
      Assert.AreEqual(hostnamem, DNSPacket.HostnameStringToMemBlock(
                      DNSPacket.HostnameMemBlockToString(hostnamem)),
                      "Hostname MemBlock dual");
    }

    [Test]
    public void TestIP() {
      String ip = "208.80.152.3";
      MemBlock ipm = MemBlock.Reference(new byte[] {0xd0, 0x50, 0x98, 0x03});
      Assert.AreEqual(ip, DNSPacket.IPMemBlockToString(ipm),
                      "IPMemBlockToString");
      Assert.AreEqual(ipm, DNSPacket.IPStringToMemBlock(ip),
                      "IPStringToMemBlock");
      Assert.AreEqual(ip, DNSPacket.IPMemBlockToString(
                      DNSPacket.IPStringToMemBlock(ip)),
                      "IP String dual");
      Assert.AreEqual(ipm, DNSPacket.IPStringToMemBlock(
                      DNSPacket.IPMemBlockToString(ipm)),
                      "IP MemBlock dual");

      String bad_ip = "Test.Test.Test.123";
      MemBlock bad_ipm = null;
      try {
        bad_ipm = DNSPacket.IPStringToMemBlock(bad_ip);
      } catch {}
      Assert.AreEqual(null, bad_ipm, "Bad IP");
    }

    [Test]
    public void TestPtr() {
      String ptr = "64.233.169.104";
      MemBlock ptrm = MemBlock.Reference(new byte[] {0x03, 0x31, 0x30, 0x34,
        0x03, 0x31, 0x36, 0x39, 0x03, 0x32, 0x33, 0x33, 0x02, 0x36, 0x34, 0x07,
        0x69, 0x6e, 0x2d, 0x61, 0x64, 0x64, 0x72, 0x04, 0x61, 0x72, 0x70, 0x61,
        0x00});
      Assert.AreEqual(ptr, DNSPacket.PtrMemBlockToString(ptrm),
                      "PtrMemBlockToString");
      Assert.AreEqual(ptrm, DNSPacket.PtrStringToMemBlock(ptr),
                      "PtrStringToMemBlock");
      Assert.AreEqual(ptr, DNSPacket.PtrMemBlockToString(
                      DNSPacket.PtrStringToMemBlock(ptr)),
                      "Ptr String dual");
      Assert.AreEqual(ptrm, DNSPacket.PtrStringToMemBlock(
                      DNSPacket.PtrMemBlockToString(ptrm)),
                      "Ptr MemBlock dual");
    }

    [Test]
    public void TestPtrRPacketWithCompression() {
      int id = 55885;
      short ID = (short) id;
      bool QUERY = false;
      byte OPCODE = 0;
      bool AA = false;

      String QNAME = "64.233.169.104";
      DNSPacket.TYPES QTYPE = DNSPacket.TYPES.PTR;
      DNSPacket.CLASSES QCLASS = DNSPacket.CLASSES.IN;
    //  Question qp = new Question(QNAME, QTYPE, QCLASS);

      String NAME = "64.233.169.104";
      DNSPacket.TYPES TYPE = DNSPacket.TYPES.PTR;
      DNSPacket.CLASSES CLASS = DNSPacket.CLASSES.IN;
      int TTL = 30;
      String RDATA = "yo-in-f104.google.com";
  //    Response rp = new Response(NAME, TYPE, CLASS, TTL, RDATA);

      MemBlock ptrm = MemBlock.Reference(new byte[] {0xda, 0x4d, 0x81, 0x80,
        0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x03, 0x31, 0x30, 0x34,
        0x03, 0x31, 0x36, 0x39, 0x03, 0x32, 0x33, 0x33, 0x02, 0x36, 0x34, 0x07,
        0x69, 0x6e, 0x2d, 0x61, 0x64, 0x64, 0x72, 0x04, 0x61, 0x72, 0x70, 0x61,
        0x00, 0x00, 0x0c, 0x00, 0x01, 0xc0, 0x0c, 0x00, 0x0c, 0x00, 0x01, 0x00,
        0x00, 0x00, 0x1e, 0x00, 0x17, 0x0a, 0x79, 0x6f, 0x2d, 0x69, 0x6e, 0x2d,
        0x66, 0x31, 0x30, 0x34, 0x06, 0x67, 0x6f, 0x6f, 0x67, 0x6c, 0x65, 0x03,
        0x63, 0x6f, 0x6d, 0x00});

      DNSPacket dm = new DNSPacket(ptrm);
      DNSPacket dp = new DNSPacket(ID, QUERY, OPCODE, AA, dm.RD, dm.RA,
                                   dm.Questions, dm.Answers, null, null);

      Assert.AreEqual(dm.ID, ID, "ID");
      Assert.AreEqual(dm.QUERY, QUERY, "QUERY");
      Assert.AreEqual(dm.OPCODE, OPCODE, "OPCODE");
      Assert.AreEqual(dm.AA, AA, "AA");
      Assert.AreEqual(dm.Questions.Length, 1, "Questions");
      Assert.AreEqual(dm.Answers.Length, 1, "Answers");
      Assert.AreEqual(dm.Packet, ptrm, "MemBlock");

      Response rm = dm.Answers[0];
      Assert.AreEqual(rm.NAME, NAME, "NAME");
      Assert.AreEqual(rm.TYPE, TYPE, "TYPE");
      Assert.AreEqual(rm.CLASS, CLASS, "CLASS");
      Assert.AreEqual(rm.TTL, TTL, "TTL");
      Assert.AreEqual(rm.RDATA, RDATA, "RDATA");

      Question qm = dm.Questions[0];
      Assert.AreEqual(qm.QNAME, QNAME, "QNAME");
      Assert.AreEqual(qm.QTYPE, QTYPE, "QTYPE");
      Assert.AreEqual(qm.QCLASS, QCLASS, "QCLASS");

      /// @todo add compression when creating dns packets... then we can
      /// build dp.Packet without using blobs and compare it to ptrm and it
      /// should pass!
      Assert.AreEqual(dp.Packet, ptrm, "Packet");
    }

    [Test]
    public void TestPtrRPacketWithoutCompression() {
      int id = 55885;
      short ID = (short) id;
      bool QUERY = false;
      byte OPCODE = 0;
      bool AA = false;

      String QNAME = "64.233.169.104";
      DNSPacket.TYPES QTYPE = DNSPacket.TYPES.PTR;
      DNSPacket.CLASSES QCLASS = DNSPacket.CLASSES.IN;
      Question qp = new Question(QNAME, QTYPE, QCLASS);

      String NAME = "64.233.169.104";
      DNSPacket.TYPES TYPE = DNSPacket.TYPES.PTR;
      DNSPacket.CLASSES CLASS = DNSPacket.CLASSES.IN;
      int TTL = 30;
      String RDATA = "yo-in-f104.google.com";
      Response rp = new Response(NAME, TYPE, CLASS, TTL, RDATA);

      DNSPacket dp = new DNSPacket(ID, QUERY, OPCODE, AA, false, false, 
                                   new Question[] {qp}, new Response[] {rp},
                                   null, null);

      MemBlock ptrm = MemBlock.Reference(new byte[] {0xda, 0x4d, 0x80, 0x00,
        0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x03, 0x31, 0x30, 0x34,
        0x03, 0x31, 0x36, 0x39, 0x03, 0x32, 0x33, 0x33, 0x02, 0x36, 0x34, 0x07,
        0x69, 0x6e, 0x2d, 0x61, 0x64, 0x64, 0x72, 0x04, 0x61, 0x72, 0x70, 0x61,
        0x00, 0x00, 0x0c, 0x00, 0x01, 0x03, 0x31, 0x30, 0x34, 0x03, 0x31, 0x36,
        0x39, 0x03, 0x32, 0x33, 0x33, 0x02, 0x36, 0x34, 0x07, 0x69, 0x6e, 0x2d,
        0x61, 0x64, 0x64, 0x72, 0x04, 0x61, 0x72, 0x70, 0x61, 0x00, 0x00, 0x0c,
        0x00, 0x01, 0x00, 0x00, 0x00, 0x1e, 0x00, 0x17, 0x0a, 0x79, 0x6f, 0x2d,
        0x69, 0x6e, 0x2d, 0x66, 0x31, 0x30, 0x34, 0x06, 0x67, 0x6f, 0x6f, 0x67,
        0x6c, 0x65, 0x03, 0x63, 0x6f, 0x6d, 0x00});

        DNSPacket dm = new DNSPacket(ptrm);
        Assert.AreEqual(dm.ID, ID, "ID");
        Assert.AreEqual(dm.QUERY, QUERY, "QUERY");
        Assert.AreEqual(dm.OPCODE, OPCODE, "OPCODE");
        Assert.AreEqual(dm.AA, AA, "AA");
        Assert.AreEqual(dm.Questions.Length, 1, "Questions");
        Assert.AreEqual(dm.Answers.Length, 1, "Answers");
        Assert.AreEqual(dm.Packet, ptrm, "MemBlock");

        Response rm = dm.Answers[0];
        Assert.AreEqual(rm.NAME, NAME, "NAME");
        Assert.AreEqual(rm.TYPE, TYPE, "TYPE");
        Assert.AreEqual(rm.CLASS, CLASS, "CLASS");
        Assert.AreEqual(rm.TTL, TTL, "TTL");
        Assert.AreEqual(rm.RDATA, RDATA, "RDATA");

        Question qm = dm.Questions[0];
        Assert.AreEqual(qm.QNAME, NAME, "QNAME");
        Assert.AreEqual(qm.QTYPE, TYPE, "QTYPE");
        Assert.AreEqual(qm.QCLASS, CLASS, "QCLASS");

        Assert.AreEqual(dp.Packet, ptrm, "DNS Packet");
    }

    [Test]
    public void TestMDNS() {
      MemBlock mdnsm = MemBlock.Reference(new byte[] {0x00, 0x00, 0x84, 0x00,
        0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x01, 0x12, 0x4c, 0x61, 0x70,
        0x70, 0x79, 0x40, 0x64, 0x61, 0x76, 0x69, 0x64, 0x2d, 0x6c, 0x61, 0x70,
        0x74, 0x6f, 0x70, 0x09, 0x5f, 0x70, 0x72, 0x65, 0x73, 0x65, 0x6e, 0x63,
        0x65, 0x04, 0x5f, 0x74, 0x63, 0x70, 0x05, 0x6c, 0x6f, 0x63, 0x61, 0x6c,
        0x00, 0x00, 0x21, 0x80, 0x01, 0x00, 0x00, 0x00, 0x78, 0x00, 0x15, 0x00,
        0x00, 0x00, 0x00, 0x14, 0xb2, 0x0c, 0x64, 0x61, 0x76, 0x69, 0x64, 0x2d,
        0x6c, 0x61, 0x70, 0x74, 0x6f, 0x70, 0xc0, 0x2e, 0xc0, 0x0c, 0x00, 0x10,
        0x80, 0x01, 0x00, 0x00, 0x11, 0x94, 0x00, 0x5c, 0x04, 0x76, 0x63, 0x3d,
        0x21, 0x09, 0x76, 0x65, 0x72, 0x3d, 0x32, 0x2e, 0x33, 0x2e, 0x31, 0x0e,
        0x6e, 0x6f, 0x64, 0x65, 0x3d, 0x6c, 0x69, 0x62, 0x70, 0x75, 0x72, 0x70,
        0x6c, 0x65, 0x0c, 0x73, 0x74, 0x61, 0x74, 0x75, 0x73, 0x3d, 0x61, 0x76,
        0x61, 0x69, 0x6c, 0x0e, 0x70, 0x6f, 0x72, 0x74, 0x2e, 0x70, 0x32, 0x70,
        0x6a, 0x3d, 0x35, 0x32, 0x39, 0x38, 0x0d, 0x6c, 0x61, 0x73, 0x74, 0x3d,
        0x57, 0x6f, 0x6c, 0x69, 0x6e, 0x73, 0x6b, 0x79, 0x09, 0x31, 0x73, 0x74,
        0x3d, 0x44, 0x61, 0x76, 0x69, 0x64, 0x09, 0x74, 0x78, 0x74, 0x76, 0x65,
        0x72, 0x73, 0x3d, 0x31, 0x09, 0x5f, 0x73, 0x65, 0x72, 0x76, 0x69, 0x63,
        0x65, 0x73, 0x07, 0x5f, 0x64, 0x6e, 0x73, 0x2d, 0x73, 0x64, 0x04, 0x5f,
        0x75, 0x64, 0x70, 0xc0, 0x2e, 0x00, 0x0c, 0x00, 0x01, 0x00, 0x00, 0x11,
        0x94, 0x00, 0x02, 0xc0, 0x1f, 0xc0, 0x1f, 0x00, 0x0c, 0x00, 0x01, 0x00,
        0x00, 0x11, 0x94, 0x00, 0x02, 0xc0, 0x0c, 0xc0, 0x45, 0x00, 0x01, 0x80,
        0x01, 0x00, 0x00, 0x00, 0x78, 0x00, 0x04, 0x0a, 0xe3, 0x38, 0x88});

      DNSPacket mdns = new DNSPacket(mdnsm);

      Assert.AreEqual(mdns.Questions.Length, 0, "Questions");
      Assert.AreEqual(mdns.Answers.Length, 4, "Answers");
      Assert.AreEqual(mdns.Authority.Length, 0, "Authority");
      Assert.AreEqual(mdns.Additional.Length, 1, "Additional");
      DNSPacket dnsp = new DNSPacket(mdns.ID, mdns.QUERY, mdns.OPCODE, mdns.AA,
                                     mdns.RD, mdns.RA, null, mdns.Answers,
                                     null, mdns.Additional);

      Assert.AreEqual(mdnsm, dnsp.Packet, "Packet");
      Assert.AreEqual(dnsp.Additional[0].NAME, "david-laptop.local", "NAME");
      Assert.AreEqual(dnsp.Additional[0].TYPE, DNSPacket.TYPES.A, "TYPE");
      Assert.AreEqual(dnsp.Additional[0].CLASS, DNSPacket.CLASSES.IN, "CLASS");
      Assert.AreEqual(dnsp.Additional[0].CACHE_FLUSH, true, "CACHE_FLUSH");
      Assert.AreEqual(dnsp.Additional[0].TTL, 120, "TTL");
      Assert.AreEqual(dnsp.Additional[0].RDATA, "10.227.56.136", "RDATA");
    }

    [Test]
    public void TestMDNS0() {
      MemBlock mdnsm = MemBlock.Reference(new byte[] {0x00, 0x00, 0x00, 0x00,
        0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x0E, 0x64, 0x61, 0x76,
        0x69, 0x64, 0x69, 0x77, 0x2D, 0x6C, 0x61, 0x70, 0x74, 0x6F, 0x70, 0x05,
        0x6C, 0x6F, 0x63, 0x61, 0x6C, 0x00, 0x00, 0xFF, 0x00, 0x01, 0xC0, 0x0C,
        0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x78, 0x00, 0x04, 0x0A, 0xFE,
        0x00, 0x01});
      DNSPacket mdns = new DNSPacket(mdnsm);

      Assert.AreEqual(mdns.Questions.Length, 1, "Questions");
      Assert.AreEqual(mdns.Answers.Length, 0, "Answers");
      Assert.AreEqual(mdns.Authority.Length, 1, "Authority");
      Assert.AreEqual(mdns.Additional.Length, 0, "Additional");
      DNSPacket dnsp = new DNSPacket(mdns.ID, mdns.QUERY, mdns.OPCODE, mdns.AA,
                                     mdns.RD, mdns.RA, mdns.Questions, mdns.Answers,
                                     mdns.Authority, mdns.Additional);

      Assert.AreEqual(dnsp.Authority[0].NAME, "davidiw-laptop.local", "NAME");
      Assert.AreEqual(dnsp.Authority[0].TYPE, DNSPacket.TYPES.A, "TYPE");
      Assert.AreEqual(dnsp.Authority[0].CLASS, DNSPacket.CLASSES.IN, "CLASS");
      Assert.AreEqual(dnsp.Authority[0].CACHE_FLUSH, false, "CACHE_FLUSH");
      Assert.AreEqual(dnsp.Authority[0].TTL, 120, "TTL");
      Assert.AreEqual(dnsp.Authority[0].RDATA, "10.254.0.1", "RDATA");

      Response old = mdns.Authority[0];
      mdns.Authority[0] = new Response(old.NAME, old.TYPE, old.CLASS,
                                         old.CACHE_FLUSH, old.TTL, "10.254.111.252");

      dnsp = new DNSPacket(mdns.ID, mdns.QUERY, mdns.OPCODE, mdns.AA,
                                     mdns.RD, mdns.RA, mdns.Questions, mdns.Answers,
                                     mdns.Authority, mdns.Additional);

      Assert.AreEqual(dnsp.Authority[0].NAME, "davidiw-laptop.local", "NAME");
      Assert.AreEqual(dnsp.Authority[0].TYPE, DNSPacket.TYPES.A, "TYPE");
      Assert.AreEqual(dnsp.Authority[0].CLASS, DNSPacket.CLASSES.IN, "CLASS");
      Assert.AreEqual(dnsp.Authority[0].CACHE_FLUSH, false, "CACHE_FLUSH");
      Assert.AreEqual(dnsp.Authority[0].TTL, 120, "TTL");
      Assert.AreEqual(dnsp.Authority[0].RDATA, "10.254.111.252", "RDATA");
    }

    [Test]
    public void Testdaap() {
      MemBlock mdnsm = MemBlock.Reference(new byte[] {0x00, 0x00, 0x00, 0x00,
        0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x10, 0x50, 0x69, 0x65,
        0x72, 0x72, 0x65, 0x27, 0x73, 0x20, 0x4C, 0x69, 0x62, 0x72, 0x61, 0x72,
        0x79, 0x05, 0x5F, 0x64, 0x61, 0x61, 0x70, 0x04, 0x5F, 0x74, 0x63, 0x70,
        0x05, 0x6C, 0x6F, 0x63, 0x61, 0x6C, 0x00, 0x00, 0xFF, 0x00, 0x01, 0xC0,
        0x0C, 0x00, 0x21, 0x00, 0x01, 0x00, 0x00, 0x00, 0x78, 0x00, 0x0D, 0x00,
        0x00, 0x00, 0x00, 0x0E, 0x69, 0x04, 0x50, 0x49, 0x42, 0x4D, 0xC0,
        0x2});
      DNSPacket mdns = new DNSPacket(mdnsm);
      Assert.AreEqual(mdns.Questions.Length, 1, "Questions");
      Assert.AreEqual(mdns.Answers.Length, 0, "Answers");
      Assert.AreEqual(mdns.Authority.Length, 1, "Authority");
      Assert.AreEqual(mdns.Additional.Length, 0, "Additional");

      Assert.AreEqual(mdns.Questions[0].QNAME_BLOB, 
        MemBlock.Reference(new byte[]{0x10, 0x50, 0x69, 0x65, 0x72, 0x72, 0x65,
        0x27, 0x73, 0x20, 0x4C, 0x69, 0x62, 0x72, 0x61, 0x72, 0x79, 0x05, 0x5F,
        0x64, 0x61, 0x61, 0x70, 0x04, 0x5F, 0x74, 0x63, 0x70, 0x05, 0x6C, 0x6F,
        0x63, 0x61, 0x6C, 0x00}), "QNAME");
      Assert.AreEqual(mdns.Questions[0].QTYPE, (DNSPacket.TYPES) 0xFF, "QTYPE");
      Assert.AreEqual(mdns.Questions[0].QCLASS, DNSPacket.CLASSES.IN, "QCLASS");
    }

    [Test]
    public void PtrNoIP() {
      MemBlock mdnsm = MemBlock.Reference(new byte[] {0x00, 0x00, 0x84, 0x00,
        0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x02, 0x10, 0x50, 0x69, 0x65,
        0x72, 0x72, 0x65, 0xe2, 0x80, 0x99, 0x73, 0x20, 0x4d, 0x75, 0x73, 0x69,
        0x63, 0x05, 0x5f, 0x64, 0x61, 0x61, 0x70, 0x04, 0x5f, 0x74, 0x63, 0x70,
        0x05, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x00, 0x00, 0x21, 0x80, 0x01, 0x00,
        0x00, 0x00, 0x78, 0x00, 0x0f, 0x00, 0x00, 0x00, 0x00, 0x0e, 0x69, 0x06,
        0x70, 0x69, 0x65, 0x72, 0x72, 0x65, 0xc0, 0x28, 0xc0, 0x0c, 0x00, 0x10,
        0x80, 0x01, 0x00, 0x00, 0x11, 0x94, 0x00, 0xb4, 0x09, 0x74, 0x78, 0x74,
        0x76, 0x65, 0x72, 0x73, 0x3d, 0x31, 0x0c, 0x4f, 0x53, 0x73, 0x69, 0x3d,
        0x30, 0x78, 0x32, 0x44, 0x46, 0x34, 0x35, 0x0e, 0x56, 0x65, 0x72, 0x73,
        0x69, 0x6f, 0x6e, 0x3d, 0x31, 0x39, 0x36, 0x36, 0x31, 0x34, 0x1c, 0x44,
        0x61, 0x74, 0x61, 0x62, 0x61, 0x73, 0x65, 0x20, 0x49, 0x44, 0x3d, 0x34,
        0x44, 0x30, 0x43, 0x30, 0x42, 0x32, 0x30, 0x43, 0x39, 0x34, 0x46, 0x31,
        0x42, 0x36, 0x31, 0x1d, 0x4d, 0x61, 0x63, 0x68, 0x69, 0x6e, 0x65, 0x20,
        0x4e, 0x61, 0x6d, 0x65, 0x3d, 0x50, 0x69, 0x65, 0x72, 0x72, 0x65, 0xe2,
        0x80, 0x99, 0x73, 0x20, 0x4d, 0x75, 0x73, 0x69, 0x63, 0x0e, 0x50, 0x61,
        0x73, 0x73, 0x77, 0x6f, 0x72, 0x64, 0x3d, 0x66, 0x61, 0x6c, 0x73, 0x65,
        0x14, 0x4d, 0x65, 0x64, 0x69, 0x61, 0x20, 0x4b, 0x69, 0x6e, 0x64, 0x73,
        0x20, 0x53, 0x68, 0x61, 0x72, 0x65, 0x64, 0x3d, 0x30, 0x16, 0x4d, 0x49,
        0x44, 0x3d, 0x30, 0x78, 0x39, 0x30, 0x32, 0x43, 0x39, 0x44, 0x45, 0x34,
        0x46, 0x35, 0x44, 0x46, 0x37, 0x45, 0x36, 0x46, 0x17, 0x4d, 0x61, 0x63,
        0x68, 0x69, 0x6e, 0x65, 0x20, 0x49, 0x44, 0x3d, 0x32, 0x31, 0x42, 0x44,
        0x36, 0x44, 0x37, 0x45, 0x31, 0x30, 0x36, 0x46, 0x09, 0x5f, 0x73, 0x65,
        0x72, 0x76, 0x69, 0x63, 0x65, 0x73, 0x07, 0x5f, 0x64, 0x6e, 0x73, 0x2d,
        0x73, 0x64, 0x04, 0x5f, 0x75, 0x64, 0x70, 0xc0, 0x28, 0x00, 0x0c, 0x00,
        0x01, 0x00, 0x00, 0x11, 0x94, 0x00, 0x02, 0xc0, 0x1d, 0xc0, 0x1d, 0x00,
        0x0c, 0x00, 0x01, 0x00, 0x00, 0x11, 0x94, 0x00, 0x02, 0xc0, 0x0c, 0x1c,
        0x69, 0x54, 0x75, 0x6e, 0x65, 0x73, 0x5f, 0x43, 0x74, 0x72, 0x6c, 0x5f,
        0x34, 0x32, 0x36, 0x35, 0x37, 0x38, 0x32, 0x41, 0x43, 0x32, 0x46, 0x43,
        0x46, 0x31, 0x41, 0x43, 0x05, 0x5f, 0x64, 0x61, 0x63, 0x70, 0xc0, 0x23,
        0x00, 0x21, 0x80, 0x01, 0x00, 0x00, 0x00, 0x78, 0x00, 0x08, 0x00, 0x00,
        0x00, 0x00, 0x0e, 0x69, 0xc0, 0x3f, 0xc1, 0x3b, 0x00, 0x10, 0x80, 0x01,
        0x00, 0x00, 0x11, 0x94, 0x00, 0x37, 0x09, 0x74, 0x78, 0x74, 0x76, 0x65,
        0x72, 0x73, 0x3d, 0x31, 0x09, 0x56, 0x65, 0x72, 0x3d, 0x36, 0x35, 0x35,
        0x33, 0x37, 0x0c, 0x4f, 0x53, 0x73, 0x69, 0x3d, 0x30, 0x78, 0x32, 0x44,
        0x46, 0x34, 0x35, 0x15, 0x44, 0x62, 0x49, 0x64, 0x3d, 0x34, 0x44, 0x30,
        0x43, 0x30, 0x42, 0x32, 0x30, 0x43, 0x39, 0x34, 0x46, 0x31, 0x42, 0x36,
        0x31, 0xc1, 0x08, 0x00, 0x0c, 0x00, 0x01, 0x00, 0x00, 0x11, 0x94, 0x00,
        0x02, 0xc1, 0x58, 0xc1, 0x58, 0x00, 0x0c, 0x00, 0x01, 0x00, 0x00, 0x11,
        0x94, 0x00, 0x02, 0xc1, 0x3b, 0xc0, 0x3f, 0x00, 0x01, 0x80, 0x01, 0x00,
        0x00, 0x00, 0x78, 0x00, 0x04, 0x0a, 0xfe, 0x00, 0x01, 0xc0, 0x3f, 0x00,
        0x1c, 0x80, 0x01, 0x00, 0x00, 0x00, 0x78, 0x00, 0x10, 0xfe, 0x80, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0xff, 0xa9, 0xff, 0xfe, 0xe4, 0x23,
        0x5f});
      DNSPacket mdns = new DNSPacket(mdnsm);

      Assert.AreEqual(mdns.Questions.Length, 0, "Questions");
      Assert.AreEqual(mdns.Answers.Length, 8, "Answers");
      Assert.AreEqual(mdns.Authority.Length, 0, "Authority");
      Assert.AreEqual(mdns.Additional.Length, 2, "Additional");
      MemBlock test = MemBlock.Reference(new byte[]{0x1c, 0x69, 0x54, 0x75,
        0x6e, 0x65, 0x73, 0x5f, 0x43, 0x74, 0x72, 0x6c, 0x5f, 0x34, 0x32, 0x36,
        0x35, 0x37, 0x38, 0x32, 0x41, 0x43, 0x32, 0x46, 0x43, 0x46, 0x31, 0x41,
        0x43, 0x05, 0x5f, 0x64, 0x61, 0x63, 0x70, 0x04, 0x5f, 0x74, 0x63, 0x70,
        0x05, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x00});
      Assert.AreEqual(mdns.Answers[5].NAME_BLOB, test, "Answers[5].NAME");
      Assert.AreEqual(mdns.Additional[0].NAME, "pierre.local",
                      "Additional[0].NAME");
      Assert.AreEqual(mdns.Additional[0].RDATA, "10.254.0.1",
                      "Additional[0].RDATA");
      Assert.AreEqual(mdns.Answers[2].TYPE, DNSPacket.TYPES.PTR,
                      "Answers[2].TYPE");
      Response original = mdns.Answers[2];
      Response copy = new Response(original.NAME, original.TYPE,
                                   original.CLASS, original.CACHE_FLUSH,
                                   original.TTL, original.RDATA);
      MemBlock original_expanded = MemBlock.Reference(new byte[]{0x09, 0x5f,
        0x73, 0x65, 0x72, 0x76, 0x69, 0x63, 0x65, 0x73, 0x07, 0x5f, 0x64, 0x6e,
        0x73, 0x2d, 0x73, 0x64, 0x04, 0x5f, 0x75, 0x64, 0x70, 0x05, 0x6c, 0x6f,
        0x63, 0x61, 0x6c, 0x00, 0x00, 0x0c, 0x00, 0x01, 0x00, 0x00, 0x11, 0x94,
        0x00, 0x12, 0x05, 0x5f, 0x64, 0x61, 0x61, 0x70, 0x04, 0x5f, 0x74, 0x63,
        0x70, 0x05, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x00});
      Assert.AreEqual(original_expanded, copy.Packet, "original");
    }
  }
#endif
}
