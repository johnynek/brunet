using System;
using Brunet;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Net;



/* The SimpleNode just works for a p2p router
 * (Doesn't generate or sink any packets)
 * Could sink; in case no route to destination is available!
 */
namespace Ipop {
  public class SimpleNodeConfig {
    public string brunet_namespace;
    [XmlArrayItem (typeof(string), ElementName = "transport")]
    public string [] RemoteTAs;
    public EdgeListener [] EdgeListeners;
  }

  public class EdgeListener {
    [XmlAttribute]
    public string type;
    public string port, port_high, port_low;
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
        Console.WriteLine("please specify the SimpleNode configuration " + 
          "file... ");
        Environment.Exit(0);
      }

      //configuration file 
      ReadConfiguration(args[0]);

      //Make a random address
      Random my_rand = new Random();
      byte[] address = new byte[Address.MemSize];
      my_rand.NextBytes(address);
      address[Address.MemSize -1] &= 0xFE;

      //local node
      Node tmp_node = new StructuredNode(new AHAddress(address),
        config.brunet_namespace);
      OSDependent routines = new OSDependent();
      //Where do we listen 
      foreach(EdgeListener item in config.EdgeListeners) {
        int port = 0;
        if(item.port_high != null && item.port_low != null && item.port == null) {
          int port_high = Int32.Parse(item.port_high);
          int port_low = Int32.Parse(item.port_low);
          Random random = new Random();
          port = (random.Next() % (port_high - port_low + 1)) + port_low;
        }
        else
            port = Int32.Parse(item.port);
        if (item.type =="tcp") { 
            tmp_node.AddEdgeListener(new TcpEdgeListener(port));
        }
        else if (item.type == "udp") {
            tmp_node.AddEdgeListener(new UdpEdgeListener(port));
        }
        else if (item.type == "udp-as") {
            tmp_node.AddEdgeListener(new ASUdpEdgeListener(port));
        }
        else {
          throw new Exception("Unrecognized transport: " + item.type);
        }
      }

      //Here is where we connect to some well-known Brunet endpoints
      tmp_node.RemoteTAs = RemoteTAs;
      System.Console.WriteLine("Calling Connect");
      tmp_node.Connect();
      while(true) System.Threading.Thread.Sleep(1000*60*60);
    }
  }
}

