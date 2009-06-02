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
    ///<summary>Creates a DhtIpopNode.</summary>
    /// <param name="NodeConfig">NodeConfig object</param>
    /// <param name="IpopConfig">IpopConfig object</param>
    public DhtIpopNode(NodeConfig node_config, IpopConfig ipop_config) :
      base(node_config, ipop_config)
    {
      _address_resolver = new DhtAddressResolver(Dht, _ipop_config.IpopNamespace);

      if("Dht".Equals(ipop_config.DNSType)) {
        _dns = new DhtDNS(Dht, _ipop_config.IpopNamespace);
      }
    }

    /// <summary>This calls a DNS Lookup using ThreadPool.</summary>
    /// <param name="ipp">The IP Packet containing the DNS query.</param>
    /// <returns>Returns true since this is implemented.</returns>
    protected override bool HandleDNS(IPPacket ipp) {
      WaitCallback wcb = delegate(object o) {
        try {
          WriteIP(_dns.LookUp(ipp).ICPacket);
        } catch {
        }
      };
      ThreadPool.QueueUserWorkItem(wcb, ipp);
      return true;
    }

    /// <summary>Called by HandleIPOut if the current packet has a Multicast 
    /// address in its destination field.  This sends the multicast packet
    /// to all members of the multicast group stored in the dht.</summary>
    /// <param name="ipp">The IP Packet destined for multicast.</param>
    /// <returns>This returns true since this is implemented.</returns>
    protected override bool HandleMulticast(IPPacket ipp) {
      if(!_ipop_config.EnableMulticast) {
        return true;
      }

      WaitCallback wcb = delegate(object o) {
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
      };

      ThreadPool.QueueUserWorkItem(wcb, ipp);
      return true;
    }

    /// <summary>We need to get the DHCPConfig as soon as possible so that we
    /// can allocate static addresses, this method helps us do that.</summary>
    protected override void GetDHCPConfig() {
      if(Interlocked.Exchange(ref _lock, 1) == 1) {
        return;
      }

      WaitCallback wcb = delegate(object o) {
        bool success = false;
        DHCPConfig dhcp_config = null;
        try {
          dhcp_config = DhtNode.DhtDHCPServer.GetDHCPConfig(Dht, _ipop_config.IpopNamespace);
          success = true;
        } catch(Exception e) {
          ProtocolLog.WriteIf(IpopLog.DHCPLog, e.ToString());
        }

        if(success) {
          lock(_sync) {
            _dhcp_config = dhcp_config;
            _dhcp_server = new DhtNode.DhtDHCPServer(Dht, _dhcp_config, _ipop_config.EnableMulticast);
          }
        }
        base.GetDHCPConfig();

        Interlocked.Exchange(ref _lock, 0);
      };

      ThreadPool.QueueUserWorkItem(wcb);
    }

    protected override DHCPServer GetDHCPServer() {
      return new DhtNode.DhtDHCPServer(Dht, _dhcp_config, _ipop_config.EnableMulticast);
    }
  }
}
