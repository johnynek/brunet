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
    protected readonly DHCPServerConfig _dhcp_config;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="networkdevice">A string indicating starting point for
    /// network probe</param>
    public RpcDHCPServer(string networkdevice) {
      MemBlock IP = RpcNodeHelper.GetNetwork(networkdevice, MemBlock.Reference(new byte[]{10, 254, 0, 0}));
      _dhcp_config = RpcNodeHelper.GenerateDHCPServerConfig(IP, MemBlock.Reference(new byte[]{255, 255, 0, 0}));
    }

    /// <summary>
    /// This method overrides GetDHCPLeaseController
    /// </summary>
    /// <param name="ipop_namespace">A string specifying ipop_namespace</param>
    /// <returns>A result RpcDHCPLeaseController</returns>
    protected override DHCPLeaseController GetDHCPLeaseController(string ipop_namespace) {
      return new RpcDHCPLeaseController(_dhcp_config);
    }

    public RpcDHCPLeaseController GetController() {
      return new RpcDHCPLeaseController(_dhcp_config);
    }
  }
}
