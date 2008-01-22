using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;

namespace Ipop {
  public class DHCPServerConfig {
    public string brunet_namespace;
    public IPOPNamespace [] ipop_namespace;
  }
  public class IPOPNamespace {
    public int leasetime;
    public string value;
    public string netmask;
    public DHCPIPPool pool;
    public DHCPReservedIPs reserved;
    public int LogSize;
  }
  public class DHCPIPPool {
    public string lower;
    public string upper;
  }
  public class DHCPReservedIPs {
    public DHCPReservedIP [] value;
  }
  public class DHCPReservedIP {
    public string ip;
    public string mask;
  }

  class DHCPServerConfigurationReader {
    public static DHCPServerConfig ReadConfig(string filename) {
      XmlSerializer serializer = new XmlSerializer(typeof(DHCPServerConfig));
      FileStream fs = new FileStream(filename, FileMode.Open);
      return (DHCPServerConfig) serializer.Deserialize(fs);
    }

    public static void PrintConfig(DHCPServerConfig config) {
      Console.Error.WriteLine(config.brunet_namespace);
      foreach(IPOPNamespace item0 in config.ipop_namespace) {
        Console.Error.WriteLine("\t{0}", item0.value);
        Console.Error.WriteLine("\t\t{0}", item0.pool.lower);
        Console.Error.WriteLine("\t\t{0}", item0.pool.upper);
        foreach(DHCPReservedIP item1 in item0.reserved.value) {
          Console.Error.WriteLine("\t\t\t{0}", item1.ip);
          Console.Error.WriteLine("\t\t\t{0}", item1.mask);
        }
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