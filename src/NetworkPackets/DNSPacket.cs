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
\namespace NetworkPackets::DNS
\brief Defines DNS Packets.
*/
namespace NetworkPackets.DNS {
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
      <description>Recursion desired - unimplemented - 0</description>
    </item>
    <item>
      <term>RA</term>
      <description>Recursion availabled - unimplemented - 0</description>
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
    /// <summary>list of Questions</summary>
    public readonly Question[] Questions;
    /// <summary>list of Responses</summary>
    public readonly Response[] Responses;

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
    <param name="Responses">A list of Responses.</param>
    */
    public DNSPacket(short ID, bool QUERY, byte OPCODE, bool AA,
                     Question[] Questions, Response[] Responses) {
      byte[] header = new byte[12];
      header[0] = (byte) ((ID >> 8) & 0xFF);
      header[1] = (byte) (ID & 0xFF);
      if(!QUERY) {
        header[2] |= 0x80;
      }
      header[2] |= (byte) (OPCODE << 3);
      if(AA) {
      // Authoritative Answer
        header[2] |= 0x4;
      // Authentication
        header[3] |= 0x20;
        header[3] |= 0x80;
      }
      header[4] = (byte) ((Questions.Length >> 8) & 0xFF);
      header[5] = (byte) (Questions.Length  & 0xFF);
      header[6] = (byte) ((Responses.Length >> 8) & 0xFF);
      header[7] = (byte) (Responses.Length  & 0xFF);
      MemBlock Header = MemBlock.Reference(header);

      _icpacket = new CopyList(Header);
      for(int i = 0; i < Questions.Length; i++) {
        _icpacket = new CopyList(_icpacket, Questions[i].ICPacket);
      }
      for(int i = 0; i < Responses.Length; i++) {
        _icpacket = new CopyList(_icpacket, Responses[i].ICPacket);
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
      int qdcount = (Packet[4] << 8) + Packet[5];
      int ancount = (Packet[6] << 8) + Packet[7];
//      int nscount = (Packet[8] << 8) + Packet[9];
//      int arcount = (Packet[10] << 8) + Packet[11];
      int idx = 12;

      Questions = new Question[qdcount];
      for(int i = 0; i < qdcount; i++) {
        Questions[i] = new Question(Packet.Slice(idx));
        idx += Questions[i].Packet.Length;
      }

      Responses = new Response[ancount];
      for(int i = 0; i < ancount; i++) {
        Responses[i] = new Response(Packet, idx);
        idx += Responses[i].Packet.Length;
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

    /** 
    <summary>Converts names from String representation to byte representation
    for DNS</summary>
    <param name="name">the name to convert</param>
    <param name="type">the type we're converting to</param>
    <returns>The byte version of the name stored in a MemBlock</returns>
     */
    public static MemBlock NameStringToBytes(String name, TYPES type) {
      byte[] res = null;
        /* With pointers we reduce overhead on the user and only return the
      * IP Address in String format rather than the pointer format
        */
      if(type == TYPES.PTR) {
        String[] pieces = name.Split('.');
          // First Length + Data + .in-addr.arpa + 0 (1 + name.Length + 1)
        res = new byte[name.Length + INADDR_ARPA.Length + 2];

        int pos = 0;
        for(int idx = pieces.Length - 1; idx >= 0; idx--) {
          res[pos++] = (byte) pieces[idx].Length;
          for(int jdx = 0; jdx < pieces[idx].Length; jdx++) {
            res[pos++] = (byte) pieces[idx][jdx];
          }
        }

        pieces = INADDR_ARPA.Split('.');
        for(int idx = 1; idx < pieces.Length; idx++) {
          res[pos++] = (byte) pieces[idx].Length;
          for(int jdx = 0; jdx < pieces[idx].Length; jdx++) {
            res[pos++] = (byte) pieces[idx][jdx];
          }
        }
        res[pos] = 0;
      }
      else if(type == TYPES.A || type == TYPES.SOA) {
        String[] pieces = name.Split('.');
          // First Length + Data + 0 (1 + name.Length + 1)
        res = new byte[name.Length + 2];
        int pos = 0;
        for(int idx = 0; idx < pieces.Length; idx++) {
          res[pos++] = (byte) pieces[idx].Length;
          for(int jdx = 0; jdx < pieces[idx].Length; jdx++) {
            res[pos++] = (byte) pieces[idx][jdx];
          }
        }
        res[pos] = 0;
      }

      MemBlock mres = MemBlock.Reference(res);
      return mres;
    }

    public static String PtrMemBlockToString(MemBlock ptr) {
      String name = HostnameMemBlockToString(ptr);
      String[] res = name.Split('.');
      name = String.Empty;
        /* The last 2 parts are the pointer IN-ADDR.ARPA, the rest is 
      * reverse notation, we don't bother the user with this.
        */
      for(int i = res.Length - 3; i > 0; i--) {
        name += res[i] + ".";
      }
      name += res[0];
      return name;
    }

    public static String IPMemBlockToString(MemBlock ip) {
      String res = ip[0].ToString();
      for(int i = 1; i < ip.Length; i++) {
        res += "." + ip[i].ToString();
      }
      return res;
    }

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
      byte[] ipb = new byte[4];
      string []bytes = ip.Split('.');
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
    public void TestPtrRPacket() {
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

      DNSPacket dp = new DNSPacket(ID, QUERY, OPCODE, AA, new Question[] {qp},
                                   new Response[] {rp});

      MemBlock ptrm = MemBlock.Reference(new byte[] {0xda, 0x4d, 0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x03, 0x31, 0x30, 0x34, 0x03, 0x31, 0x36, 0x39, 0x03, 0x32, 0x33, 0x33, 0x02, 0x36, 0x34, 0x07, 0x69, 0x6e, 0x2d, 0x61, 0x64, 0x64, 0x72, 0x04, 0x61, 0x72, 0x70, 0x61, 0x00, 0x00, 0x0c, 0x00, 0x01, 0xc0, 0x0c, 0x00, 0x0c, 0x00, 0x01, 0x00, 0x00, 0x00, 0x1e, 0x00, 0x17, 0x0a, 0x79, 0x6f, 0x2d, 0x69, 0x6e, 0x2d, 0x66, 0x31, 0x30, 0x34, 0x06, 0x67, 0x6f, 0x6f, 0x67, 0x6c, 0x65, 0x03, 0x63, 0x6f, 0x6d, 0x00});
      DNSPacket dm = new DNSPacket(ptrm);
      Assert.AreEqual(dm.ID, ID, "ID");
      Assert.AreEqual(dm.QUERY, QUERY, "QUERY");
      Assert.AreEqual(dm.OPCODE, OPCODE, "OPCODE");
      Assert.AreEqual(dm.AA, AA, "AA");
      Assert.AreEqual(dm.Questions.Length, 1, "Questions");
      Assert.AreEqual(dm.Responses.Length, 1, "Responses");
      Assert.AreEqual(dm.Packet, ptrm, "MemBlock");

      Response rm = dm.Responses[0];
      Assert.AreEqual(rm.NAME, NAME, "NAME");
      Assert.AreEqual(rm.TYPE, TYPE, "TYPE");
      Assert.AreEqual(rm.CLASS, CLASS, "CLASS");
      Assert.AreEqual(rm.TTL, TTL, "TTL");
      Assert.AreEqual(rm.RDATA, RDATA, "RDATA");

      Question qm = dm.Questions[0];
      Assert.AreEqual(qm.QNAME, NAME, "QNAME");
      Assert.AreEqual(qm.QTYPE, TYPE, "QTYPE");
      Assert.AreEqual(qm.QCLASS, CLASS, "QCLASS");
    }
  }
#endif
}
