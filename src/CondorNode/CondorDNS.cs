using Brunet;
using Brunet.Applications;
using Ipop;
using System;
using System.Net;

#if NUNIT
using NUnit.Framework;
#endif

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
    /// <summary>All hostnames must end in this domain name</summary>
    public static readonly String SUFFIX = ".ipop";
    /// <summary>The base ip address to perfom lookups on</summary>
    protected volatile MemBlock _base_address;
    /// <summary>The mask for the ip address to perform lookups on.</summary>
    protected volatile MemBlock _netmask;
    /// <summary>Needed for multithreading.</summary>
    protected Object _sync;
    /// <summary>Becomes true after the first UpdatePoolRange.</summary>
    protected volatile bool _active;

    /**
    <summary>Initializes a new CondorDNS object.</summary>
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
    public void UpdatePoolRange(MemBlock ip_address, MemBlock netmask) {
      byte[] ba = new byte[ip_address.Length];
      for(int i = 0; i < ip_address.Length; i++) {
        ba[i] = (byte) (ip_address[i] & netmask[i]);
      }
      lock(_sync) {
        _base_address = MemBlock.Reference(ba);
        _netmask = netmask;
      }
      _active = true;
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
      if(!_active) {
        return null;
      }
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
        res = _base_address[0] + "." + res.Substring(0, res.Length - 1);
      }
      return res;
    }

    /**
    <summary>Called during LookUp to perfrom a translation from IP to hostname.</summary>
    <param name="IP">The IP to look up.</param>
    <returns>The name or null if none exists for the IP.</returns>
    */
    public override String NameLookUp(String IP) {
      if(!_active) {
        return null;
      }

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

#if NUNIT
  [TestFixture]
  public class CondorDNSTest {
    [Test]
    public void Test() {
      CondorDNS dns = new CondorDNS();
      Assert.AreEqual(dns.NameLookUp("10.250.1.1"), null, "NameLookUp Dns not set.");
      Assert.AreEqual(dns.AddressLookUp("C250001001.ipop"), null, "AddressLookUp Dns not set.");
      dns.UpdatePoolRange("10.250.0.0", "255.255.0.0");
      Assert.AreEqual(dns.NameLookUp("10.250.1.1"), "C250001001.ipop", "NameLookUp Dns set in range.");
      Assert.AreEqual(dns.NameLookUp("10.251.1.1"), null, "NameLookUp Dns set out of range.");
      Assert.AreEqual(dns.AddressLookUp("C250001001.ipop"), "10.250.1.1", "AddressLookUp Dns set.");
      Assert.AreEqual(dns.AddressLookUp("C250001001.blaha"), null, "AddressLookUp Dns set bad dns name: blaha.");
      Assert.AreEqual(dns.AddressLookUp("C250001001.blah"), null, "AddressLookUp Dns set bad dns name: blah.");
      dns.UpdatePoolRange("10.251.0.0", "255.255.0.0");
      Assert.AreEqual(dns.NameLookUp("10.250.1.1"), null, "NameLookUp Dns changed out of range.");
      Assert.AreEqual(dns.NameLookUp("10.251.1.1"), "C251001001.ipop", "NameLookUp Dns changed in range.");
    }
    [Test]
    public void SmallMaskTest() {
      CondorDNS dns = new CondorDNS();
      dns.UpdatePoolRange("10.1.2.0", "255.255.255.0");
      Assert.AreEqual(dns.NameLookUp("10.1.2.94"), "C001002094.ipop");
      Assert.AreEqual(dns.AddressLookUp("C001002094.ipop"), "10.1.2.94");
    }
  }
#endif
}
