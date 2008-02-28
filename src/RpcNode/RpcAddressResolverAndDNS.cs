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
using Brunet.Applications;
using Brunet.DistributedServices;
using Ipop;
using NetworkPackets;
using NetworkPackets.DNS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Ipop.RpcNode {
  public class RpcAddressResolverAndDNS: DNS, IAddressResolver, IRpcHandler, ITranslator {
    protected StructuredNode _node;
    protected RpcManager _rpc;
    protected volatile Hashtable dns_a;
    protected volatile Hashtable dns_ptr;
    /// <summary>Maps MemBlock IP Addresses to Brunet Address as Address
    protected volatile Hashtable ip_addr;
    /// <summary>Maps Brunet Address as Address to MemBlock IP Addresses
    protected volatile Hashtable addr_ip;
    /// <summary>Helps assign remote end points</summary>
    protected RpcDHCPLeaseController _rdlc;
    protected Object _sync;

    protected MemBlock _local_ip;

    public RpcAddressResolverAndDNS(StructuredNode node) {
      _sync = new Object();
      _node = node;
      _rpc = RpcManager.GetInstance(node);
      dns_a = new Hashtable();
      dns_ptr = new Hashtable();
      ip_addr = new Hashtable();
      addr_ip = new Hashtable();

      _rpc.AddHandler("RpcIpopNode", this);
    }

    public override String NameLookUp(String IP) {
      return (String) dns_ptr[IP];
    }

    public override String AddressLookUp(String Name) {
      return (String) dns_a[Name];
    }

    /**
    <summary>This is called by the outside node to tell us what our IP Address
    is.  This is here since, the IP may change dynamically.</summary>
    <param name="IP">The IP Address of our node</param>
    */
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
    <param name="ipp">The IP Packet to translate.</param>
    <param name="from">The Brunet address the packet was sent from.</param>
    <returns>The translated IP Packet.</returns>
    */
    public IPPacket Translate(IPPacket ipp, Address from) {
      MemBlock source_ip = (MemBlock) addr_ip[from];
      IPPacket new_ipp = new IPPacket(ipp.Protocol, source_ip, _local_ip, ipp.ICPayload);
      return new_ipp;
    }

    /**
     * Returns the Brunet address given an IP
     * @param IP IP Address to look up
     * @return null if no IP exists or the Brunet.Address
     */
    public Address Resolve(String IP) {
      return (Address) ip_addr[IP];
    }

    public void HandleRpc(ISender caller, String method, IList arguments, object request_state) {
      try {
        ReqrepManager.ReplyState _rs = (ReqrepManager.ReplyState) caller;
        UnicastSender _us = (UnicastSender) _rs.ReturnPath;
        IPEndPoint _ep = (IPEndPoint) _us.EndPoint;
        String ip = _ep.Address.ToString();
        if(ip != "127.0.0.1") {
          throw new Exception();
        }
      }
      catch {
        Object rs = new InvalidOperationException("Not calling from local BrunetRpc locally!");
        _rpc.SendResult(request_state, rs);
        return;
      }

      Object result = new InvalidOperationException("Invalid method");
      try {
        if(method.Equals("RegisterMapping")) {
          String name = (String) arguments[0];
          Address addr = AddressParser.Parse((String) arguments[1]);
          result = RegisterMapping(name, addr);
        }
        else if(method.Equals("UnregisterMapping")) {
          String name = (String) arguments[0];
          result = UnregisterMapping(name);
        }
      }
      catch {
        result = new InvalidOperationException("Bad parameters.");
      }
      _rpc.SendResult(request_state, result);
    }

    /**
     * Called by external rpc program to register a mapping name and address
     * mapping and returns an IP.
     * @param name the name to register
     * @param addr the address of the remote node which we wanted mapped to name
     * @return IP Address of node or exception if it there is something invalid
     * REVIEW and FIX
     */

    protected String RegisterMapping(String name, Address addr) {
      String ip = (String) dns_a[name];
      if(ip != null) {
        if(addr.Equals(ip_addr[ip])) {
          return ip;
        }
        else {
          throw new Exception(String.Format("Name ({0}) already exists.", name));
        }
      }
      return null;
    }

    /**
     * Called by external rpc program to unregister a name, ip, address mapping
     * @param name the name to unregister
     * @return true if unregistered and exists, false otherwise
     * REVIEW and FIX
     */

    protected bool UnregisterMapping(String name) {
      if(dns_a.Contains(name)) {
        throw new Exception(String.Format("Name ({0}) doesnot exists", name));
      }
      lock(_sync) {
        String ip = (String) dns_a[name];
        dns_a.Remove(name);
        try {
          dns_ptr.Remove(ip);
        } catch {}
        try {
          Address addr = (Address) ip_addr[ip];
          ip_addr.Remove(ip);
          addr_ip.Remove(addr);
        } catch {}
      }
      return true;
    }
  }
}
