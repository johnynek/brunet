/*
Copyright (C) 2008  Pierre St Juste <ptony82@ufl.edu>, University of Florida
                    David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using NetworkPackets.Dhcp;
using System;
using System.Collections;
using System.IO;
using System.Text;

namespace Ipop.Managed {

  /// <summary>
  /// Subclass of DhcpServer implements GetDhcpLeaseController method
  /// </summary>
  public class ManagedDhcpServer : DhcpServer {
    public readonly byte[] LocalIP;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="networkdevice">A string indicating starting point for
    /// network probe</param>
    protected ManagedDhcpServer(DHCPConfig config) : base(config) {
      LocalIP = new byte[4];
      BaseIP.CopyTo(LocalIP, 0);
      LocalIP[3] = 2;
    }

    public static ManagedDhcpServer GetManagedDhcpServer(string networkdevice) {
      MemBlock IP = ManagedNodeHelper.GetNetwork(networkdevice, MemBlock.Reference(new byte[]{172, 31, 0, 0}));
      byte[] nm = new byte[4] { 255, 255, 0, 0 };
      return GetManagedDhcpServer(IP, MemBlock.Reference(nm));
    }

    public static ManagedDhcpServer GetManagedDhcpServer(MemBlock ip, MemBlock netmask) {
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

      return new ManagedDhcpServer(config);
    }


    public override byte[] RequestLease(byte[] RequestedAddr, bool Renew,
                                               string node_address, params object[] para) {
      return LocalIP;
    }
  }
}
