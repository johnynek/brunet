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
using NetworkPackets;
using System;
using System.Collections;
using System.Collections.Generic;

/**
\namespace NetworkPacket::Dhcp
\brief Defines Dhcp Packets.
*/
namespace NetworkPackets.Dhcp {
  /**
  <summary>Encapsulates a Dhcp Packet in an immutable object providing both
  a byte array and a parsed version of the dhcp information</summary>
  <remarks>
  The outline of a Dhcp Packet:
  <code>
  0                   1                   2                   3
  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
  |     op (1)    |   htype (1)   |   hlen (1)    |   hops (1)    |
  +---------------+---------------+---------------+---------------+
  |                            xid (4)                            |
  +-------------------------------+-------------------------------+
  |           secs (2)            |           flags (2)           |
  +-------------------------------+-------------------------------+
  |                          ciaddr  (4)                          |
  +---------------------------------------------------------------+
  |                          yiaddr  (4)                          |
  +---------------------------------------------------------------+
  |                          siaddr  (4)                          |
  +---------------------------------------------------------------+
  |                          giaddr  (4)                          |
  +---------------------------------------------------------------+
  |                                                               |
  |                          chaddr  (16)                         |
  |                                                               |
  |                                                               |
  +---------------------------------------------------------------+
  |                                                               |
  |                          sname   (64)                         |
  +---------------------------------------------------------------+
  |                                                               |
  |                          file    (128)                        |
  +---------------------------------------------------------------+
  |                                                               |
  |                          options (variable)                   |
  +---------------------------------------------------------------+
  </code>
  <list type="table">
    <listheader>
      <term>Field</term>
      <description>Description</description>
    </listheader>
    <item>
      <term>OP</term>
      <description>1 for request, 2 for response</description>
    </item>
    <item>
      <term>htype</term>
      <description>hardware address type - leave at 1</description>
    </item>
    <item>
      <term>hlen</term>
      <description>hardware address length - 6 for ethernet mac address</description>
    </item>
    <item>
      <term>hops</term>
      <description>optional - leave at 0, no relay agents</description>
    </item>
    <item>
      <term>xid</term>
      <description>transaction id</description>
    </item>
    <item>
      <term>secs</term>
      <description>seconds since beginning renewal</description>
    </item>
    <item>
      <term>flags</term>
      <description></description></item>
    <item>
      <term>ciaddr</term>
      <description>clients currrent ip (client in bound, renew, or rebinding state)</description>
    </item>
    <item>
      <term>yiaddr</term>
      <description>ip address server is giving to client</description>
    </item>
    <item>
      <term>siaddr</term>
      <description>server address</description>
    </item>
    <item>
      <term>giaddr</term>
      <description>leave at zero, no relay agents</description>
    </item>
    <item>
      <term>chaddr</term>
      <description>client hardware address</description>
    </item>
    <item>
      <term>sname</term>
      <description>optional server hostname</description>
    </item>
    <item>
      <term>file</term>
      <description>optional</description>
    </item>
    <item>
      <term>magic cookie</term>
      <description>yuuuum! - byte[4] = {99, 130, 83, 99}</description>
    </item>
    <item>
      <term>options</term>
      <description>starts at 240!</description>
    </item>
  </list>
  </remarks>
  */
  public class DhcpPacket: DataPacket {
    /// <summary>The type of dhcp message.</summary>
    public enum MessageTypes {
      /// <summary>A node looking for an IP Address</summary>
      DISCOVER = 1,
      /// <summary>A response to a DISCOVER</summary>
      OFFER = 2,
      /// <summary>A node responding to an OFFER or renewing a lease</summary>
      REQUEST = 3,
      /// <summary>A node is rejecting the offer</summary>
      DECLINE = 4,
      /// <summary>A node is accepting a REQUEST</summary>
      ACK = 5,
      /// <summary>Clients lease is invalid</summary>
      NAK = 6,
      /// <summary>A node is cancelling a lease</summary>
      RELEASE = 7,
      /// <summary>A node updating its parameters</summary>
      INFORM = 8
    };

