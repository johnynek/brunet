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
using Brunet.Util;
using NetworkPackets;
using System;
using System.Collections;
using System.Net;
using System.Threading;

/**
\namespace Ipop::Managed
\brief Defines Ipop.Managed provide the ability to set up translation tables via Managed
*/
namespace Ipop.Managed {
  /// <summary>
  /// This class is a subclass of IpopNode
  /// </summary>
  public class ManagedIpopNode: IpopNode {
    /// <summary>Provides Address resolution, dns, and translation.</summary>
    protected ManagedAddressResolverAndDns _marad;

    /// <summary>
    /// The constructor takes two config files
    /// </summary>
    /// <param name="NodeConfigPath">Node config object</param>
    /// <param name="IpopConfigPath">Ipop config object</param>
    public ManagedIpopNode(NodeConfig node_config, IpopConfig ipop_config) :
      base(node_config, ipop_config, null)
    {
      _dhcp_server = ManagedDhcpServer.GetManagedDhcpServer(_ipop_config.VirtualNetworkDevice);  
      _dhcp_config = _dhcp_server.Config;
      _marad = new ManagedAddressResolverAndDns(AppNode.Node, _dhcp_server,
          ((ManagedDhcpServer) _dhcp_server).LocalIP, _ipop_config.Dns.NameServer,
          _ipop_config.Dns.ForwardQueries);
      _dns = _marad;
      _address_resolver = _marad;
      _translator = _marad;
    }

    protected override DhcpServer GetDhcpServer() {
      return _dhcp_server;
    }

    protected override void GetDhcpConfig() {
    }

    /// <summary>
    /// This method handles incoming Dns Packets
    /// </summary>
    /// <param name="ipp">A Dns IPPacket to be processed</param>
    /// <returns>A boolean result</returns>
    protected override bool HandleDns(IPPacket ipp) {
      WriteIP(_dns.LookUp(ipp).ICPacket);
      return true;
    }

    /// <summary>
    /// This method handles multicast packets (not yet implemented)
    /// </summary>
    /// <param name="ipp">A multicast packet to be processed</param>
    /// <returns></returns>
    protected override bool HandleMulticast(IPPacket ipp) {
      foreach(Address addr in _marad.mcast_addr) {
        SendIP(addr, ipp.Packet);
      }
      return true;
    }
  }
}
