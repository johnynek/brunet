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
  <summary>Represents a Dns Question</summary>
  <remarks><para>Sadly the size of these can only be determined by parsing
  the entire packet.</para>
  <para>It looks like this:</para>
  <code>
                                  1  1  1  1  1  1
    0  1  2  3  4  5  6  7  8  9  0  1  2  3  4  5
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                                               |
  /                     QName                     /
  /                                               /
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                     QType                     |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  |                     QClass                    |
  +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  </code></remarks>
  */
  public class Question: DataPacket {
    /// <summary>What type of qname do we have ptr or name</summary>
    public enum Types {
      /// <summary>a pointer / ip address</summary>
      IpAddr,
      /// <summary>name</summary>
      CharArray
    };
    /// <summary>string representation of the qname</summary>
    public readonly String QName;
    /// <summary>the blob format for the qname</summary>
    public readonly MemBlock QNameBlob;
    /// <summary>the query type</summary>
    public readonly DnsPacket.Types QType;
    /// <summary>the network class</summary>
    public readonly DnsPacket.Classes QClass;

    /**
    <summary>Constructor when creating a Dns Query</summary>
    <param name="QName">the name of resource you are looking up, IP Address 
    when QType = Ptr otherwise hostname</param>
    <param name="QType"> the type of look up to perform</param>
    <param name="QClass">should always be IN</param>
    */
    public Question(String QName, DnsPacket.Types QType, DnsPacket.Classes QClass) {
      this.QName = QName;
      this.QType = QType;
      this.QClass = QClass;

      if(QType == DnsPacket.Types.A) {
        QNameBlob = DnsPacket.HostnameStringToMemBlock(QName);
      }
      else if(QType == DnsPacket.Types.Ptr) {
        QNameBlob = DnsPacket.PtrStringToMemBlock(QName);
      }
      else {
        throw new Exception("Invalid QType: " + QType + "!");
      }

        // 2 for QType + 2 for QClass
      byte[] data = new byte[4];
      int idx = 0;
      data[idx++] = (byte) ((((int) QType) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) QType) & 0xFF);
      data[idx++] = (byte) ((((int) QClass) >> 8) & 0xFF);
      data[idx++] = (byte) (((int) QClass) & 0xFF);
      _icpacket = new CopyList(QNameBlob, MemBlock.Reference(data));
    }

    /**
    <summary>Constructor when parsing a Dns Query</summary>
    <param name="Data"> must pass in the entire packet from where the question
    begins, after parsing, can check Data.Length to find where next
    container begins.</param>
    */
    public Question(MemBlock Data, int Start) {
      int idx = 0;
      QNameBlob = DnsPacket.RetrieveBlob(Data, Start, out idx);
      int qtype = (Data[idx++] << 8) + Data[idx++];
      QType = (DnsPacket.Types) qtype;

      int qclass = (Data[idx++] << 8) + Data[idx];
      QClass = (DnsPacket.Classes) qclass;

      if(QType == DnsPacket.Types.A) {
        QName = DnsPacket.HostnameMemBlockToString(QNameBlob);
      }
      else if(QType == DnsPacket.Types.Ptr) {
        QName = DnsPacket.PtrMemBlockToString(QNameBlob);
      }

      _icpacket = _packet = Data.Slice(Start, idx + 1 - Start);
    }
  }

#if NUNIT
  [TestFixture]
  public class DnsQuestionTest {
    [Test]
    public void DnsPtrTest() {
      String NAME = "64.233.169.104";
      DnsPacket.Types TYPE = DnsPacket.Types.Ptr;
      DnsPacket.Classes CLASS = DnsPacket.Classes.IN;
      Question qp = new Question(NAME, TYPE, CLASS);

      MemBlock ptrm = MemBlock.Reference(new byte[] {0x03, 0x31, 0x30, 0x34,
        0x03, 0x31, 0x36, 0x39, 0x03, 0x32, 0x33, 0x33, 0x02, 0x36, 0x34, 0x07,
        0x69, 0x6e, 0x2d, 0x61, 0x64, 0x64, 0x72, 0x04, 0x61, 0x72, 0x70, 0x61,
        0x00, 0x00, 0x0c, 0x00, 0x01});
      Question qm = new Question(ptrm, 0);

      Assert.AreEqual(qp.Packet, ptrm, "Packet");
      Assert.AreEqual(qm.QName, NAME, "NAME");
      Assert.AreEqual(qm.QType, TYPE, "TYPE");
      Assert.AreEqual(qm.QClass, CLASS, "CLASS");
    }


    [Test]
    public void DnsATest() {
      String NAME = "www.cnn.com";
      DnsPacket.Types TYPE = DnsPacket.Types.A;
      DnsPacket.Classes CLASS = DnsPacket.Classes.IN;
      Question qp = new Question(NAME, TYPE, CLASS);

      MemBlock namem = MemBlock.Reference(new byte[] {0x03, 0x77, 0x77, 0x77,
        0x03, 0x63, 0x6e, 0x6e, 0x03, 0x63, 0x6f, 0x6d, 0x00, 0x00, 0x01, 0x00,
        0x01});
      Question qm = new Question(namem, 0);

      Assert.AreEqual(qp.Packet, namem, "Packet");
      Assert.AreEqual(qm.QName, NAME, "NAME");
      Assert.AreEqual(qm.QType, TYPE, "TYPE");
      Assert.AreEqual(qm.QClass, CLASS, "CLASS");
    }
  }
#endif
}
