/*
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet.Util;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

#if NUNIT
using NUnit.Framework;
#endif

namespace NetworkPackets {
  /// <summary>Parses and creates Stun packets as defined by RFC 5389.</summary>
  public class StunPacket {
    /// Stun packet:
    ///  0                   1                   2                   3
    ///  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |0 0|     STUN Message AttributeType     |         Message Length        |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |                         Magic Cookie                          |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |                                                               |
    /// |                     Transaction ID (96 bits)                  |
    /// |                                                               |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |         AttributeType                  |            Length             |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |                         Value (variable)                ....
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |         AttributeType                  |            Length             |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |                         Value (variable)                ....
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// Attributes are optional
    ///

    /// <summary>Holds a generic Xmpp Attribute.</summary>
    public class Attribute {
      public enum AttributeType {
        MappedAddress = 0x0001,
        ResponseAddress = 0x0002,
        ChangeAddress = 0x0003,
        SourceAddress = 0x0004,
        ChangedAddress = 0x0005,
        Username = 0x0006,
        MessageIntegrity = 0x0008,
        Realm = 0x0014,
        Nonce = 0x0015,
        XorMappedAddress = 0x0020,
        Software = 0x8022,
        AlternateServer = 0x8023,
        Fingerprint = 0x8028
      };

      /// <summary>The Attribute type.</summary>
      public readonly AttributeType Type;
      /// <summary>The binary represtation of the value.</summary>
      public readonly MemBlock Value;
      /// <summary>Data for the attribute.</summary>
      public readonly MemBlock Data;

      /// <summary>Create a new Attribute.</summary>
      public Attribute(AttributeType type, MemBlock value)
      {
        Type = type;
        Value = value;
        byte[] data = new byte[4 + value.Length];
        NumberSerializer.WriteUShort((ushort) type, data, 0);
        NumberSerializer.WriteUShort((ushort) value.Length, data, 2);
        value.CopyTo(data, 4);
        Data = MemBlock.Reference(data);
      }

      /// <summary>Parse an Attribute.</summary>
      public Attribute(MemBlock data)
      {
        Data = data;
        Type = (AttributeType) ((ushort) NumberSerializer.ReadShort(data, 0));
        ushort length = (ushort) NumberSerializer.ReadShort(data, 2);
        Value = MemBlock.Reference(data, 4, length);
      }

      /// <summary>Converts an attribute into a specific type if a parser exists
      /// for it, otherwise stuffs it into a generic Attribute object.</summary>
      public static Attribute Parse(MemBlock data)
      {
        if(4 > data.Length) {
          throw new Exception("Poorly formed packet");
        }
        ushort type = (ushort) NumberSerializer.ReadShort(data, 0);
        ushort length = (ushort) NumberSerializer.ReadShort(data, 2);
        if(4 + length > data.Length) {
          throw new Exception("Poorly formed packet");
        }
        MemBlock att_data = MemBlock.Reference(data, 4, length);

        AttributeType attype = (AttributeType) type;
        if(attype == AttributeType.MappedAddress ||
            attype == AttributeType.ResponseAddress ||
            attype == AttributeType.ChangeAddress ||
            attype == AttributeType.SourceAddress)
        {
          return new AttributeAddress(attype, att_data);
        }

        switch(attype) {
          default:
            return new Attribute(attype, att_data);
        }
      }
    }

    /// <summary>An attribute class for those that involve network addresses.</summary>
    public class AttributeAddress : Attribute {
      public enum FamilyType {
        IPv4 = 0x01,
        IPv6 = 0x02
      };
      /// <summary>The address family type.</summary>
      public readonly FamilyType Family;
      /// <summary>The port associated with the AttributeAddress.</summary>
      public readonly ushort Port;
      /// <summary>The address associated with the AttributeAddress.</summary>
      public readonly IPAddress IP;

      /// <summary>Parse an AttributeAddress.</summary>
      public AttributeAddress(AttributeType type, MemBlock data) :
        base(type, data)
      {
        Family = (FamilyType) data[1];
        Port = (ushort) NumberSerializer.ReadShort(data, 2);
        byte[] addr = new byte[data.Length - 4];
        data.Slice(4).CopyTo(addr, 0);
        IP = new IPAddress(addr);
      }

      /// <summary>Create an AttributeAddress.</summary>
      public AttributeAddress(AttributeType type, IPAddress ip, ushort port) :
        base(type, CreateAddressData(ip, port))
      {
        if(ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
          Family = FamilyType.IPv4;
        } else {
          Family = FamilyType.IPv6;
        }

        IP = ip;
        Port = port;
      }

      public static MemBlock CreateAddressData(IPAddress ip, ushort port)
      {
        byte[] addr = ip.GetAddressBytes();
        byte[] data = new byte[4 + addr.Length];
        if(ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
          data[1] = (byte) FamilyType.IPv4;
        } else {
          data[1] = (byte) FamilyType.IPv6;
        }
        NumberSerializer.WriteUShort(port, data, 2);
        addr.CopyTo(data, 4);
        return MemBlock.Reference(data);
      }
    }

    /// <summary>Different types of Stun message classes.</summary>
    public enum ClassType {
      /// <summary>Desire information.</summary>
      Request = 0x000,
      /// <summary>No expected response.</summary>
      Indication = 0x010,
      /// <summary>Response to a request.</summary>
      Response = 0x100,
      /// <summary>An error</summary>
      Error = 0x101
    };

    /// <summary>Different types of Stun messages.</summary>
    public enum MessageType {
      /// <summary>IP Address information.</summary>
      Binding = 0x0001
    };

    // Common constants
    public const ushort CLASS_MASK = 0x0101;
    public const ushort MESSAGE_MASK = 0x3EFE;
    public const ushort ZERO_MASK = 0xC0;
    public const int ZERO_MASK_INT = 0xC000;
    public const int TRANASCTION_ID_LENGTH = 12;
    public const int MINIMUM_SIZE = 20;
    public static readonly List<Attribute> EMPTY_ATTRIBUTES = new List<Attribute>(0);
    public static readonly byte[] MAGIC_COOKIE = new byte[] {0x21, 0x12, 0xA4, 0x42};

    /// <summary>The class type for this packet.</summary>
    public readonly ClassType Class;
    /// <summary>The message type for this packet.</summary>
    public readonly MessageType Message;
    /// <summary>Transaction ID that uniquely identifies this packet.</summary>
    public readonly MemBlock TransactionID;
    /// <summary>The whole packet.</summary>
    public MemBlock Data;
    /// <summary>List of attributes in this packet.</summary>
    public readonly List<Attribute> Attributes;

    /// <summary>Generate an Stun packet.</summary>
    public StunPacket(ClassType ct, MessageType mt, List<Attribute> attributes)
    {
      Class = ct;
      Message = mt;
      Attributes = new List<Attribute>(attributes);

      int size = MINIMUM_SIZE;
      // Let's get the size of this packet
      foreach(Attribute attr in attributes) {
        size += attr.Data.Length;
      }

      byte[] data = new byte[size];
      // Make the message
      ushort message = (ushort) ((ushort) ct | (ushort) mt);
      NumberSerializer.WriteUShort(message, data, 0);
      // Write the size
      NumberSerializer.WriteUShort((ushort) (size - MINIMUM_SIZE), data, 2);
      // Add the cookie
      MAGIC_COOKIE.CopyTo(data, 4);
      // Add a transaction id
      byte[] transaction_id = new byte[12];
      (new Random()).NextBytes(transaction_id);
      transaction_id.CopyTo(data, 8);
      TransactionID = MemBlock.Reference(data, 8, 12);
      // Finished

      int offset = MINIMUM_SIZE;
      foreach(Attribute attr in attributes) {
        attr.Data.CopyTo(data, offset);
        offset += attr.Data.Length;
      }
      Data = MemBlock.Reference(data);
    }

    /// <summary>Parse a Stun packet.</summary>
    public StunPacket(MemBlock packet)
    {
      if((packet[0] & ZERO_MASK) != 0) {
        throw new Exception("Invalid packet, initial bits are not 0");
      } else if(packet.Length < MINIMUM_SIZE) {
        throw new Exception("Invalid packet, too small");
      }

      Data = packet;
      ushort message = (ushort) NumberSerializer.ReadShort(packet, 0);
      Class = (ClassType) (CLASS_MASK & message);
      Message = (MessageType) (MESSAGE_MASK & message);
      TransactionID = MemBlock.Reference(packet, 8, 12);

      int offset = MINIMUM_SIZE;
      if(packet.Length > MINIMUM_SIZE) {
        Attributes = new List<Attribute>();
        // Attributes
        while(offset < packet.Length) {
          MemBlock attrm = MemBlock.Reference(packet, offset, packet.Length - offset);
          Attribute attr = Attribute.Parse(attrm);
          offset += attr.Data.Length;
          Attributes.Add(attr);
        }
      } else {
        Attributes = EMPTY_ATTRIBUTES;
      }
    }
  }

#if NUNIT
  [TestFixture]
  public class StunTest {
    [Test]
    public void TestRequest()
    { 
      byte[] request = new byte[] {0x00, 0x01, 0x00, 0x00, 0x21, 0x12, 0xa4,
        0x42, 0x99, 0x7b, 0x63, 0x08, 0x17, 0x9d, 0x00, 0xbc, 0xe9, 0xb5, 0x9f,
        0x57};
      MemBlock req = MemBlock.Reference(request);
      StunPacket from_mb = new StunPacket(req);
      StunPacket from_input = new StunPacket(StunPacket.ClassType.Request,
          StunPacket.MessageType.Binding, StunPacket.EMPTY_ATTRIBUTES);
      from_input = new StunPacket(from_input.Data);

      Assert.AreEqual(from_mb.Attributes, from_input.Attributes, "Attributes");
      Assert.AreEqual(from_mb.Message, from_input.Message, "Message");
      Assert.AreEqual(from_mb.Class, from_input.Class, "Class");
      Assert.AreEqual(from_mb.Data.Length, from_input.Data.Length, "Length");
    }

    [Test]
    public void TestResponse()
    {
      byte[] response = new byte[] {0x01, 0x01, 0x00, 0x18, 0x21, 0x12, 0xa4,
        0x42, 0x99, 0x7b, 0x63, 0x08, 0x17, 0x9d, 0x00, 0xbc, 0xe9, 0xb5, 0x9f,
        0x57, 0x00, 0x01, 0x00, 0x08, 0x00, 0x01, 0xe1, 0x6e, 0x46, 0xb9, 0x63,
        0xb6, 0x00, 0x04, 0x00, 0x08, 0x00, 0x01, 0x4b, 0x66, 0x4a, 0x7d, 0x5f,
        0x7e};
      MemBlock resp = MemBlock.Reference(response);
      StunPacket from_mb = new StunPacket(resp);

      List<StunPacket.Attribute> attrs = new List<StunPacket.Attribute>(2);
      attrs.Add(new StunPacket.AttributeAddress(
          StunPacket.Attribute.AttributeType.MappedAddress,
          IPAddress.Parse("70.185.99.182"),
          57710));
      attrs.Add(new StunPacket.AttributeAddress(
          StunPacket.Attribute.AttributeType.SourceAddress,
          IPAddress.Parse("74.125.95.126"),
          19302));
      StunPacket from_input = new StunPacket(StunPacket.ClassType.Response,
          StunPacket.MessageType.Binding, attrs);
      from_input = new StunPacket(from_input.Data);

      Assert.AreEqual(from_mb.Attributes.Count, from_input.Attributes.Count, "Attributes");
      for(int i = 0; i < from_mb.Attributes.Count; i++) {
        Assert.AreEqual(from_mb.Attributes[i].Data, from_input.Attributes[i].Data, "Attribute " + i);
      }
      Assert.AreEqual(from_mb.Message, from_input.Message, "Message");
      Assert.AreEqual(from_mb.Class, from_input.Class, "Class");
      Assert.AreEqual(from_mb.Data.Length, from_input.Data.Length, "Length");
    }
  }
#endif

  /*
  public class MainTest {
    public static void Main()
    {
      IPAddress ip = Dns.GetHostEntry("stun.l.google.com").AddressList[0];
      IPEndPoint ipep = new IPEndPoint(ip, 19302);
      Socket s = new Socket(ipep.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
      StunPacket sp = new StunPacket(StunPacket.ClassType.Request,
          StunPacket.MessageType.Binding, StunPacket.EMPTY_ATTRIBUTES);
      s.SendTo(sp.Data, ipep);
      EndPoint any = new IPEndPoint(IPAddress.Any, 0);
      byte[] data = new byte[600];
      int length = s.ReceiveFrom(data, ref any);
      new StunPacket(MemBlock.Reference(data, 0, length));
    }
  }
  */
}
