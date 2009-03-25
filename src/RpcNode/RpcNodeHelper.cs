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
using System.Collections;
using Brunet;
using Brunet.Applications;

namespace Ipop.RpcNode {
  /// <summary>
  /// This class implements various methods needed for the RpcIpopNode
  /// </summary>
  public class RpcNodeHelper {
    /// <summary> 
    /// Finds an available IP range on the system 
    /// </summary>
    /// <param name="networkdevice">Device to be ignored</param> 
    /// <param name="startip">Device to be ignored</param> 
    /// <returns>Return IP to use</returns> 
    public static MemBlock GetNetwork(string networkdevice, MemBlock startip) {
      IPAddresses ipaddrs = IPAddresses.GetIPAddresses();
      ArrayList used_networks = new ArrayList();
      MemBlock netip = startip;

      foreach (Hashtable ht in ipaddrs.AllInterfaces) {
        if (ht["inet addr"] != null && ht["Mask"] != null
            && (string)ht["interface"] != networkdevice) {
          MemBlock addr = MemBlock.Reference(Utils.StringToBytes((string) ht["inet addr"], '.'));
          MemBlock mask = MemBlock.Reference(Utils.StringToBytes((string) ht["Mask"], '.'));

          byte[] tmp = new byte[addr.Length];
          for(int i = 0; i < addr.Length; i++) {
            tmp[i] = (byte)(addr[i] & mask[i]);
          }
          used_networks.Add(MemBlock.Reference(tmp));
        }
      }

      while(used_networks.Contains(netip)) {
        byte[] tmp = new byte[netip.Length];
        netip.CopyTo(tmp, 0);
        if (tmp[1] == 0) {
          throw new Exception("Out of Addresses!");
        }
        tmp[1] -= 1;
        netip = MemBlock.Reference(tmp);
      }
      return netip;
    }

    /// <summary>
    /// This method replaces IP addresses based on some identifier
    /// </summary>
    /// <param name="payload">Payload to be translated</param>
    /// <param name="old_ss_ip">Old source IP address</param>
    /// <param name="old_sd_ip">Old destination IP</param>
    /// <param name="new_ss_ip">New source IP address</param>
    /// <param name="new_sd_ip">New destination IP address</param>
    /// <param name="packet_id">A packet identifier</param>
    /// <returns>A MemBlock of the translated payload</returns>
    public static MemBlock TextTranslate(MemBlock payload, string old_ss_ip,
                                         string old_sd_ip, string new_ss_ip,
                                         string new_sd_ip, string packet_id) {
      string sdata = payload.GetString(System.Text.Encoding.UTF8);
      if(sdata.Contains(packet_id)) {
        sdata = sdata.Replace(old_ss_ip, new_ss_ip);
        sdata = sdata.Replace(old_sd_ip, new_sd_ip);
        payload = MemBlock.Reference(System.Text.Encoding.UTF8.GetBytes(sdata));
      }
      return payload;
    }
  }
}
