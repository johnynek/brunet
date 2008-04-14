/*
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
  /// <summary>Contains a lease response.</summary>
  public class DHCPReply {
    /// <summary>The given IP Address.</summary>
    public byte [] ip;
    /// <summary>The given netmask.</summary>
    public byte [] netmask;
    /// <summary>The given lease time.</summary>
    public byte [] leasetime;
  }

  /// <summary>Allocates IP Addresses based upon a DHCPServerConfig</summary>
  public abstract class DHCPLeaseController {
    /// <summary>The Server's IP Address</summary>
    public readonly byte[] ServerIP;

    /// <summary>Defines an existing lease.</summary>
    protected struct Lease {
      /// <summary>A given away IP.</summary>
      public byte [] ip;
      /// <summary>The associated value, hardware address or node address.</summary>
      public byte [] hwaddr;
      /// <summary>When the lease expires.</summary>
      public DateTime expiration;
    }

    /// <summary>Random number generator to guess IP Addresses</summary>
    protected Random _rand = new Random();
    /// <summary>The lease time given for all leases.</summary>
    protected int leasetime;
    /// <summary>Maximum supported log size.</summary>
    protected long logsize;
    /// <summary>The namespace name.</summary>
    protected string namespace_value;
    /// <summary>The netmask for the namespace.</summary>
    protected byte [] netmask;
    /// <summary>The lowest available IP Address for the namespace.</summary>
    protected byte [] lower;
    /// <summary>The highest available IP Address for the namespace.</summary>
    protected byte [] upper;
    /// <summary>Byte array representation of the lease time.</summary>
    protected byte [] leasetimeb;
    /// <summary>A list of reserved IPs</summary>
    protected byte [][] reservedIP;
    /// <summary>Netmasks mapped to the list of reserved IPs.</summary>
    protected byte [][] reservedMask;

    /**
    <summary>Creates a DHCPLeaseController based upon the given config</summary>
    <param name="config">A IPOPNamespace object.</param>
    */
    public DHCPLeaseController(DHCPServerConfig config) {
      leasetime = config.leasetime;
      leasetimeb = new byte[]{((byte) ((leasetime >> 24))),
        ((byte) ((leasetime >> 16))),
        ((byte) ((leasetime >> 8))),
        ((byte) (leasetime))};
      namespace_value = config.Namespace;
      logsize = config.LogSize * 1024; /* Bytes */
      lower = Utils.StringToBytes(config.pool.lower, '.');
      upper = Utils.StringToBytes(config.pool.upper, '.');
      netmask = Utils.StringToBytes(config.netmask, '.');

      if(config.ReservedIPs != null) {
        reservedIP = new byte[config.ReservedIPs.Length + 1][];
        reservedMask = new byte[config.ReservedIPs.Length + 1][];
        for(int i = 1; i < config.ReservedIPs.Length + 1; i++) {
          reservedIP[i] = Utils.StringToBytes(
            config.ReservedIPs[i-1].ip, '.');
          reservedMask[i] = Utils.StringToBytes(
            config.ReservedIPs[i-1].mask, '.');
        }
      }
      else {
        reservedIP = new byte[1][];
        reservedMask = new byte[1][];
      }
      reservedIP[0] = new byte[4];
      ServerIP = new byte[4];

      for(int i = 0; i < 3; i++) {
        reservedIP[0][i] = (byte) (lower[i] & netmask[i]);
        ServerIP[i] = reservedIP[0][i];
      }
      reservedIP[0][3] = 1;
      ServerIP[3] = 1;
      reservedMask[0] = new byte[4] {255, 255, 255, 255};
    }

    /**
    <summary>Makes sure that an IP Address is valid.</summary>
    <param name="ip">Checks to see if an IP Address is valid.</param>
    */
    public bool ValidIP(byte [] ip) {
      /* No 255 or 0 in ip[3]] */
      if(ip[3] == 255 || ip[3] == 0)
        return false;
      /* Check range */
      for(int i = 0; i < ip.Length; i++)
        if(ip[i] < lower[i] || ip[i] > upper[i])
          return false;
      /* Check Reserved */
      for(int i = 0; i < reservedIP.Length; i++) {
        for(int j = 0; j < reservedIP[i].Length; j++) {
          if((ip[j] & reservedMask[i][j]) != 
            (reservedIP[i][j] & reservedMask[i][j]))
            break;
          if(j == reservedIP[i].Length - 1)
            return false;
        }
      }
      return true;
    }

    /**
    <summary>Increments the inputted IP Address to the next valid one.</summary>
    <param name="ip">The IP Address to increment.</param>
    */
    public byte [] IncrementIP(byte [] ip) {
      if(ip[3] == 0) {
        ip[3] = 1;
      }
      else if(ip[3] == 254 || ip[3] == upper[3]) {
        ip[3] = lower[3];
        if(ip[2] < upper[2])
          ip[2]++;
        else {
          ip[2] = lower[2];
          if(ip[1] < upper[1])
            ip[1]++;
          else {
            ip[1] = lower[1];
            if(ip[0] < upper[0])
              ip[0]++;
            else {
              ip[0] = lower[0];
            }
          }
        }
      }
      else {
        ip[3]++;
      }

      if(!ValidIP(ip))
        ip = IncrementIP(ip);

      return ip;
    }

    /**
    <summary>Generates a random IP Address in the valid address range.</summary>
    */
    protected byte[] GenerateRandomIPAddress() {
      byte[] randomIP = new byte[4];
      for (int k = 0; k < randomIP.Length; k++) {
        int max = upper[k];
        int min = lower[k];
        if(k == randomIP.Length - 1) {
          max = (max > 254) ?  254 : max;
          min = (min < 1) ?  1 : min;
        }
        randomIP[k] = (byte) _rand.Next(min, max + 1);
      }
      return randomIP;
    }

    /**
    <summary>This attempts to generate a valid Random IPAddress and throws an
    exception, if after 100 tries, it still has not generated a valid IP
    Address.</summary>
    */
    public byte[] RandomIPAddress() {
      int i = 100;
      while(i-- > 0) {
        byte[] ip = GenerateRandomIPAddress();
        if(ValidIP(ip)) {
          return ip;
        }
      }
      throw new Exception("Unable to generate a random IP Address");
    }

    /**
    <summary>Implemented in sub-classes, used to get a lease.</summary>
    <param name="address">A requested IP Address</param>
    <param name="renew">Is this an attempt to renew?</param>
    <param name="node_address">The unique identifier for this node, such as a
    Node Address.</param>
    <param name="para">Extra parameters.</param>
    */
    public abstract DHCPReply GetLease(byte[] address, bool renew,
                                       string node_address, params object[] para);
  }
}
