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

namespace Ipop {
  public class DNSPacket: DataPacket {
  /*
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

    ID - identification - client generated, do not change
    QR - query / reply, client sends 0, server replies 1
    Opcode - 0 for query, 1 inverse query
    AA - Authoritative answer - True when there is a mapping
    TC - Truncation - ignored - 0
    RD - Recursion desired - unimplemented - 0
    RA - Recursion availabled - unimplemented - 0
    Z - Reserved - must be 0
    RCODE - ignored, stands for error code - 0
    QDCOUNT - questions - should be 1
    ANCOUNT - answers - should be 0 until we answer!
    NSCOUNT - name server records - somewhat supported, but I can't
      find a reason why it needs to be so I've left in ZoneAuthority code
      in case it is ever needed!
    ARCOUNT - additional records - unsupported
  */
    public const String INADDR_ARPA = ".in-addr.arpa";
    public enum TYPES {A = 1, SOA = 6, PTR = 12};
    public enum CLASSES {IN = 1};
    public readonly short ID;
    public readonly bool QUERY;
    public readonly byte OPCODE;
    public readonly bool AA;
    public readonly Question[] Questions;
    public readonly Response[] Responses;

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

    public static MemBlock BuildFailedReplyPacket(DNSPacket Packet) {
      byte[] res = new byte[Packet.Packet.Length];
      Packet.Packet.CopyTo(res, 0);
      res[3] |= 5;
      res[2] |= 0x80;
      return MemBlock.Reference(res);
    }

    /**
    * Represents a DNS Question, sadly the size of these can only be 
    * determined by parsing the entire packet.
    *                                1  1  1  1  1  1
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
  
    @see Question()
      */
    public class Question: DataPacket {
      public enum Types {IP_ADDR, CHAR_ARRAY};
      public readonly Types QNAME_TYPE;
      public readonly String QNAME;
      public readonly TYPES QTYPE;
      public readonly CLASSES QCLASS;

     /**
      * Constructor when creating a DNS Query
      * @param QNAME the name of resource you are looking up, IP Address 
      *   when QTYPE = PTR otherwise hostname
      * @param QTYPE the type of look up to perform
      * @param QCLASS should always be IN
      */
      public Question(String QNAME, TYPES QTYPE, CLASSES QCLASS) {
        this.QNAME = QNAME;
        this.QTYPE = QTYPE;
        this.QCLASS = QCLASS;

        MemBlock name = NameStringToBytes(QNAME, QTYPE);

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
      * Constructor when parsing a DNS Query
      * @param Data must pass in the entire packet from where the question
      *  begins, after parsing, can check Data.Length to find where next
      *  container begins.
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
          QTYPE = (TYPES) qtype;
        }
        catch {
          throw new Exception("Invalid DNS_QTYPE " + qtype);
        }

        if(QTYPE == TYPES.PTR) {
          QNAME = NameStringPtr(QNAME);
        }

        int qclass = (Data[idx++] << 8) + Data[idx];
        try {
          QCLASS = (CLASSES) qclass;
        }
        catch {
          throw new Exception("Invalid DNS_QCLASS " + qclass);
        }

        _icpacket = _packet = Data.Slice(0, idx + 1);
      }
    }

      /**
    * Represents a Response to a DNS Query.

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

    @see Response()
      */
    public class Response: DataPacket {
      public readonly String NAME;
      public readonly TYPES TYPE;
      public readonly CLASSES CLASS;
      public readonly int TTL;
      public readonly short RDLENGTH;
      public readonly String RDATA;
      public readonly ZoneAuthority ZARDATA;

      protected Response(String NAME, TYPES TYPE, CLASSES CLASS, int TTL, ICopyable RDATA) {
        this.NAME = NAME;
        this.CLASS = CLASS;
        this.TTL = TTL;

        try {
          if(Enum.GetName(typeof(TYPES), TYPE).Equals(String.Empty)) {
            throw new Exception();
          }
        }
        catch {
          throw new Exception("Unsupported type " + TYPE);
        }
        this.TYPE = TYPE;

        try {
          if(Enum.GetName(typeof(CLASSES), CLASS).Equals(String.Empty)) {
            throw new Exception();
          }
        }
        catch {
          throw new Exception("Unsupported type " + CLASS);
        }
        this.CLASS = CLASS;

        MemBlock name_bytes = NameStringToBytes(NAME, TYPE);
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

      public Response(String NAME, TYPES TYPE, CLASSES CLASS, int TTL, String RDATA):
        this(NAME, TYPE, CLASS, TTL, RRNameToBytes(RDATA, TYPE)) {}

      public static MemBlock RRNameToBytes(String name, TYPES TYPE) {
        MemBlock reply = null;
        if(TYPE == TYPES.PTR) {
          reply = NameStringToBytes(name, TYPES.A);
        }
        else if(TYPE == TYPES.A){
          byte[] bytes_reply = new byte[4];
          string []bytes = name.Split('.');
          for(int i = 0; i < bytes_reply.Length; i++) {
            bytes_reply[i] = Byte.Parse(bytes[i]);
          }
          reply = MemBlock.Reference(bytes_reply);
        }
        return reply;
      }

      public Response(String NAME, CLASSES CLASS, int TTL, ZoneAuthority RDATA):
        this(NAME, TYPES.SOA, CLASS, TTL, RDATA.Packet) {
        ZARDATA = RDATA;
      }

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
          TYPE = (TYPES) type;
        }
        catch {
          throw new Exception("Invalid DNS_TYPE " + type);
        }

