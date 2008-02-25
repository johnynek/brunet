using Brunet;
using NetworkPackets;
using NetworkPackets.DNS;
using System;
using System.Collections;

namespace Ipop {
  /**
  <summary>A basic abstract DNS server.  Must implement a way for entries
  to get into the lookup tables.</summary>
  */
  public abstract class DNS {
    /**  <summary>If for some cases you don't want to bother continuing the
    lookup, specify a suffix (ie domain name).</summary>*/
    public virtual String SUFFIX { get { return String.Empty; } }
    /// <summary>Maps names to IP Addresses</summary>
    protected volatile Hashtable dns_a;
    /// <summary>Maps IP Addresses to names</summary>
    protected volatile Hashtable dns_ptr;
    /// <summary>lock object to make this class thread safe</summary>
    protected Object _sync;

    /// <summary>Base constructor initializing base variables</summary>
    public DNS() {
      _sync = new Object();
      dns_a = new Hashtable();
      dns_ptr = new Hashtable();
    }

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
          qname_response = (string) dns_a[qname];
          if(qname_response == null && qname.EndsWith(SUFFIX)) {
            qname_response = UnresolvedName(qname);
          }
          if(qname_response == null) {
            throw new Exception("Unable to resolve name: " + qname);
          }
          lock(_sync) {
            dns_ptr[qname_response] = qname;
          }
        }
        else if(dnspacket.Questions[0].QTYPE == DNSPacket.TYPES.PTR) {
          qname_response = (string) dns_ptr[qname];
          if(qname_response == null) {
            throw new Exception("DNS PTR does not contain a record: " + qname);
          }
        }
        Response response = new Response(qname, dnspacket.Questions[0].QTYPE,
            dnspacket.Questions[0].QCLASS, 1800, qname_response);
        DNSPacket res_packet = new DNSPacket(dnspacket.ID, false, dnspacket.OPCODE, true,
                                             dnspacket.Questions, new Response[] {response});
        rdnspacket = res_packet.ICPacket;
      }
      catch(Exception e) {
        ProtocolLog.WriteIf(IpopLog.DNS, e.Message);
        rdnspacket = DNSPacket.BuildFailedReplyPacket(dnspacket);
      }
      UDPPacket res_udpp = new UDPPacket(req_udpp.DestinationPort,
                                         req_udpp.SourcePort, rdnspacket);
      IPPacket res_ipp = new IPPacket(IPPacket.Protocols.UDP,
                                       req_ipp.DestinationIP, req_ipp.SourceIP, res_udpp.ICPacket);
      return res_ipp;
    }

    /**
    <summary>Optionally implement this, if you want to have entries looked up 
    through another method if the local hashtable is missing an entry</summary>
    <param name="qname"> the string name to lookup</param>
    <returns> the string result of the lookup, throw an exception on failure!
    </returns>
    */
    public virtual String UnresolvedName(String qname) {
      throw new Exception("Unable to resolve name: " + qname);
    }
  }
}