    /// <summary>An enum of commonly used Options</summary>
    public enum OptionTypes {
      /// <summary>The subnet mask for the IP Address</summary>
      SUBNET_MASK = 1,
      /// <summary>The default router address</summary>
      ROUTER = 3,
      /// <summary>Name server address</summary>
      NAME_SERVER = 5,
      /// <summary>Domain name server address</summary>
      DOMAIN_NAME_SERVER = 6,
      /// <summary>A hostname for the client (I haven't seen this work)</summary>
      HOST_NAME = 12,
      /// <summary>A domain name for the client</summary>
      DOMAIN_NAME = 15,
      /// <summary>Maximum packet size</summary>
      MTU = 26,
      /// <summary>A client may have an IP it prefers, it'd be here</summary>
      REQUESTED_IP = 50,
      /// <summary>The length of a dhcp lease.</summary>
      LEASE_TIME = 51,
      /// <summary>Type of a Dhcp message.</summary>
      MESSAGE_TYPE = 53,
      /// <summary>The IP of the responding server, use a.b.c.1</summary>
      SERVER_ID = 54,
      /// <summary>A list of parameters the node would like.</summary>
      PARAMETER_REQUEST_LIST = 55
    };

    /// <summary>1 for boot request, 2 for boot response</summary>
    public readonly byte op;
    /// <summary>unique packet id</summary>
    public readonly MemBlock xid;
    /// <summary>clients current ip address</summary>
    public readonly MemBlock ciaddr;
    /// <summary>ip address server is giving to the client</summary>
    public readonly MemBlock yiaddr;
    /// <summary>server address</summary>
    public readonly MemBlock siaddr;
    /// <summary>clients hardware address</summary>
    public readonly MemBlock chaddr;
    /// <summary>A hashtable indexed by OptionTypes and numbers of options</summary>
    public readonly Dictionary<OptionTypes, MemBlock> Options;
    /// <summary>Embedded right before the options.</summary>
    public static readonly MemBlock magic_key = 
        MemBlock.Reference(new byte[4] {99, 130, 83, 99});

    /**
    <summary>Parse a MemBlock packet into a Dhcp Packet</summary>
    <param name="Packet">The dhcp packet to parse</param>
    */
    public DhcpPacket(MemBlock Packet) {
      if(Packet.Length < 240) {
        throw new Exception("Invalid Dhcp Packet:  Length < 240.");
      }

      _packet = Packet;
      op = Packet[0];
      int hlen = Packet[2];
      xid = Packet.Slice(4, 4);
      ciaddr = Packet.Slice(12, 4);
      yiaddr = Packet.Slice(16, 4);
      siaddr = Packet.Slice(20, 4);
      chaddr = Packet.Slice(28, hlen);
      MemBlock key = Packet.Slice(236, 4);
      if(!key.Equals(magic_key)) {
        throw new Exception("Invalid Dhcp Packet: Invalid magic key.");
      }
      int idx = 240;

      /* Parse the options */
      Options = new Dictionary<OptionTypes, MemBlock>();
      /*  255 is end of options */
      while(Packet[idx] != 255) {
        /* 0 is padding */
        if(Packet[idx] != 0) {
          OptionTypes type = (OptionTypes) Packet[idx++];
          byte length = Packet[idx++];
          Options[type] = Packet.Slice(idx, length);
          idx += length;
        }
        else {
          idx++;
        }
      }
    }

    public DhcpPacket(byte op, MemBlock xid, MemBlock ciaddr, MemBlock yiaddr,
                     MemBlock siaddr, MemBlock chaddr, Dictionary<OptionTypes, MemBlock> Options) {
      this.op = op;
      this.xid = xid;
      this.ciaddr = ciaddr;
      this.yiaddr = yiaddr;
      this.siaddr = siaddr;
      this.chaddr = chaddr;
      this.Options = Options;

      byte[] header = new byte[240];
      header[0] = op;
      header[1] = 1;
      header[2] = (byte) chaddr.Length;
      header[3] = 0;

      xid.CopyTo(header, 4);
      for(int i = 8; i < 12; i++) {
        header[i] = 0;
      }
      ciaddr.CopyTo(header, 12);
      yiaddr.CopyTo(header, 16);
      siaddr.CopyTo(header, 20);
      for(int i = 24; i < 28; i++) {
        header[i] = 0;
      }
      chaddr.CopyTo(header, 28);
      for(int i = 34; i < 236; i++) {
        header[i] = 0;
      }
      magic_key.CopyTo(header, 236);

      _icpacket = new CopyList(MemBlock.Reference(header));
      foreach(KeyValuePair<OptionTypes, MemBlock> kvp in Options) {
        MemBlock value = kvp.Value;

        byte[] tmp = new byte[2];
        tmp[0] = (byte) kvp.Key;
        tmp[1] = (byte) value.Length;

        _icpacket = new CopyList(_icpacket, MemBlock.Reference(tmp), value);
      }
      byte []end = new byte[1]{255}; /* End of Options */
      _icpacket = new CopyList(_icpacket, MemBlock.Reference(end));
    }
  }
}
