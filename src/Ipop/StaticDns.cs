using Brunet;
using Brunet.Applications;
using Brunet.Util;
using Ipop;
using System;
using System.Net;

#if NUNIT
using NUnit.Framework;
#endif

namespace Ipop {
  /// <summary>Provides a static mapping for all nodes, just so that they can
  /// have a unique Dns name as required by classical applications such as
  /// Condor.  They will be of the format C111222333.ipop, where the ip address
  /// is 000.111.222.333.</summary>
  /// <remarks>An IP Address of 11.22.33.44 would translate into C022033044.ipop,
  /// the extra "0"s are necessary and keep all names at a constant length of 15
  /// (including the domain name .ipop).</remarks>
  public class StaticDns : Dns {
    public StaticDns(MemBlock ip_address, MemBlock netmask, string name_server,
        bool forward_queries) : 
      base(ip_address, netmask, name_server, forward_queries)
    {
    }

    /// <summary>Called during LookUp to perform translation from hostname to IP.
    /// If an entry isn't in cache, we can try to get it from the Dht.  Throws
    /// an exception if the name is invalid and returns null if no name is found.
    /// </summary>
    /// <param name="name">The name to lookup</param>
    /// <returns>The IP Address or null if none exists for the name.  If the name
    /// is invalid, it will throw an exception.</returns>
    public override String AddressLookUp(String name)
    {
      String res = null;
      // C 123 123 123 . domain.length
      int length = 1 + 9 + 1 + DomainName.Length;

      if(name.Length != length ||
          (name[0] != 'c' && name[0] != 'C') ||
          !name.EndsWith(DomainName, StringComparison.OrdinalIgnoreCase))
      {
        return null;
      }

      res = String.Empty;
      for(int i = 0; i < 3; i++) {
        res += Int32.Parse(name.Substring((3*i)+1, 3)) + ".";
      }
      return _base_address[0] + "." + res.Substring(0, res.Length - 1);
    }

    /// <summary>Called during LookUp to perfrom a translation from IP to hostname.</summary>
    /// <param name="ip">The IP to look up.</param>
    /// <returns>The name or null if none exists for the IP.</returns>
    public override String NameLookUp(String ip)
    {
      if(!InRange(ip)) {
        throw new Exception("Unable to resolve");
      }

      String res = null;
      if(InRange(ip)) {
        res = "C";
        byte [] ipb = Utils.StringToBytes(ip, '.');
        for(int i = 1; i < 4; i++) {
          if(ipb[i] < 10) {
            res += "00";
          }
          else if(ipb[i] < 100) {
            res += "0";
          }
          res += ipb[i];
        }
        res += "." + DomainName;
      }
      return res;
    }
  }

#if NUNIT
  [TestFixture]
  public class StaticDnsTest {
    [Test]
    public void Test() {
      StaticDns dns = new StaticDns(
          MemBlock.Reference(Utils.StringToBytes("10.250.0.0", '.')),
          MemBlock.Reference(Utils.StringToBytes("255.255.0.0", '.')),
          string.Empty, false);

      Assert.AreEqual(dns.NameLookUp("10.250.1.1"), "C250001001.ipop", "NameLookUp Dns set in range.");

      try {
        Assert.AreEqual(dns.NameLookUp("10.251.1.1"), null, "NameLookUp Dns set out of range.");
      } catch { }

      Assert.AreEqual(dns.AddressLookUp("C250001001.ipop"), "10.250.1.1", "AddressLookUp Dns set.");

      try {
        Assert.AreEqual(dns.AddressLookUp("C250001001.blaha"), null, "AddressLookUp Dns set bad dns name: blaha.");
      } catch { }

      try {
        Assert.AreEqual(dns.AddressLookUp("C250001001.blah"), null, "AddressLookUp Dns set bad dns name: blah.");
      } catch { }

      dns = new StaticDns(
          MemBlock.Reference(Utils.StringToBytes("10.251.0.0", '.')),
          MemBlock.Reference(Utils.StringToBytes("255.255.0.0", '.')),
          string.Empty, false);

      try {
        Assert.AreEqual(dns.NameLookUp("10.250.1.1"), null, "NameLookUp Dns changed out of range.");
      } catch { }

      Assert.AreEqual(dns.NameLookUp("10.251.1.1"), "C251001001.ipop", "NameLookUp Dns changed in range.");
    }

    [Test]
    public void SmallMaskTest() {
      StaticDns dns = new StaticDns(
          MemBlock.Reference(Utils.StringToBytes("10.1.2.0", '.')),
          MemBlock.Reference(Utils.StringToBytes("255.255.255.0", '.')),
          string.Empty, false);

      Assert.AreEqual(dns.NameLookUp("10.1.2.94"), "C001002094.ipop", "test1");
      Assert.AreEqual(dns.AddressLookUp("C001002094.ipop"), "10.1.2.94", "test2");
    }
  }
#endif
}
