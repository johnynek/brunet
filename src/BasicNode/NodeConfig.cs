using System;
using System.Text;
using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Ipop {
  public class NodeConfig {
    public String BrunetNamespace;
    [XmlArrayItem (typeof(String), ElementName = "Transport")]
    public String[] RemoteTAs;
    public EdgeListener[] EdgeListeners;
    public String NodeAddress;
    [XmlArrayItem (typeof(String), ElementName = "Device")]
    public String[] DevicesToBind;
    public Service RpcDht;
    public Service XmlRpcManager;
  }

  public class Service {
    public bool Enabled;
    public int Port;
  }

  public class EdgeListener {
    [XmlAttribute]
    public String type;
    public int port;
  }

  public class NodeConfigHandler {
    public static NodeConfig Read(String path) {
      XmlSerializer serializer = new XmlSerializer(typeof(NodeConfig));
      NodeConfig config = null;
      using(FileStream fs = new FileStream(path, FileMode.Open)) {
        config = (NodeConfig) serializer.Deserialize(fs);
      }
      return config;
    }

    public static void Write(String path, NodeConfig config) {
      using(FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write)) {
        XmlSerializer serializer = new XmlSerializer(typeof(NodeConfig));
        serializer.Serialize(fs, config);
      }
    }
  }
}
