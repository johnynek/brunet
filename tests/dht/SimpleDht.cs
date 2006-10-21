using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;

using log4net;
using log4net.Config;

using Brunet;


/* The SimpleNode just works for a p2p router
 * (Doesn't generate or sink any packets)
 * Could sink; in case no route to destination is available!
 */
namespace Brunet.Dht {


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

  public class SimpleDht {

    private static readonly log4net.ILog _log =
    log4net.LogManager.GetLogger(System.Reflection.MethodBase.
				 GetCurrentMethod().DeclaringType);
 

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
      XmlConfigurator.Configure(new System.IO.FileInfo("logconfig.xml"));
      if (args.Length < 1) {
        Console.WriteLine("please specify the configuration file... ");
      }

      //configuration file 
      ReadConfiguration(args[0]);
      int up_time = -1;
      if (args.Length >= 2) {
	up_time = Int32.Parse(args[1])*1000;
	System.Console.WriteLine("SimpleNode starting up for: {0} ms ...", up_time);
	//_log.Debug("SimpleNode starting up for: {0} ms ...", up_time);
      } else {
	System.Console.WriteLine("SimpleNode starting up forever.");
	//_log.Debug("SimpleNode starting up forever.");
      }


      //Make a random address
      Random my_rand = new Random();
      byte[] address = new byte[Address.MemSize];
      my_rand.NextBytes(address);
      address[Address.MemSize -1] &= 0xFE;

      //local node
      Node tmp_node = new StructuredNode(new AHAddress(address), config.brunet_namespace);

      //Where do we listen 
      int local_port = 0;
      foreach(EdgeListener item in config.EdgeListeners) {
	local_port = item.port;
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
      //create a Dht instance on this node
      Dht dht = null;
      if (config.dht_media.Equals("disk")) {
	dht = new Dht(tmp_node, EntryFactory.Media.Disk);
      } else if (config.dht_media.Equals("memory")) {
	dht = new Dht(tmp_node, EntryFactory.Media.Memory);	
      }
      System.Console.WriteLine("Calling Connect");
      _log.Debug("IGNORE");
      //_log.Debug(tmp_node.Address + "::::" + DateTime.UtcNow.ToString("s")
      //+ "::::Connecting::::" + local_port);
      _log.Debug(tmp_node.Address + "::::" + DateTime.UtcNow.Ticks
		 + "::::Connecting::::" + System.Net.Dns.GetHostName() + "::::" + local_port);

      try {
	tmp_node.Connect();
	if (up_time < 0) {
	  while(true) System.Threading.Thread.Sleep(1000*60*60);
	} else {
	  System.Threading.Thread.Sleep(up_time);
	}
      } catch (Exception e) {
	
      }
      System.Console.WriteLine("Time to disconnect,,,"); 
      //_log.Debug(tmp_node.Address + "::::" + DateTime.UtcNow.ToString("s")
      //+ "::::Disconnecting");
      _log.Debug(tmp_node.Address + "::::" + DateTime.UtcNow.Ticks
		 + "::::Disconnecting");
      tmp_node.Disconnect();


      System.Console.WriteLine("Sleep for 30000 ms,,,");
      //additional 30 seconds for disconnect to complete
      System.Threading.Thread.Sleep(30000);
    }
  }
}

