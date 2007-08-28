using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Security.Cryptography;

namespace Brunet {
  public class RouteTestHandler: IDataHandler {
    public RouteTestHandler() {
      
    }
    public void HandleData(MemBlock p, ISender from, object state)
    {
      RoutingTester.Verify(p, from, state);
    }
  }

  public class RpcRoutingTestHandler {
    Node _node;
    public RpcRoutingTestHandler(Node n) {
      _node = n;
    }
    public byte[] GetIdentification() {
      //
      // Returns the identify of the current node.
      //
      return (byte[]) _node.Address.ToMemBlock();
    }
  }

  public class RoutingTester {
    protected static object _class_lock = new object();
    protected static string routing_test = "routing_test";
    protected static SortedList _sorted_node_list = new SortedList();
    protected static ArrayList _node_list = new ArrayList();
    protected static Hashtable _node_to_port = new Hashtable();

    protected static int _sent = 0;
    protected static int _received = 0;
    protected static int _wrongly_routed = 0;
    public static void Verify(MemBlock p, ISender from, object state) {
      lock(_class_lock) {
	//
	// Make sure that the packet equals my state.
	//
	if (!p.Equals(state)) {
	  _wrongly_routed++;
	}
	_received++;
      }
    }
    protected static bool CheckStatus() {
      bool complete = true;
      for (int idx = 0; idx < _sorted_node_list.Count; idx++) {
	Node n1 = (Node) _sorted_node_list.GetByIndex(idx);
	Node n2 = (Node) _sorted_node_list.GetByIndex((idx + 1)%_node_list.Count);
	
	Connection con = n1.ConnectionTable.GetConnection(ConnectionType.Structured, n2.Address);
	if (con != null) {
	  Console.WriteLine("Found connection (forward) at: {0} -> {1}", n1.Address, con);
	} else {
	  complete = false;
	  Console.WriteLine("Missing connection (forward) between: {0} and {1}", n1.Address, n2.Address);
	}
	con = n2.ConnectionTable.GetConnection(ConnectionType.Structured, n1.Address);
	if (con != null) {
	  Console.WriteLine("Found connection (reverse) at: {0} -> {1}", n2.Address, con);
	} else {
	  complete = false;
	  Console.WriteLine("Missing connection (reverse) between: {0} and {1}", n2.Address, n1.Address);
	}	
      }
      if (complete) {
	Console.WriteLine("Ring status: complete");
	return true;
      } else {
	Console.WriteLine("Ring status: incomplete");
	return false;
      }
    }
    public static void Main(string []args) {
      if (args.Length < 1) {
        Console.WriteLine("please specify the number of p2p nodes."); 
        Environment.Exit(0);
      }
      if (args.Length < 2) {
        Console.WriteLine("please specify the number of missing edges."); 
        Environment.Exit(0);
      }
      bool tunnel = false;
      int base_port = 54000;
      int network_size = Int32.Parse(args[0]);
      int missing_count = Int32.Parse(args[1]);
      try {
	tunnel = args[2].Trim().Equals("tunnel");
      } catch (Exception) {}

      Console.WriteLine("use tunnel edges: {0}", tunnel);

      Random rand = new Random();

      ArrayList missing_edges = new ArrayList();
      for (int i = 0; i < missing_count; i++) {
	int idx = -1;
	int left, right;
	do {
	  idx = rand.Next(0, network_size);
	  left = (idx + 1)%network_size;
	  if (idx == 0) {
	    right = network_size - 1;
	  } else {
	    right = idx - 1;
	  }
	} while (missing_edges.Contains(idx)); //|| 
	    //missing_edges.Contains(left) ||
	    //missing_edges.Contains(right));
	
	Console.WriteLine("Will drop a left edge on idx {0}: ", idx);
	missing_edges.Add(idx);
      }
      
      //
      // Sort missing edges.
      //
      missing_edges.Sort();
      SortedList dist = new SortedList();
      //
      // Compute the average distance between missing edges. 
      //
      if (missing_count > 1) {
	for (int i = 0; i < missing_count; i++) {
	  int idx = (int) missing_edges[i];
	  int idx_next;
	  int d;
	  if (i == missing_count - 1) {
	    idx_next = (int) missing_edges[0];
	    d = (network_size - 1) - idx + idx_next;
	  } else {
	    idx_next = (int) missing_edges[i+1];
	    d = idx_next - idx - 1;
	  }
	  if (!dist.Contains(d)) {
	    dist[d] = 0;
	  } else {
	    int c = (int) dist[d];
	    dist[d] = c + 1;
	  }
	}
      }
      double sum = 0.0;
      int num = 0;
      Console.WriteLine("distribution of missing edges separation");
      foreach(DictionaryEntry de in dist) {
	int k = (int) de.Key;
	int c = (int) de.Value;
	Console.WriteLine("{0} {1}", k, c);
	sum = sum + k*c;
	num = num + c;
      }

      Console.WriteLine("average separation: {0}", (double) sum/num);
      string brunet_namespace = "testing";
      Console.WriteLine("Initializing...");

      ArrayList RemoteTA = new ArrayList();
      for(int i = 0; i < network_size; i++) {
        //RemoteTA.Add(TransportAddressFactory.CreateInstance("brunet.udp://localhost:" + (base_port + i)));
        RemoteTA.Add(TransportAddressFactory.CreateInstance("brunet.function://localhost:" + (base_port + i)));
      }

      for(int i = 0; i < network_size; i++) {
        AHAddress address = new AHAddress(new RNGCryptoServiceProvider());
        Node node = new StructuredNode(address, brunet_namespace);
        _sorted_node_list.Add((Address) address, node);
	_node_list.Add(node);
	RouteTestHandler test_handler = new RouteTestHandler();
	node.GetTypeSource(new PType(routing_test)).Subscribe(test_handler, address.ToMemBlock());
	RpcManager rpc_man = RpcManager.GetInstance(node);
	rpc_man.AddHandler("rpc_routing_test", new  RpcRoutingTestHandler(node));
      }

      for (int i = 0; i < network_size; i++) {
	Node node = (Node) _sorted_node_list.GetByIndex(i);
	Console.WriteLine("Configuring node: {0} ", node.Address);
	TAAuthorizer ta_auth = null;
	if (missing_edges.Contains(i)) {
	  int remote_port;
	  if (i == network_size - 1) {
	    remote_port = base_port;
	  } else {
	    remote_port = base_port + i + 1;
	  }

	  PortTAAuthorizer port_auth = new PortTAAuthorizer(remote_port);
	  Console.WriteLine("Adding a port TA authorizer at: {0} for remote port: {1}", base_port + i, remote_port);
	  ArrayList arr_tas = new ArrayList();
	  arr_tas.Add(port_auth);
	  arr_tas.Add(new ConstantAuthorizer(TAAuthorizer.Decision.Allow));
	  ta_auth = new SeriesTAAuthorizer(arr_tas);
	}
	
	
        //node.AddEdgeListener(new UdpEdgeListener(base_port + i, null, ta_auth));
	node.AddEdgeListener(new FunctionEdgeListener(base_port + i, -1.00, ta_auth));	
	if (tunnel) {
	  Console.WriteLine("Adding a tunnel edge listener");
	  node.AddEdgeListener(new TunnelEdgeListener(node));
	}
	_node_to_port[node] = base_port + i;
        node.RemoteTAs = RemoteTA;	
      }

      //start nodes one by one.
      for (int i  = 0; i < network_size; i++) {
	Node node = (Node) _node_list[i];
	Console.WriteLine("Starting node: {0}, {1}", i, node.Address);
        node.Connect();
	Console.WriteLine("Going to sleep for 2 seconds.");
        System.Threading.Thread.Sleep(2000);
      }

      //wait for 120 more seconds
      Console.WriteLine("Going to sleep for 120 seconds.");
      System.Threading.Thread.Sleep(120000);
      bool complete = CheckStatus();

//       //
//       // now churn the network, kill one node and add a new node
//       //
//       while (true) {
// 	int idx = rand.Next(0, _sorted_node_list.Count);
// 	//kill the node.
// 	Node n = (Node) _sorted_node_list.GetByIndex(idx);
// 	n.Disconnect();
// 	bool add = false;
// 	if (missing_edges.Contains(idx)) {
// 	  add = true;
// 	  missing_edges.Remove(idx);
// 	}

// 	//remove the node.
// 	_node_list.Remove(n);
// 	_sorted_node_list.Remove(_node.Address);
// 	_node_to_port.Remove(n);


// 	// start a new node.
// 	AHAddress address = new AHAddress(new RNGCryptoServiceProvider());
//         Node node = new StructuredNode(address, brunet_namespace);
//         _sorted_node_list.Add((Address) address, node);
// 	_node_list.Add(node);
// 	RouteTestHandler test_handler = new RouteTestHandler();
// 	node.GetTypeSource(new PType(routing_test)).Subscribe(test_handler, address.ToMemBlock());
// 	int idx = _sorted_node_list.IndexOfKey(address);
	
// 	//Is there a need to add a new missing edge here.
// 	TAAuthorizer ta_auth = null;
// 	if (add) {
// 	  //
// 	  // make this node miss an edge to its next node.
// 	  //
// 	  Node next = (Node) _sorted_node_list.GetByIndex((idx+1)%_sorted_node_list.Count);
// 	  int remote_port = (int) _node_to_port[next];
// 	  PortTAAuthorizer port_auth = new PortTAAuthorizer(remote_port);
// 	  ArrayList arr_tas = new ArrayList();
// 	  arr_tas.Add(port_auth);
// 	  arr_tas.Add(new ConstantAuthorizer(TAAuthorizer.Decision.Allow));
// 	  ta_auth = new SeriesTAAuthorizer(arr_tas);
// 	}
// 	//add the edge listener.
	

	
	
//       }
      
      

      int count = 0;
      //
      // Send a large number of packets as exact packets to random destinations
      // and make sure exact routing is perfect.
      //
      
      for (int i = 0; i < network_size; i++) {
	for (int j = 0; j < network_size; j++) {
	  
	  int src_idx = i;
	  int dest_idx = j;
	  Node src_node = (Node) _sorted_node_list.GetByIndex(src_idx);
	  Node dest_node = (Node) _sorted_node_list.GetByIndex(dest_idx);
	  //Console.WriteLine("{0} -> {1}", src_idx, dest_idx);
	  Address dest_address = (Address) dest_node.Address;
	  ISender s = new AHExactSender(src_node, dest_address);
	  MemBlock p = dest_address.ToMemBlock();
	  s.Send(new CopyList(new PType(routing_test), p));
	  _sent++;
	  //System.Threading.Thread.Sleep(10);
	  s.Send(new CopyList(new PType(routing_test), p));
	  _sent++;
	  //System.Threading.Thread.Sleep(10);
	}
      }
      //wait for 10 more seconds
      Console.WriteLine("Going to sleep for 10 seconds.");
      System.Threading.Thread.Sleep(10000);      
      Console.WriteLine("Final statistics");
      lock(_class_lock) {
	Console.WriteLine("Sent: {0}, Received: {1}, Wrongly routed: {2}", 
			  _sent, _received, _wrongly_routed);
      }

      int missing_rpcs = 0;
      int correct_rpcs = 0;
      int incorrect_rpcs = 0;
      Hashtable queue_to_address = new Hashtable();
      for (int i = 0; i < network_size; i++) {
	for (int j = 0; j < network_size; j++) {
	  
	  int src_idx = i;
	  int dest_idx = j;
	  Node src_node = (Node) _sorted_node_list.GetByIndex(src_idx);
	  Node dest_node = (Node) _sorted_node_list.GetByIndex(dest_idx);
	  //Console.WriteLine("{0} -> {1}", src_idx, dest_idx);
	  Address dest_address = (Address) dest_node.Address;
	  ISender s = new AHExactSender(src_node, dest_address);
	  RpcManager rpc_man = RpcManager.GetInstance(src_node);
	  Channel q = new Channel();
	  lock (_class_lock) {
	    queue_to_address[q] = dest_address;
	  }
	  q.CloseAfterEnqueue();
	  q.CloseEvent += delegate(object o, EventArgs cargs) {
	    lock(_class_lock) {
	      Channel qu = (Channel) o;
	      if (qu.Count == 0) {
		missing_rpcs++;
	      }
	      queue_to_address.Remove(qu);
	    }
	  };
	  q.EnqueueEvent += delegate(object o, EventArgs cargs) {
	    lock(_class_lock) {
	      Channel qu = (Channel) o;
	      RpcResult rpc_reply = (RpcResult) qu.Peek();
	      byte []result = (byte[]) rpc_reply.Result;
	      Address target = new AHAddress(result);
	      if (target.Equals(queue_to_address[qu])) {
		correct_rpcs++;
	      } else {
		incorrect_rpcs++;
	      }
	    }
	  };
	  rpc_man.Invoke(s, q, "rpc_routing_test.GetIdentification", new object[]{});
	}
      }
      
      //wait for 10 more seconds
      while (true) {
	int c = -1;
	lock(_class_lock) {
	  c = incorrect_rpcs + missing_rpcs + correct_rpcs;
	}
	if (c < network_size*network_size) {
	  Console.WriteLine("Going to sleep for 10 seconds.");
	  System.Threading.Thread.Sleep(10000);
	} else {
	  break;
	}
      }
      
      Console.WriteLine("Final statistics");
      Console.WriteLine("correct rpcs: {0}, incorrect rpcs: {1}, missing rpcs: {2}", 
			correct_rpcs, incorrect_rpcs, missing_rpcs);
      
      System.Environment.Exit(1);
      //foreach(DictionaryEntry de in _sorted_node_list) {
      //  Node node = (Node)de.Value;
      //  node.Disconnect();
      //}      
    }
  }
}
