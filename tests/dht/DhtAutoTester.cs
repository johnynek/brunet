using System;
using System.Threading;

using System.Collections;
using System.Text;
using System.Security.Cryptography;

using Brunet;
using Brunet.Dht;

namespace Brunet.Dht {

  public class DhtAutoTester {
    public static void Main(string[] args) 
    {
      int net_size = Int32.Parse(args[0]);
      int base_port = Int32.Parse(args[1]);
      int num_keys = Int32.Parse(args[2]);
      EntryFactory.Media media = EntryFactory.Media.Memory;
      if (args[3].Equals("disk")) {
	media = EntryFactory.Media.Disk;
      }

      ArrayList node_list = new ArrayList();
      ArrayList dht_list = new ArrayList();
      
      Console.WriteLine("Building the network...");

      //create a network:
      for (int loop1 = 0; loop1 < net_size; loop1++) { 
	Console.WriteLine("Creating node: {0}", loop1);
	AHAddress addr = new AHAddress(new RNGCryptoServiceProvider());
	Console.WriteLine(addr);
	Node node = new StructuredNode(addr);
	node.AddEdgeListener(new UdpEdgeListener(base_port + loop1));
	
	for (int loop2 = 0; loop2 < loop1; loop2++) {
	  //we dont want to make us our own TA
	    int port = base_port + loop2;
	    string remoteTA = "gnucla.udp://localhost:" + port;
	    node.RemoteTAs.Add(TransportAddressFactory.CreateInstance(remoteTA));
	}
	node.Connect();
	node_list.Add(node);
	//create a Dht
	Dht dht = new Dht(node, media);
	//add the dht to the list:
	dht_list.Add(dht);

	//sleep 10 seconds
	Thread.Sleep(30000);

	//test if we have a correct ring
	Dht curr_dht = (Dht) dht_list[0];
	Address start_addr = curr_dht.Address;


	Address curr_addr = start_addr;
	
	Console.WriteLine("Starting to crawl the ring");
	for (int loop3 = 0; loop3 <= loop1; loop3++) {
	  Console.WriteLine("Hop#: {0}", loop3+1);
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
      
      //wait a little for the network to form
      //Thread.Sleep(60000);
      //inserting keys into the network

      int trials = 0;
      while(trials++ < 2) 
      {
	//Console.WriteLine("Enter command");
	//string command = Console.ReadLine();
	//if (command.Equals("quit")) {
	//break;
	//}
      Console.WriteLine("Inserting keys into the network....");

      ArrayList key_list = new ArrayList();
      ArrayList pass_list = new ArrayList();
      ArrayList hit_list = new ArrayList();
      ArrayList attempt_list = new ArrayList();


      Random rr = new Random();
      //now put 10 keys from arbitrary points in the network
      for (int i = 0; i < num_keys; i++) {

	//generate a random key
	byte[] key = new byte[10];
	rr.NextBytes(key);
	
	key_list.Add(key);

	byte[] data = new byte[1];
	data[0] = 0;
	
	//pick a random binary password
	byte[] bin_pass = new byte[10];
	rr.NextBytes(bin_pass);

	//following is what we would send for Delete or Replace
	pass_list.Add("SHA1:" + Convert.ToBase64String(bin_pass));
	HashAlgorithm algo = new SHA1CryptoServiceProvider();

	byte [] sha1_pass = algo.ComputeHash(bin_pass);
	string stored_pass = "SHA1:" + Convert.ToBase64String(sha1_pass);

	//now pick a random number between 0 and net_size
	int idx = rr.Next(0, net_size);
	Dht dht = (Dht) dht_list[idx];

	Console.WriteLine("Inserting Key# {0} at Node: {1}", i, idx);
	
	BlockingQueue q = dht.Put(key, 10000, stored_pass, data);
	try {
	  RpcResult res = (RpcResult) q.Dequeue();
	  object result = res.Result;
	  Console.WriteLine(result);
	} catch (Exception e) {
	  Console.WriteLine(e);
	}
	Thread.Sleep(10000);
      }
      //now we have all the keys in place, start querying them:
      for (int i = 0; i < key_list.Count; i++) {
	//now pick a random number between 0 and net_size
	int idx = rr.Next(0, net_size);
	Dht dht = (Dht) dht_list[idx];


	byte[] key = (byte[]) key_list[i];
	
	Console.WriteLine("Querying Key# {0} at Node: {1}", i, idx);
	
	BlockingQueue q = dht.Get(key, 500, null);
	int attempts = 0;
	int num_hits = 0; 
	try {
	  while (num_hits == 0) {
	    attempts++;
	    RpcResult res = q.Dequeue() as RpcResult;
	    ArrayList result = res.Result as ArrayList;
	    
	    if (result == null || result.Count < 3) {
	      Console.WriteLine("Something messed up with Get()...");
	      continue;
	    }
	    ArrayList values = (ArrayList) result[0];

	    num_hits = values.Count;

	    Console.WriteLine("# of matching entries: " + values.Count);
	    foreach (Hashtable ht in values) {
	      Console.WriteLine("age: {0}", ht["age"]);
	      byte[] data = (byte[]) ht["data"];
	      Console.WriteLine("data: {0}", data[0]);
	    }
	  }
	} catch (Exception e) {
	  Console.WriteLine(e);
	}
	hit_list.Add(num_hits);
	attempt_list.Add(attempts);
      }

      Console.WriteLine("Final statistics:");
      for (int i = 0; i < hit_list.Count; i++) {
	Console.WriteLine("Key# {0} : Hits = {1}; Attempts = {2}", 
			  i, hit_list[i], attempt_list[i]);
      }
      //finally we collect all the keys:
      for (int i = 0; i < key_list.Count; i++) {
	//now pick a random number between 0 and net_size
	int idx = rr.Next(0, net_size);
	Dht dht = (Dht) dht_list[idx];


	byte[] key = (byte[]) key_list[i];
	
	Console.WriteLine("Deleting Key# {0} at Node: {1}", i, idx);
	string stored_pass = (string) pass_list[i];

	BlockingQueue q = dht.Delete(key, stored_pass);
	
	try {
	  RpcResult res = q.Dequeue() as RpcResult;
	  Console.WriteLine("Get a deletion response {0}: ", res.Result);
	} catch(Exception e) {
	  Console.WriteLine(e);
	}
	
      }
      }
      for (int i = 0; i < node_list.Count; i++) {
	Console.WriteLine("Disconnecting node: {0}", i);
	Node n = (Node) node_list[i];
	n.Disconnect();
      }
    }
  }
}
