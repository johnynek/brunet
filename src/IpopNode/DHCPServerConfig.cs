/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Xml;
using System.Xml.Serialization;
using System.IO;

namespace Ipop {
  /// <summary>A Configuration class for the DHCP Lease Controller</summary>
  public class DHCPServerConfig {
    /// <summary>The length of a lease.</summary>
    public int leasetime;
    /// <summary>The name of the IPOP Namespace.</summary>
    public string Namespace;
    /// <summary>The netmask for the IPOP Namespace.</summary>
    public string netmask;
    /// <summary>The available IP range for the IPOP Namespace.</summary>
    public IPPool pool;
    /// <summary>An array of reserved IP Addresses.</summary>
    public ReservedIP[] ReservedIPs;
    /// <summary>The maximum size for a DHCP Log (optional)</summary>
    public int LogSize;

    /// <summary>Defines an available IP range</summary>
    public class IPPool {
      /// <summary>The lower bound.</summary>
      public string lower;
      /// <summary>The upper bound.</summary>
      public string upper;
    }

    /// <summary>Specifies reserved IP cases</summary>
    public class ReservedIP {
      /// <summary>The address to reserve.</summary>
      public string ip;
      /// <summary>The mask to use.</summary>
      public string mask;
    }

    /**
    <summary>Reads an XML File version of the DHCPServerConfig</summary>
    <param name="filename">The path and filename of the XML File containing the
    DHCPServerConfig</param>
    <returns>A DHCPServerconfig object</returns>
    */
    public static DHCPServerConfig ReadConfig(string filename) {
      XmlSerializer serializer = new XmlSerializer(typeof(DHCPServerConfig));
      FileStream fs = new FileStream(filename, FileMode.Open);
      return (DHCPServerConfig) serializer.Deserialize(fs);
    }

    /**
    <summary>Prints a DHCPServer config to the console.</summary>
    <param name="config">The DHCPServerConfig object to print</param>
    */
    public static void PrintConfig(DHCPServerConfig config) {
      Console.Error.WriteLine(config.Namespace);
      Console.Error.WriteLine("\t\t{0}", config.pool.lower);
      Console.Error.WriteLine("\t\t{0}", config.pool.upper);
      foreach(DHCPServerConfig.ReservedIP item0 in config.ReservedIPs) {
          Console.Error.WriteLine("\t\t\t{0}", item0.ip);
          Console.Error.WriteLine("\t\t\t{0}", item0.mask);
        }
      }

/*  Unused Example Code
    public static void Main(string [] args) {
      WriteConfig(args[0]);
      ReadConfig(args[0]);
    }

    public static void WriteConfig(string filename) {
      XmlSerializer serializer = new XmlSerializer(typeof(DHCPServerConfig));
      TextWriter writer = new StreamWriter(filename);
      DHCPServerConfig config = new DHCPServerConfig();
      config.brunet_namespace = "brunet";
      config.ipop_namespace = new IPOPNamespace[2];
      config.ipop_namespace[0] = new IPOPNamespace();
      config.ipop_namespace[0].value = "ipop";
      config.ipop_namespace[0].pool = new DHCPIPPool();
      config.ipop_namespace[0].pool.lower = "192.168.0.1";
      config.ipop_namespace[0].pool.upper = "192.168.0.255";
      config.ipop_namespace[0].reserved = new DHCPReservedIPs();
      config.ipop_namespace[0].reserved.value = new DHCPReservedIP[2];
      config.ipop_namespace[0].reserved.value[0] = new DHCPReservedIP();
      config.ipop_namespace[0].reserved.value[0].ip = "192.168.0.1";
      config.ipop_namespace[0].reserved.value[0].mask = "255.255.255.255";
      config.ipop_namespace[0].reserved.value[1] = new DHCPReservedIP();
      config.ipop_namespace[0].reserved.value[1].ip = "192.168.0.3";
      config.ipop_namespace[0].reserved.value[1].mask = "255.255.255.255";

      config.ipop_namespace[1] = new IPOPNamespace();
      config.ipop_namespace[1].value = "ipop";
      config.ipop_namespace[1].pool = new DHCPIPPool();
      config.ipop_namespace[1].pool.lower = "192.168.0.1";
      config.ipop_namespace[1].pool.upper = "192.168.0.255";
      config.ipop_namespace[1].reserved = new DHCPReservedIPs();
      config.ipop_namespace[1].reserved.value = new DHCPReservedIP[2];
      config.ipop_namespace[1].reserved.value[0] = new DHCPReservedIP();
      config.ipop_namespace[1].reserved.value[0].ip = "192.168.0.1";
      config.ipop_namespace[1].reserved.value[0].mask = "255.255.255.255";
      config.ipop_namespace[1].reserved.value[1] = new DHCPReservedIP();
      config.ipop_namespace[1].reserved.value[1].ip = "192.168.0.3";
      config.ipop_namespace[1].reserved.value[1].mask = "255.255.255.255";
      serializer.Serialize(writer, config);
      writer.Close();
    }
*/
  }
}