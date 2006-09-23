using System;
using System.Threading;

using System.Collections;
using System.Text;
using System.Security.Cryptography;

using Brunet;
using Brunet.Dht;

namespace Brunet.Dht {

  public class DhtTraceTester {
    public abstract class Event {
      protected double _time;
      public Event(double time) {
	_time = time;
      }
      public abstract void Fire();
    }
    public class StartEvent: Event {
      public StartEvent(double time): base(time){}
      public override void Fire() {
	Console.WriteLine("Starting new node");
	AHAddress addr = new AHAddress(new RNGCryptoServiceProvider());
	Console.WriteLine("Starting: {0}", addr);
	Node node = new StructuredNode(addr);
	int port = base_port + net_size;
	net_size++;
	if (proto.Equals("udp")) {
	  node.AddEdgeListener(new UdpEdgeListener(port));
	} else 	if (proto.Equals("tcp")) {
	  node.AddEdgeListener(new TcpEdgeListener(port));
	}
	
	for (int k = 0; k < node_list.Count; k++) {
	  //we dont want to make us our own TA
	  
	  int remote_port = (int) port_list[k];
	  string remoteTA = null;
	  if (proto.Equals("udp")) {
	    remoteTA = "gnucla.udp://localhost:" + remote_port;
	  } else if (proto.Equals("tcp")) {
	    remoteTA = "gnucla.tcp://localhost:" + remote_port;
	  } 
	  node.RemoteTAs.Add(new TransportAddress(remoteTA));
	}
	node.Connect();
	node_list.Add(node);
	//create a Dht
	Dht dht = new Dht(node, media);
	//add the dht to the list:
	dht_list.Add(dht);
	port_list.Add(port);
      }
    }
    public class KillEvent: Event {
      public KillEvent(double time): base(time){}
      public override void Fire() {
	int idx = rr.Next(0, node_list.Count);
	Node n = (Node) node_list[idx];
	Console.WriteLine("Killing: {0}", n.Address);
	n.Disconnect();
	node_list.RemoveAt(idx);
	dht_list.RemoveAt(idx);
	port_list.RemoveAt(idx);
      }
    }
    public class Entry {
      protected string _key;
      public string Key {
	get {
	  return _key;
	}
      }
      protected string _data;
      public string Data {
	get {
	  return _data;
	}
      }
      protected string _passwd;
      public string Passwd {
	get {
	  return _passwd;
	}
      }
      
      public Entry(string key, string data, string passwd) {
	_key = key;
	_data = data;
	_passwd = passwd;
      }
    }
    public class CreateEvent: Event {
      protected Entry _entry;
      public CreateEvent(double time, Entry e):base(time) {
	_entry = e;
      }
      public override void Fire() {
	string str_key = _entry.Key;
	string str_data = _entry.Data;
	string str_pass = _entry.Passwd;

	int ttl = 100000;
	
	Console.WriteLine("Create() on key:{0}",str_key);

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
	int idx = rr.Next(0, node_list.Count);
	Dht dht = (Dht) dht_list[idx];

	Node n  = (Node) node_list[idx];
	Console.WriteLine("Issuing the Create() command on idx: {0} address: {1}", idx, n.Address);

	BlockingQueue q = dht.Create(utf8_key, ttl, stored_pass, utf8_data);
	RpcResult res = q.Dequeue() as RpcResult;
	Console.WriteLine("RpcResult for Create(): {0}", res.Result);
      }
    }

