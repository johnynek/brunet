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
      string sample_file =  args[1].Trim();
      ArrayList nc_list = new ArrayList();
      ArrayList addr_list = new ArrayList();
      Console.WriteLine("Building the network...");
      for (int i = 0; i < net_size; i++) {
	addr_list.Add(new AHAddress(new RNGCryptoServiceProvider()));
	nc_list.Add(new NCService());
      }
      
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
	float o_rawLatency = float.Parse(ss[3]);
	nc_local.ProcessSample(o_stamp, addr_remote, remote_state.Position, 
			       remote_state.WeightedError, o_rawLatency);
      }
      for (int i = 0; i < net_size; i++) {
	NCService nc = (NCService) nc_list[i];
	Address addr = (Address) addr_list[i];
	NCService.VivaldiState state = nc.State;
	Console.Error.WriteLine("node: {0}, position: {1}, error: {2}", i, state.Position, state.WeightedError);
      }

    }
  }
}
