using System;
using System.IO;
using System.Text;
using System.Collections;

using System.Security.Cryptography;

namespace Ipop {
  abstract public class DHCPLeaseParam {
  }

  public struct Lease {
    public byte [] ip;
    public byte [] hwaddr;
    public DateTime expiration;
  }

  public class DHCPLeaseResponse {
    public byte [] ip;
    public byte [] netmask;
    public byte [] leasetime;
  }

  abstract public class DHCPLease {
    protected int size, index, leasetime;
    protected long logsize;
    protected string namespace_value;
    protected byte [] netmask;
    protected byte [] lower;
    protected byte [] upper;
    protected byte [] leasetimeb;
    protected byte [][] reservedIP;
    protected byte [][] reservedMask;


    public DHCPLease(IPOPNamespace config) {
      leasetime = config.leasetime;
      leasetimeb = new byte[]{((byte) ((leasetime >> 24))),
        ((byte) ((leasetime >> 16))),
        ((byte) ((leasetime >> 8))),
        ((byte) (leasetime))};
      namespace_value = config.value;
      logsize = config.LogSize * 1024; /* Bytes */
      lower = IPOP_Common.StringToBytes(config.pool.lower, '.');
      upper = IPOP_Common.StringToBytes(config.pool.upper, '.');
      netmask = IPOP_Common.StringToBytes(config.netmask, '.');

      if(config.reserved != null) {
        reservedIP = new byte[config.reserved.value.Length + 1][];
        reservedMask = new byte[config.reserved.value.Length + 1][];
        for(int i = 1; i < config.reserved.value.Length + 1; i++) {
          reservedIP[i] = IPOP_Common.StringToBytes(
            config.reserved.value[i-1].ip, '.');
          reservedMask[i] = IPOP_Common.StringToBytes(
            config.reserved.value[i-1].mask, '.');
        }
      }
      else {
        reservedIP = new byte[1][];
        reservedMask = new byte[1][];
      }
      reservedIP[0] = new byte[4];

      for(int i = 0; i < 3; i++)
        reservedIP[0][i] = (byte) (lower[i] & netmask[i]);
      reservedIP[0][3] = 1;
      reservedMask[0] = new byte[4] {255, 255, 255, 255};
    }

    protected bool ValidIP(byte [] ip) {
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

   protected byte [] IncrementIP(byte [] ip) {
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
              this.size = this.index;
              this.index = 0;
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

    public abstract DHCPLeaseResponse GetLease(DHCPLeaseParam param, byte messageType);
  }
}