    public class DeleteEvent: Event {
      protected Entry _entry;
      public DeleteEvent(double time, Entry e):base(time) {
	_entry = e;
      }
      public override void Fire() {
	string str_key = _entry.Key;
	string str_pass = _entry.Passwd;

	Console.WriteLine("Delete on key: {0}", str_key );
	byte[] utf8_key = Encoding.UTF8.GetBytes(str_key);
	byte[] utf8_pass = Encoding.UTF8.GetBytes(str_pass);

	string base64_pass = Convert.ToBase64String(utf8_pass);
	string send_pass = "SHA1:" + base64_pass;

	int idx = rr.Next(0, node_list.Count);
	Dht dht = (Dht) dht_list[idx];
	Node n  = (Node) node_list[idx];
	Console.WriteLine("Issuing the Delete() command on idx: {0} address: {1}", idx, n.Address);

	BlockingQueue q = dht.Delete(utf8_key, send_pass);
	RpcResult res = q.Dequeue() as RpcResult;
	object o = res.Result;
      }
    }
    public class GetEvent: Event {
      protected Entry _entry;
      public GetEvent(double time, Entry e):base(time) {
	_entry = e;
      }
      public override void Fire() {
	string str_key = _entry.Key;
	byte[] utf8_key = Encoding.UTF8.GetBytes(str_key);

	int idx = rr.Next(0, node_list.Count);
	Dht dht = (Dht) dht_list[idx];
	Node n  = (Node) node_list[idx];
	Console.WriteLine("Issuing the Get() command on idx: {0} address: {1}", idx, n.Address);
	Console.WriteLine("Get() on key: {0}", str_key );
	BlockingQueue q = dht.Get(utf8_key, 500, null);
	int count = 0;

	while (count++ < 2) {
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
      }
    }    
    
