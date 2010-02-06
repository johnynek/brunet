/*
Copyright (C) 2010 Pierre St Juste <ptony82@ufl.edu>, University of Florida

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
using System.Collections.Generic;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  public class SocialDnsManager {

    protected readonly Dictionary<string, DnsMapping> _mappings;

    protected readonly List<DnsMapping> _tmappings;

    protected readonly SocialNode _node;

    protected readonly SocialUser _local_user;

    public SocialDnsManager(SocialNode node, SocialUser local_user) {
      _mappings = new Dictionary<string, DnsMapping>();
      _tmappings = new List<DnsMapping>();
      _node = node;
      _local_user = local_user;
    }

    public bool AddMapping(string mapping) {
      string[] parts = mapping.Split(DnsMapping.DELIM);
      string alias = parts[0];
      string address = parts[1];
      string source = _local_user.Name;
      return AddMapping(new DnsMapping(alias, address, source));
    }

    public bool AddMapping(DnsMapping mapping) {
      Console.WriteLine("Adding " + mapping);
      _mappings.Add(mapping.Alias, mapping);
      //TODO - Add to RpcIpopNode (real dns system)
      _node.AddDnsMapping(mapping.Alias, mapping.Address);
      return true;
    }

    public string GetMapping(string alias) {
      Console.WriteLine("Getting " + alias);
      string result = DnsMapping.MISS;
      if (_mappings.ContainsKey(alias)) {
        result = _mappings[alias].ToString();
        Console.WriteLine("Found " + result);
      }
      return result;
    }

    public bool AddTmpMapping(string mapping) {
      string[] parts = mapping.Split(DnsMapping.DELIM);
      string alias = parts[0];
      string address = parts[1];
      string source = parts[2];
      return AddTmpMapping(new DnsMapping(alias, address, source));
    }

    public bool AddTmpMapping(DnsMapping mapping) {
      Console.WriteLine("Adding tmp " + mapping);
      foreach (DnsMapping tmapping in _tmappings) {
        if (mapping.Equals(tmapping)) {
          Console.WriteLine("Incrementing rating");
          tmapping.Rating++;
          return true;
        }
      }
      _tmappings.Add(mapping);
      return true;
    }

    public void ClearResults() {
      _tmappings.Clear();
    }

    public string GetState() {
      DnsState state = new DnsState();
      state.Mappings = new DnsMapping[_mappings.Count];
      _mappings.Values.CopyTo(state.Mappings, 0);
      _tmappings.Sort(new MappingComparer());
      state.TmpMappings = _tmappings.ToArray();
      return SocialUtils.ObjectToXml1<DnsState>(state);
    }
  }

  public class DnsMapping {

    public const char DELIM = ':';
    public const string MISS = "miss";
    public string Alias;
    public string Address;
    public string Source;
    public int Rating;

    public DnsMapping() {}

    public DnsMapping(string alias, string address, string source) {
      Alias = alias;
      Address = address;
      Source = source;
      Rating = 1;
    }

    public bool Equals(DnsMapping mapping) {
      return ( mapping.Alias == Alias && mapping.Address == Address);
    }

    public override string ToString() {
      return Alias + DELIM + Address + DELIM + Source + DELIM + Rating;
    }
  }

  public class DnsState {
    public DnsMapping[] Mappings;
    public DnsMapping[] TmpMappings;
  }

  public class MappingComparer : IComparer<DnsMapping> {
    public int Compare(DnsMapping x, DnsMapping y) {
      return y.Rating - x.Rating;
    }
  }



#if SVPN_NUNIT
  [TestFixture]
  public class SocialDnsManagerTester {
    [Test]
    public void SocialDnsManagerTest() {
      Assert.AreEqual("test", "test");
      SocialDnsManager sdm = new SocialDnsManager();
      DnsMapping mapping = new DnsMapping("www.pierre.sdns", "127.0.0.1",
        "source", "rating");
      sdm.AddMapping(mapping);
      sdm.AddTmpMapping(mapping);
      sdm.GetMapping(mapping.Alias);
      string xml = sdm.GetState();
      Console.WriteLine(xml);
    }
  } 
#endif
}
