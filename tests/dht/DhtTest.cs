using System;
using System.Collections;
using System.Text;
using System.Security.Cryptography;

using Brunet;
using Brunet.Dht;

namespace Brunet.Dht {

  public class DhtTest {
    public static void Main(string[] args) 
    {
      EntryFactory.Media media = EntryFactory.Media.Disk;
      string proto = args[0];
      int port = Int32.Parse(args[1]);
      string remote_ta = args[2];
      if (args[3].Equals("disk")) {
	media = EntryFactory.Media.Disk;
      }
      AHAddress addr = new AHAddress(new RNGCryptoServiceProvider());
      Console.WriteLine(addr);
      Node node = new StructuredNode(addr);
      if (proto.Equals("tcp")) {
	node.AddEdgeListener(new TcpEdgeListener(port));
      } if (proto.Equals("udp")) {
	node.AddEdgeListener(new UdpEdgeListener(port));
      }
      node.RemoteTAs.Add(TransportAddressFactory.CreateInstance(remote_ta));

      Dht dht = new Dht(node, media);

      if (dht == null) {
	Console.WriteLine("Something is terribly wrong....");
      }
      node.Connect();
      
      while (true) {
	Console.Write("Enter operation (Get/Put/Create/Delete):");
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

	  BlockingQueue q = dht.Put(utf8_key, ttl, stored_pass, utf8_data);
	  while (true) {
	    RpcResult res = q.Dequeue() as RpcResult;
	    Console.WriteLine(res.Result);
	  }
	} else if (str_oper.Equals("Create")) {
	  Console.Write("Enter key:");
	  string str_key = Console.ReadLine();
	  Console.Write("Enter data:");
	  string str_data = Console.ReadLine();
	  Console.Write("Enter password:");
	  string str_pass = Console.ReadLine();
	  Console.Write("Enter TTL:");
	  int ttl = Int32.Parse(Console.ReadLine());
	  

	  byte[] utf8_key = Encoding.UTF8.GetBytes(str_key);
	  byte[] utf8_data = Encoding.UTF8.GetBytes(str_data);
	  
	  HashAlgorithm algo = new SHA1CryptoServiceProvider();
	  byte[] utf8_pass = Encoding.UTF8.GetBytes(str_pass);

	  byte [] sha1_pass = algo.ComputeHash(utf8_pass);
	  string base64_pass = Convert.ToBase64String(sha1_pass);
	  string stored_pass = "SHA1:" + base64_pass;

	  //Console.WriteLine(utf8_key.Length);
	  //Console.WriteLine(utf8_data.Length);
	  //Console.WriteLine(base64_pass.Length);

	  BlockingQueue q = dht.Create(utf8_key, ttl, stored_pass, utf8_data);
	  while(true) {
	    RpcResult res = q.Dequeue() as RpcResult;
	    Console.WriteLine("RpcResult for Create(): {0}", res.Result);
	  }
	} else if (str_oper.Equals("Get")) {
	  Console.Write("Enter key:");
	  string str_key = Console.ReadLine();
	  
	  byte[] utf8_key = Encoding.UTF8.GetBytes(str_key);


	  BlockingQueue q = dht.Get(utf8_key, 500, null);

	  while (true) {
	    RpcResult res = q.Dequeue() as RpcResult;
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
	  }
	} else if (str_oper.Equals("Delete")) {
	  Console.Write("Enter key:");
	  string str_key = Console.ReadLine();
	  Console.Write("Enter password:");
	  string str_pass = Console.ReadLine();

	  byte[] utf8_key = Encoding.UTF8.GetBytes(str_key);
	  byte[] utf8_pass = Encoding.UTF8.GetBytes(str_pass);

	  string base64_pass = Convert.ToBase64String(utf8_pass);
	  string send_pass = "SHA1:" + base64_pass;

	  BlockingQueue q = dht.Delete(utf8_key, send_pass);
	  while(true) {
	    RpcResult res = q.Dequeue() as RpcResult;
	    object o = res.Result;
	  }
	}
	} catch (Exception e) {
	  Console.WriteLine(e);
	}
      }
    }
  }

}

