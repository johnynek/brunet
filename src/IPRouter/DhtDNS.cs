using Brunet;
using Brunet.Dht;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Ipop {
  public class DhtDNS {
    protected IpopNode _node;
    object _sync = new object();
    Cache dns_a = new Cache(100);
    Cache dns_ptr = new Cache(100);

    public DhtDNS(IpopNode node) {
      _node = node;
    }

/*    public static void Main() {
      Socket _s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
      EndPoint ipep = new IPEndPoint(IPAddress.Any, 53);
      _s.Bind(ipep);
      while(true) {
        byte[] packet = new byte[1000];
        EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
        int recv_count = _s.ReceiveFrom(packet, ref ep);
        byte[] small_packet = new byte[recv_count];
        Array.Copy(packet, 0, small_packet, 0, recv_count);
        packet = LookUp(small_packet);
        _s.SendTo(packet, ep);
      }
    }*/

    public void LookUp(object req_ippo) {
      IPPacket req_ipp = (IPPacket) req_ippo;
      UDPPacket req_udpp = new UDPPacket(req_ipp.Payload);
      MemBlock res_payload = null;
      try {
        int qtype = 0;
        string qname = ReadRequestPacket(req_udpp.Payload, out qtype);
        string qname_response = String.Empty;

        if(qtype == DNS_QTYPE_A) {
          qname_response = (string) dns_a[qname];
          if(qname_response == null) {
            try {
              qname_response = _node.Dht.Get(qname)[0].valueString;
            }
            catch {
              throw new Exception("Dht does not contain a record for " + qname);
            }
          }
          lock(_sync) {
            dns_ptr[qname_response + DNS_SUFFIX] = qname;
          }
        }
        else if(qtype == DNS_QTYPE_PTR) {
          qname_response = (string) dns_ptr[qname];
          if(qname_response == null) {
            throw new Exception("DNS PTR does not contain a record for " + qname);
          }
        }
        res_payload = BuildReplyPacket(req_udpp.Payload, qname_response, qtype);
      }
      catch(Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, e.ToString());
        res_payload = BuildFailedReplyPacket(req_udpp.Payload);
      }
      UDPPacket res_udpp = new UDPPacket(req_udpp.DestinationPort,
                                         req_udpp.SourcePort, res_payload);
      IPPacket res_ipp = new IPPacket((byte) IPPacket.Protocols.UDP,
                 req_ipp.DestinationIP, req_ipp.SourceIP, res_udpp.ICPacket);
      _node.Ether.Write(res_ipp.ICPacket, EthernetPacket.Types.IP, _node.MAC);
    }

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

  ID - identification - client generated, do not change
  QR - query / reply, client sends 0, server replies 1
  Opcode - 0 for query
  AA - Authoritative answer - if we find a response in the dht
  TC - Truncation - ignored - 0
  RD - Recursion desired - unimplemented - 0
  RA - Recursion availabled - unimplemented - 0
  Z - Reserved - must be 0
  RCODE - ignored, stands for error code - 0
  QDCOUNT - questions - should be 1
  ANCOUNT - answers - should be 0 until we answer!
  NSCOUNT - name server records
  ARCOUNT - additional records - unsupported
  */
    public static readonly int DNS_QCLASS_IN = 1;
    public static readonly int DNS_QTYPE_A = 1;
    public static readonly int DNS_QTYPE_PTR = 12;
    public static readonly string DNS_SUFFIX = ".ipop_dns";

    public static string ReadRequestPacket(byte[] packet, out int qtype) {
      byte qr = (byte) ((packet[2] & 0x80) >> 7);
      byte opcode = (byte) ((packet[2] & 0x78) >> 3);
      int qdcount = (packet[4] << 8) + packet[5];
      int ancount = (packet[6] << 8) + packet[7];
      int nscount = (packet[8] << 8) + packet[9];
      int arcount = (packet[10] << 8) + packet[11];

      if((qr != 0) || (opcode != 0) || (qdcount != 1) || (ancount != 0) ||
          (nscount != 0) || (arcount != 0)) {
        throw new Exception("Invalid request!");
      }

      string qname = String.Empty;
      int current = 12;
      // The format is length data length data ... where data is split by '.'
      while(packet[current] != 0) {
        byte length = packet[current++];
        for(int i = 0; i < length; i++) {
          qname += (char) packet[current++];
        }
        if(packet[current] != 0) {
          qname += ".";
        }
      }
      current++;

      qtype = (packet[current++] << 8) + packet[current++];
      if(qtype != DNS_QTYPE_A && qtype != DNS_QTYPE_PTR) {
        throw new Exception("Invalid DNS_QTYPE " + qtype);
      }
      int qclass = (packet[current++] << 8) + packet[current];
      if(qclass != DNS_QCLASS_IN) {
        throw new Exception("Invalid DNS_QCLASS " + qclass);
      }

      if(qtype == DNS_QTYPE_PTR) {
        string[] res = qname.Split('.');
        qname = string.Empty;
        /* The last 2 parts are the pointer IN-ADDR.ARPA, the rest is 
           reverse notation
         */
        for(int i = res.Length - 3; i > 0; i--) {
          qname += res[i] + ".";
        }
        qname += res[0] + DNS_SUFFIX;
      }
      else if(qtype == DNS_QTYPE_A) {
        string[] res = qname.Split('.');
        qname = res[0] + "." + res[1];
        if(!qname.EndsWith(DNS_SUFFIX)) {
          throw new Exception("Invalid DNS name: " + qname);
        }
      }
      return qname;
    }

/*
    Build a response record!

    1  1  1  1  1  1
    0  1  2  3  4  5  6  7  8  9  0  1  2  3  4  5
    +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
    |                      NAME                     |
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
    Name - a pointer to the qname in the original packet
    Type - same as qtype
    Class - same as qclass
    TTL - time that the entry is valid for
    RDLength - Length of RDATA
    RDATA - Response!
*/
    public static byte[] BuildReplyPacket(byte[] packet, string response, int type) {
      // base + response.length + null (eos)
      ArrayList rpacket = new ArrayList(packet);
      // Authoritative Answer
      rpacket[2] = (byte) ((byte) rpacket[2] | 0x84);
      // Answer!
      rpacket[7] = (byte) 1;
      // Authentication
      rpacket[3] = (byte) ((byte) rpacket[3] | 0x20);
      // Name - pointer qname
      rpacket.Add((byte) 0xC0);
      rpacket.Add((byte) 12);
      // Type
      rpacket.Add((byte) ((type >> 8) & 0xFF));
      rpacket.Add((byte) (type & 0xFF));
      // Class
      rpacket.Add((byte) ((DNS_QCLASS_IN >> 8) & 0xFF));
      rpacket.Add((byte) (DNS_QCLASS_IN & 0xFF));
      // TTL - 30 Minutes
      rpacket.Add((byte) 0x00);
      rpacket.Add((byte) 0x00);
      rpacket.Add((byte) 0x07);
      rpacket.Add((byte) 0x08);
      // RDLength and RDATA
      byte[] resb = ConvertResponseToByteArray(response, type);
      rpacket.Add((byte) ((resb.Length >> 8) & 0xFF));
      rpacket.Add((byte) (resb.Length & 0xFF));
      for(int i = 0; i < resb.Length; i++) {
        rpacket.Add(resb[i]);
      }
      return (byte[]) rpacket.ToArray(typeof(byte));
    }

    public static byte[] ConvertResponseToByteArray(string response, int type) {
      byte[] res = new byte[1]{0};
      // The format is length data length data ... where data is split by '.'
      if(type == DNS_QTYPE_PTR) {
        string[] pieces = response.Split('.');
        res = new byte[response.Length + 2];
        int c = 0;
        for(int i = 0; i < pieces.Length; i++) {
          char[] res_c = pieces[i].ToCharArray();
          res[c++] = (byte) res_c.Length;
          for(int j = 0; j < res_c.Length; j++) {
            res[c++] = (byte) ((int) res_c[j] & 0xFF);
          }
        }
        res[c] = 0;
      }
      else if(type == DNS_QTYPE_A) {
        string []bytes = response.Split('.');
        res = new byte[4];
        for(int i = 0; i < bytes.Length; i++) {
          res[i] = Byte.Parse(bytes[i]);
        }
      }
      return res;
    }

    public static byte[] BuildFailedReplyPacket(byte[] packet) {
      byte[] res = new byte[packet.Length];
      packet.CopyTo(res, 0);
      res[3] |= 5;
      res[2] |= 0x80;
      return res;
    }
  }
}