        int rclass = (Data[idx++] << 8) + Data[idx++];
        try {
          CLASS = (CLASSES) rclass;
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
        if(TYPE == TYPES.PTR) {
          NAME = NameStringPtr(NAME);
          for(int i = 0; i < 3; i++) {
            RDATA += Data[idx++].ToString() + ".";
          }
          RDATA += Data[idx];
        }
        else if(TYPE == TYPES.A) {
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

      public class ZoneAuthority: DataPacket {
        /**
          Authority RR RDATA
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
          @see ZoneAuthority()
        */

        public readonly string MNAME, RNAME;
        public readonly int SERIAL, REFRESH, RETRY, EXPIRE, MINIMUM;

     /**
      * Constructor when creating a ZoneAuthority, 
      * @param QNAME the name of resource you are looking up, IP Address 
      *   when QTYPE = PTR otherwise hostname
      * @param QTYPE the type of look up to perform
      * @param QCLASS should always be IN
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

        public ZoneAuthority(string MNAME, string RNAME, int SERIAL,
                             int REFRESH, int RETRY, int EXPIRE, int MINIMUM) {
          this.MNAME = MNAME;
          this.RNAME = RNAME;
          this.SERIAL = SERIAL;
          this.REFRESH = REFRESH;
          this.RETRY = RETRY;
          this.EXPIRE = EXPIRE;
          this.MINIMUM = MINIMUM;

          MemBlock mname = NameStringToBytes(MNAME, TYPES.A);
          MemBlock rname = NameStringToBytes(RNAME, TYPES.A);
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

      /** 
    * Converts names from String representation to byte representation for DNS
    * @param name the name to convert
    * @param type the type we're converting to
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
    * Converts a Pointer Name String to just the IP String
    * @param name the Pointer name
    * @return the IP String
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
    * Converts a IP String to a Pointer Name String
    * @param name the IP String
    * @return the Pointer name
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
    * Converts a response string into bytes
    * @param response the response we're sending off!
    * @param type the type of response
    * @return a MemBlock containing the bytes
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

/* Test Code
    public static void Main() {
      Socket _s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
      EndPoint ipep = new IPEndPoint(IPAddress.Any, 53);
      _s.Bind(ipep);
      while(true) {
        byte[] packet = new byte[1000];
        EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
        int recv_count = _s.ReceiveFrom(packet, ref ep);
        MemBlock Packet = MemBlock.Reference(packet);
        DNSPacket dnspacket = new DNSPacket(Packet);
        MemBlock RPacket = null;
        if(dnspacket.Questions[0].QNAME == "davidiw.pooper") {
          Response response = new Response("davidiw.pooper", TYPES.A, CLASSES.IN, 3600, "192.168.1.155");
          DNSPacket rdnspacket = new DNSPacket(dnspacket.ID, false, dnspacket.OPCODE, 
                                               true, dnspacket.Questions, new Response[1] { response });
          byte[] rpacket = new byte[rdnspacket.Data.Length];
          rdnspacket.Data.CopyTo(rpacket, 0);
          RPacket = MemBlock.Reference(rpacket);
          Console.WriteLine("TYPES.A");
        }
        else if(dnspacket.Questions[0].QNAME == "192.168.1.155") {
          Response response0 = new Response("192.168.1.155", TYPES.PTR, CLASSES.IN, 3600, "hitme.hard");
          Response response1 = new Response("192.168.1.155", TYPES.PTR, CLASSES.IN, 3600, "hitme.harder");
          Response response2 = new Response("192.168.1.155", TYPES.PTR, CLASSES.IN, 3600, "hitme.hardest");
          DNSPacket rdnspacket = new DNSPacket(dnspacket.ID, false, dnspacket.OPCODE, 
                                               true, dnspacket.Questions, new Response[3] { response0, response1, response2 });
          byte[] rpacket = new byte[rdnspacket.Data.Length];
          rdnspacket.Data.CopyTo(rpacket, 0);
          RPacket = MemBlock.Reference(rpacket);
          Console.WriteLine("TYPES.PTR");
        }
        else {
          RPacket = DNSPacket.BuildFailedReplyPacket(dnspacket);
        }
        Console.WriteLine(dnspacket.Questions[0].QNAME);
        Console.WriteLine(ep);
        _s.SendTo(RPacket, ep);
      }
    }
*/
  }
}
