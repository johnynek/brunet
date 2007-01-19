using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Text;
using System.Security.Cryptography;

using Brunet;
using Brunet.Dht;
#if IPOP_LOG
using log4net;
using log4net.Config;
#endif

namespace PeerVM {

  public class SimpleDhtClient {
#if IPOP_LOG
    private static readonly log4net.ILog _log =
    log4net.LogManager.GetLogger(System.Reflection.MethodBase.
				 GetCurrentMethod().DeclaringType);
#endif
 
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

      
    //list of keys in the system
    public static ArrayList key_list = new ArrayList();

    
    public static void Main(string[] args) 
    {
#if IPOP_LOG
      XmlConfigurator.Configure(new System.IO.FileInfo("logconfig.xml.new"));
#endif     
      if (args.Length < 1) {
        Console.WriteLine("please specify the configuration file... ");
      }

      //configuration file 
      ReadConfiguration(args[0]);

      System.Console.WriteLine("SimpleDhtClient starting up...");

      //Make a random address
      Random my_rand = new Random();
      byte[] address = new byte[Address.MemSize];
      my_rand.NextBytes(address);
      address[Address.MemSize -1] &= 0xFE;

      //local node
      Node tmp_node = new StructuredNode(new AHAddress(address), config.brunet_namespace);
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
      //create a Dht instance on this node
      FDht dht = null;
      if (config.dht_media.Equals("disk")) {
	dht = new FDht(tmp_node, EntryFactory.Media.Disk, 3);
      } else if (config.dht_media.Equals("memory")) {
	dht = new FDht(tmp_node, EntryFactory.Media.Memory, 3);	
      }

#if IPOP_LOG
      _log.Debug("IGNORE");
      _log.Debug(tmp_node.Address + "::::" + DateTime.UtcNow.Ticks
                 + "::::Connecting::::" + System.Net.Dns.GetHostName() + "::::" + listener_log);
#endif      

      tmp_node.Connect();
      System.Console.WriteLine("Called Connect");

      int oper_count = 0;
      while(true) {
	Console.Write("{0}: Enter operation (Get/Put/Create/Delete/Sleep/Done):", oper_count++);
	string str_oper = Console.ReadLine();
	try {
	if (str_oper.Equals("Put")) {
	  Console.Write("Enter key:");
	  string str_key = Console.ReadLine();
	  Console.Write("Enter data:");
	  string str_data = Console.ReadLine();
	  Console.Write("Enter password:");
	  string str_pass = Console.ReadLine();
	  Console.Write("Enter TTL:");
	  int ttl = Int32.Parse(Console.ReadLine());	  
	  Console.WriteLine("Put() on key:{0}",str_key);
	  
	  byte[] utf8_key = Encoding.UTF8.GetBytes(str_key);
	  byte[] utf8_data = Encoding.UTF8.GetBytes(str_data);
	  
	  HashAlgorithm algo = new SHA1CryptoServiceProvider();
	  byte[] utf8_pass = Encoding.UTF8.GetBytes(str_pass);
	  byte [] sha1_pass = algo.ComputeHash(utf8_pass);
	  string base64_pass = Convert.ToBase64String(sha1_pass);
	  string stored_pass = "SHA1:" + base64_pass;

	  Console.WriteLine(utf8_key.Length);
	  Console.WriteLine(utf8_data.Length);
	  Console.WriteLine(base64_pass.Length);
	  
	  BlockingQueue[] q = dht.PutF(utf8_key, ttl, stored_pass, utf8_data);
	  RpcResult res = q[0].Dequeue() as RpcResult;
	  for (int i = 0; i < q.Length; i++) {
	    q[i].Close();
	  }
	  key_list.Add(utf8_key);
	  Console.WriteLine("RpcResult for Put(): {0}", res.Result);

	} else if (str_oper.Equals("Create")) {
	  Console.Write("Enter key:");
	  string str_key = Console.ReadLine();
	  Console.Write("Enter data:");
	  string str_data = Console.ReadLine();
	  Console.Write("Enter password:");
	  string str_pass = Console.ReadLine();
	  Console.Write("Enter TTL:");
	  int ttl = Int32.Parse(Console.ReadLine());
	  Console.WriteLine("Create() on key:{0}",str_key);	  

	  byte[] utf8_key = Encoding.UTF8.GetBytes(str_key);
	  byte[] utf8_data = Encoding.UTF8.GetBytes(str_data);
	  
	  HashAlgorithm algo = new SHA1CryptoServiceProvider();
	  byte[] utf8_pass = Encoding.UTF8.GetBytes(str_pass);

	  byte [] sha1_pass = algo.ComputeHash(utf8_pass);
	  string base64_pass = Convert.ToBase64String(sha1_pass);
	  string stored_pass = "SHA1:" + base64_pass;

	  BlockingQueue[] q = dht.CreateF(utf8_key, ttl, stored_pass, utf8_data);
	  RpcResult res = q[0].Dequeue() as RpcResult;
	  for (int i = 0; i < q.Length; i++) {
	    q[i].Close();
	  }
	  Console.WriteLine("RpcResult for Create(): {0}", res.Result);
	  key_list.Add(utf8_key);
	} else if (str_oper.Equals("Get")) {
	  Console.Write("Enter key:");
	  string str_key = Console.ReadLine();
	  
	  byte[] utf8_key = Encoding.UTF8.GetBytes(str_key);

	  BlockingQueue[] q = dht.GetF(utf8_key, 500, null);
	  int count = 0;

	  RpcResult res = q[0].Dequeue() as RpcResult;
	  ArrayList result = res.Result as ArrayList;
	  
	  if (result == null || result.Count < 3) {
	    Console.WriteLine("Something messed up with Get()...");
	    continue;
	  }
	  Console.WriteLine("Result from Get() looks good: " + result.Count);
	  ArrayList values = (ArrayList) result[0];
	  Console.WriteLine("# of matching entries: " + values.Count);
	  foreach (Hashtable ht in values) {
	    Console.WriteLine(ht["age"]);
	    byte[] data = (byte[]) ht["data"];
	    string val = Encoding.UTF8.GetString(data);
	    Console.WriteLine(val);
	  }
	  for (int i = 0; i < q.Length; i++) {
	    q[i].Close();
	  }
	} else if (str_oper.Equals("Delete")) {
	  Console.Write("Enter key:");
	  string str_key = Console.ReadLine();
	  Console.Write("Enter password:");
	  string str_pass = Console.ReadLine();
	  Console.WriteLine("Delete on key: {0}", str_key );

	  byte[] utf8_key = Encoding.UTF8.GetBytes(str_key);
	  byte[] utf8_pass = Encoding.UTF8.GetBytes(str_pass);

	  string base64_pass = Convert.ToBase64String(utf8_pass);
	  string send_pass = "SHA1:" + base64_pass;

	  int r_idx = -1;
	  for (int i = 0; i < key_list.Count; i++) {
	    byte[] key = (byte[]) key_list[i];
	    string skey =  Encoding.UTF8.GetString(key);
	    if (str_key.Equals(skey)) {
	      r_idx = i;
	      break;
	    }
	  }
	  if (r_idx >= 0) {
	    key_list.RemoveAt(r_idx);
	  } else {
	    Console.WriteLine("Fatal: Requested deletion of a non-existent key: {0}", str_key);
	  }
	  BlockingQueue[] q = dht.DeleteF(utf8_key, send_pass);
	  RpcResult res = q[0].Dequeue() as RpcResult;
	  object o = res.Result;
	  for (int i = 0; i < q.Length; i++) {
	    q[i].Close();
	  }
	} else if (str_oper.Equals("Done")) {
	  System.Console.WriteLine("Time to disconnect,,,"); 
#if IPOP_LOG
	  _log.Debug(tmp_node.Address + "::::" + DateTime.UtcNow.Ticks
		     + "::::Disconnecting");
#endif
	  tmp_node.Disconnect();

	  System.Console.WriteLine("Sleep for 10000 ms,,,");
	  //additional 10 seconds for disconnect to complete
	  System.Threading.Thread.Sleep(10000);
	  break;
	} else if (str_oper.Equals("Sleep")) {
	  Console.Write("Enter sleep time:");
	  string str_sleep = Console.ReadLine();	
	  Console.WriteLine("You want me to sleep for: {0} ms", str_sleep);
	  int sleep_time = (int) Double.Parse(str_sleep.Trim());
	  Console.WriteLine("Going to sleep for some time: {0} ms", sleep_time);
	  System.Threading.Thread.Sleep(sleep_time);	  
	}
	} catch (Exception e) {
	  Console.Error.WriteLine(e);
	}
      }
    }
  }
}
