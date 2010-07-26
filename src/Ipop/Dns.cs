using Brunet;
using Brunet.Applications;
using Brunet.Util;
using NetworkPackets;
using NetworkPackets.Dns;
using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;

namespace Ipop {
  /**
  <summary>A basic abstract Dns server.  Must implement the translation process
  using NameLookUp and AddressLookUp.</summary>
  */
  public abstract class Dns {
    /// <summary>The base ip address to perfom lookups on</summary>
    protected MemBlock _base_address;
    /// <summary>The mask for the ip address to perform lookups on.</summary>
    protected MemBlock _netmask;
    /// <summary>Dns Server </summary>
    protected EndPoint _name_server; 
    /// <summary>Is true if IPOP is asked to forward Dns queries to external nameserver </summary>
    protected bool _forward_queries;
    /// <summary>Domain name is:</summary>
    public static string DomainName = "ipop";

    /// <summary>Default constructor</summary>
    /// <param name="ip_address">An IP Address in the range.</param>
    /// <param name="netmask">The netmask for the range.</param>
    /// <param name="name_server">The external name server to be queried.</param>
    /// <param name="forward_queries">Set if queries are to be forwarded to external name server.</param>
    public Dns(MemBlock ip_address, MemBlock netmask, string name_server,
        bool forward_queries)
    {
      if(forward_queries) {
        if(name_server == null || name_server == string.Empty) {
          // GoogleDns
          name_server = "8.8.8.8";
        }

        _name_server = new IPEndPoint(IPAddress.Parse(name_server), 53);
      }
        
      _forward_queries = forward_queries;
      _netmask = netmask;

      byte[] ba = new byte[ip_address.Length];
      for(int i = 0; i < ip_address.Length; i++) {
        ba[i] = (byte) (ip_address[i] & netmask[i]);
      }
      _base_address = MemBlock.Reference(ba);
    }

    /// <summary>Look up a hostname given a Dns request in the form of IPPacket
    /// </summary>
    /// <param name="in_ip">An IPPacket containing the Dns request</param>
    /// <returns>An IPPacket containing the results</returns>
    public virtual IPPacket LookUp(IPPacket in_ip)
    {
      UdpPacket in_udp = new UdpPacket(in_ip.Payload);
      DnsPacket in_dns = new DnsPacket(in_udp.Payload);
      ICopyable out_dns = null;
      string qname = string.Empty;
      bool invalid_qtype = false;

      try {
        string qname_response = String.Empty;
        qname = in_dns.Questions[0].QName;
        DnsPacket.Types type = in_dns.Questions[0].QType;

        if(type == DnsPacket.Types.A || type == DnsPacket.Types.AAAA) {
          qname_response = AddressLookUp(qname);
        } else if(type == DnsPacket.Types.Ptr) {
          qname_response = NameLookUp(qname);
        } else {
          invalid_qtype = true;
        }

        if(qname_response == null) {
          throw new Exception("Unable to resolve");
        }

        Response response = new Response(qname, in_dns.Questions[0].QType,
            in_dns.Questions[0].QClass, 1800, qname_response);
        //Host resolver will not accept if recursive is not available 
        //when it is desired
        DnsPacket res_packet = new DnsPacket(in_dns.ID, false,
            in_dns.Opcode, true, in_dns.RD, in_dns.RD,
            in_dns.Questions, new Response[] {response}, null, null);

        out_dns = res_packet.ICPacket;
      } catch(Exception e) {
        bool failed_resolve = false;
        // The above resolver failed, let's see if another resolver works
        if(_forward_queries) {
          try {
            out_dns = Resolve(_name_server, (byte[]) in_dns.Packet);
          } catch(Exception ex) {
            e = ex;
            failed_resolve = true;
          }
        }

        if(!_forward_queries || failed_resolve) {
          ProtocolLog.WriteIf(IpopLog.Dns, "Failed to resolve: " + qname + "\n\t" + e.Message);
          out_dns = DnsPacket.BuildFailedReplyPacket(in_dns, !invalid_qtype);
        }
      }

      UdpPacket out_udp = new UdpPacket(in_udp.DestinationPort,
                                         in_udp.SourcePort, out_dns);
      return new IPPacket(IPPacket.Protocols.Udp, in_ip.DestinationIP,
                                       in_ip.SourceIP,
                                       out_udp.ICPacket);
    }

    /// <summary>Determines if an IP Address is in  the applicable range for
    /// the Dns server</summary>
    /// <param name="IP">The IP Address to test.</param>
    /// <returns>False if the IP Address or netmask is undefined or the Address
    /// is not in applicable range, True if it is.</returns>
    protected bool InRange(String IP)
    {
      if(_base_address == null || _netmask == null) {
        return false;
      }
      byte[] ipb = Utils.StringToBytes(IP, '.');
      for(int i = 0; i < 4; i++) {
        if((ipb[i] & _netmask[i]) != _base_address[i]) {
          return false;
        }
      }
      return true;
    }

    /// <summary>
    /// Sends Dns query to Dns Server and returns the response. 
    /// </summary>
    /// <param name="dns_server">The IPEndPoint of the Dns Server 
    /// <param name="request"> Dns Packet to be sent</param>
    /// <returns></returns>
    public MemBlock Resolve(EndPoint server, byte[] request)
    {
      Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
      socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 1000);
      socket.Connect(server);
      socket.Send(request, request.Length, SocketFlags.None);

      MemBlock response = null;
      try {
        byte[] tmp = new byte[512];
        int  length = socket.Receive(tmp);
        response = MemBlock.Reference(tmp, 0, length);
      } finally {
        socket.Close();
      }

      // Is this a response to our request?
      if ((response[0] != request[0]) || (response[1] != request[1])) {
        throw new Exception("Invalid response");
      }

      return response;
    }

    /// <summary>Called during LookUp to perform translation from hostname to IP</summary>
    /// <param name="name">The name to lookup</param>
    /// <returns>The IP Address or null if none exists for the name</returns>
    public abstract String AddressLookUp(String name);

    /// <summary>Called during LookUp to perfrom a translation from IP to hostname.</summary>
    /// <param name="IP">The IP to look up.</param>
    /// <returns>The name or null if none exists for the IP.</returns>
    public abstract String NameLookUp(String IP);

  }
}
