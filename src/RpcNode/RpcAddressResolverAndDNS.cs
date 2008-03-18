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
     return IPPacket.Translate(packet, source_ip, _local_ip);
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
        ReqrepManager.ReplyState _rs = (ReqrepManager.ReplyState)caller;
        UnicastSender _us = (UnicastSender)_rs.ReturnPath;
        IPEndPoint _ep = (IPEndPoint)_us.EndPoint;
        if (!_ep.Address.ToString().Equals("127.0.0.1")) {
          throw new Exception("Not calling from local BrunetRpc locally!");
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
          RegisterMapping(name, addr, request_state);
        }
        else if (method.Equals("UnregisterMapping")) {
          String name = (String)arguments[0];
          UnregisterMapping(name, request_state);
        }
        else if (method.Equals("CheckInstance")) {
          _rpc.SendResult(request_state, true);
        }
        else if (method.Equals("CheckBuddy")) {
          Address address = AddressParser.Parse((String)arguments[0]);
          CheckBuddy(address, request_state);
        }
        else { 
          throw new InvalidOperationException("Invalid Method ");
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
    protected void RegisterMapping(String name, Address addr, object request_state) {
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
    protected void CheckBuddy(Address address, object request_state) { 
      Channel q = new Channel();
      q.CloseAfterEnqueue();

      // Delegate code called by CloseEvent from the channel object
      q.CloseEvent += delegate(Object o, EventArgs eargs) {
        Object result = null;
        try {
          RpcResult res = (RpcResult)q.Dequeue();
          result = res.Result;
        }
        catch (Exception e) {
          result = e;
        }
        _rpc.SendResult(request_state, result);
      };

      ISender s = new AHExactSender(_node, address);
      _rpc.Invoke(s, q, "sys:link.Ping", true);
    }
  }
}
