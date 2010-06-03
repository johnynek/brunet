/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida
                    Pierre St Juste <ptony82@ufl.edu>, University of Florida   

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
using Brunet.Applications;
using Brunet.Symphony;
using Brunet.Util;
using NetworkPackets;
using NetworkPackets.Dns;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

#if ManagedIpopNodeNUNIT
using NUnit.Framework;
#endif

namespace Ipop.Managed {
  /// <summary>
  /// This class implements Dns, IAddressResolver, IManagedHandler, and
  /// ITranslator. It provides most functionality needed by ManagedIpopNode.
  /// </summary>
  public class ManagedAddressResolverAndDns : Dns, IAddressResolver, ITranslator {
    /// <summary>The node to do ping checks on.</summary>
    protected StructuredNode _node;
    /// <summary>Contains ip:hostname mapping.</summary>
    protected Hashtable _dns_a;
    /// <summary>Contains hostname:ip mapping.</summary>
    protected Hashtable _dns_ptr;
    /// <summary>Maps MemBlock IP Addresses to Brunet Address as Address</summary>
    protected Hashtable _ip_addr;
    /// <summary>Maps Brunet Address as Address to MemBlock IP Addresses</summary>
    protected Hashtable _addr_ip;
    /// <summary>Keeps track of blocked addresses</summary>
    protected Hashtable _blocked_addrs;
    /// <summary>MemBlock of the IP mapped to local node</summary>
    protected MemBlock _local_ip;
    /// <summary>Helps assign remote end points</summary>
    protected DhcpServer _dhcp;
    /// <summary>Array list of multicast addresses</summary>
    public ArrayList mcast_addr;
    protected object _sync;

    /// <summary>
    /// Constructor for the class, it initializes various objects
    /// </summary>
    /// <param name="node">Takes in a structured node</param>
    public ManagedAddressResolverAndDns(StructuredNode node, DhcpServer dhcp,
        MemBlock local_ip, string name_server, bool forward_queries) :
      base(MemBlock.Reference(dhcp.BaseIP), MemBlock.Reference(dhcp.Netmask),
          name_server, forward_queries)
    {
      _node = node;
      _dns_a = new Hashtable();
      _dns_ptr = new Hashtable();
      _ip_addr = new Hashtable();
      _addr_ip = new Hashtable();
      _blocked_addrs = new Hashtable();
      mcast_addr = new ArrayList();

      _dhcp = dhcp;
      _local_ip = local_ip;
      _sync = new object();
    }

    // Return string of localIP
    public string LocalIP {
      get { return Utils.MemBlockToString(_local_ip, '.'); }
    }

    /// <summary>
    /// This method does an inverse lookup for the Dns
    /// </summary>
    /// <param name="IP">IP address of the name that's being looked up</param>
    /// <returns>Returns the name as string of the IP specified</returns>
    public override String NameLookUp(String ip) {
      return (String)_dns_ptr[ip];
    }

    /// <summary>
    /// This method does an address lookup on the Dns
    /// </summary>
    /// <param name="Name">Takes in name as string to lookup</param>
    /// <returns>The result as a String Ip address</returns>
    public override String AddressLookUp(String name) {
      return (String)_dns_a[name];
    }

