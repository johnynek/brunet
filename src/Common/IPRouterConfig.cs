using System;
using System.Text;
using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Ipop {
  public class IPRouterConfig {
    public string ipop_namespace;
    public string brunet_namespace;
    public string device;
    [XmlArrayItem (typeof(string), ElementName = "transport")]
    public string [] RemoteTAs;
    public EdgeListener [] EdgeListeners;
    public string NodeAddress;
    public AddressInfo AddressData;
    [XmlArrayItem (typeof(string), ElementName = "Device")]
    public string [] DevicesToBind;
    public PortEnable RpcDht;
    public PortEnable XmlRpcManager;
  }

  public class PortEnable {
    public bool Enabled;
    public int Port;
  }

  public class AddressInfo {
    public string Hostname;
    public string IPAddress;
    public string Netmask;
  }

  public class EdgeListener {
    [XmlAttribute]
    public string type;
    public int port;
  }

  public class IPRouterConfigHandler {
    public static IPRouterConfig Read(string configFile) {
      XmlSerializer serializer = new XmlSerializer(typeof(IPRouterConfig));
      IPRouterConfig config = null;
      using(FileStream fs = new FileStream(configFile, FileMode.Open)) {
        config = (IPRouterConfig) serializer.Deserialize(fs);
      }
      return config;
    }

    public static void Write(string configFile,
      IPRouterConfig config) {
      using(FileStream fs = new FileStream(configFile, FileMode.Create, 
            FileAccess.Write)) {
        XmlSerializer serializer = new XmlSerializer(typeof(IPRouterConfig));
        serializer.Serialize(fs, config);
      }
    }
  }
}
