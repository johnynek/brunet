using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;

using Brunet;
using Brunet.Dht;

#if IPOP_LOG
using log4net;
using log4net.Config;
#endif


/* The SimpleNode just works for a p2p router
 * (Doesn't generate or sink any packets)
 * Could sink; in case no route to destination is available!
 */
namespace PeerVM {
  public class SimpleNodeConfig {
    public string brunet_namespace;
    public string dht_media;
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
#if IPOP_LOG
    private static readonly log4net.ILog _log =
    log4net.LogManager.GetLogger(System.Reflection.MethodBase.
                                 GetCurrentMethod().DeclaringType);
#endif

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

#if IPOP_LOG
      if (args.Length < 2) {
        Console.WriteLine("please specify the full path to the Logger " + 
          "configuration file...");
        Environment.Exit(1);
      }
      XmlConfigurator.Configure(new System.IO.FileInfo(args[1]));
#endif

      //Make a random address
      Random my_rand = new Random();
      byte[] address = new byte[Address.MemSize];
      my_rand.NextBytes(address);
      address[Address.MemSize -1] &= 0xFE;
      address[3] = 0;

      //local node
      Node tmp_node = new StructuredNode(new AHAddress(address),
        config.brunet_namespace);
#if IPOP_LOG
      string listener_log = "BeginListener::::";
#endif
      //Where do we listen 
      foreach(EdgeListener item in config.EdgeListeners) {
	
#if IPOP_LOG
	listener_log += item.type + "::::" + item.port + "::::";
#endif	

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

#if IPOP_LOG
      listener_log += "EndListener";
#endif

      //Here is where we connect to some well-known Brunet endpoints
      tmp_node.RemoteTAs = RemoteTAs;

      //following line of code enables DHT support inside the SimpleNode
      Dht dht = null;
      if (config.dht_media == null || config.dht_media.Equals("disk")) {
        dht = new Dht(tmp_node, EntryFactory.Media.Disk);
      } else if (config.dht_media.Equals("memory")) {
	dht = new Dht(tmp_node, EntryFactory.Media.Memory);
      }	

      System.Console.WriteLine("Calling Connect");
#if IPOP_LOG
      _log.Debug("IGNORE");
      _log.Debug(tmp_node.Address + "::::" + DateTime.UtcNow.Ticks
                 + "::::Connecting::::" + System.Net.Dns.GetHostName() + "::::" + listener_log);
#endif      
      tmp_node.Connect();
      while(true) System.Threading.Thread.Sleep(1000*60*60);
    }
  }
}