    /**
    <summary>Implements the ITranslator portion for ManagedAddress..., takes an
    IP Packet, based upon who the originating Brunet Sender was, changes who
    the packet was sent from and then switches the destination address to the
    local nodes address</summary>
    <param name="packet">The IP Packet to translate.</param>
    <param name="from">The Brunet address the packet was sent from.</param>
    <returns>The translated IP Packet.</returns>
    */
    public MemBlock Translate(MemBlock packet, Address from) {
      MemBlock source_ip = (MemBlock) _addr_ip[from];
      if(source_ip == null) {
        throw new Exception("Invalid mapping " + from + ".");
      }

      // Attempt to translate a MDns packet
      IPPacket ipp = new IPPacket(packet);
      MemBlock hdr = packet.Slice(0,12);
      bool fragment = ((packet[6] & 0x1F) | packet[7]) != 0;

      if(ipp.Protocol == IPPacket.Protocols.Udp && !fragment) {
        UdpPacket udpp = new UdpPacket(ipp.Payload);
        // MDns runs on 5353
        if(udpp.DestinationPort == 5353) {
          DnsPacket dnsp = new DnsPacket(udpp.Payload);
          String ss_ip = DnsPacket.IPMemBlockToString(source_ip);
          bool change = mDnsTranslate(dnsp.Answers, ss_ip);
          change |= mDnsTranslate(dnsp.Authority, ss_ip);
          change |= mDnsTranslate(dnsp.Additional, ss_ip);
          // If we make a change let's make a new packet!
          if(change) {
            dnsp = new DnsPacket(dnsp.ID, dnsp.Query, dnsp.Opcode, dnsp.AA,
                                 dnsp.RA, dnsp.RD, dnsp.Questions, dnsp.Answers,
                                 dnsp.Authority, dnsp.Additional);
            udpp = new UdpPacket(udpp.SourcePort, udpp.DestinationPort,
                                 dnsp.ICPacket);
            ipp = new IPPacket(ipp.Protocol, source_ip, ipp.DestinationIP,
                               hdr, udpp.ICPacket);
            return ipp.Packet;
          }
        }
        else if(udpp.DestinationPort >= 5060 && udpp.DestinationPort < 5100) {
          udpp = SIPTranslate(udpp, source_ip, ipp.SSourceIP,
                              ipp.SDestinationIP);
          ipp = new IPPacket(ipp.Protocol, source_ip, _local_ip, hdr,
                             udpp.ICPacket);
          return ipp.Packet;
        }
      }
      return IPPacket.Translate(packet, source_ip, _local_ip);
    }

    /**
    <summary>Translates mDns RRs, used on Answer and Additional RRs.</summary>
    <param name="responses">An array containing RRs to translate.</param>
    <param name="ss_ip">The defined source ip from the remote end point.</param>
    <returns>True if there was a translation, false otherwise.</returns>
    */
    public static bool mDnsTranslate(Response[] responses, String ss_ip) {
      bool change = false;
      for(int i = 0; i < responses.Length; i++) {
        if(responses[i].Type == DnsPacket.Types.A) {
          change = true;
          Response old = responses[i];
          responses[i] = new Response(old.Name, old.Type, old.Class,
                                         old.CacheFlush, old.Ttl, ss_ip);
        }
        else if(responses[i].Type == DnsPacket.Types.Ptr) {
          Response old = responses[i];
          if(DnsPacket.StringIsIP(old.Name)) {
            responses[i] = new Response(ss_ip, old.Type,  old.Class,
                                        old.CacheFlush, old.Ttl, old.RData);
            change = true;
          }
        }
      }
      return change;
    }

    /// <summary>
    /// Check to see if it's a SIP packet and translates
    /// </summary>
    /// <param name="payload">Udp payload</param>
    /// <param name="source_ip">New source IP</param>
    /// <param name="old_ss_ip">Old source IP</param>
    /// <param name="old_sd_ip">Old destination IP</param>
    /// <returns>Returns a Udp packet</returns>
    public UdpPacket SIPTranslate(UdpPacket udpp, MemBlock source_ip, 
                                    string old_ss_ip, string old_sd_ip) {
      string new_ss_ip = Utils.MemBlockToString(source_ip, '.');
      string new_sd_ip = Utils.MemBlockToString(_local_ip, '.'); 
      string packet_id = "SIP/2.0";
      MemBlock payload = ManagedNodeHelper.TextTranslate(udpp.Payload, old_ss_ip,
                                             old_sd_ip, new_ss_ip, new_sd_ip,
                                             packet_id); 
      return new UdpPacket(udpp.SourcePort, udpp.DestinationPort, payload);
    }

    /// <summary>
    /// Returns the Brunet address given an IP
    /// </summary>
    /// <param name="IP">A MemBlock of the IP</param>
    /// <returns>A brunet Address for the IP</returns>
    public Address Resolve(MemBlock IP) {
      return (Address)_ip_addr[IP];
    }

