using Brunet;
using NetworkPackets;
using NetworkPackets.DNS;
using System;
using System.Collections;

namespace Ipop {
  /**
  <summary>A basic abstract DNS server.  Must implement the translation process
  using NameLookUp and AddressLookUp.</summary>
  */
  public abstract class DNS {
    /**
    <summary>Look up a hostname given a DNS request in the form of IPPacket
    </summary>
    <param name="req_ipp">An IPPacket containing the DNS request</param>
    <returns>An IPPacket containing the results</returns>
    */
    public virtual IPPacket LookUp(IPPacket req_ipp) {
      UDPPacket req_udpp = new UDPPacket(req_ipp.Payload);
      DNSPacket dnspacket = new DNSPacket(req_udpp.Payload);
      ICopyable rdnspacket = null;
      try {
        string qname_response = String.Empty;
        string qname = dnspacket.Questions[0].QNAME;
        if(dnspacket.Questions[0].QTYPE == DNSPacket.TYPES.A) {
          qname_response = AddressLookUp(qname);
        }
        else if(dnspacket.Questions[0].QTYPE == DNSPacket.TYPES.PTR) {
          qname_response = NameLookUp(qname);
        }
        if(qname_response == null) {
          throw new Exception("Unable to resolve name: " + qname);
        }
        Response response = new Response(qname, dnspacket.Questions[0].QTYPE,
          dnspacket.Questions[0].QCLASS, 1800, qname_response);

        // For some reason, if RD is set and we don't have RA it Linux `host`
        // doesn't work!
        DNSPacket res_packet = new DNSPacket(dnspacket.ID, false,
          dnspacket.OPCODE, true, dnspacket.RD, dnspacket.RD,
          dnspacket.Questions, new Response[] {response}, null, null);

        rdnspacket = res_packet.ICPacket;
      }
      catch(Exception e) {
        ProtocolLog.WriteIf(IpopLog.DNS, e.Message);
        Console.WriteLine(e);
        rdnspacket = DNSPacket.BuildFailedReplyPacket(dnspacket);
      }
      UDPPacket res_udpp = new UDPPacket(req_udpp.DestinationPort,
                                         req_udpp.SourcePort, rdnspacket);
      IPPacket res_ipp = new IPPacket(IPPacket.Protocols.UDP,
                                       req_ipp.DestinationIP,
                                       req_ipp.SourceIP,
                                       res_udpp.ICPacket);
      return res_ipp;
    }

    /**
    <summary>Called during LookUp to perform translation from hostname to IP</summary>
    <param name="name">The name to lookup</param>
    <returns>The IP Address or null if none exists for the name</returns>
    */
    public abstract String AddressLookUp(String name);

    /**
    <summary>Called during LookUp to perfrom a translation from IP to hostname.</summary>
    <param name="IP">The IP to look up.</param>
    <returns>The name or null if none exists for the IP.</returns>
    */
    public abstract String NameLookUp(String IP);
  }
}
