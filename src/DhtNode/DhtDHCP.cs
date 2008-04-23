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
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Ipop.DhtNode {
  /**
  <summary>The DhtDHCPLeaseController provides mechanisms to acquire IP
  Addresses in the Dht.  This is responsible for IP Address allocation,
  hostname reservation, and multicast subscribing, which is done in GetLease.
  </summary>
  */
  public class DhtDHCPLeaseController: DHCPLeaseController {
    /// <summary>The dht object used to stored lease information.</summary>
    protected Dht _dht;
    /// <summary>Multicast enabled.</summary>
    protected bool _multicast;
    /// <summary>Speed optimization for slow dht, current ip</summary>
    protected String _current_ip;
    /// <summary>Speed optimization for slow dht, lease quarter time.</summary>
    protected DateTime _current_quarter_lifetime;
    /// <summary>Speed optimization for slow dht, DHCPReply.</summary>
    protected DHCPReply _current_dhcpreply;

    /**
    <summary>Creates a DhtDHCPLeaseController for a specific namespace</summary>
    <param name="dht">The dht object use to store lease information.</param>
    <param name="config">The DHCPServerConfig used to define the Lease
    parameters.</param>
    <param name="EnableMulticast">Defines if Multicast is to be enabled during
    the lease.</param>
    */
    public DhtDHCPLeaseController(Dht dht, DHCPServerConfig config,
                                  bool EnableMulticast): base(config) {
      _dht = dht;
      _multicast = EnableMulticast;
    }

    /**
    <summary>This provides a mechanism for a node to get a lease by using the
    Dht.  This uses Dht.Create which provides an atomic operation on the Dht,
    where this node is the first to store a value at a specific key.  The idea
    being that, this node being the first to store the IP, all nodes doing a
    lookup for that IP Address would be directed to this node.</summary>
    <remarks>Working with the Dht is a little tricky as transient errors could
    be misrepresented as a failed Create.  It is that reason why there is a 
    Renew parameter. If that is set, the algorithm for obtaining an address
    is slightly changed with more weight on reobtaining the RequestedAddr.
    </remarks>
    <param name="RequestedAddr">Optional parameter if the node would like to
    request a specific address.</param>
    <param name="Renew">Is the RequestedAddr a renewal?</param>
    <param name="node_address">The Brunet.Address where the DhtIpopNode resides
    </param>
    <param name="para">Optional, position 0 should hold the hostname.</param>
    */
    public override DHCPReply GetLease(byte[] RequestedAddr, bool Renew,
                                       string node_address, params object[] para) {
      int max_attempts = 2, max_renew_attempts = 1;
      int attempts = max_attempts, renew_attempts = max_renew_attempts;

      String hostname = null;
      try {
        hostname = (String) para[0];
      } catch {}

      if(RequestedAddr == null || !ValidIP(RequestedAddr)) {
        RequestedAddr = RandomIPAddress();
      }
      else if(Renew) {
        renew_attempts = 2;
      }

      if(_current_ip != null && Utils.BytesToString(RequestedAddr, '.') == _current_ip &&
            DateTime.UtcNow < _current_quarter_lifetime) {
        return _current_dhcpreply;
      }

      bool res = false;

      while (attempts-- > 0) {
        string str_addr = Utils.BytesToString(RequestedAddr, '.');
        string key = "dhcp:ipop_namespace:" + namespace_value + ":ip:" + str_addr;
        while(renew_attempts-- > 0) {
          try {
            res = _dht.Create(key, node_address, leasetime);
            if(hostname != null) {
              _dht.Put(namespace_value + "." + hostname, str_addr, leasetime);
            }
            if(_multicast) {
              _dht.Put(namespace_value + ".multicast.ipop_vpn", node_address,
                       leasetime);
            }
            _dht.Put(node_address, key + "|" + DateTime.Now.Ticks, leasetime);
          }
          catch {
            res = false;
          }
        }
        if(res) {
          _current_ip = str_addr;
          _current_quarter_lifetime = DateTime.UtcNow.AddSeconds(leasetime / 4); 
          break;
        }
        else {
          // Failure!  Guess a new IP address
          RequestedAddr = RandomIPAddress();
          renew_attempts = max_renew_attempts;
        }
      }

      if(!res) {
        throw new Exception("Unable to get an IP Address!");
      }

      DHCPReply reply = new DHCPReply();
      reply.ip = RequestedAddr;
      reply.netmask = netmask;
      reply.leasetime = leasetimeb;
      _current_dhcpreply = reply;
      return reply;
    }
  }

  /**
  <summary>DhtDHCPServer provides a DHCP Server where Brunet Dht can be used
  to store DHCP Leases, reserve hostnames, and subscribe to multicast.</summary>
  */
  public class DhtDHCPServer: DHCPServer {
    /// <summary>The dht object where DHCP information will be stored.</summary>
    protected Dht _dht;
    /// <summary>If multicast is supported.</summary>
    protected bool _multicast;

    /**
    <summary>Creates a DhtDHCPServer to get DHCPServerConfigs from the Dht and
    to store DHCP leases in the Dht.</summary>
    <param name="dht">The dht object where DHCP information will be stored</param>
    <param name="EnableMulticast">Enabled if the client wants to subscribe to
    Multicast.</param>
    */
    public DhtDHCPServer(Dht dht, bool EnableMulticast) {
      _multicast = EnableMulticast;
      _dht = dht;
    }

    /**
    <summary>Checks the _dhcp_lease_controllers to see if an existing
    controllers exists.  Otherwise, it attempts to get a DHCPServerConfig from
    the Dht.  If it succeeds, it creates a new DHCPLeaseController.</summary>
    <param name="ipop_namespace">Specifies which DHCPLeaseController to use.
    As DHCPLeaseControllers are allocated per-namespace.</param>
    */
    protected override DHCPLeaseController GetDHCPLeaseController(string ipop_namespace) {
      if (_dhcp_lease_controllers.ContainsKey(ipop_namespace)) {
        return (DHCPLeaseController) _dhcp_lease_controllers[ipop_namespace];
      }
      string ns_key = "dhcp:ipop_namespace:" + ipop_namespace;
      DhtGetResult[] results = _dht.Get(ns_key);
      if(results == null || results.Length == 0 || results[0].valueString == null)  {
        Debug.WriteLine("Namespace ({0}) does not exist", ipop_namespace);
        return null;
      }

      string xml_str = results[0].valueString.ToString();
      XmlSerializer serializer = new XmlSerializer(typeof(DHCPServerConfig));
      TextReader stringReader = new StringReader(xml_str);
      DHCPServerConfig ipop_ns = (DHCPServerConfig) serializer.Deserialize(stringReader);
      DHCPLeaseController dhcpLeaseController = new DhtDHCPLeaseController(_dht, ipop_ns, _multicast);
      _dhcp_lease_controllers[ipop_namespace] = dhcpLeaseController;
      return dhcpLeaseController;
    }
  }
}