    public bool Check(MemBlock ip, Address addr) {
      return _addr_ip[addr].Equals(ip) && _ip_addr[ip].Equals(addr);
    }

    /// <summary>
    /// Registers a name and addr combination and returns an IP.  Idempotent
    /// and changes the hostname if one already exists.
    /// </summary>
    /// <param name="name">A string name to be added to Dns</param>
    /// <param name="addr">A brunet address that is to be mapped</param>
    public string RegisterMapping(String name, Address addr) {
      String ips = null;
      lock(_sync) {
        MemBlock ip = (MemBlock) _addr_ip[addr];
        string ip_dns = (String)_dns_a[name];

        if(ip == null && _blocked_addrs.Contains(addr)) {
          ip = (MemBlock) _blocked_addrs[addr];
          _blocked_addrs.Remove(addr);
          _addr_ip.Add(addr, ip);
          _ip_addr.Add(ip, addr);
          mcast_addr.Add(addr);
        }

        if(ip != null) {
          ips = Utils.MemBlockToString(ip, '.');
        }

        // either both null, the same value, or ip_dns isn't set
        if(ips != ip_dns && ip_dns != null) {
          throw new Exception(String.Format
            ("Name ({0}) already exists with different address.", name));
        } else if(ips == null) {
          do {
            ip = MemBlock.Reference(_dhcp.RandomIPAddress());
          } while (_ip_addr.ContainsValue(ip));
          ips = Utils.MemBlockToString(ip, '.');
          _addr_ip.Add(addr, ip);
          _ip_addr.Add(ip, addr);
          mcast_addr.Add(addr);
        }

        // set the dns name only once!
        if(ip_dns == null) {
          // We don't support multiple hostnames per ID
          if(_dns_ptr.Contains(ips)) {
            _dns_a.Remove(_dns_ptr[ips]);
            _dns_ptr.Remove(ips);
          }
          _dns_a.Add(name, ips);
          _dns_ptr.Add(ips, name);
        }
      }
      return ips;
    }

    /// <summary>
    /// Unregisters a name and potentially ip and address mapping
    /// </summary>
    /// <param name="name">A string name that needs to be removed</param>
    /// <returns>true if successful</returns>
    public bool UnregisterMapping(String name) {
      lock(_sync) {
        if (!_dns_a.Contains(name)) {
          throw new Exception(String.Format("Name ({0}) does not exists", name));
        }

        String ips = (String)_dns_a[name];
        MemBlock  ip = MemBlock.Reference(Utils.StringToBytes(ips, '.'));

        _dns_a.Remove(name);
        _dns_ptr.Remove(ips);
        Address addr = (Address)_ip_addr[ip];
        _ip_addr.Remove(ip);
        _addr_ip.Remove(addr);
        _blocked_addrs.Add(addr,ip);
        mcast_addr.Remove(addr);
      }
      return true;
    }

    /// <summary>
    /// Sets up Dns for localhost
    /// </summary>
    /// <param name="name">The Dns alias for the localhost</param>
    /// <returns>true if successful</returns>
    public bool MapLocalDns(string name) {
      _dns_a.Add(name, LocalIP);
      _dns_ptr.Add(LocalIP, name);
      return true;
    }

    /// <summary>
    /// Maps IP address to Brunet address.
    /// </summary>
    /// <param name="ip">IP address to map</param>
    /// <param name="addr">Brunet address to map</param>
    /// <returns>IP string of the allocated IP</returns>
    public string AddIPMapping(string ip, Address addr) {
      MemBlock ip_bytes;

      lock(_sync) {
        if(ip == null || ip == String.Empty) {
          do {
            ip_bytes = MemBlock.Reference(_dhcp.RandomIPAddress());
          } while (_ip_addr.ContainsValue(ip_bytes));
        }
        else {
          ip_bytes = MemBlock.Reference(Utils.StringToBytes(ip, '.'));
          if (!_dhcp.ValidIP(ip_bytes)) {
            throw new Exception("Invalid IP");
          }
        }

        if (_ip_addr.ContainsValue(addr) || _addr_ip.ContainsValue(ip_bytes)) {
          throw new Exception("IP/P2P address is already found");
        }

        _ip_addr.Add(ip_bytes, addr);
        _addr_ip.Add(addr, ip_bytes);
        mcast_addr.Add(addr);
      }
      return Utils.BytesToString(ip_bytes, '.');
    }

