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
using System.Collections.Generic;
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
    protected bool _connected;

    ///<summary>Creates a DhtIpopNode.</summary>
    /// <param name="NodeConfig">NodeConfig object</param>
    /// <param name="IpopConfig">IpopConfig object</param>
    public DhtIpopNode(NodeConfig node_config, IpopConfig ipop_config,
        DHCPConfig dhcp_config) : base(node_config, ipop_config, dhcp_config)
    {
      _address_resolver = new DhtAddressResolver(Dht, _ipop_config.IpopNamespace);

      _connected = false;
      Brunet.StateChangeEvent += StateChangeHandler;
      StateChangeHandler(Brunet, Brunet.ConState);
    }

    public DhtIpopNode(NodeConfig node_config, IpopConfig ipop_config) :
        this(node_config, ipop_config, null)
    {
    }

    /// <summary> Occassionally nodes will get a true return from a allocation
    /// attempt, in order to prevent this, we reissue all dhcp requests after
    /// getting "connected" to the overlay.</summary>
    protected void StateChangeHandler(Node n, Node.ConnectionState state) {
      List<MemBlock> ips = null;

      lock(_sync) {
        if(state == Node.ConnectionState.Connected) {
          if(_connected) {
            return;
          }
          Brunet.StateChangeEvent -= StateChangeHandler;
          _connected = true;
        } else {
          return;
        }

        ips = new List<MemBlock>(_ip_to_ether.Keys.Count);
        foreach(MemBlock ip in _ip_to_ether.Keys) {
          ips.Add(ip);
        }
      }

      WaitCallback callback = delegate(object o) {
        // Get a new DHCP server so we get new state!
        DHCPServer dhcp_server = GetDHCPServer();
        foreach(MemBlock ip in ips) {
          try {
            dhcp_server.RequestLease(ip, true, Brunet.Address.ToString(),
                _ipop_config.AddressData.Hostname);
          } catch(Exception e) {
            ProtocolLog.WriteIf(IpopLog.DHCPLog, e.Message);
          }
        }
      };

      ThreadPool.QueueUserWorkItem(callback, null);
    }

    /// <summary>Someone told us we didn't have a mapping... let's fix that.</summary>
    protected override bool MappingMissing(MemBlock ip)
    {
      if(!base.MappingMissing(ip)) {
        return false;
      }

      // Easiest approach is to simply update the mapping...
      DHCPServer dhcp_server = GetDHCPServer();
      try {
        dhcp_server.RequestLease(ip, true, Brunet.Address.ToString(),
            _ipop_config.AddressData.Hostname);
      } catch(Exception e) {
        ProtocolLog.WriteIf(IpopLog.DHCPLog, e.Message);
      }

      return true;
    }

    protected override bool SupportedDNS(string dns) {
      if("DhtDNS".Equals(dns)) {
        return true;
      }

      return base.SupportedDNS(dns);
    }

    protected override void SetDNS() {
      if(_dns != null) {
        return;
      }

      if("DhtDNS".Equals(_ipop_config.DNS.Type)) {
        _dns = new DhtDNS(
            MemBlock.Reference(Utils.StringToBytes(_dhcp_config.IPBase, '.')),
            MemBlock.Reference(Utils.StringToBytes(_dhcp_config.Netmask, '.')),
            _ipop_config.DNS.NameServer, _ipop_config.DNS.ForwardQueries,
            Dht, _ipop_config.IpopNamespace);
      } else {
        base.SetDNS();
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

    /// <summary>Calls HandleMulticast.</summary>
    protected override bool HandleBroadcast(IPPacket ipp) {
      return HandleMulticast(ipp);
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
