/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida
                    Pierre St Juste <ptony82@ufl.edu>, University of Florida   

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
using Brunet.Applications;
using Brunet.DistributedServices;
using NetworkPackets;
using NetworkPackets.DNS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

#if RpcIpopNodeNUNIT
using NUnit.Framework;
#endif

namespace Ipop.RpcNode {
  /// <summary>
  /// This class implements DNS, IAddressResolver, IRpcHandler, and
  /// ITranslator. It provides most functionality needed by RpcIpopNode.
  /// </summary>
  public class RpcAddressResolverAndDNS : DNS, IAddressResolver, IRpcHandler,
    ITranslator {
    /// <summary>The node to do ping checks on.</summary>
    protected StructuredNode _node;
    /// <summary>The rpc manager to make rpc requests over.</summary>
    protected RpcManager _rpc;
    /// <summary>Contains ip:hostname mapping.</summary>
    protected volatile Hashtable dns_a;
    /// <summary>Contains hostname:ip mapping.</summary>
    protected volatile Hashtable dns_ptr;
    /// <summary>Maps MemBlock IP Addresses to Brunet Address as Address</summary>
    protected volatile Hashtable ip_addr;
    /// <summary>Maps Brunet Address as Address to MemBlock IP Addresses</summary>
    protected volatile Hashtable addr_ip;

    /// <summary> List of connected addresses</summary>
    protected volatile ArrayList conn_addr;

    /// <summary> Indicated if client needs sync</summary>
    protected bool need_sync;

    /// <summary>Returns the Connected Addresses.</summary>
    public ArrayList ConnectedAddresses {
      get { return conn_addr; }
    }

    /// <summary>Helps assign remote end points</summary>
    protected RpcDHCPLeaseController _rdlc;
    protected Object _sync;

    protected MemBlock _local_ip;

    /// <summary>
    /// Constructor for the class, it initializes various objects
    /// </summary>
    /// <param name="node">Takes in a structured node</param>
    public RpcAddressResolverAndDNS(StructuredNode node) {
      _sync = new Object();
      _node = node;
      _rpc = RpcManager.GetInstance(node);
      dns_a = new Hashtable();
      dns_ptr = new Hashtable();
      ip_addr = new Hashtable();
      addr_ip = new Hashtable();
      conn_addr = new ArrayList();
      need_sync = false;

      _rpc.AddHandler("RpcIpopNode", this);
    }

    /// <summary>
    /// This method does an inverse lookup for the DNS
    /// </summary>
    /// <param name="IP">IP address of the name that's being looked up</param>
    /// <returns>Returns the name as string of the IP specified</returns>
    public override String NameLookUp(String IP) {
      return (String)dns_ptr[IP];
    }

    /// <summary>
    /// This method does an address lookup on the DNS
    /// </summary>
    /// <param name="Name">Takes in name as string to lookup</param>
    /// <returns>The result as a String Ip address</returns>
    public override String AddressLookUp(String Name) {
      return (String)dns_a[Name];
    }

    /// <summary>
    /// This is called by the outside node to tell us what our IP Address
    /// is.  This is here since, the IP may change dynamically.
    /// </summary>
    /// <param name="IP">A string ip that is to be updated</param>
    /// <param name="Netmask">A string netmask that is also given</param>
    public void UpdateAddressData(String IP, String Netmask) {
      _local_ip = MemBlock.Reference(Utils.StringToBytes(IP, '.'));
      DHCPServerConfig dhcp_config = RpcNodeHelper.GenerateDHCPServerConfig(IP, Netmask);
      _rdlc = new RpcDHCPLeaseController(dhcp_config);
    }

    /**
    <summary>Implements the ITranslator portion for RpcAddress..., takes an
    IP Packet, based upon who the originating Brunet Sender was, changes who
    the packet was sent from and then switches the destination address to the
    local nodes address</summary>
    <param name="packet">The IP Packet to translate.</param>
    <param name="from">The Brunet address the packet was sent from.</param>
    <returns>The translated IP Packet.</returns>
    */
    public MemBlock Translate(MemBlock packet, Address from) {
      MemBlock source_ip = (MemBlock) addr_ip[from];
      if(source_ip == null) {
        throw new Exception("Invalid mapping " + from + ".");
      }

      // Attempt to translate a MDNS packet
      IPPacket ipp = new IPPacket(packet);
      MemBlock ID = packet.Slice(4,2);
      if(ipp.Protocol == IPPacket.Protocols.UDP) {
        UDPPacket udpp = new UDPPacket(ipp.Payload);
        // MDNS runs on 5353
        if(udpp.DestinationPort == 5353) {
          DNSPacket dnsp = new DNSPacket(udpp.Payload);
          String ss_ip = DNSPacket.IPMemBlockToString(source_ip);
          bool change = mDnsTranslate(dnsp.Answers, ss_ip);
          change |= mDnsTranslate(dnsp.Authority, ss_ip);
          change |= mDnsTranslate(dnsp.Additional, ss_ip);
          // If we make a change let's make a new packet!
          if(change) {
            dnsp = new DNSPacket(dnsp.ID, dnsp.QUERY, dnsp.OPCODE, dnsp.AA,
                                 dnsp.RA, dnsp.RD, dnsp.Questions, dnsp.Answers,
                                 dnsp.Authority, dnsp.Additional);
            udpp = new UDPPacket(udpp.SourcePort, udpp.DestinationPort,
                                 dnsp.ICPacket);
            ipp = new IPPacket(ipp.Protocol, source_ip, ipp.DestinationIP,
                               ID, udpp.ICPacket);
            return ipp.Packet;
          }
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
        if(responses[i].TYPE == DNSPacket.TYPES.A) {
          change = true;
          Response old = responses[i];
          responses[i] = new Response(old.NAME, old.TYPE, old.CLASS,
                                         old.CACHE_FLUSH, old.TTL, ss_ip);
        }
        else if(responses[i].TYPE == DNSPacket.TYPES.PTR) {
          Response old = responses[i];
          if(DNSPacket.StringIsIP(old.NAME)) {
            responses[i] = new Response(ss_ip, old.TYPE,  old.CLASS,
                                        old.CACHE_FLUSH, old.TTL, old.RDATA);
            change = true;
          }
        }
      }
      return change;
    }

    /// <summary>
    /// Returns the Brunet address given an IP
    /// </summary>
    /// <param name="IP">A MemBlock of the IP</param>
    /// <returns>A brunet Address for the IP</returns>
    public Address Resolve(MemBlock IP) {
      return (Address)ip_addr[IP];
    }

    /// <summary>
    /// This method handles Rpc calls for this object
    /// </summary>
    /// <param name="caller">An ISender</param>
    /// <param name="method">A string that specifies the name of the called method</param>
    /// <param name="arguments">An IList of parameters for the method</param>
    /// <param name="request_state">An object state</param>
    public void HandleRpc(ISender caller, String method, IList arguments, object request_state) {
      Object result = null;
      try {
        if (!method.Equals("FriendOnline")) {
          ReqrepManager.ReplyState _rs = (ReqrepManager.ReplyState)caller;
          UnicastSender _us = (UnicastSender)_rs.ReturnPath;
          IPEndPoint _ep = (IPEndPoint)_us.EndPoint;
          if (!_ep.Address.ToString().Equals("127.0.0.1")) { 
            throw new Exception("Not calling from local BrunetRpc locally!");
          }
        }
      }
      catch (Exception e){
        result = new InvalidOperationException(e.Message);
        _rpc.SendResult(request_state, result);
        return;
      }

      try {
        if (method.Equals("RegisterMapping")) {
          String name = (String)arguments[0];
          Address addr = AddressParser.Parse((String)arguments[1]);
          RegisterMapping(name, addr, request_state, true);
        }
        else if (method.Equals("UnregisterMapping")) {
          String name = (String)arguments[0];
          UnregisterMapping(name, request_state);
        }
        else if (method.Equals("GetConnected")) {
          String res = String.Empty;
          foreach(Address addr in conn_addr) {
            res += addr.ToString()+" ";
          }
          _rpc.SendResult(request_state, res);
        }
        else if (method.Equals("CheckInstance")) {
          _rpc.SendResult(request_state, true);
        }
        else if (method.Equals("FriendOnline")) {
          Address address = AddressParser.Parse((String)arguments[0]);
          FriendOnline(address, request_state, true);
        }
        else if (method.Equals("Sync")) {
          _rpc.SendResult(request_state, need_sync);
          need_sync = false;
        }
        else { 
          throw new InvalidOperationException("Invalid Method");
        }
      }
      catch (Exception e) {
        result = e;
        _rpc.SendResult(request_state, result);
      }
    }

    /// <summary>
    /// Called by external rpc program to register a mapping name and address
    /// mapping and returns an IP string to rpc caller.
    /// </summary>
    /// <remarks>
    /// This method directly sends the result to rpc caller except in the
    /// case of an exception in which HandleRpc method would request exception
    /// </remarks>
    /// <param name="name">A string name to be added to DNS</param>
    /// <param name="addr">A brunet address that is to be mapped</param>
    /// <param name="request_state">Request state object for rpc</param>
    /// <param name="send_rpc">Indicates if rpc result should be sent</param>
    protected void RegisterMapping(String name, Address addr, 
                     object request_state, bool send_rpc) {
      MemBlock ip = null;
      String ips = null;
      lock (_sync) {
        ips = (String)dns_a[name];

        if (ips != null) {
          ip = MemBlock.Reference(Utils.StringToBytes(ips, '.'));
        }

        if (ips == null) {
          do {
            ip = MemBlock.Reference(_rdlc.RandomIPAddress());
          } while (ip_addr.ContainsValue(ip));
          ips = Utils.MemBlockToString(ip, '.');
          addr_ip.Add(addr, ip);
          ip_addr.Add(ip, addr);
          dns_a.Add(name, ips);
          dns_ptr.Add(ips, name); 
        }

        else if (addr.Equals(ip_addr[ip])) {
          ips = Utils.MemBlockToString(ip, '.');
        }
        else {
          throw new Exception(String.Format
            ("Name ({0}) already exists with different address.", name));
        }
      }
      CheckBuddy(addr);
      if(send_rpc) {
        _rpc.SendResult(request_state, ips);
      }
    }

    /// <summary>
    /// Called by external rpc program to unregister a name, ip, address mapping
    /// </summary>
    /// <remarks>
    /// This method directly sends the result to rpc caller except in the
    /// case of an exception in which HandleRpc method would request exception
    /// </remarks>
    /// <param name="name">A string name that needs to be removed</param>
    protected void UnregisterMapping(String name, object request_state) {
      if (!dns_a.Contains(name)) {
        throw new Exception(String.Format("Name ({0}) does not exists", name));
      }
      MemBlock ip = null;
      String ips = (String)dns_a[name];

      if (ips != null) {
        ip = MemBlock.Reference(Utils.StringToBytes(ips, '.'));
      } 
      lock (_sync) {
          dns_a.Remove(name);
          dns_ptr.Remove(ips);
          Address addr = (Address)ip_addr[ip];
          ip_addr.Remove(ip);
          addr_ip.Remove(addr);
      }
      _rpc.SendResult(request_state, true);
    }

    /// <summary>
    /// Called by external rpc program to see if node is accessible
    /// </summary>
    /// <remarks>
    /// This method directly sends the result to rpc caller 
    /// </remarks>
    /// <param name="address">A brunet adddress of the node to check</param>
    /// <param name="request_state">Request state of object</param>
    /// <param name="send_rpc">Flag indicting if send is necessary</param>
    protected void CheckBuddy(Address address) { 
      Channel q = new Channel();
      q.CloseAfterEnqueue();

      // Delegate code called by CloseEvent from the channel object
      q.CloseEvent += delegate(Object o, EventArgs eargs) {
        Object result = null;
        try {
          RpcResult res = (RpcResult)q.Dequeue();
          result = AddressParser.Parse((String)res.Result);
          if(addr_ip.Contains(result)) {
            lock (_sync) {
              if(!conn_addr.Contains(result)) {
                conn_addr.Add(result);
              }
            }
          }
        }
        catch (Exception e) {
          result = e;
          if(conn_addr.Contains(address)) {
            conn_addr.Remove(address);
          }
        }
      };
      ISender s = new AHExactSender(_node, address);
      _rpc.Invoke(s, q, "RpcIpopNode.FriendOnline", _node.Address.ToString());
    }

    /// <summary>
    /// Rpc Method that is called when a friend is announced
    /// </summary>
    /// <param name="address">Brunet address</param>
    /// <param name="request_state">Request state </param>
    /// <param name="send_rpc"> Indicates if result should be sent</param>
    protected void FriendOnline(Address address, object request_state, bool send_rpc) {
      if(!addr_ip.Contains(address)) {
        need_sync = true;
      }
      else {
        lock (_sync) { 
          if(!conn_addr.Contains(address)) {
            conn_addr.Add(address);
          }
        }
      }
      if(send_rpc) {
        _rpc.SendResult(request_state, _node.Address.ToString());
      } 
    }
    
  }
#if RpcIpopNodeNUNIT
  [TestFixture]
  public class RpcTester {
    [Test]
    public void Test() { 
      MemBlock mdnsm = MemBlock.Reference(new byte[] {0x00, 0x00, 0x00, 0x00,
        0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x0E, 0x64, 0x61, 0x76,
        0x69, 0x64, 0x69, 0x77, 0x2D, 0x6C, 0x61, 0x70, 0x74, 0x6F, 0x70, 0x05,
        0x6C, 0x6F, 0x63, 0x61, 0x6C, 0x00, 0x00, 0xFF, 0x00, 0x01, 0xC0, 0x0C,
        0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x78, 0x00, 0x04, 0x0A, 0xFE,
        0x00, 0x01});
      DNSPacket mdns = new DNSPacket(mdnsm);
      String ss_ip = "10.254.112.232";
      bool change = RpcAddressResolverAndDNS.mDnsTranslate(mdns.Answers, ss_ip);
      change |= RpcAddressResolverAndDNS.mDnsTranslate(mdns.Authority, ss_ip);
      change |= RpcAddressResolverAndDNS.mDnsTranslate(mdns.Additional, ss_ip);
      // If we make a change let's make a new packet!
      if(change) {
          mdns = new DNSPacket(mdns.ID, mdns.QUERY, mdns.OPCODE, mdns.AA,
                               mdns.RA, mdns.RD, mdns.Questions, mdns.Answers,
                               mdns.Authority, mdns.Additional);
      }
      Assert.AreEqual(mdns.Authority[0].NAME, "davidiw-laptop.local", "NAME");
      Assert.AreEqual(mdns.Authority[0].TYPE, DNSPacket.TYPES.A, "TYPE");
      Assert.AreEqual(mdns.Authority[0].CLASS, DNSPacket.CLASSES.IN, "CLASS");
      Assert.AreEqual(mdns.Authority[0].CACHE_FLUSH, false, "CACHE_FLUSH");
      Assert.AreEqual(mdns.Authority[0].TTL, 120, "TTL");
      Assert.AreEqual(mdns.Authority[0].RDATA, "10.254.112.232", "RDATA");
    }
  } 
#endif
}
