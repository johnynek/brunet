using Brunet;
using System;
using System.Collections;

namespace Ipop {
  public abstract class DNS {
    public virtual String SUFFIX { get { return String.Empty; } } /**< If for
      some cases you don't want to bother continuing the lookup, specify a 
      suffix (ie domain name). */
    protected volatile Hashtable dns_a; /**< Maps names to IP Addresses */
    protected volatile Hashtable dns_ptr; /**< Maps IP Addresses to names */
    protected Object _sync;

    public DNS() {
      _sync = new Object();
      dns_a = new Hashtable();
      dns_ptr = new Hashtable();
    }

    /**
     * Look up a hostname given a DNS request in the form of IPPacket
     * @param req_ipp an IPPacket containing the DNS request
     * @return returns an IPPacket containing the results
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
      return res_ipp;
    }

    /**
     * Optionally implement this, if you want 
     * @param qname the string name to lookup
     * @return the string result of the lookup, throw an exception on failure!
     */
    public virtual String UnresolvedName(String qname) {
      throw new Exception("Unable to resolve name: " + qname);
    }
  }
}
