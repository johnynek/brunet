using System;
using System.Threading;
using System.Collections;
using System.IO;
using System.Text;
using System.Security.Cryptography;

using Brunet;
using Brunet.Coordinate;

/*
 * The following class provides a method to evaluate the efficacy of 
 * coordinate computation.
 */

namespace Brunet.Coordinate {
  public class DhtAutoTester {
    public static void Main(string[] args) 
    {
      int net_size = Int32.Parse(args[0]);
      string mode = args[1].Trim();
      ArrayList nc_list = new ArrayList();
      ArrayList addr_list = new ArrayList();
      Console.WriteLine("Building the network...");
      for (int i = 0; i < net_size; i++) {
	addr_list.Add(new AHAddress(new RNGCryptoServiceProvider()));
	nc_list.Add(new NCService());
      }
      if (mode.Equals("-s")) {
	string sample_file =  args[2].Trim();
	//
	// Start processing samples.
	//
	
	StreamReader br = new StreamReader(new FileStream(sample_file, FileMode.Open, FileAccess.Read));
	DateTime now = DateTime.Now;
	while(true) {
	  string s = br.ReadLine();
	  if (s == null) {
	    break;
	  }
	  string[] ss = s.Split();
	  int seconds = Int32.Parse(ss[0]);
	  DateTime o_stamp = now + new TimeSpan(0, 0, seconds);
	  int local_idx = Int32.Parse(ss[1]);
	  NCService nc_local = (NCService) nc_list[local_idx];
	  int remote_idx = Int32.Parse(ss[2]);
	  Address addr_remote = (Address) addr_list[remote_idx];
	  NCService nc_remote = (NCService) nc_list[remote_idx];
	  NCService.VivaldiState remote_state = nc_remote.State;
	  double o_rawLatency = double.Parse(ss[3]);
	  nc_local.ProcessSample(o_stamp, addr_remote, remote_state.Position, 
				 remote_state.WeightedError, o_rawLatency);
	}

      } 
      else if (mode.Equals("-l")) {
	Random rr = new Random();
	//
	// Use pairwise latencies between nodes (assume a complete matrix).
	//
	string latency_file = args[2].Trim();
	int max_neighbors = Int32.Parse(args[3].Trim());
	int max_rounds = Int32.Parse(args[4]);
	
	double[][] rtt_matrix= new double[net_size][];
	ArrayList neighbors = new ArrayList();
	for (int i = 0; i < net_size; i++) {
	  neighbors.Insert(i, new ArrayList());
	  rtt_matrix[i] = new double[net_size];
	}
	
	//
	// Add neighbors to the nodes
	//
	
	for (int i = 0; i < net_size; i++) {
	  ArrayList i_neighbors = (ArrayList) neighbors[i];
	  ArrayList j_neighbors = null;
	  int j = -1;
	  while (i_neighbors.Count < max_neighbors) {
	    do {
	      j = rr.Next(0, net_size);
	      j_neighbors = (ArrayList) neighbors[j];
	    } while (j == i && i_neighbors.Contains(j) && j_neighbors.Count >= max_neighbors);
	    i_neighbors.Add(j);
	    j_neighbors.Add(i);
	  }	  
	}
	
	
	for (int i = 0; i < net_size; i++) {
	  for (int j = 0; j < net_size; j++) {
	    rtt_matrix[i][j] = -1.0f;
	  }
	}

	StreamReader br = new StreamReader(new FileStream(latency_file, FileMode.Open, FileAccess.Read));
	while(true) {
	  string s = br.ReadLine();
	  if (s == null) {
	    break;
	  }
	  string[] ss = s.Split();
	  int local_idx = Int32.Parse(ss[0]);
	  int remote_idx = Int32.Parse(ss[1]);
	  rtt_matrix[local_idx][remote_idx] = double.Parse(ss[2]);
	  rtt_matrix[remote_idx][local_idx] = double.Parse(ss[2]);
	 
	}
	
	//
	// Now the rounds of iteration
	//
	DateTime now = DateTime.Now;
	int x = 0;
	while (x < max_rounds) {
	  int local_idx = rr.Next(0, net_size);
	  ArrayList my_neighbors = (ArrayList) neighbors[local_idx];
	  int remote_idx = (int) my_neighbors[rr.Next(0, my_neighbors.Count)];
	  NCService nc_local = (NCService) nc_list[local_idx];
	  Address addr_remote = (Address) addr_list[remote_idx];
	  NCService nc_remote = (NCService) nc_list[remote_idx];
	  NCService.VivaldiState remote_state = nc_remote.State;
	  double o_rawLatency = rtt_matrix[local_idx][remote_idx];
	  //Console.WriteLine("{0} {1} {2}", local_idx, remote_idx, o_rawLatency);
	  nc_local.ProcessSample(now + new TimeSpan(0, 0, x), addr_remote, remote_state.Position, 
				 remote_state.WeightedError, o_rawLatency);
	  x += 1;
	  
	}
      }
      
      for (int i = 0; i < net_size; i++) {
	NCService nc = (NCService) nc_list[i];
	Address addr = (Address) addr_list[i];
	NCService.VivaldiState state = nc.State;
	Console.Error.WriteLine("{0} {1}", i, state.Position);
      }
    }
  }
}
