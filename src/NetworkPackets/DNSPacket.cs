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
        idx += Questions[i].Packet.Length + 1;
      }

      Responses = new Response[ancount];
      for(int i = 0; i < ancount; i++) {
        Responses[i] = new Response(Packet, idx);
        idx += Responses[i].Packet.Length + 1;
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

    /**
    <summary>Converts a Pointer Name String to just the IP String</summary>
    <param name="name">the Pointer name</param>
    <returns>the IP String</returns>
     */
    public static String NameStringPtr(String name) {
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

    /**
    <summary>Converts a IP String to a Pointer Name String</summary>
    <param name="name">the IP as a string</param>
    <returns>the Pointer name</returns>
     */
    public static String NameBytesPtr(String name) {
      String[] res = name.Split('.');
      name = String.Empty;
      for(int i = res.Length - 1; i > 0; i--) {
        name += res[i] + ".";
      }
      name += res[0] + INADDR_ARPA;
      return name;
    }

    /**
    <summary>Converts a response string into bytes</summary>
    <param name="response"> the response we're sending off!</param>
    <param name="type"> the type of response</param>
    <returns> a MemBlock containing the bytes</returns>
     */
    public static MemBlock ResponseStringToBytes(String response, TYPES type) {
      MemBlock mres = null;
        // The format is length data length data ... where data is split by '.'
      if(type == TYPES.PTR) {
          // This guy can already convert it for us!
        mres = NameStringToBytes(response, TYPES.A);
      }
      else if(type == TYPES.A) {
        String []bytes = response.Split('.');
        byte[] res = new byte[4];
        for(int i = 0; i < bytes.Length; i++) {
          res[i] = Byte.Parse(bytes[i]);
        }
        mres = MemBlock.Reference(res);
      }
      return mres;
    }
  }

  /**
  <summary>Represents a DNS Question</summary>
  <remarks><para>Sadly the size of these can only be determined by parsing
  the entire packet.</para>
  <para>It looks like this:</para>
  <code>
                                  1  1  1  1  1  1
    0  1  2  3  4  5  6  7  8  9  0  1  2  3  4  5
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                                               |
  /                     QNAME                     /
  /                                               /
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                     QTYPE                     |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                     QCLASS                    |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  </code></remarks>
  */
  public class Question: DataPacket {
    /// <summary>What type of qname do we have ptr or name</summary>
    public enum Types {
      /// <summary>a pointer / ip address</summary>
      IP_ADDR,
      /// <summary>name</summary>
      CHAR_ARRAY
    };
    /// <summary>format of the qname type</summary>
    public readonly Types QNAME_TYPE;
    /// <summary>string representation of the qname</summary>
    public readonly String QNAME;
    /// <summary>the query type</summary>
    public readonly DNSPacket.TYPES QTYPE;
    /// <summary>the network class</summary>
    public readonly DNSPacket.CLASSES QCLASS;

    /**
    <summary>Constructor when creating a DNS Query</summary>
    <param name="QNAME">the name of resource you are looking up, IP Address 
    when QTYPE = PTR otherwise hostname</param>
    <param name="QTYPE"> the type of look up to perform</param>
    <param name="QCLASS">should always be IN</param>
    */
    public Question(String QNAME, DNSPacket.TYPES QTYPE, DNSPacket.CLASSES QCLASS) {
      this.QNAME = QNAME;
      this.QTYPE = QTYPE;
      this.QCLASS = QCLASS;

      MemBlock name = DNSPacket.NameStringToBytes(QNAME, QTYPE);

        // 2 for QTYPE + 2 for QCLASS
      byte[] data = new byte[4];
      int idx = 0;
      data[idx++] = (byte) ((((int) QTYPE) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) QTYPE) & 0xFF);
      data[idx++] = (byte) ((((int) QCLASS) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) QCLASS) & 0xFF);
      _icpacket = new CopyList(name, MemBlock.Reference(data));
    }

    /**
    <summary>Constructor when parsing a DNS Query</summary>
    <param name="Data"> must pass in the entire packet from where the question
    begins, after parsing, can check Data.Length to find where next
    container begins.</param>
    */
    public Question(MemBlock Data) {
      int idx = 0;
      QNAME = String.Empty;
      while(Data[idx] != 0) {
        byte length = Data[idx++];
        for(int i = 0; i < length; i++) {
          QNAME += (char) Data[idx++];
        }
        if(Data[idx] != 0) {
          QNAME  += ".";
        }
      }
      idx++;

      int qtype = (Data[idx++] << 8) + Data[idx++];
      try {
        QTYPE = (DNSPacket.TYPES) qtype;
      }
      catch {
        throw new Exception("Invalid DNS_QTYPE " + qtype);
      }

      if(QTYPE == DNSPacket.TYPES.PTR) {
        QNAME = DNSPacket.NameStringPtr(QNAME);
      }

      int qclass = (Data[idx++] << 8) + Data[idx];
      try {
        QCLASS = (DNSPacket.CLASSES) qclass;
      }
      catch {
        throw new Exception("Invalid DNS_QCLASS " + qclass);
      }

      _icpacket = _packet = Data.Slice(0, idx + 1);
    }
  }

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
    /// <summary>the name rdata resolves</summary>
    public readonly String NAME;
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
    /// <summary>incomplete</summary>
    public readonly ZoneAuthority ZARDATA;
    /// <summary>this needs to be implemented to support generic RDATA</summary>
    public readonly ICopyable ICRDATA;

    /**
    <summary>Creates a response from the parameter fields with RDATA being
    a memory chunk.</summary>
    <param name="NAME">The name to resolve.</param>
    <param name="TYPE">The query type.</param>
    <param name="CLASS">The network type.</param>
    <param name="TTL">How long to hold the result in the local dns cache.</param>
    <param name="RDATA">RDATA in byte format.</param>
    */
    protected Response(String NAME, DNSPacket.TYPES TYPE, DNSPacket.CLASSES CLASS, int TTL, ICopyable RDATA) {
      this.NAME = NAME;
      this.CLASS = CLASS;
      this.TTL = TTL;

      try {
        if(Enum.GetName(typeof(DNSPacket.TYPES), TYPE).Equals(String.Empty)) {
          throw new Exception();
        }
      }
      catch {
        throw new Exception("Unsupported type " + TYPE);
      }
      this.TYPE = TYPE;

      try {
        if(Enum.GetName(typeof(DNSPacket.CLASSES), CLASS).Equals(String.Empty)) {
          throw new Exception();
        }
      }
      catch {
        throw new Exception("Unsupported type " + CLASS);
      }
      this.CLASS = CLASS;

      MemBlock name_bytes = DNSPacket.NameStringToBytes(NAME, TYPE);
      RDLENGTH = (short) RDATA.Length;

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

      _icpacket = new CopyList(name_bytes, MemBlock.Reference(data), RDATA);
    }

    /**
    <summary>Creates a response from the parameter fields with RDATA being
    a string.</summary>
    <param name="NAME">The name to resolve.</param>
    <param name="TYPE">The query type.</param>
    <param name="CLASS">The network type.</param>
    <param name="TTL">How long to hold the result in the local dns cache.</param>
    <param name="RDATA">RDATA in string format.</param>
    */
    public Response(String NAME, DNSPacket.TYPES TYPE, DNSPacket.CLASSES CLASS, int TTL, String RDATA):
      this(NAME, TYPE, CLASS, TTL, RRNameToBytes(RDATA, TYPE)) {}

    /**
    <summary>Given a NAME as a string converts it into bytes given the type
    of query.</summary>
    <param name="name">The name to convert (and resolve).</param>
    <param name="TYPE">The type of response packet.</param>
    */
    public static MemBlock RRNameToBytes(String name, DNSPacket.TYPES TYPE) {
      MemBlock reply = null;
      if(TYPE == DNSPacket.TYPES.PTR) {
        reply = DNSPacket.NameStringToBytes(name, DNSPacket.TYPES.A);
      }
      else if(TYPE == DNSPacket.TYPES.A){
        byte[] bytes_reply = new byte[4];
        string []bytes = name.Split('.');
        for(int i = 0; i < bytes_reply.Length; i++) {
          bytes_reply[i] = Byte.Parse(bytes[i]);
        }
        reply = MemBlock.Reference(bytes_reply);
      }
      return reply;
    }

    /**
    <summary>Incomplete</summary>
    */
    public Response(String NAME, DNSPacket.CLASSES CLASS, int TTL, ZoneAuthority RDATA):
      this(NAME, DNSPacket.TYPES.SOA, CLASS, TTL, RDATA.Packet) {
      ZARDATA = RDATA;
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
      int pos = Start;
      NAME = String.Empty;
      int offset = Start;
      if(0xC == (Data[Start] | 0xC)) {
        offset = (Data[pos++] & 0x3 << 8);
        offset |= Data[pos++];
      }

      int idx = offset;
      while(Data[idx] != 0) {
        byte length = Data[idx++];
        for(int i = 0; i < length; i++) {
          NAME += (char) Data[idx++];
        }
        if(Data[idx] != 0) {
          NAME += ".";
        }
      }

      if(offset == Start) {
        pos += idx + 1;
      }

      int type = (Data[idx++] << 8) + Data[idx++];
      try {
        TYPE = (DNSPacket.TYPES) type;
      }
      catch {
        throw new Exception("Invalid DNS_TYPE " + type);
      }

      int rclass = (Data[idx++] << 8) + Data[idx++];
      try {
        CLASS = (DNSPacket.CLASSES) rclass;
      }
      catch {
        throw new Exception("Invalid DNS_CLASS " + rclass);
      }

      TTL = (Data[idx++] << 24);
      TTL |= (Data[idx++] << 16);
      TTL |= (Data[idx++] << 8);
      TTL |= (Data[idx++]);

      RDLENGTH = (short) ((Data[idx++] << 8) + Data[idx++]);

      RDATA = String.Empty;
      if(TYPE == DNSPacket.TYPES.PTR) {
        NAME = DNSPacket.NameStringPtr(NAME);
        for(int i = 0; i < 3; i++) {
          RDATA += Data[idx++].ToString() + ".";
        }
        RDATA += Data[idx];
      }
      else if(TYPE == DNSPacket.TYPES.A) {
        while(Data[idx] != 0) {
          byte length = Data[idx++];
          for(int i = 0; i < length; i++) {
            RDATA += (char) Data[idx++];
          }
          if(Data[idx] != 0) {
            RDATA += ".";
          }
        }
      }
      _icpacket = _packet = Data.Slice(Start, Start + idx + 1);
    }
  }

  /**
  <summary>This is for a SOA type Response and would be considered
  a AR and not an RR.</summary>
  <remarks>
  <para>Authority RR RDATA, this gets placed in a response packet
  RDATA</para>
  <code>
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  /                     MNAME                     /
  /                                               /
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  /                     RNAME                     /
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    SERIAL                     |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    REFRESH                    |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                     RETRY                     |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    EXPIRE                     |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                    MINIMUM                    |
  |                                               |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  </code>
  </remarks>
  */
  public class ZoneAuthority: DataPacket {
    /// <summary>Incomplete</summary>
    public readonly string MNAME;
    /// <summary>Incomplete</summary>
    public readonly string RNAME;
    /// <summary>Incomplete</summary>
    public readonly int SERIAL;
    /// <summary>Incomplete</summary>
    public readonly int REFRESH;
    /// <summary>Incomplete</summary>
    public readonly int RETRY;
    /// <summary>Incomplete</summary>
    public readonly int EXPIRE;
    /// <summary>Incomplete</summary>
    public readonly int MINIMUM;

    /**
    <summary>Constructor when creating a ZoneAuthority from a MemBlock, this
    is incomplete.</summary>
    */
    public ZoneAuthority(MemBlock data) {
      int idx = 0;
      MNAME = String.Empty;
      while(data[idx] != 0) {
        byte length = data[idx++];
        for(int i = 0; i < length; i++) {
          MNAME += (char) data[idx++];
        }
        if(data[idx] != 0) {
          MNAME  += ".";
        }
      }
      idx++;

      RNAME = String.Empty;
      while(data[idx] != 0) {
        byte length = data[idx++];
        for(int i = 0; i < length; i++) {
          RNAME += (char) data[idx++];
        }
        if(data[idx] != 0) {
          RNAME  += ".";
        }
      }
      idx++;

      SERIAL = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      REFRESH = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      RETRY = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      EXPIRE = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      MINIMUM = (data[idx++] << 24) + (data[idx++] << 16) +
          (data[idx++] << 8) + data[idx++] << 24;
      _icpacket = _packet = data.Slice(0, idx);
    }

    /**
    <summary>Creates a Zone authority from field data in the parameters,
    this is incomplete.</summary>
    */
    public ZoneAuthority(string MNAME, string RNAME, int SERIAL,
                          int REFRESH, int RETRY, int EXPIRE, int MINIMUM) {
      this.MNAME = MNAME;
      this.RNAME = RNAME;
      this.SERIAL = SERIAL;
      this.REFRESH = REFRESH;
      this.RETRY = RETRY;
      this.EXPIRE = EXPIRE;
      this.MINIMUM = MINIMUM;

      MemBlock mname = DNSPacket.NameStringToBytes(MNAME, DNSPacket.TYPES.A);
      MemBlock rname = DNSPacket.NameStringToBytes(RNAME, DNSPacket.TYPES.A);
      byte[] rest = new byte[20];
      int idx = 0;
      rest[idx++] = (byte) ((SERIAL >> 24) & 0xFF);
      rest[idx++] = (byte) ((SERIAL >> 16) & 0xFF);
      rest[idx++] = (byte) ((SERIAL >> 8) & 0xFF);
      rest[idx++] = (byte) (SERIAL  & 0xFF);
      rest[idx++] = (byte) ((REFRESH >> 24) & 0xFF);
      rest[idx++] = (byte) ((REFRESH >> 16) & 0xFF);
      rest[idx++] = (byte) ((REFRESH >> 8) & 0xFF);
      rest[idx++] = (byte) (REFRESH  & 0xFF);
      rest[idx++] = (byte) ((RETRY >> 24) & 0xFF);
      rest[idx++] = (byte) ((RETRY >> 16) & 0xFF);
      rest[idx++] = (byte) ((RETRY >> 8) & 0xFF);
      rest[idx++] = (byte) (RETRY  & 0xFF);
      rest[idx++] = (byte) ((EXPIRE >> 24) & 0xFF);
      rest[idx++] = (byte) ((EXPIRE >> 16) & 0xFF);
      rest[idx++] = (byte) ((EXPIRE >> 8) & 0xFF);
      rest[idx++] = (byte) (EXPIRE  & 0xFF);
      rest[idx++] = (byte) ((MINIMUM >> 24) & 0xFF);
      rest[idx++] = (byte) ((MINIMUM >> 16) & 0xFF);
      rest[idx++] = (byte) ((MINIMUM >> 8) & 0xFF);
      rest[idx++] = (byte) (MINIMUM  & 0xFF);
      _icpacket = new CopyList(mname, rname, MemBlock.Reference(rest));
    }

    public ZoneAuthority() : this("grid-appliance.org",
      "grid-appliance.org", 12345678, 1800, 900, 604800, 60480) {}
  }
}
