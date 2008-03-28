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

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Security.Cryptography;
using Brunet.Applications;

namespace Ipop {
  /// <summary>
  /// This is a subclass of DHCPLeaseController, it implements GetLease method
  /// </summary>
  public class RpcDHCPLeaseController : DHCPLeaseController {

    /// <summary>
    /// Constructor takes DHCPServerConfig object and passes it to base 
    /// constructor.
    /// </summary>
    /// <param name="config">A DHCPServerConfig passed to base</param>
    public RpcDHCPLeaseController(DHCPServerConfig config) : base(config) {}

    /// <summary>
    /// This method creates the DHCPReply that will be return to DHCP server
    /// </summary>
    /// <param name="address">A byte array representing the last ip</param>
    /// <param name="renew">A boolean that determines if it's renewal request</param>
    /// <param name="node_address"> A string representing brunet node address</param>
    /// <param name="para"> An object array for additional params</param>
    /// <returns>A DHCP reply with IP, netmask, leasetime</returns>
    public override DHCPReply GetLease(byte[] address, bool renew,
                                   string node_address, params object[] para) {
      DHCPReply reply = new DHCPReply();
      reply.ip = lower;
      reply.ip[3] = 2;
      reply.netmask = netmask;
      reply.leasetime = leasetimeb;
      return reply;
    }
  }
}