    /// <summary>
    /// Remove IP to Brunet address mapping.
    /// </summary>
    /// <param name="ip">IP address to remove</param>
    public void RemoveIPMapping(string ip) {
      MemBlock ip_bytes = MemBlock.Reference(Utils.StringToBytes(ip, '.'));
      Address addr = (Address) _ip_addr[ip_bytes];
      lock(_sync) {
        _ip_addr.Remove(ip_bytes);
        _addr_ip.Remove(addr);
        mcast_addr.Remove(addr);
      }
    }

    /// <summary>
    /// Maps DNS alias to IP address.
    /// </summary>
    /// <param name="alias">Dns alias to map</param>
    /// <param name="ip">IP address to map</param>
    /// <param name="reverse">If true, add reverve mapping</param>
    public void AddDnsMapping(string alias, string ip, bool reverse) {
      lock(_sync) {
        _dns_a.Add(alias, ip);
        if (reverse) {
          _dns_ptr.Add(ip, alias);
        }
      }
    }

    /// <summary>
    /// Remove Dns alias mapping.
    /// </summary>
    /// <param name="alias">Dns alias to remove</param>
    /// <param name="reverse">If true, remove reverse mapping</param>
    public void RemoveDnsMapping(string alias, bool reverse) {
      string ip = (string)_dns_a[alias];
      lock (_sync) {
        _dns_a.Remove(alias);
        if (reverse) {
          _dns_ptr.Remove(ip);
        }
      }
    }

  }

#if ManagedIpopNodeNUNIT
  [TestFixture]
  public class ManagedTester {
    [Test]
    public void Test() { 
      MemBlock mdnsm = MemBlock.Reference(new byte[] {0x00, 0x00, 0x00, 0x00,
        0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x0E, 0x64, 0x61, 0x76,
        0x69, 0x64, 0x69, 0x77, 0x2D, 0x6C, 0x61, 0x70, 0x74, 0x6F, 0x70, 0x05,
        0x6C, 0x6F, 0x63, 0x61, 0x6C, 0x00, 0x00, 0xFF, 0x00, 0x01, 0xC0, 0x0C,
        0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x78, 0x00, 0x04, 0x0A, 0xFE,
        0x00, 0x01});
      DnsPacket mdns = new DnsPacket(mdnsm);
      String ss_ip = "10.254.112.232";
      bool change = ManagedAddressResolverAndDns.mDnsTranslate(mdns.Answers, ss_ip);
      change |= ManagedAddressResolverAndDns.mDnsTranslate(mdns.Authority, ss_ip);
      change |= ManagedAddressResolverAndDns.mDnsTranslate(mdns.Additional, ss_ip);
      // If we make a change let's make a new packet!
      if(change) {
          mdns = new DnsPacket(mdns.ID, mdns.Query, mdns.Opcode, mdns.AA,
                               mdns.RA, mdns.RD, mdns.Questions, mdns.Answers,
                               mdns.Authority, mdns.Additional);
      }
      Assert.AreEqual(mdns.Authority[0].Name, "davidiw-laptop.local", "Name");
      Assert.AreEqual(mdns.Authority[0].Type, DnsPacket.Types.A, "Type");
      Assert.AreEqual(mdns.Authority[0].Class, DnsPacket.Classes.IN, "Class");
      Assert.AreEqual(mdns.Authority[0].CacheFlush, false, "CacheFlush");
      Assert.AreEqual(mdns.Authority[0].Ttl, 120, "Ttl");
      Assert.AreEqual(mdns.Authority[0].RData, "10.254.112.232", "RData");
    }
  } 
#endif
}
