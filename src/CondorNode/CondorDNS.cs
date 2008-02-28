using Brunet;
using Brunet.Applications;
using Ipop;
using System;
using System.Net;

namespace Ipop.CondorNode {
  /**
  <summary>Provides a static mapping for all nodes, just so that they can
  have a unique DNS name as required by classical applications such as
  Condor.  They will be of the format C111222333.ipop, where the ip address
  is 000.111.222.333.</summary>
  <remark>An IP Address of 11.22.33.44 would translate into C022033044.ipop,
  the extra "0"s are necessary and keep all names at a constant length of 15
  (including the domain name .ipop).
  */
  public class CondorDNS: DNS {
    /// <summary></summary>
    public static readonly String SUFFIX = ".ipop";
    /// <summary></summary>
    protected volatile MemBlock _base_address;
    /// <summary></summary>
    protected volatile MemBlock _netmask;
    /// <summary></summary>
    protected Object _sync;

    /**
    <summary></summary>
     */
    public CondorDNS() {
      _sync = new Object();
    }

    /**
    <summary>This is called by the outside to let the DNS know what is the
    applicable ranges of IP Addresses to resolve.</summary>
    <param name="ip_address">An IP Address in the range.</param>
    <param name="netmask">The netmask for the range.</param>
    */
    public void UpdatePoolRange(String ip_address, String netmask) {
      byte[] ba = Utils.StringToBytes(ip_address, '.');
      byte[] nm = Utils.StringToBytes(netmask, '.');
      for(int i = 0; i < 4; i++) {
        ba[i] &= nm[i];
      }
      lock(_sync) {
        _base_address = MemBlock.Reference(ba);
        _netmask = MemBlock.Reference(nm);
      }
    }


    /**
    <summary>Called during LookUp to perform translation from hostname to IP.
    If an entry isn't in cache, we can try to get it from the Dht.  Throws
    an exception if the name is invalid and returns null if no name is found.
    </summary>
    <param name="name">The name to lookup</param>
    <returns>The IP Address or null if none exists for the name.  If the name
    is invalid, it will throw an exception.</returns>
    */
    public override String AddressLookUp(String name) {
      String res = null;
      if(name.Length == 15 && name[0] == 'C' && name.EndsWith(SUFFIX)) {
        res = String.Empty;
        for(int i = 0; i < 3; i++) {
          try {
            res += Int32.Parse(name.Substring((3*i)+1, 3)) + ".";
          }
          catch {
            return null;
          }
        }
        res = res.Substring(0, res.Length - 1);
      }
      return res;
    }

    /**
    <summary>Called during LookUp to perfrom a translation from IP to hostname.</summary>
    <param name="IP">The IP to look up.</param>
    <returns>The name or null if none exists for the IP.</returns>
    */
    public override String NameLookUp(String IP) {
      String res = null;
      if(InRange(IP)) {
        try {
          res = "C";
          byte [] ipb = Utils.StringToBytes(IP, '.');
          for(int i = 1; i < 4; i++) {
            if(ipb[i] < 10) {
              res += "00";
            }
            else if(ipb[i] < 100) {
              res += "0";
            }
            res += ipb[i];
          }
          res += ".ipop";
        }
        catch {
          res = null;
        }
      }
      return res;
    }

    /**
    <summary>Determines if an IP Address is in  the applicable range for
    the DNS server</summary>
    <param name="IP">The IP Address to test.</param>
    <returns>False if the IP Address or netmask is undefined or the Address
    is not in applicable range, True if it is.</returns>
    */
    protected bool InRange(String IP) {
      if(_base_address == null || _netmask == null) {
        return false;
      }
      byte[] ipb = Utils.StringToBytes(IP, '.');
      for(int i = 0; i < 4; i++) {
        if((ipb[i] & _netmask[i]) != _base_address[i]) {
          return false;
        }
      }
      return true;
    }
  }
}
