/*
Copyright (C) 2008  Pierre St Juste <ptony82@ufl.edu>, University of Florida
                    David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using NetworkPackets;
using NetworkPackets.DHCP;
using System;
using System.Collections;
using System.IO;
using System.Text;

namespace Ipop.RpcNode {

  /// <summary>
  /// Subclass of DHCPServer implements GetDHCPLeaseController method
  /// </summary>
  public class RpcDHCPServer : DHCPServer {
    public readonly byte[] LocalIP;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="networkdevice">A string indicating starting point for
    /// network probe</param>
    protected RpcDHCPServer(DHCPConfig config) : base(config) {
      LocalIP = new byte[4];
      BaseIP.CopyTo(LocalIP, 0);
      LocalIP[3] = 2;
    }

    public static RpcDHCPServer GetRpcDHCPServer(string networkdevice) {
      MemBlock IP = RpcNodeHelper.GetNetwork(networkdevice, MemBlock.Reference(new byte[]{10, 254, 0, 0}));
      byte[] nm = new byte[4] { 255, 255, 0, 0 };
      return GetRpcDHCPServer(IP, MemBlock.Reference(nm));
    }

    public static RpcDHCPServer GetRpcDHCPServer(MemBlock ip, MemBlock netmask) {
      DHCPConfig config = new DHCPConfig();
      config.LeaseTime = 3200;
      config.Netmask = Utils.MemBlockToString(netmask, '.');
      config.IPBase = Utils.MemBlockToString(ip, '.');

      config.ReservedIPs = new DHCPConfig.ReservedIP[1];
      config.ReservedIPs[0] = new DHCPConfig.ReservedIP();

      byte[] local_ip = new byte[4];
      ip.CopyTo(local_ip, 0);
      local_ip[3] = 2;

      config.ReservedIPs[0].IPBase = Utils.BytesToString(local_ip, '.');
      config.ReservedIPs[0].Mask = "255.255.255.255";

      return new RpcDHCPServer(config);
    }


    public override byte[] RequestLease(byte[] RequestedAddr, bool Renew,
                                               string node_address, params object[] para) {
      return LocalIP;
    }
  }
}
