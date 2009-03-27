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
using System;
using System.Collections;
using System.Net;
using System.Text;
using System.Threading;

/**
\namespace Ipop::DhtNode
\brief Defines DhtIpopNode and the utilities necessary to use Ipop over Dht.
*/
namespace Ipop.DhtNode {
  /// <summary>This class provides an IpopNode that does address and name
  /// resolution using Brunet's Dht.  Multicast is supported.</summary>
  public class DhtIpopNode: IpopNode {
    /// <summary>This makes sure only one dhcp request is being handle at a 
    /// time</summary>
    protected int in_dhcp;

    ///<summary>Creates a DhtIpopNode.</summary>
    /// <param name="NodeConfigPath">The path to a NodeConfig xml file</param>
    /// <param name="IpopConfigPath">The path to a IpopConfig xml file</param>
    public DhtIpopNode(string NodeConfigPath, string IpopConfigPath):
      base(NodeConfigPath, IpopConfigPath) {
      in_dhcp = 0;
      _dns = new DhtDNS(Dht, _ipop_config.IpopNamespace);
      _address_resolver = new DhtAddressResolver(Dht, _ipop_config.IpopNamespace);
    }

    /// <summary>Let's see if we can route for an IP.  Default is do
    /// nothing!</summary>
    /// <param name="ip">The IP in question.</param>
    protected override void HandleNewStaticIP(MemBlock ether_addr, MemBlock ip) {
      if(IpopLog.DHCPLog.Enabled) {
        ProtocolLog.WriteIf(IpopLog.DHCPLog, String.Format(
                            "Incoming new IP: {0}, DHCP Status: {1}.",
                            Utils.MemBlockToString(ip, '.'), in_dhcp));
      }
      if(Interlocked.Exchange(ref in_dhcp, 1) == 1) {
        return;
      }

      WaitCallback wcb = delegate(object o) {
        if(_dhcp_server == null && !StartDHCP()) {
          Interlocked.Exchange(ref in_dhcp, 0);
          return;
        }

        byte[] res_ip = null;
        try {
          res_ip = _dhcp_server.RequestLease(ip, true,
              Brunet.Address.ToString(), 
              _ipop_config.AddressData.Hostname);
        } catch { }

        if(res_ip != null) {
          UpdateAddressData(MemBlock.Reference(ip), MemBlock.Reference(_dhcp_server.Netmask));
        } else {
          ProtocolLog.WriteIf(IpopLog.DHCPLog, String.Format(
                "Request for {0} failed!", Utils.MemBlockToString(ip, '.')));
        }

        Interlocked.Exchange(ref in_dhcp, 0);
      };

      ThreadPool.QueueUserWorkItem(wcb);
    }

    /**
    <summary>Handles DHCP calls coming from HandleIPOut.  Requests are sent to
    HandleDHCP(Object) if in_dhcp is false.</summary>
    <param name="ipp">The IP Packet containing the DHCP Packet.</param>
    <returns>Returns true since this method is implemented.</returns>
    */
    protected override bool HandleDHCP(IPPacket ipp) {
      if(IpopLog.DHCPLog.Enabled) {
        ProtocolLog.Write(IpopLog.DHCPLog, String.Format(
                            "Incoming DHCP Request, DHCP Status: {0}.", in_dhcp));
      }
      if(Interlocked.Exchange(ref in_dhcp, 1) == 1) {
        return true;
      }

      WaitCallback wcb = delegate(object o) {
        if(_dhcp_server == null && !StartDHCP()) {
          Interlocked.Exchange(ref in_dhcp, 0);
          return;
        }

        ProcessDHCP(ipp, _ipop_config.AddressData.Hostname);
        Interlocked.Exchange(ref in_dhcp, 0);
      };

      ThreadPool.QueueUserWorkItem(wcb);
      return true;
    }

    protected bool StartDHCP() {
      try {
        _dhcp_server = DhtDHCPServer.GetDhtDHCPServer(Dht,
            _ipop_config.IpopNamespace,
            _ipop_config.EnableMulticast);
      } catch(Exception e) {
        ProtocolLog.WriteIf(IpopLog.DHCPLog, e.ToString());
        return false;
      }
      return true;
    }

    /**
    <summary>This calls a DNS Lookup using ThreadPool.</summary>
    <param name="ipp">The IP Packet containing the DNS query.</param>
    <returns>Returns true since this is implemented.</returns>
    */
    protected override bool HandleDNS(IPPacket ipp) {
      ThreadPool.QueueUserWorkItem(new WaitCallback(HandleDNS), ipp);
      return true;
    }

    /**
    <summary>This is called by HandleDNS(IPPacket) as a ThreadPool thread.
    This contacts the underlying DNS to lookup a packet and writes the results
    to the Ethernet.</summary>
    <param name="ippo">An Object encapsulating the IP Packet containing the
    query.</param>
    */
    protected void HandleDNS(Object ippo) {
      IPPacket ipp = (IPPacket) ippo;
      WriteIP(_dns.LookUp(ipp).ICPacket);
    }

    /**
    <summary>Called by HandleIPOut if the current packet has a Multicast 
    address in its destination field.  This calls HandleMulticast(Object) using
    a ThreadPool thread.</summary>
    <param name="ipp">The IP Packet destined for multicast.</param>
    <returns>This returns true since this is implemented.</returns>
    */
    protected override bool HandleMulticast(IPPacket ipp) {
      if(_ipop_config.EnableMulticast) {
        ThreadPool.QueueUserWorkItem(new WaitCallback(HandleMulticast), ipp);
      }
      return true;
    }

    /**
    <summary>This handles multicast in a separate thread and is called by
    HandleMulticast(IPPacket).  DhtIpopNode does multicast naively, where all
    nodes are assumed to be in a LAN and will all receive a multicast packet
    if they are part of the multicast.ipop group.  The host operating
    system will have to decide what to do with the packet.</summary>
    <param name="ippo">An Object encapsulating the IP Packet containing the
    multicast packet.</param>
    */
    public void HandleMulticast(Object ippo) {
      IPPacket ipp = (IPPacket) ippo;
      Hashtable[] results = null;
      try {
        results = Dht.Get(Encoding.UTF8.GetBytes(_ipop_config.IpopNamespace + ".multicast.ipop"));
      } catch {
        return;
      }
      foreach(Hashtable result in results) {
        try {
          AHAddress target = (AHAddress) AddressParser.Parse(Encoding.UTF8.GetString((byte[]) result["value"]));
          if(IpopLog.PacketLog.Enabled) {
            ProtocolLog.Write(IpopLog.PacketLog, String.Format(
                              "Brunet destination ID: {0}", target));
          }
          SendIP(target, ipp.Packet);
        }
        catch {}
      }
    }

    /**
    <summary>This creates a DhtIpopNode and Runs it.</summary>
    <param name="args">This application requires two inputs: a path to a
    NodeConfig and the path to a IpopConfig.</param>
    */
    public static new void Main(String[] args) {
      DhtIpopNode node = new DhtIpopNode(args[0], args[1]);
      node.Run();
    }
  }
}
