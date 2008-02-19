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
using Brunet.Dht;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Ipop {
  public class DhtDNS {
    public const string SUFFIX = ".ipop_vpn";
    protected IpopNode _node;
    protected readonly object _sync = new object();
    protected Cache dns_a = new Cache(100);
    protected Cache dns_ptr = new Cache(100);

    public DhtDNS(IpopNode node) {
      _node = node;
    }

    public void LookUp(object req_ippo) {
      IPPacket req_ipp = (IPPacket) req_ippo;
      UDPPacket req_udpp = new UDPPacket(req_ipp.Payload);
      DNSPacket dnspacket = new DNSPacket(req_udpp.Payload);
      ICopyable rdnspacket = null;
      try {
        string qname_response = String.Empty;
        string qname = dnspacket.Questions[0].QNAME;
        if(dnspacket.Questions[0].QTYPE == DNSPacket.TYPES.A) {
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
            dns_ptr[qname_response] = qname;
          }
        }
        else if(dnspacket.Questions[0].QTYPE == DNSPacket.TYPES.PTR) {
          qname_response = (string) dns_ptr[qname];
          if(qname_response == null) {
            throw new Exception("DNS PTR does not contain a record for " + qname);
          }
        }
        DNSPacket.Response response = new DNSPacket.Response(qname, dnspacket.Questions[0].QTYPE,
                                dnspacket.Questions[0].QCLASS, 1800, qname_response);
        DNSPacket res_packet = new DNSPacket(dnspacket.ID, false, dnspacket.OPCODE, true,
                                         dnspacket.Questions, new DNSPacket.Response[] {response});
        rdnspacket = res_packet.ICPacket;
      }
      catch(Exception e) {
        ProtocolLog.WriteIf(IpopLog.DNS, e.Message);
        rdnspacket = DNSPacket.BuildFailedReplyPacket(dnspacket);
      }
      UDPPacket res_udpp = new UDPPacket(req_udpp.DestinationPort,
                                         req_udpp.SourcePort, rdnspacket);
      IPPacket res_ipp = new IPPacket((byte) IPPacket.Protocols.UDP,
                 req_ipp.DestinationIP, req_ipp.SourceIP, res_udpp.ICPacket);
      EthernetPacket res_ep = new EthernetPacket(_node.MACAddress, EthernetPacket.UnicastAddress,
          EthernetPacket.Types.IP, res_ipp.ICPacket);
      _node.Ethernet.Send(res_ep.ICPacket);
    }

/* Tester ... remember to make the caches public and a remove ipopnode entries
    public static void Main() {
        Socket _s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        EndPoint ipep = new IPEndPoint(IPAddress.Any, 53);
        _s.Bind(ipep);
        DhtDNS dns = new DhtDNS();
        dns.dns_a["davidiw.pooper"] = "192.168.1.1";
        dns.dns_ptr["192.168.1.1"] = "davidiw.pooper";
        while(true) {
        byte[] packet = new byte[1000];
        EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
        int recv_count = _s.ReceiveFrom(packet, ref ep);
        byte[] small_packet = new byte[recv_count];
        Array.Copy(packet, 0, small_packet, 0, recv_count);
        packet = dns.LookUp(small_packet);
        _s.SendTo(packet, ep);
      }
    }
*/
  }
}
