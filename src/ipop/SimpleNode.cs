using System;
using Brunet;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;

/* The SimpleNode just works for a p2p router
 * (Doesn't generate or sink any packets)
 * Could sink; in case no route to destination is available!
 */
namespace PeerVM {
  public class SimpleNodeConfig {
    public string brunet_namespace;
    [XmlArrayItem (typeof(string), ElementName = "transport")]
    public string [] RemoteTAs;
    public EdgeListener [] EdgeListeners;
  }

  public class EdgeListener {
    [XmlAttribute]
    public string type;
    public int port;
  }

  public class SimpleNode {
    static SimpleNodeConfig config;
    static ArrayList RemoteTAs;

    private static void ReadConfiguration(string configFile) {
      XmlSerializer serializer = new XmlSerializer(typeof(SimpleNodeConfig));
      FileStream fs = new FileStream(configFile, FileMode.Open);
      config = (SimpleNodeConfig) serializer.Deserialize(fs);
      RemoteTAs = new ArrayList();
      foreach(string TA in config.RemoteTAs) {
        TransportAddress ta = new TransportAddress(TA);
        RemoteTAs.Add(ta);
      }
      fs.Close();
    }

    public static void Main(string []args) {
      if (args.Length < 1) {
        Console.WriteLine("please specify the configuration file... ");
      }

      //configuration file 
      ReadConfiguration(args[0]);

      System.Console.WriteLine("SimpleNode starting up...");

      //Make a random address
      Random my_rand = new Random();
      byte[] address = new byte[Address.MemSize];
      my_rand.NextBytes(address);
      address[Address.MemSize -1] &= 0xFE;

      //local node
      Node tmp_node = new StructuredNode(new AHAddress(address), config.brunet_namespace);

      //Where do we listen 
      foreach(EdgeListener item in config.EdgeListeners) {
        if (item.type =="tcp") { 
            tmp_node.AddEdgeListener(new TcpEdgeListener(item.port));
        }
        else if (item.type == "udp") {
            tmp_node.AddEdgeListener(new UdpEdgeListener(item.port));
        }
        else if (item.type == "udp-as") {
            tmp_node.AddEdgeListener(new ASUdpEdgeListener(item.port));
        }
        else {
          throw new Exception("Unrecognized transport: " + item.type);
        }
      }

      //Here is where we connect to some well-known Brunet endpoints
      tmp_node.RemoteTAs = RemoteTAs;

      tmp_node.Connect();
      System.Console.WriteLine("Called Connect");
      while(true) System.Threading.Thread.Sleep(1000*60*60);
    }
  }
}

