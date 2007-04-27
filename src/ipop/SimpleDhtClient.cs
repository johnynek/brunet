using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Text;
using System.Security.Cryptography;

using Brunet;
using Brunet.Dht;

namespace Ipop {

  public class SimpleDhtClient {
    public static void Main(string[] args) 
    {
      OSDependent.DetectOS();
      if (args.Length < 1) {
        Console.Error.WriteLine("please specify the SimpleNode configuration " + 
          "file... ");
        Environment.Exit(0);
      }

      //configuration file 
      IPRouterConfig config = IPRouterConfigHandler.Read(args[0]);

      //local node
      Node brunetNode = new StructuredNode(IPOP_Common.GenerateAHAddress(),
        config.brunet_namespace);
      //Where do we listen 
      foreach(EdgeListener item in config.EdgeListeners) {
        int port = Int32.Parse(item.port);
        Brunet.EdgeListener el = null;
        if(config.DevicesToBind == null) {
          if (item.type =="tcp")
            el = new TcpEdgeListener(port);
          else if (item.type == "udp")
            el = new UdpEdgeListener(port);
          else if (item.type == "udp-as")
            el = new ASUdpEdgeListener(port);
	  else if (item.type == "tunnel")
            el = new TunnelEdgeListener(brunetNode);
          else
            throw new Exception("Unrecognized transport: " + item.type);
        }
        else {
/*          if (item.type =="tcp")
            el = new TcpEdgeListener(port, (IEnumerable) (new IPAddresses(config.DevicesToBind)), null);*/
          if (item.type == "udp")
            el = new UdpEdgeListener(port, OSDependent.GetIPAddresses(config.DevicesToBind));
/*          else if (item.type == "udp-as")
            el = new ASUdpEdgeListener(port, (IEnumerable) (new IPAddresses(config.DevicesToBind)), null);*/
	  else if (item.type == "tunnel")
            el = new TunnelEdgeListener(brunetNode);
          else
            throw new Exception("Unrecognized transport: " + item.type);
        }
        brunetNode.AddEdgeListener(el);
      }


      //Here is where we connect to some well-known Brunet endpoints
      ArrayList RemoteTAs = new ArrayList();
      foreach(string ta in config.RemoteTAs)
        RemoteTAs.Add(TransportAddressFactory.CreateInstance(ta));
      brunetNode.RemoteTAs = RemoteTAs;

      

      //following line of code enables DHT support inside the SimpleNode
      FDht dht = null;
      if (config.dht_media == null || config.dht_media.Equals("disk")) {
        dht = new FDht(brunetNode, EntryFactory.Media.Disk, 3);
      } else if (config.dht_media.Equals("memory")) {
	dht = new FDht(brunetNode, EntryFactory.Media.Memory, 3);
      }	

      System.Console.Error.WriteLine("Calling Connect");

      brunetNode.Connect();

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
	  Console.Error.WriteLine("Put() on key:{0}",str_key);
	  
	  byte[] utf8_key = Encoding.UTF8.GetBytes(str_key);
	  byte[] utf8_data = Encoding.UTF8.GetBytes(str_data);
	  
	  HashAlgorithm algo = new SHA1CryptoServiceProvider();
	  byte[] utf8_pass = Encoding.UTF8.GetBytes(str_pass);
	  byte [] sha1_pass = algo.ComputeHash(utf8_pass);
	  string base64_pass = Convert.ToBase64String(sha1_pass);
	  string stored_pass = "SHA1:" + base64_pass;

	  Console.Error.WriteLine(utf8_key.Length);
	  Console.Error.WriteLine(utf8_data.Length);
	  Console.Error.WriteLine(base64_pass.Length);
	  
	  BlockingQueue[] q = dht.PutF(utf8_key, ttl, stored_pass, utf8_data);
	  RpcResult res = q[0].Dequeue() as RpcResult;
	  for (int i = 0; i < q.Length; i++) {
	    q[i].Close();
	  }
	  Console.Error.WriteLine("RpcResult for Put(): {0}", res.Result);

	} else if (str_oper.Equals("Create")) {
	  Console.Write("Enter key:");
	  string str_key = Console.ReadLine();
	  Console.Write("Enter data:");
	  string str_data = Console.ReadLine();
	  Console.Write("Enter password:");
	  string str_pass = Console.ReadLine();
	  Console.Write("Enter TTL:");
	  int ttl = Int32.Parse(Console.ReadLine());
	  Console.Error.WriteLine("Create() on key:{0}",str_key);	  

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
	  Console.Error.WriteLine("RpcResult for Create(): {0}", res.Result);
	} else if (str_oper.Equals("Get")) {
	  Console.Write("Enter key:");
	  string str_key = Console.ReadLine();
	  
	  byte[] utf8_key = Encoding.UTF8.GetBytes(str_key);

	  BlockingQueue[] q = dht.GetF(utf8_key, 500, null);
	  int count = 0;

	  RpcResult res = q[0].Dequeue() as RpcResult;
	  ArrayList result = res.Result as ArrayList;
	  
	  if (result == null || result.Count < 3) {
	    Console.Error.WriteLine("Something messed up with Get()...");
	    continue;
	  }
	  Console.Error.WriteLine("Result from Get() looks good: " + result.Count);
	  ArrayList values = (ArrayList) result[0];
	  Console.Error.WriteLine("# of matching entries: " + values.Count);
	  foreach (Hashtable ht in values) {
	    Console.Error.WriteLine(ht["age"]);
	    byte[] data = (byte[]) ht["data"];
	    string val = Encoding.UTF8.GetString(data);
	    Console.Error.WriteLine(val);
	  }
	  for (int i = 0; i < q.Length; i++) {
	    q[i].Close();
	  }
	} else if (str_oper.Equals("Delete")) {
	  Console.Write("Enter key:");
	  string str_key = Console.ReadLine();
	  Console.Write("Enter password:");
	  string str_pass = Console.ReadLine();
	  Console.Error.WriteLine("Delete on key: {0}", str_key );

	  byte[] utf8_key = Encoding.UTF8.GetBytes(str_key);
	  byte[] utf8_pass = Encoding.UTF8.GetBytes(str_pass);

	  string base64_pass = Convert.ToBase64String(utf8_pass);
	  string send_pass = "SHA1:" + base64_pass;

	  BlockingQueue[] q = dht.DeleteF(utf8_key, send_pass);
	  RpcResult res = q[0].Dequeue() as RpcResult;
	  object o = res.Result;
	  for (int i = 0; i < q.Length; i++) {
	    q[i].Close();
	  }
	} else if (str_oper.Equals("Done")) {
	  System.Console.Error.WriteLine("Time to disconnect,,,"); 
	  brunetNode.Disconnect();

	  System.Console.Error.WriteLine("Sleep for 10000 ms,,,");
	  //additional 10 seconds for disconnect to complete
	  System.Threading.Thread.Sleep(10000);
	  break;
	} else if (str_oper.Equals("Sleep")) {
	  Console.Write("Enter sleep time:");
	  string str_sleep = Console.ReadLine();	
	  Console.Error.WriteLine("You want me to sleep for: {0} ms", str_sleep);
	  int sleep_time = (int) Double.Parse(str_sleep.Trim());
	  Console.Error.WriteLine("Going to sleep for some time: {0} ms", sleep_time);
	  System.Threading.Thread.Sleep(sleep_time);	  
	}
	} catch (Exception e) {
	  Console.Error.WriteLine(e);
	}
      }
    }
  }
}
