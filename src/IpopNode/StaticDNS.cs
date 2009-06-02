using Brunet;
using Brunet.Applications;
using Ipop;
using System;
using System.Net;

#if NUNIT
using NUnit.Framework;
#endif

namespace Ipop {
  /// <summary>Provides a static mapping for all nodes, just so that they can
  /// have a unique DNS name as required by classical applications such as
  /// Condor.  They will be of the format C111222333.ipop, where the ip address
  /// is 000.111.222.333.</summary>
  /// <remarks>An IP Address of 11.22.33.44 would translate into C022033044.ipop,
  /// the extra "0"s are necessary and keep all names at a constant length of 15
  /// (including the domain name .ipop).</remarks>
  public class StaticDNS : DNS {
    /// <summary>Called during LookUp to perform translation from hostname to IP.
    /// If an entry isn't in cache, we can try to get it from the Dht.  Throws
    /// an exception if the name is invalid and returns null if no name is found.
    /// </summary>
    /// <param name="name">The name to lookup</param>
    /// <returns>The IP Address or null if none exists for the name.  If the name
    /// is invalid, it will throw an exception.</returns>
    public override String AddressLookUp(String name)
    {
      if(!_active) {
        return null;
      }
      String res = null;
      // C 123 123 123 . domain.length
      int length = 1 + 9 + 1 + DomainName.Length;
      Console.WriteLine(name);
      if(name.Length == length && name[0] == 'C' && name.EndsWith(DomainName)) {
      Console.WriteLine(name);
        res = String.Empty;
        for(int i = 0; i < 3; i++) {
          try {
            res += Int32.Parse(name.Substring((3*i)+1, 3)) + ".";
          }
          catch {
      Console.WriteLine(name + "HMM");
            return null;
          }
        }
      Console.WriteLine(name);
        res = _base_address[0] + "." + res.Substring(0, res.Length - 1);
      }
      Console.WriteLine(name);
      return res;
    }

    /// <summary>Called during LookUp to perfrom a translation from IP to hostname.</summary>
    /// <param name="IP">The IP to look up.</param>
    /// <returns>The name or null if none exists for the IP.</returns>
    public override String NameLookUp(String IP)
    {
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
  }

#if NUNIT
  [TestFixture]
  public class StaticDNSTest {
    [Test]
    public void Test() {
      StaticDNS dns = new StaticDNS();
      Assert.AreEqual(dns.NameLookUp("10.250.1.1"), null, "NameLookUp Dns not set.");
      Assert.AreEqual(dns.AddressLookUp("C250001001.ipop"), null, "AddressLookUp Dns not set.");
      dns.SetAddressInfo(MemBlock.Reference(Utils.StringToBytes("10.250.0.0", '.')),
          MemBlock.Reference(Utils.StringToBytes("255.255.0.0", '.')));
      Assert.AreEqual(dns.NameLookUp("10.250.1.1"), "C250001001.ipop", "NameLookUp Dns set in range.");
      Assert.AreEqual(dns.NameLookUp("10.251.1.1"), null, "NameLookUp Dns set out of range.");
      Assert.AreEqual(dns.AddressLookUp("C250001001.ipop"), "10.250.1.1", "AddressLookUp Dns set.");
      Assert.AreEqual(dns.AddressLookUp("C250001001.blaha"), null, "AddressLookUp Dns set bad dns name: blaha.");
      Assert.AreEqual(dns.AddressLookUp("C250001001.blah"), null, "AddressLookUp Dns set bad dns name: blah.");
      dns.SetAddressInfo(MemBlock.Reference(Utils.StringToBytes("10.251.0.0", '.')),
          MemBlock.Reference(Utils.StringToBytes("255.255.0.0", '.')));
      Assert.AreEqual(dns.NameLookUp("10.250.1.1"), null, "NameLookUp Dns changed out of range.");
      Assert.AreEqual(dns.NameLookUp("10.251.1.1"), "C251001001.ipop", "NameLookUp Dns changed in range.");
    }

    [Test]
    public void SmallMaskTest() {
      StaticDNS dns = new StaticDNS();
      dns.SetAddressInfo(MemBlock.Reference(Utils.StringToBytes("10.1.2.0", '.')),
          MemBlock.Reference(Utils.StringToBytes("255.255.255.0", '.')));
      Assert.AreEqual(dns.NameLookUp("10.1.2.94"), "C001002094.ipop");
      Assert.AreEqual(dns.AddressLookUp("C001002094.ipop"), "10.1.2.94");
    }
  }
#endif
}