    public static ArrayList node_list = new ArrayList();
    public static ArrayList dht_list = new ArrayList();
    public static ArrayList port_list = new ArrayList();    
    public static Random rr = new Random();
    public static string proto;
    public static int net_size;
    public static int base_port;
    public static EntryFactory.Media media = EntryFactory.Media.Memory;


    
    public static void Main(string[] args)
    {
      proto = args[0];
      net_size = Int32.Parse(args[1]);
      base_port = Int32.Parse(args[2]);


      if (args[3].Equals("disk")) {
	media = EntryFactory.Media.Disk;
      }      
 

      Console.WriteLine("Building the network...");

      //create a network:
      for (int loop1 = 0; loop1 < net_size; loop1++) { 
	Console.WriteLine("Creating node: {0}", loop1);
	AHAddress addr = new AHAddress(new RNGCryptoServiceProvider());
	Console.WriteLine(addr);
	Node node = new StructuredNode(addr);
	int port = base_port + loop1;
	if (proto.Equals("udp")) {
	  node.AddEdgeListener(new UdpEdgeListener(port));
	} else 	if (proto.Equals("tcp")) {
	  node.AddEdgeListener(new TcpEdgeListener(port));
	}
	
	for (int loop2 = 0; loop2 < loop1; loop2++) {
	  //we dont want to make us our own TA
	  
	  int remote_port = base_port + loop2;
	  string remoteTA = null;
	  if (proto.Equals("udp")) {
	    remoteTA = "gnucla.udp://localhost:" + remote_port;
	  } else if (proto.Equals("tcp")) {
	    remoteTA = "gnucla.tcp://localhost:" + remote_port;
	  } 
	  node.RemoteTAs.Add(new TransportAddress(remoteTA));
	}
	node.Connect();
	node_list.Add(node);
	//create a Dht
	Dht dht = new Dht(node, media);
	//add the dht to the list:
	dht_list.Add(dht);
	port_list.Add(port);

	//sleep 60 seconds
	Thread.Sleep(60000);

	//test if we have a correct ring
	Dht curr_dht = (Dht) dht_list[0];
	Address start_addr = curr_dht.Address;


	Address curr_addr = start_addr;
	
	Console.WriteLine("Starting to crawl the ring");
	for (int loop3 = 0; loop3 <= loop1; loop3++) {
	  Console.WriteLine("Hop#: {0}, at {1}", loop3+1, curr_addr);
	  curr_addr = curr_dht.LeftAddress;
	  if (curr_addr == null) {
	    break;
	  }
	  for (int k = 0; k < dht_list.Count; k++) {
	    Dht test_dht = (Dht) dht_list[k];
	    if (test_dht.Address.Equals(curr_addr)) {
	      curr_dht = test_dht;
	      break;
	    }
	  }
	}
	Console.WriteLine("Finsihed crawling the ring");
	//eventually the next address should point back to us;
	if (curr_addr == null) {
	  Console.WriteLine("Broken ring detected at: {0}", curr_dht.Address);
	}
	else if (!curr_addr.Equals(start_addr)) {
	  Console.WriteLine("Incomplete ring: Test failed");
	} 
	else {
	  Console.WriteLine("Complete ring: Test passed.");
	}
	
      }
      
      Random rr = new Random();
      //now put 10 keys from arbitrary points in the network
      while(true) {
	Console.Write("Enter operation (Get/Put/Create/Delete/Start/Kill/Print/Test/Sleep):");
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
	  
	  int idx = rr.Next(0, node_list.Count);
	  Dht dht = (Dht) dht_list[idx];
	  Node n  = (Node) node_list[idx];
	  Console.WriteLine("Issuing the Put() command on idx: {0} address: {1}", idx, n.Address);

	  BlockingQueue q = dht.Put(utf8_key, ttl, stored_pass, utf8_data);
	  RpcResult res = q.Dequeue() as RpcResult;
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

	  //Console.WriteLine(utf8_key.Length);
	  //Console.WriteLine(utf8_data.Length);
	  //Console.WriteLine(base64_pass.Length);
	  int idx = rr.Next(0, node_list.Count);
	  Dht dht = (Dht) dht_list[idx];

	  Node n  = (Node) node_list[idx];
	  Console.WriteLine("Issuing the Create() command on idx: {0} address: {1}", idx, n.Address);

	  BlockingQueue q = dht.Create(utf8_key, ttl, stored_pass, utf8_data);
	  RpcResult res = q.Dequeue() as RpcResult;
	  Console.WriteLine("RpcResult for Create(): {0}", res.Result);

	} else if (str_oper.Equals("Get")) {
	  Console.Write("Enter key:");
	  string str_key = Console.ReadLine();
	  
	  byte[] utf8_key = Encoding.UTF8.GetBytes(str_key);

	  int idx = rr.Next(0, node_list.Count);
	  Dht dht = (Dht) dht_list[idx];
	  Node n  = (Node) node_list[idx];
	  Console.WriteLine("Issuing the Get() command on idx: {0} address: {1}", idx, n.Address);
	  Console.WriteLine("Get() on key: {0}", str_key );
	  BlockingQueue q = dht.Get(utf8_key, 500, null);
	  int count = 0;

	  while (count++ < 2) {
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
	  Console.WriteLine("Delete on key: {0}", str_key );

	  byte[] utf8_key = Encoding.UTF8.GetBytes(str_key);
	  byte[] utf8_pass = Encoding.UTF8.GetBytes(str_pass);

	  string base64_pass = Convert.ToBase64String(utf8_pass);
	  string send_pass = "SHA1:" + base64_pass;

	  int idx = rr.Next(0, node_list.Count);
	  Dht dht = (Dht) dht_list[idx];
	  Node n  = (Node) node_list[idx];
	  Console.WriteLine("Issuing the Delete() command on idx: {0} address: {1}", idx, n.Address);

	  BlockingQueue q = dht.Delete(utf8_key, send_pass);
	  RpcResult res = q.Dequeue() as RpcResult;
	  object o = res.Result;
	} else if (str_oper.Equals("Kill") && node_list.Count > 1) { 
	  Console.WriteLine("Killing a node");
	  int idx = rr.Next(0, node_list.Count);
	  Node n = (Node) node_list[idx];
	  Console.WriteLine("Killing: {0}", n.Address);
	  n.Disconnect();
	  node_list.RemoveAt(idx);
	  dht_list.RemoveAt(idx);
	  port_list.RemoveAt(idx);
	} else if (str_oper.Equals("Start")) { 
	  Console.WriteLine("Starting new node");
	  AHAddress addr = new AHAddress(new RNGCryptoServiceProvider());
	  Console.WriteLine("Starting: {0}", addr);
	  Node node = new StructuredNode(addr);
	  int port = base_port + net_size;
	  net_size++;
	  if (proto.Equals("udp")) {
	    node.AddEdgeListener(new UdpEdgeListener(port));
	  } else 	if (proto.Equals("tcp")) {
	    node.AddEdgeListener(new TcpEdgeListener(port));
	  }
	
	  for (int k = 0; k < node_list.Count; k++) {
	  //we dont want to make us our own TA
	  
	    int remote_port = (int) port_list[k];
	    string remoteTA = null;
	    if (proto.Equals("udp")) {
	      remoteTA = "gnucla.udp://localhost:" + remote_port;
	    } else if (proto.Equals("tcp")) {
	      remoteTA = "gnucla.tcp://localhost:" + remote_port;
	    } 
	    node.RemoteTAs.Add(new TransportAddress(remoteTA));
	  }
	  node.Connect();
	  node_list.Add(node);
	  //create a Dht
	  Dht dht = new Dht(node, media);
	  //add the dht to the list:
	  dht_list.Add(dht);
	  port_list.Add(port);
	} else if (str_oper.Equals("Test")) { 
	//test if we have a correct ring
	  Dht curr_dht = (Dht) dht_list[0];
	  Address start_addr = curr_dht.Address;


	  Address curr_addr = start_addr;
	
	  Console.WriteLine("Starting to crawl the ring");
	  for (int loop3 = 0; loop3 < node_list.Count; loop3++) {
	    Console.WriteLine("Hop#: {0}, at {1}", loop3+1, curr_addr);
	    curr_addr = curr_dht.LeftAddress;
	    if (curr_addr == null) {
	      break;
	    }
	    for (int k = 0; k < dht_list.Count; k++) {
	      Dht test_dht = (Dht) dht_list[k];
	      if (test_dht.Address.Equals(curr_addr)) {
		curr_dht = test_dht;
		break;
	      }
	    }
	  }
	  Console.WriteLine("Finsihed crawling the ring");
	  //eventually the next address should point back to us;
	  if (curr_addr == null) {
	    Console.WriteLine("Broken ring detected at: {0}", curr_dht.Address);
	  }
	  else if (!curr_addr.Equals(start_addr)) {
	    Console.WriteLine("Incomplete ring: Test failed");
	  } 
	  else {
	    Console.WriteLine("Complete ring: Test passed.");
	  }
	} else if (str_oper.Equals("Print")) { 
	  Console.WriteLine("# of nodes: {0}", node_list.Count);
	  for (int k = 0; k < node_list.Count; k++) {
	    Dht dht = (Dht) dht_list[k];
	    Console.WriteLine("{0}: # of key-value pairs: {1}", dht.Address, dht.Count);
	    Hashtable ht = dht.All;
	    foreach(byte[] key in ht.Keys) {
	      string str_key = Encoding.UTF8.GetString(key);
	      Console.WriteLine("Key: {0}", str_key);
	    }
	  }
	} else if (str_oper.Equals("Sleep")) {
	  //we should also have a user specified sleep here at the end
	  Console.Write("Enter sleep time:");
	  string str_sleep = Console.ReadLine();	
	  Console.WriteLine("You want me to sleep for: {0} ms", str_sleep);
	  int sleep_time = (int) Double.Parse(str_sleep.Trim());
	  if (sleep_time < 0) {
	    break;
	  }
	  Console.WriteLine("Going to sleep for some time: {0} ms", sleep_time);
	  Thread.Sleep(sleep_time);
	}	  
	} catch (Exception e) {
	  Console.WriteLine(e);
	}
      }
      foreach (Node n in node_list) {
	n.Disconnect();
      }
    }
  }
}
