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
    /// <summary>string representation of the qname</summary>
    public readonly String QNAME;
    /// <summary>the blob format for the qname</summary>
    public readonly MemBlock QNAME_BLOB;
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

      if(QTYPE == DNSPacket.TYPES.A) {
        QNAME_BLOB = DNSPacket.HostnameStringToMemBlock(QNAME);
      }
      else if(QTYPE == DNSPacket.TYPES.PTR) {
        QNAME_BLOB = DNSPacket.PtrStringToMemBlock(QNAME);
      }
      else {
        throw new Exception("Invalid QTYPE: " + QTYPE + "!");
      }

        // 2 for QTYPE + 2 for QCLASS
      byte[] data = new byte[4];
      int idx = 0;
      data[idx++] = (byte) ((((int) QTYPE) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) QTYPE) & 0xFF);
      data[idx++] = (byte) ((((int) QCLASS) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) QCLASS) & 0xFF);
      _icpacket = new CopyList(QNAME_BLOB, MemBlock.Reference(data));
    }

    /**
    <summary>Constructor when creating a DNS Query with a qname blob.</summary>
    <param name="QNAME_BLOB">The QNAME in a byte array blob</param>
    <param name="QTYPE"> the type of look up to perform</param>
    <param name="QCLASS">should always be IN</param>
    */
    public Question(MemBlock QNAME_BLOB, DNSPacket.TYPES QTYPE, DNSPacket.CLASSES QCLASS) {
      this.QNAME_BLOB = QNAME_BLOB;
      this.QTYPE = QTYPE;
      this.QCLASS = QCLASS;

      if(QTYPE == DNSPacket.TYPES.A) {
        QNAME = DNSPacket.HostnameMemBlockToString(QNAME_BLOB);
      }
      else if(QTYPE == DNSPacket.TYPES.PTR) {
        QNAME = DNSPacket.PtrMemBlockToString(QNAME_BLOB);
      }

        // 2 for QTYPE + 2 for QCLASS
      byte[] data = new byte[4];
      int idx = 0;
      data[idx++] = (byte) ((((int) QTYPE) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) QTYPE) & 0xFF);
      data[idx++] = (byte) ((((int) QCLASS) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) QCLASS) & 0xFF);
      _icpacket = new CopyList(QNAME_BLOB, MemBlock.Reference(data));
    }

    /**
    <summary>Constructor when parsing a DNS Query</summary>
    <param name="Data"> must pass in the entire packet from where the question
    begins, after parsing, can check Data.Length to find where next
    container begins.</param>
    */
    public Question(MemBlock Data) {
      int idx = 0;
      while(Data[idx] != 0) {
        idx++;
      }

      QNAME_BLOB = Data.Slice(0, ++idx);

      int qtype = (Data[idx++] << 8) + Data[idx++];
      QTYPE = (DNSPacket.TYPES) qtype;

      int qclass = (Data[idx++] << 8) + Data[idx];
      QCLASS = (DNSPacket.CLASSES) qclass;

      if(QTYPE == DNSPacket.TYPES.A) {
        QNAME = DNSPacket.HostnameMemBlockToString(QNAME_BLOB);
      }
      else if(QTYPE == DNSPacket.TYPES.PTR) {
        QNAME = DNSPacket.PtrMemBlockToString(QNAME_BLOB);
      }

      _icpacket = _packet = Data.Slice(0, idx + 1);
    }
  }

#if NUNIT
  [TestFixture]
  public class DNSQuestionTest {
    [Test]
    public void DNSPtrTest() {
      String NAME = "64.233.169.104";
      DNSPacket.TYPES TYPE = DNSPacket.TYPES.PTR;
      DNSPacket.CLASSES CLASS = DNSPacket.CLASSES.IN;
      Question qp = new Question(NAME, TYPE, CLASS);

      MemBlock ptrm = MemBlock.Reference(new byte[] {0x03, 0x31, 0x30, 0x34,
        0x03, 0x31, 0x36, 0x39, 0x03, 0x32, 0x33, 0x33, 0x02, 0x36, 0x34, 0x07,
        0x69, 0x6e, 0x2d, 0x61, 0x64, 0x64, 0x72, 0x04, 0x61, 0x72, 0x70, 0x61,
        0x00, 0x00, 0x0c, 0x00, 0x01});
      Question qm = new Question(ptrm);

      Assert.AreEqual(qp.Packet, ptrm, "Packet");
      Assert.AreEqual(qm.QNAME, NAME, "NAME");
      Assert.AreEqual(qm.QTYPE, TYPE, "TYPE");
      Assert.AreEqual(qm.QCLASS, CLASS, "CLASS");
    }

    [Test]
    public void DNSBlobTest() {
      MemBlock NAME_BLOB = MemBlock.Reference(new byte[] {0x03, 0x31, 0x30, 0x34,
        0x03, 0x31, 0x36, 0x39, 0x03, 0x32, 0x33, 0x33, 0x02, 0x36, 0x34, 0x07,
        0x69, 0x6e, 0x2d, 0x61, 0x64, 0x64, 0x72, 0x04, 0x61, 0x72, 0x70, 0x61,
        0x00});
      DNSPacket.TYPES TYPE = DNSPacket.TYPES.PTR;
      DNSPacket.CLASSES CLASS = DNSPacket.CLASSES.IN;
      Question qp = new Question(NAME_BLOB, TYPE, CLASS);

      MemBlock ptrm = MemBlock.Reference(new byte[] {0x03, 0x31, 0x30, 0x34,
        0x03, 0x31, 0x36, 0x39, 0x03, 0x32, 0x33, 0x33, 0x02, 0x36, 0x34, 0x07,
        0x69, 0x6e, 0x2d, 0x61, 0x64, 0x64, 0x72, 0x04, 0x61, 0x72, 0x70, 0x61,
        0x00, 0x00, 0x0c, 0x00, 0x01});
      Question qm = new Question(ptrm);

      Assert.AreEqual(qp.Packet, ptrm, "Packet");
      Assert.AreEqual(qm.QNAME_BLOB, NAME_BLOB, "NAME_BLOB");
      Assert.AreEqual(qm.QTYPE, TYPE, "TYPE");
      Assert.AreEqual(qm.QCLASS, CLASS, "CLASS");
    }

    [Test]
    public void DNSATest() {
      String NAME = "www.cnn.com";
      DNSPacket.TYPES TYPE = DNSPacket.TYPES.A;
      DNSPacket.CLASSES CLASS = DNSPacket.CLASSES.IN;
      Question qp = new Question(NAME, TYPE, CLASS);

      MemBlock namem = MemBlock.Reference(new byte[] {0x03, 0x77, 0x77, 0x77,
        0x03, 0x63, 0x6e, 0x6e, 0x03, 0x63, 0x6f, 0x6d, 0x00, 0x00, 0x01, 0x00,
        0x01});
      Question qm = new Question(namem);

      Assert.AreEqual(qp.Packet, namem, "Packet");
      Assert.AreEqual(qm.QNAME, NAME, "NAME");
      Assert.AreEqual(qm.QTYPE, TYPE, "TYPE");
      Assert.AreEqual(qm.QCLASS, CLASS, "CLASS");
    }
  }
#endif
}