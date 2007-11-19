#define DEBUG
#define BRUNET_NUNIT
using System;
using Brunet;
using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography;


#if BRUNET_NUNIT
using System.Collections.Specialized;
using NUnit.Framework;
#endif

namespace Brunet {
  public abstract class Authorizer {
    public abstract bool Allow(SimNode n1, SimNode n2);
  }
  public class ConstantAuthorizer: Authorizer {
    public override bool Allow(SimNode n1, SimNode n2) {
      return true;
    }
  }
  public class NatAuthorizer: Authorizer
  {
    public override bool Allow (SimNode n1, SimNode n2) {
      if (n1.Nat == NatType.Symmetric && n2.Nat == NatType.Cone) {
	return false;
      }
      if (n2.Nat == NatType.Symmetric && n1.Nat == NatType.Cone) {
	return false;
      }
      if (n2.Nat == NatType.Symmetric && n2.Nat == NatType.Symmetric) {
	return false;
      }
      return true;
    }
  }
  public class PairAuthorizer: Authorizer
  {
    protected static Random _rand = new Random();
    protected bool[,] _deny_matrix;
    public PairAuthorizer(int network_size, int unique_bad_pairs)
    {
      _deny_matrix = new bool[network_size, network_size];
      for (int i = 0; i < network_size; i++) {
	for (int j = 0; j < network_size; j++) {
	  _deny_matrix[i, j] = false;
	}
      }
      Console.WriteLine("Network size: {0}", network_size);
      Console.WriteLine("Unique bad pairs: {0}", unique_bad_pairs);
      while (unique_bad_pairs > 0) {
	int x1, x2;
	do {
	  x1 = _rand.Next(0, network_size);
	  x2 = _rand.Next(0, network_size);	  
	} while (x1 == x2 || _deny_matrix[x1, x2] == true ||
		 _deny_matrix[x2, x1] == true);
	_deny_matrix[x1, x2] = _deny_matrix[x2, x1] = true;
	unique_bad_pairs -= 1;
      }
    }

    public override bool Allow(SimNode n1, SimNode n2) {
      return !(_deny_matrix[n1.Idx, n2.Idx]);
    }
  }
  public class RandomAuthorizer: Authorizer {
    protected static Random _rand = new Random();
    protected bool[,] _deny_matrix;
    
    public RandomAuthorizer(int network_size, double loss_prob) {
      _deny_matrix = new bool[network_size, network_size];
      int deny = 0;
      for (int i = 0; i < network_size; i++) {
	for (int j = i; j < network_size; j++) {
	  double s = _rand.NextDouble();
	  if (s < loss_prob) {
	    _deny_matrix[i, j] = _deny_matrix[j, i] =  false;
	  } else {
	    _deny_matrix[i, j] = _deny_matrix[j, i] =  true;
	    deny += 1;
	  }
	}
      }
      Console.WriteLine("Number of unique bad pairs: {0}", deny);
    }
    public override bool Allow(SimNode n1, SimNode n2) {
      return !(_deny_matrix[n1.Idx, n2.Idx]);
    }
  }
  public class PowerLaw {
    protected static Random _rand = new Random();
    protected double _constant;
    protected double _order;
    public PowerLaw(int low, int high, double order) {
      _order = order;
      double sum = 0.0;
      for (int i = low; i <= high; i++) {
	sum += (1.0/Math.Pow(i, order));
      }
      _constant = 1.0/sum;	
      //Console.WriteLine("Constant: {0}", _constant);
    }
    public int Next() {
      double cdf = 0.0;
      int k = 0;
      double p = _rand.NextDouble();
      while (cdf < p) {
	k += 1;
	cdf += _constant/Math.Pow(k, _order);
      }
      return k;
    }
  }

  public class ClusterAuthorizer:Authorizer {
    //generates clusters distributed using power-law
    //sizes vary from 1 to n.
    // p(n) proportional to c/(n^k).
    //first task is to compute c

    protected int[] _node_to_cluster;
    protected PowerLaw _power;
    public ClusterAuthorizer(int network_size, int cutoff, double order) {
      _node_to_cluster = new int[network_size];
      _power = new PowerLaw(1, cutoff, order);
      int Idx = 0;
      int cluster_id = 0;
      while (Idx < network_size) {
	//generate a cluster
	int size = _power.Next();
	Console.WriteLine("cluster size: {0}", size);
	//the value of k is the current cluster size
	//[Idx, Idx+size) get assigned to current cluster
	int last = Idx + size;
	if (last > network_size) {
	  last = network_size;
	}
	for (; Idx < last; Idx++) {
	  _node_to_cluster[Idx] = cluster_id;
	}
	//next cluster
	cluster_id += 1;
      }
    }
    public override bool Allow(SimNode n1, SimNode n2) {
      return _node_to_cluster[n1.Idx] != _node_to_cluster[n2.Idx];
    }    
  }
  public class SimPacket {
    public static readonly int MAX_TTL = 100;
    public static readonly int MAX_UPHILL_HOPS = 1; 

    public AHAddress Source;
    public AHAddress Destination;
    public RoutingMode Mode;
    public int Hops;
    public SimPacket(AHAddress source, AHAddress destination, RoutingMode mode) {
      this.Source = source;
      this.Destination = destination;
      this.Mode = mode;
      this.Hops = 0;
    }
  }
  public enum RoutingMode : byte 
  {
    Greedy = 1,
    Exact = 2,
    Annealing = 3,
  }


  public enum NatType : byte 
  {
    Cone = 1,
    Public = 2,
    Symmetric = 3
  } 

  public enum ConType : byte
  {
    Near = 1,
    Shortcut = 2
  }
  
  public class SimConnectionTable {
    protected ArrayList _con_list;
    protected Hashtable _ht;
    public SimConnectionTable() {
      _con_list = new ArrayList();
      _ht = new Hashtable();
    }
    public bool Add(AHAddress address, ConType t) {
      int idx = _con_list.BinarySearch(address);
      if (idx < 0) {
	idx = ~idx;
	_con_list.Insert(idx, address);
	if (!_ht.ContainsKey(t)) {
	  _ht[t] = new ArrayList();
	}
	((ArrayList) _ht[t]).Add(address);
	return true;
      } else {
	return false;
      }
    }

    public void Remove(AHAddress address, ConType t) {
      _con_list.Remove(address);
      if (_ht.ContainsKey(t)) {
	((ArrayList) _ht[t]).Remove(address);
      }
    }

    public void Remove(ConType t) {
//       //remove all connections of a type
//       if (!_ht.ContainsKey(t)) {
// 	return;
//       }
//       ArrayList con_list = (ArrayList) _ht[t];
//       _ht.Remove(t);
//       foreach(AHAddress tt in tr) {
	
//       }
    }

    public AHAddress GetByIndex(int idx) {
      if (_con_list.Count == 0) {
	return null;
      }
      idx %= _con_list.Count;
      if( idx < 0 ) {
	idx += _con_list.Count;
      }
      return (AHAddress) _con_list[idx];
    }
    public int IndexOf(AHAddress address) {
      int dest_idx = 0;
      if (_con_list.Count == 0) {
	dest_idx = ~dest_idx;
      }
      else {
	dest_idx = _con_list.BinarySearch(address);
      }
      return dest_idx;
    }
    public int GetCount(ConType t) {
      if (_ht.ContainsKey(t)) {
	return ((ArrayList) _ht[t]).Count;
      } else {
	return 0;
      }
    }
    public int GetTotalCount() {
      return _con_list.Count;
    }

    public ArrayList GetAllConnections() {
      return (ArrayList) _con_list.Clone();
    }

    public ArrayList GetConnections(ConType t) {
      if (_ht.ContainsKey(t)) {
	return (ArrayList) ((ArrayList) _ht[t]).Clone();
      } else {
	return new ArrayList();
      }
    }

    public int LeftInclusiveCount(AHAddress a1, AHAddress a2) {
      if( a1.Equals(a2) ) { return 0; }
      int dist;
      //This list never changes:
      int count = _con_list.Count;
      int a2_idx = _con_list.BinarySearch(a2);
      int a1_idx = _con_list.BinarySearch(a1);
        /*
         * There are four cases, we deal with each separately:
         * 0) neither a1 nor a2 are in the table
         * 1) a1 is not, but a2 is
         * 2) a1 is, but a2 is not
         * 3) a1 and a2 are.
         */

      bool a2_present = true;
      bool a1_present = true;
      if( a2_idx < 0 ) {
        a2_present = false;
        a2_idx = ~a2_idx;
      }
      if( a1_idx < 0 ) {
        a1_present = false;
        a1_idx = ~a1_idx;
      }
      if( a1_idx == a2_idx ) {
        //This is an easy case:
        int max_dist = count;
        if( a2_present ) {
          max_dist--;
        }
        if( a1_present ) {
          max_dist--;
        }
        if( a2.CompareTo( a1 ) > 0 ) {
          dist = 0;  
        }
        else {
          dist = max_dist;
        }
      }
      else {
        //These two indices are different:
        dist = a2_idx - a1_idx;
        if( dist < 0 ) {
          //Wrap around.
          dist += count;
        }
        if( a1_present ) {
          /*
           * In thie case, our calculations are too much by one, in both
           * cases (dist > 0, dist < 0), so we decrease by one:
           */
          dist = dist - 1;
        }
      }
      return dist;
    }
    /**
     * @return the number of Structured Connections in the interval
     * (a1, a2) (not including the end points) when we move to the right.
     */
    public int RightInclusiveCount(AHAddress a1, AHAddress a2) {
      if( a1.Equals(a2) ) { return 0; }
      int dist;
      
      int count = _con_list.Count;
      int a2_idx = _con_list.BinarySearch(a2);
      int a1_idx = _con_list.BinarySearch(a1);
      
        /*
         * There are four cases, we deal with each separately:
         * 0) neither a1 nor a2 are in the table
         * 1) a1 is not, but a2 is
         * 2) a1 is, but a2 is not
         * 3) a1 and a2 are.
         */

      bool a2_present = true;
      bool a1_present = true;
      if( a2_idx < 0 ) {
        a2_present = false;
        a2_idx = ~a2_idx;
      }
      if( a1_idx < 0 ) {
        a1_present = false;
        a1_idx = ~a1_idx;
      }
      if( a1_idx == a2_idx ) {
        //This is an easy case:
        int max_dist = count;
        if( a2_present ) {
          max_dist--;
        }
        if( a1_present ) {
          max_dist--;
        }
        if( a2.CompareTo( a1 ) < 0 ) {
          dist = 0;  
        }
        else {
          dist = max_dist;
        }
      }
      else {
        //These two indices are different:
        dist = a1_idx - a2_idx;
        if( dist < 0 ) {
          //Wrap around.
          dist += count;
        }
        if( a2_present ) {
          /*
           * In thie case, our calculations are too much by one, in both
           * cases (dist > 0, dist < 0), so we decrease by one:
           */
          dist = dist - 1;
        }
      }
      return dist;
    }
  }

  public class SimNode {
    protected static Random _rand = new Random();
    public AHAddress LocalAddress;
    public NatType Nat;
    public int Idx;
    protected SimConnectionTable _con_table;
    public SimConnectionTable ConnectionTable {
      get {
	return _con_table;
      }
    }
    public int NetworkSize {
      get {
	return _network_size;
      }
    }
    protected int _network_size;

    public SimNode(AHAddress address, NatType nat, int idx) {
      this.LocalAddress = address;
      this.Nat = nat;
      this.Idx = idx;
      this._con_table = new SimConnectionTable();
    }
    public void ResetConnections() {
      this._con_table = new SimConnectionTable();
    }
    protected void EstimateSize() {
      int net_size = -1;
      BigInteger least_dist = null;
      BigInteger greatest_dist = null;
      int shorts = 0;
      int structs = _con_table.GetTotalCount();
      if( structs + 1 > net_size ) {
	net_size = structs + 1;
      }
      /* We estimate the density of nodes in the address space,
       * and since we know the size of the whole address space,
       * we can use the density to estimate the number of nodes.
       */
      AHAddress local = this.LocalAddress;
      foreach(AHAddress c in _con_table.GetConnections(ConType.Near)) {
	BigInteger dist = local.DistanceTo(c);
	if( shorts == 0 ) {
	  //This is the first one
	  least_dist = dist;
	  greatest_dist = dist;
	}
	else {
	  if( dist > greatest_dist ) {
	    greatest_dist = dist;
	  }
	  if( dist < least_dist ) {
	    least_dist = dist;
	  }
	} 
	shorts++;
      }
      /*
       * Now we have the distance between the range of our neighbors
       */
      if( shorts > 0 ) {
	if ( greatest_dist > least_dist ) {
	  BigInteger width = greatest_dist - least_dist;
	  //Here is our estimate of the inverse density:
	  BigInteger inv_density = width/(shorts);
	  //The density times the full address space is the number
	  BigInteger full = Brunet.Address.Full;
	  BigInteger total = full / inv_density;
	  int total_int = total.IntValue();
	  if( total_int > net_size ) {
	    net_size = total_int;
          }
	}
      }
      _network_size = net_size;
    }

    public bool AddConnection(SimNode other, ConType t) {
      if (other == this) {
	Console.WriteLine("Cannot add myself.");
	Environment.Exit(1);
      }
      if (_con_table.Add(other.LocalAddress, t)) {
	other.AddConnection(this, t);
	EstimateSize();
	return true;
      } else {
	return false;
      }
    }

    public void RemoveConnection(SimNode other, ConType t) {
      Trim(other, t);
      other.Trim(this, t);
    }

    protected void Trim(SimNode other, ConType t) {
      _con_table.Remove(other.LocalAddress, t);
    }
    
    public AHAddress NextHop(AHAddress from, SimPacket p, out bool deliverlocally) {
      return NextHop(from, p, false, out deliverlocally);
    }
    
    public AHAddress NextHop(AHAddress from, SimPacket p, bool debug, out bool deliverlocally) {
      Debug.WriteLineIf(debug, "src:" +  p.Source + ", dest:" + p.Destination);
      Debug.WriteLineIf(debug, "Dumping local connections for:" +  this.LocalAddress.ToString());
      foreach(AHAddress cons in _con_table.GetAllConnections()) {
	Debug.WriteLineIf(debug, cons.ToString());
      }
      AHAddress next_node = null;  
      deliverlocally = false;
      AHAddress dest = (AHAddress)p.Destination;
      if( p.Hops > SimPacket.MAX_TTL) {
	Debug.WriteLineIf(debug, "TTL exceeded.");
	next_node = null;
      }
      else if ( LocalAddress.Equals(dest) ) {
	Debug.WriteLineIf(debug, "Destination reached.");
	deliverlocally = true;
	next_node = null;
      }
      else {
	/*
	 * else we know hops < ttl, we can route:
	 * We now need to check the ConnectionTable
	 */
	int dest_idx = _con_table.IndexOf(dest);
	
	if( dest_idx >= 0 ) {
	  //We actually have a connection to this node:
	  next_node = _con_table.GetByIndex(dest_idx);
	  Debug.WriteLineIf(debug, "We actually have a connection to that node.");
	} else if( _con_table.GetTotalCount() == 0) {
	  //We don't have any structured connections.  I guess we are the closest:
	  Debug.WriteLineIf(debug, "No structured connections");
	  deliverlocally = true;
	  next_node = null;
	}
	else {
	  dest_idx = ~dest_idx;
	  /*
	   * Here are the right and left neighbors of the destination
	   * left is increasing, right is decreasing.
	   * Remember the ConnectionTable wraps around, so no need to worry
	   * about the size of index
	   */
	  int left_idx = dest_idx;
	  AHAddress left_n = _con_table.GetByIndex(left_idx);
	  int right_idx = dest_idx - 1;
	  AHAddress right_n = _con_table.GetByIndex(right_idx);
	  
	  //We check the a couple of connections:
	  BigInteger l_dist = dest.DistanceTo((AHAddress)left_n).abs();
	  BigInteger r_dist = dest.DistanceTo((AHAddress)right_n).abs();
	  AHAddress closest_node;
	  AHAddress other_node;
	  BigInteger closest_dist;
	  BigInteger other_dist;
	  if( l_dist < r_dist ) {
	    closest_node = left_n;
	    other_node = right_n;
	    closest_dist = l_dist;
	    other_dist = r_dist;
	    Debug.WriteLineIf(debug, "Left seems closest:" + left_n.ToString());
	  }
	  else {
	    closest_node = right_n;
	    other_node = left_n;
	    closest_dist = r_dist;
	    other_dist = l_dist;
	    Debug.WriteLineIf(debug, "Right seems closest:" + right_n.ToString());
	  }
	  if (p.Mode == RoutingMode.Greedy) {
	    Debug.WriteLineIf(debug, "Executing greedy routing mode. ");
	    BigInteger our_dist = dest.DistanceTo(this.LocalAddress).abs();
	    if( closest_dist < our_dist ) {
	      if( closest_node != from ) {
		next_node = closest_node;
		Debug.WriteLineIf(debug, "Closest distance less that our distance (valid next node).");
	      } else {
		next_node = null;
		Debug.WriteLineIf(debug, "Closest distance less that our distance. (but still setting to null)");
	      }
	      deliverlocally = false;
	    } else {
	      next_node = null;
	      deliverlocally = true;
	      Debug.WriteLineIf(debug, "Looks like we are the closest.");
	    }
	  }
	  else //other routing modes 
	  {
	    Debug.WriteLineIf(debug, "Executing other routing modes. ");

	    int our_idx = _con_table.IndexOf(this.LocalAddress);
	    
	    Debug.Assert(our_idx < 0,"I cannot be in my own routing table:" + our_idx);
	    //Compute our left neighbor.

	    if( our_idx < 0 ) {
	      our_idx = ~our_idx;
	    }

	    AHAddress our_left_n = _con_table.GetByIndex(our_idx);
	    if( left_n == our_left_n ) {
	      Debug.WriteLineIf(debug, "Common neighbor case: " + our_left_n);
	      /*
	       * We share a common left neighbor, so we should deliver locally
	       * This is the only case where we should deliver locally,
	       * otherwise there is at least one node on either side of the
	       * target, so one of them should probably get the packet.
	       */
	      deliverlocally = true;
	      //The next step should be the node on the "other side"
	      if(this.LocalAddress.IsLeftOf( dest ) ) {
		Debug.WriteLineIf(debug, "We are to the left. ");
		next_node = right_n;
	      }
	      else {
		Debug.WriteLineIf(debug, "We are to the right. ");
		next_node = left_n;
	      }
	      if(from == next_node) {
		//Console.WriteLine("setting next node to null");
		next_node = null;
	      }
	    }
	    else if ( p.Hops == 0 ) {
	      /*
	       * This is the case that we sent the packet, and we are not
	       * a neighbor of the packet (the previous case)
	       * So, the closest_con must be good since we are the source
	       */
	      Debug.WriteLineIf(debug, "0 hops, still good to go.");
	      next_node = closest_node;
	    }
	    else if (p.Hops <= SimPacket.MAX_UPHILL_HOPS ) {
	      /*
	       * We will allow the packet to go uphill (get further from the source)
	       * at first, but this has to stop in order to prevent loops
	       *
	       * This may help the network form in the massive join case, or under
	       * heavy churn. @todo analyze approaches for improving stabilization
	       * in massively disordered cases.
	       */
	      Debug.WriteLineIf(debug, "Uphill hops. ");
	      if( closest_node != from ) {
		//Awesome.  This is an easy case...
		Debug.WriteLineIf(debug, "Still good to go. ");
		next_node = closest_node;
	      }
	      else {
		Debug.WriteLineIf(debug, "The second closest case. ");
		/*
		 * Look at the two next closest and choose the minimum distance of
		 * the three
		 */
		int sc_idx = -1;
		if( closest_node == right_n ) {
		  //move one over
		  sc_idx = right_idx - 1;
		}
		else {
		  //Must be the left:
		  sc_idx = left_idx + 1;
		}
		AHAddress second_closest = _con_table.GetByIndex(sc_idx);
		BigInteger second_dist =
		  dest.DistanceTo( (AHAddress)second_closest).abs();
		if( second_dist < other_dist ) {
		  other_node = second_closest;
		}
		if( other_node != from ) {
		  //If we only have one neighbor,
		  //other and closest might be the same
		  next_node = other_node;
		}
		else {
		  //We just can't win...
		  //Console.WriteLine("setting next node to null (cannot win)");
		  next_node = null;
		}
	      }
	    }
	    else {
	      Debug.WriteLineIf(debug, "Not a neighbor case, and beyond 2-hops. ");
	      /*
	       * This is the case where we are not a neighbor of the destination
	       * according to our table, and the packet has taken at least 2 hops.
	       */
	      deliverlocally = false;
	      if( ( closest_node == from ) 
		  && ( other_node != from ) ) {
		closest_dist = other_dist;
		closest_node = other_node;
	      }
	      AHAddress prev = from;
	      if( prev != null ) {
		BigInteger prev_dist = dest.DistanceTo( (AHAddress)prev ).abs();
		if( closest_dist >= prev_dist ) {
		  //Don't send it if you can't get it closer than it was before
		  next_node = null;
		  Debug.WriteLineIf(debug, "Prev distance smaller than closest. Stop.");
		  //Console.WriteLine("setting next node to null (cannot get any closer than before)");
		}
		else {
		  Debug.WriteLineIf(debug, "Prev distance larger than closest. Keep going.");
		  next_node = closest_node;
		}
	      }
	      else {
		//This is the case that we don't have a connection
		//on the Edge the packet came from, this shouldn't happen,
		//but it is not a disaster.
		Debug.WriteLineIf(debug, "Prev distance smalled than closest. ");
		next_node = closest_node;
	      }
	    }//End of non-neareast neighbor case
	  }//End of Annealing case	    
	} //End of the case where we had to find a near route
      }
      if(p.Mode == RoutingMode.Exact) {
	if(this.LocalAddress.Equals(dest) ) {
          deliverlocally = true;
          next_node = null;
        }
        else {
          deliverlocally = false;
        }
      }
      return next_node;
    }
    
    public AHAddress ComputeShortcutTarget() {
      AHAddress local_address = this.LocalAddress;
      int network_size = _network_size;
      double logN = (double)(Brunet.Address.MemSize * 8);
      double logk = Math.Log( (double) network_size, 2.0 );
      double p = _rand.NextDouble();
      double ex = logN - (1.0 - p)*logk;
      int ex_i = (int)Math.Floor(ex);
      double ex_f = ex - Math.Floor(ex);
      //Make sure 2^(ex_long+1)  will fit in a long:
      int ex_long = ex_i % 63;
      int ex_big = ex_i - ex_long;
      ulong dist_long = (ulong)Math.Pow(2.0, ex_long + ex_f);
      //This is 2^(ex_big):
      BigInteger big_one = 1;
      BigInteger dist_big = big_one << ex_big;
      BigInteger rand_dist = dist_big * dist_long;

      // Add or subtract random distance to the current address
      BigInteger t_add = local_address.ToBigInteger();

      // Random number that is 0 or 1
      if( _rand.Next(2) == 0 ) {
        t_add += rand_dist;
      }
      else {
        t_add -= rand_dist;
      }

      BigInteger target_int = new BigInteger(t_add % Address.Full);
      return new AHAddress(target_int);
    }
  }


  public class Simulator {
    protected static Random _rand = new Random();
    protected static ArrayList sorted_node_list = new ArrayList();
    protected static Hashtable address_to_node = new Hashtable();
    protected static AHAddress []key_list;
    protected static Authorizer authorizer = null;

    public static int ComputeOverlap(ArrayList list1, ArrayList list2) {
      int count = 0;
      foreach(AHAddress x in list1) {
	if (list2.Contains(x)) {
	  count += 1;
	}
      }
      return count;
    }
    public static void Main(string []args) {
      Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
      Debug.AutoFlush = true;
      Debug.WriteLineIf(true, "hello brother");
      int network_size = Int32.Parse(args[0].Trim());
      int m = Int32.Parse(args[1].Trim());//number of connections per node.
//       if (m < 2) {
// 	//a node is not allowed to have less than 2 neighbor connections.
// 	Console.WriteLine("atlest 2 neighbor connections required at each node");
// 	Environment.Exit(1);
//       }
      int index = 3;
      string mode = args[2].Trim();
      if (mode.Equals("pair")) {
	int unique_bad_pairs = Int32.Parse(args[3].Trim());
	index += 1;
	authorizer = new PairAuthorizer(network_size, unique_bad_pairs);
      } 
      else if (mode.Equals("random")) {
	double loss_prob = double.Parse(args[3].Trim());
	index += 1;
	authorizer = new RandomAuthorizer(network_size, loss_prob);	
      }
      else if (mode.Equals("cluster")) {
	int cutoff = Int32.Parse(args[3].Trim());
	double order = double.Parse(args[4].Trim());
	index += 2;
	authorizer = new ClusterAuthorizer(network_size, cutoff, order);	
      }
      else {
	Console.WriteLine("enter a valid mode");
	Environment.Exit(1);
      }


      //Console.WriteLine(tunnel_mode);
      string routing_mode = string.Empty;
      if (args.Length > index) {
	routing_mode = args[index].Trim();
	index++;
      } 
      string operation_mode = string.Empty;
      if (args.Length > index) {
	operation_mode = args[index].Trim();
	index++;
      }
      int node_idx = 0;
      for (int i = 0; i < network_size; i++) {
	AHAddress address = new AHAddress(new RNGCryptoServiceProvider());
	SimNode node = new SimNode(address, NatType.Public, node_idx++);
	int idx = sorted_node_list.BinarySearch(address);
	if (idx < 0) {
	  idx = ~idx;
	} else {
	  Console.Error.WriteLine("Duplicate node");
	}
	sorted_node_list.Insert(idx, address);
	address_to_node[address] = node;
      }
      Console.WriteLine("Nodes created: {0}.", network_size);
      if (routing_mode.Equals("inexact")) {
	//generate 10*network_size many keys
	key_list = new AHAddress[10*network_size];
	for (int i = 0; i < key_list.Length; i++) {
	  key_list[i] = new AHAddress(new RNGCryptoServiceProvider());
	}
	Console.WriteLine("Keys generated: {0}.", 10*network_size);	
      }      
      if (operation_mode.Equals(string.Empty) || operation_mode.Equals("both")) {
	Console.WriteLine("Operation mode: no_tunnel");
	CreateNetwork(m, false);
	if (routing_mode.Equals("exact")) {
	  DoExactRouting();
	}
	if (routing_mode.Equals("inexact")) {
	  DoInexactRouting();	
	}
      }
      //
      // now redo the experiment in tunnel mode.
      //
      if (operation_mode.Equals("tunnel") || operation_mode.Equals("both")) {	  
	ResetNetwork();
	Console.WriteLine("Operation mode: tunnel");	
	CreateNetwork(m, true);
	if (routing_mode.Equals("exact")) {
	  DoExactRouting();
	}
	if (routing_mode.Equals("inexact")) {
	  DoInexactRouting();
	}
      }
      
    }
    public static void ResetNetwork() {
      int network_size = sorted_node_list.Count;
      //drop all connections on a node
      foreach(SimNode n in address_to_node.Values) {
	n.ResetConnections();
      }
    }

    public static void CreateNetwork(int m, bool tunnel_mode) {
      //
      // Place nears.
      //
      int network_size = sorted_node_list.Count;
      int [] missing_left = new int[m+1];
      int [] missing_right = new int[m+1];
      for (int i = 1; i <=m; i++) {
	missing_left[i] = 0;
	missing_right[i] = 0;
      }
      int k_max = 1;
      double k_average = 0.0;
      for (int i = 0; i < network_size; i++) {
	SimNode node_1 = (SimNode) address_to_node[(AHAddress) sorted_node_list[i]];
	// add near connections to the left
	int added = 0;
	int k = 1;
	for(; (tunnel_mode && k <= m) || (!tunnel_mode && added < m); k++) {
	  //for(; k <= m; k++) {
	  SimNode node_k = (SimNode) address_to_node[(AHAddress) sorted_node_list[(i + k)%network_size]];
	  if (authorizer.Allow(node_1, node_k)) {
	    node_1.AddConnection(node_k, ConType.Near);
	    added += 1;
	  } else {
	    //Console.WriteLine("Cannot add near: {0} <-> {1}", node_1.LocalAddress, node_k.LocalAddress);
	    if (k <= m) {
	      missing_left[k] += 1;
	    }
	  }
	}
	//Console.WriteLine("Distance travelled by near connections: {0}", k);
	k = k - 1;
	k_average += k;
	if (k > k_max) {
	  k_max = k;
	}
	//add connections to the right
	added = 0;
	k = 1;
	for(; (tunnel_mode && k <= m) || (!tunnel_mode && added < m); k++) {
	  //for(; k <= m; k++) {	
	  int next_right = i - k;
	  if (next_right < 0) {
	    next_right = next_right + network_size;
	  }
	  SimNode node_k = (SimNode) address_to_node[(AHAddress) sorted_node_list[next_right]];
	  if (authorizer.Allow(node_1, node_k)) {
	    node_1.AddConnection(node_k, ConType.Near);
	    added += 1;
	  } else {
	    //Console.WriteLine("Cannot add near: {0} <-> {1}", node_1.LocalAddress, node_k.LocalAddress);
	    if (k <= m) {
	      missing_right[k] += 1;
	    }
	  }
	}
	//Console.WriteLine("Distance travelled by near connections: {0}", k);
	k = k - 1;
	k_average += k;
	if (k > k_max) {
	  k_max = k;
	}
      }
      
      Console.WriteLine("Near connections added.");
      for (int i = 1;i <= m; i++) {
	Console.WriteLine("Missing left {0}-near connections: {1}.", i, missing_left[i]);	
	Console.WriteLine("Missing right {0}-near connections: {1}.", i, missing_right[i]);	
      }
      Console.WriteLine("Distance travelled by near connections max: {0}, average: {1}", 
			k_max, k_average/(2*network_size));


      //
      // we will need to remember where all to add tunnel edges. 
      //
      
      ArrayList[] tunnels = new ArrayList[m + 1];
      for (int k = 1; k <=m; k++) {
	tunnels[k] = new ArrayList();
      }
      for (int i = 0; i < network_size; i++) {
	SimNode node_1 = (SimNode) address_to_node[(AHAddress) sorted_node_list[i]];
	for (int k = 1; k <= m; k++) {
	  SimNode node_k = (SimNode) address_to_node[(AHAddress) sorted_node_list[(i + k)%network_size]];	
	  //check n1 and nk for the strength of overlap
	  int idx = node_1.ConnectionTable.IndexOf(node_k.LocalAddress); 
	  if (idx < 0) {
	    int overlap = ComputeOverlap(node_1.ConnectionTable.GetConnections(ConType.Near), 
					 node_k.ConnectionTable.GetConnections(ConType.Near));
	    if (k == 1) {
	      Console.WriteLine("first overlap: {0}", overlap);
	      //add for sure
	      //tunnels.Add(new SimNode[] {node_1, node_k});	      
	    }
	    if (k == 2) {
	      Console.WriteLine("second overlap: {0}", overlap);
	    }
	    if (overlap >= 1) {
	      tunnels[k].Add(new SimNode[] {node_1, node_k});
	    }
	  }
	}
      }

      //for all nodes that had an overlap >= 2 add additional near connection between them
      if (tunnel_mode) {
	for (int k = 1; k <= m; k++) {
	  int count = 0;
	  foreach(SimNode []pair in tunnels[k]) {
	    SimNode n1 = pair[0];
	    SimNode n2 = pair[1];
	    n1.AddConnection(n2, ConType.Near);
	    count += 1;
	  }
	  Console.WriteLine("{0}-tunnels added: {1}.", k, count);
	}
	//now add the remaining edges to each node
	k_max = 1;
	k_average = 0.0;
	for (int i = 0; i < network_size; i++) {
	  SimNode node_1 = (SimNode) address_to_node[(AHAddress) sorted_node_list[i]];
	  // make sure sufficient near connections to left neighbors
	  int added = 0;
	  int k = 1;
	  for(; added < m; k++) {
	    SimNode node_k = (SimNode) address_to_node[(AHAddress) sorted_node_list[(i + k)%network_size]];
	    int idx = node_1.ConnectionTable.IndexOf(node_k.LocalAddress);
	    if (idx >= 0) {
	      //fine we have a connection, might even be a tunnel connection.
	      added += 1;
	    } else {
	      if (k <= m) {
		Debug.Assert(!authorizer.Allow(node_1, node_k), "Cannot miss an allowed connection");
	      } 
	      //try to add this connection
	      if (authorizer.Allow(node_1, node_k)) {
		node_1.AddConnection(node_k, ConType.Near);
		added += 1;
	      } 
	    }
	  }
	  k = k - 1;
	  //Console.WriteLine("Distance travelled by near connections: {0}", k);
	  k_average += k;
	  if (k > k_max) {
	    k_max = k;
	  }
	  //add connections to the right
	  added = 0;
	  k = 1;
	  for(; added < m; k++) {
	    int next_right = i - k;
	    if (next_right < 0) {
	      next_right = next_right + network_size;
	    }
	    SimNode node_k = (SimNode) address_to_node[(AHAddress) sorted_node_list[next_right]];
	    int idx = node_1.ConnectionTable.IndexOf(node_k.LocalAddress);
	    if (idx >= 0) {
	      //fine we have a connection, might even be a tunnel connection.
	      added += 1;
	    } else {
	      if (k <= m) {
		Debug.Assert(!authorizer.Allow(node_1, node_k), "Cannot miss an allowed connection");
	      }
	      //keep going
	      //try to add this connection
	      if (authorizer.Allow(node_1, node_k)) {
		node_1.AddConnection(node_k, ConType.Near);
		added += 1;
	      } 
	    }
	  }
	  k = k - 1;
	  //Console.WriteLine("Distance travelled by near connections: {0}", k);
	  k_average += k;
	  if (k > k_max) {
	    k_max = k;
	  }
	}
	Console.WriteLine("Distance travelled by near connections max: {0}, average: {1}", 
			  k_max, k_average/(2.0*network_size));	
      }
      //we will now trim excess connections.
      int trim_count = 0;
      Hashtable near_dist = new Hashtable();
      int max_near = 0;
      for (int i = 0; i < network_size; i++) {
	SimNode node_1 = (SimNode) address_to_node[(AHAddress) sorted_node_list[i]];
	ArrayList near_trim = new ArrayList();
	int near_count = node_1.ConnectionTable.GetConnections(ConType.Near).Count;
	//Console.WriteLine("near count: {0}", near_count);
	if (!near_dist.Contains(near_count)) {
	  near_dist[near_count] = 1;
	} else {
	  int x = (int) near_dist[near_count];
	  near_dist[near_count] = x + 1;	  
	}
	if (near_count > max_near) {
	  max_near = near_count;
	}
	foreach (AHAddress target in node_1.ConnectionTable.GetConnections(ConType.Near)) {
	  int left = node_1.ConnectionTable.LeftInclusiveCount(node_1.LocalAddress, target);
	  int right = node_1.ConnectionTable.RightInclusiveCount(node_1.LocalAddress, target);
	  if (left > 2*m && right > 2*m) {
	    near_trim.Add(target);
	  }
	}

	//with some probability trim each connections
	foreach(AHAddress to_trim in near_trim) {
	  double p = _rand.NextDouble();
	  if (p > 0.5) {
	    node_1.RemoveConnection((SimNode) address_to_node[to_trim], ConType.Near);
	    Console.WriteLine("trimming connection");
	    trim_count += 1;
	  }
	}
      }
      Console.WriteLine("Near connections per node.");
      for (int i = 0; i <= max_near; i++) {
	int x = -1;
	if (near_dist.Contains(i)) {
	  x = (int) near_dist[i];
	} else {
	  x = 0;
	}
	Console.WriteLine("{0} {1}", i, x);
      }
      Console.WriteLine("Number of connections trimmed: {0}", trim_count);
      //
      // Place shortcuts.
      //
      int max_sc = (int) Math.Log( (double) network_size, 2.0 );
      for (int iter = 0; iter < 100*max_sc; iter += 1) {
	//Console.WriteLine("iter: {0}", iter);
	foreach (SimNode n in address_to_node.Values) {
	  int logk = (int) Math.Log( (double) network_size, 2.0 );
	  ArrayList shortcuts = (ArrayList) n.ConnectionTable.GetConnections(ConType.Shortcut);
	  //if (shortcuts == null || shortcuts.Count < 1) {
	  if (shortcuts == null || shortcuts.Count < logk) {
	    AHAddress t_addr = n.ComputeShortcutTarget();
	    int idx = sorted_node_list.BinarySearch(t_addr);
	    if (idx < 0) {
	      idx = ~idx;
	      idx = idx%network_size;
	    } else {
	      Console.WriteLine("what a conincidence.");
	    }
	    SimNode t_node = (SimNode) address_to_node[(AHAddress) sorted_node_list[idx]];
	    if (t_node == n) {
	      //Console.WriteLine("Cannot pick myself as shortcut target");
	      //give up the chance to connect.
	      continue;
	    }
	    if (authorizer.Allow(n, t_node)) {
	      if (!n.AddConnection(t_node, ConType.Shortcut)) {
		//Console.WriteLine("Cannot add duplicate shortcut: {0} <-> {1}", n.LocalAddress, t_node.LocalAddress);
	      }
	    } else {
	      //Console.WriteLine("Cannot add shortcut: {0} <-> {1}", n.LocalAddress, t_node.LocalAddress);
	    }
	  }
	  //trim connections if there is a need to do so.
	  //delete a random shortcut connection.
	  ArrayList sc_trim = new ArrayList();
	  foreach(AHAddress target in shortcuts) {
	    int left = n.ConnectionTable.LeftInclusiveCount(n.LocalAddress, target);
	    int right = n.ConnectionTable.RightInclusiveCount(n.LocalAddress, target);
	    if( left >= m && right >= m ) { 
	      sc_trim.Add(target);
	    }
	  }
	  if (sc_trim.Count > logk) {
	    int idx = _rand.Next(0, sc_trim.Count);
	    n.RemoveConnection((SimNode) address_to_node[(Address) shortcuts[idx]], ConType.Shortcut);
	  }
	}
      }

      Console.WriteLine("Shortcuts added.");
      //Check if no node is in its own connection table
      foreach(SimNode n in address_to_node.Values) {
	int self_idx = n.ConnectionTable.IndexOf(n.LocalAddress);
	Debug.Assert(self_idx < 0, "Cannot be in my own connection table");
      }  

      //ArrayList shortcut_dist = new ArrayList();
      //
      // Study the distribution of shortcut connections.
      //
//       foreach(SimNode n in address_to_node.Values) {
// 	int my_sorted_idx = sorted_node_list.BinarySearch(n.LocalAddress);
// 	foreach(AHAddress c in n.ConnectionTable.GetConnections(ConType.Shortcut)) {
// 	  int your_sorted_idx = sorted_node_list.BinarySearch(c);
// 	  int d;
// 	  if (my_sorted_idx > your_sorted_idx) {
// 	    int temp = my_sorted_idx;
// 	    my_sorted_idx = your_sorted_idx;
// 	    your_sorted_idx = temp;
// 	  }
// 	  //your_sorted_idx greater than my_sorted_idx
// 	  d = your_sorted_idx - my_sorted_idx;
// 	  if (d > sorted_node_list.Count/2) {
// 	    d = sorted_node_list.Count - d;
// 	  }	  

// 	  int idx = shortcut_dist.BinarySearch(d);
// 	  if (idx < 0) {
// 	    idx = ~idx;
// 	  }
// 	  shortcut_dist.Insert(idx, d);
// 	}
//       }
//       //
//       // Compute the distance to which each shortcut goes.
//       //
//       ArrayList shortcut_prob = new ArrayList();
//       ArrayList shortcut_cumul = new ArrayList();
//       int curr_dist = -1;
//       int curr_count = 0;
//       int cumul_count = 0;
//       foreach(int d in shortcut_dist) {
// 	if (curr_dist < 0) {
// 	  curr_dist = d;
// 	  curr_count = 1;
// 	  cumul_count = 1;
// 	  continue;
// 	}
// 	if (d == curr_dist) {
// 	  curr_count += 1;
// 	  cumul_count += 1;
// 	  continue;
// 	}
// 	if (d != curr_dist) {
// 	  Trace.WriteLineIf(d > curr_dist, "Error in distance cumulative distribution");
// 	  shortcut_prob.Add(new object[] {curr_dist, curr_count});
// 	  shortcut_cumul.Add(new object[] {curr_dist, cumul_count});
// 	  curr_dist = d;
// 	  curr_count = 1;
// 	  cumul_count += 1;
// 	}
//       }
//       if (curr_dist > 0) {
// 	shortcut_prob.Add(new object[] {curr_dist, curr_count});	
// 	shortcut_cumul.Add(new object[] {curr_dist, cumul_count});	
//       }

//       Console.WriteLine("Computing probability distribution");
//       foreach(object[] o in shortcut_prob) {
// 	Console.WriteLine("{0} {1}", (int) o[0], (int) o[1]);
//       }

//       Console.WriteLine("Computing cumulative distribution");
//       foreach(object[] o in shortcut_cumul) {
// 	Console.WriteLine("{0} {1}", (int) o[0], (int) o[1]);
//       }

      Console.WriteLine("Analysing connections per node");
      double sum = 0.0;
      int max_con = 0;
      Hashtable dist = new Hashtable();
      foreach(SimNode n in address_to_node.Values) {
	int count = n.ConnectionTable.GetTotalCount();
	sum += count;
	if (count > max_con) {
	  max_con = count;
	}
	if (dist.ContainsKey(count)) {
	  int c = (int) dist[count];
	  dist[count] = c + 1;
	} else {
	  dist[count] = 1;
 	}
      }
      for (int i = 0; i <= max_con; i++) {
	Console.WriteLine("{0} {1}", i, dist.ContainsKey(i)? dist[i]:0);
      }
    
    }

    public static void DoExactRouting() {
      //
      // Analyse exact (greedy, annealing) routing.
      //
      int network_size = sorted_node_list.Count;
      Console.WriteLine("Analysing exact (greedy, annealing) routing");
      int greedy_broken_routes = 0;
      double greedy_success_hop_count = 0.0;
      double greedy_fail_hop_count = 0.0;
      double greedy_all_hop_count = 0.0;
      double greedy_destinations = 0.0;
      //double greedy_tunnel_hops_fail = 0.0;
      //double greedy_tunnel_hops_success = 0.0;
      //double greedy_tunnel_hops_all = 0.0;
      double greedy_real_fail = 0.0;
      double greedy_real_success = 0.0;
      double greedy_real_all = 0.0;

      int annealing_broken_routes = 0;
      double annealing_success_hop_count = 0.0;
      double annealing_fail_hop_count = 0.0;
      double annealing_all_hop_count = 0.0;
      double annealing_destinations = 0.0;
      int annealing_sucks = 0;

      foreach (SimNode n1 in address_to_node.Values) {
	foreach (SimNode n2 in address_to_node.Values) {
	  bool greedy_success = false;
	  bool annealing_success = false;
	  ArrayList destinations = new ArrayList();
	  SimPacket p = new SimPacket(n1.LocalAddress, n2.LocalAddress, RoutingMode.Greedy);
	  SimNode curr = n1;
	  AHAddress prev_addr = null;
	  int real_hops = 0;
	  while (true) {
	    bool deliverlocally;
	    AHAddress next = curr.NextHop(prev_addr, p, out deliverlocally);
	    if (deliverlocally) {
	      destinations.Add(curr);
	    }
	    if (next == null) {
	      break;
	    }
	    if (curr.ConnectionTable.IndexOf(next) < 0) {
	      Console.WriteLine("Taking a wrong hop.");	    
	    }
	    SimNode next_node = (SimNode) address_to_node[next];
	    bool tunnel_hop = false;
	    //if this hop was over a tunnel edge.
	    //we can infer this from the authorizer.
	    if (!authorizer.Allow(curr, next_node)) {
	      tunnel_hop = true;
	    }
	    prev_addr = curr.LocalAddress;
	    curr = next_node;
	    p.Hops += 1;
	    //add another hop if it is a tunnel hop
	    if (tunnel_hop) {
	      real_hops += 2;
	    } else {
	      real_hops += 1;
	    }
	    
	  }
	  greedy_destinations += destinations.Count;
	  if (!destinations.Contains(n2)) {
	    greedy_broken_routes += 1;
	    greedy_fail_hop_count += p.Hops;
	    greedy_real_fail += real_hops;

	  } else {
	    greedy_success_hop_count += p.Hops;
	    greedy_success = true;
	    greedy_real_success += real_hops;
	  }
	  greedy_all_hop_count += p.Hops;
	  greedy_real_all += real_hops;

	  //also check annealing in the same loop
	  destinations = new ArrayList();
	  p = new SimPacket(n1.LocalAddress, n2.LocalAddress, RoutingMode.Exact);
	  real_hops = 0;
	  curr = n1;
	  prev_addr = null;
	  while (true) {
	    bool deliverlocally;
	    AHAddress next = curr.NextHop(prev_addr, p, out deliverlocally);
	    if (deliverlocally) {
	      destinations.Add(curr);
	    }
	    if (next == null) {
	      break;
	    }
	    if (curr.ConnectionTable.IndexOf(next) < 0) {
	      Console.WriteLine("Taking a wrong hop.");	    
	    }

	    SimNode next_node = (SimNode) address_to_node[next];
	    bool tunnel_hop = false;
	    //if this hop was over a tunnel edge.
	    //we can infer this from the authorizer.
	    if (!authorizer.Allow(curr, next_node)) {
	      tunnel_hop = true;
	    }
	    prev_addr = curr.LocalAddress;
	    curr = next_node;
	    p.Hops += 1;
	    if (tunnel_hop) {
	      real_hops += 2;
	    } else {
	      real_hops += 1;
	    }
	  }
	  annealing_destinations += destinations.Count;
	  if (!destinations.Contains(n2)) {
	    annealing_broken_routes += 1;
	    annealing_fail_hop_count += p.Hops;
	    if (p.Hops >= 99) {
	      Console.WriteLine("ttl expired for annealing");
	    }
	  } else {
	    annealing_success_hop_count += p.Hops;
	    annealing_success = true;
	    //Console.WriteLine("success: {0}", p.Hops);
	  }
	  annealing_all_hop_count += p.Hops;
	  if (greedy_success && !annealing_success) {
	    Console.WriteLine("annealing sucks");
	    annealing_sucks += 1;
	  }
	}
      }
      Console.WriteLine("broken pairs (greedy): {0}", greedy_broken_routes);
      Console.WriteLine("hop count (greedy, fail): {0}", greedy_fail_hop_count/greedy_broken_routes);
      Console.WriteLine("hop count (greedy, success): {0}", 
			greedy_success_hop_count/(network_size*network_size - greedy_broken_routes));
      Console.WriteLine("average hop count (greedy): {0}", 
			greedy_all_hop_count/(network_size*network_size));
      Console.WriteLine("real hop count (greedy, fail): {0}", greedy_real_fail/greedy_broken_routes);
      Console.WriteLine("real hop count (greedy, success): {0}", 
			greedy_real_success/(network_size*network_size - greedy_broken_routes));
      Console.WriteLine("real average hop count (greedy): {0}", 
			greedy_real_all/(network_size*network_size));


      Console.WriteLine("destinations (greedy): {0}", greedy_destinations/(network_size*network_size));
      
      Console.WriteLine("broken pairs (annealing): {0}", annealing_broken_routes);
      Console.WriteLine("hop count (annealing, fail): {0}", annealing_fail_hop_count/annealing_broken_routes);
      Console.WriteLine("hop count (annealing, success): {0}", 
			annealing_success_hop_count/(network_size*network_size - annealing_broken_routes));
      Console.WriteLine("average hop count (annealing): {0}", 
			annealing_all_hop_count/(network_size*network_size));
      Console.WriteLine("destinations (annealing): {0}", annealing_destinations/(network_size*network_size));
      Console.WriteLine("sucks (annealing): {0}", annealing_sucks);
    }
    
    public static void DoInexactRouting() {
      //
      // Analyse routing for keys. (large number of keys).
      //


      int network_size = sorted_node_list.Count;
      double greedy_inconsistent = 0;
      double annealing_inconsistent = 0;
      int max_destinations = 0;
      Hashtable annealing_destinations = new Hashtable();

      double greedy_success_hop_count = 0.0;
      double greedy_fail_hop_count = 0.0;
      double greedy_all_hop_count = 0.0;

      double annealing_success_hop_count = 0.0;
      double annealing_fail_hop_count = 0.0;
      double annealing_all_hop_count = 0.0;
      

      for (int i = 0; i < key_list.Length; i++) {
	if (i%1000 == 0) {
	  Console.WriteLine("current greedy inconsistent: {0}", greedy_inconsistent);
	  Console.WriteLine("current annealing inconsistent: {0}", annealing_inconsistent);	    
	}
	AHAddress key = key_list[i];
	//first determine the left and right
	int left_idx = sorted_node_list.BinarySearch(key);
	if (left_idx > 0) {
	  Console.WriteLine("astronomically improbable.");
	  continue;
	}
	left_idx = ~left_idx;
	left_idx = left_idx%network_size;
	int right_idx = left_idx - 1;
	if (right_idx < 0) {
	  right_idx += network_size;
	}
	SimNode left = (SimNode) address_to_node[(AHAddress) sorted_node_list[left_idx]];
	SimNode right = (SimNode) address_to_node[(AHAddress) sorted_node_list[right_idx]];
	  
	// source the key at different nodes in the network.
	foreach (SimNode n in address_to_node.Values) {
	  ArrayList destinations = new ArrayList();
	  SimPacket p = new SimPacket(n.LocalAddress, key, RoutingMode.Greedy);
	  SimNode curr = n;
	  AHAddress prev_addr = null;
	  while (true) {
	    bool deliverlocally;
	    AHAddress next = curr.NextHop(prev_addr, p, out deliverlocally);
	    if (deliverlocally) {
	      destinations.Add(curr);
	    }
	    if (next == null) {
	      break;
	    }
	    prev_addr = curr.LocalAddress;
	    curr = (SimNode) address_to_node[next];
	    p.Hops += 1;
	  }
	  //lets see how well we did
	  if (destinations.Contains(left) || destinations.Contains(right)) {
	    //we are good.
	    greedy_success_hop_count += p.Hops;
	  } else {
	    greedy_inconsistent += 1;
	    greedy_fail_hop_count += p.Hops;
	  }
	  greedy_all_hop_count += p.Hops;
	  //now do annealing routing for the keys
	  destinations = new ArrayList();
	  p = new SimPacket(n.LocalAddress, key, RoutingMode.Annealing);
	  curr = n;
	  prev_addr = null;
	  while (true) {
	    bool deliverlocally;
	    AHAddress next = curr.NextHop(prev_addr, p, out deliverlocally);
	    if (deliverlocally) {
	      destinations.Add(curr);
	    }
	    if (next == null) {
	      break;
	    }
	    prev_addr = curr.LocalAddress;
	    curr = (SimNode) address_to_node[next];
	    p.Hops += 1;
	  }
	  //lets see how well we did
	  if (destinations.Contains(left) || destinations.Contains(right)) {
	    //we are good.
	    annealing_success_hop_count += p.Hops ;
	  } else {
	    annealing_inconsistent += 1;
	    annealing_fail_hop_count += p.Hops;
	  }
	  annealing_all_hop_count += p.Hops;
	  if (destinations.Contains(left)) {
	    destinations.Remove(left);
	  }
	  if (destinations.Contains(right)) {
	    destinations.Remove(right);
	  }
	  int x = destinations.Count;
	  if (annealing_destinations.Contains(x)) {
	    int y = (int) annealing_destinations[x];
	    annealing_destinations[x] = y + 1;
	  } else {
	    annealing_destinations[x] = 1;
	  }
	  if (x > max_destinations) {
	    max_destinations = x;
	  }
	  //Console.WriteLine("destinations.count: {0}", destinations.Count);
	}
      }
      Console.WriteLine("total greedy inconsistent: {0}", greedy_inconsistent);
      Console.WriteLine("Average (greedy) inconsistency per key: {0}", greedy_inconsistent/(1.0*key_list.Length));
      Console.WriteLine("hop count (greedy, fail): {0}", greedy_fail_hop_count/greedy_inconsistent);
      Console.WriteLine("hop count (greedy, success): {0}", 
			greedy_success_hop_count/(network_size*key_list.Length - greedy_inconsistent));
      Console.WriteLine("average hop count (greedy): {0}", 
			greedy_all_hop_count/(network_size*key_list.Length));

      Console.WriteLine("total annealing inconsistent: {0}", annealing_inconsistent);	    
      Console.WriteLine("Average (annealing) inconsistency per key: {0}", annealing_inconsistent/(1.0*key_list.Length));
      Console.WriteLine("distribution of annealing destinations");
      for (int i = 0; i <= max_destinations; i++) {
	Console.WriteLine("dist annealing {0} {1}", i, (int) annealing_destinations[i]);
      }
      Console.WriteLine("hop count (annealing, fail): {0}", annealing_fail_hop_count/annealing_inconsistent);
      Console.WriteLine("hop count (annealing, success): {0}", 
			annealing_success_hop_count/(network_size*key_list.Length - annealing_inconsistent));
      Console.WriteLine("average hop count (annealing): {0}", 
			annealing_all_hop_count/(network_size*key_list.Length));
    }
  }

#if BRUNET_NUNIT

  [TestFixture]
  public class ConTableTester {
    [Test]
    public void TestConTable() {
      SortedList list = new SortedList();
      for (int i = 0; i < 10; i++) {
	AHAddress address = new AHAddress(new RNGCryptoServiceProvider());
	SimNode node = new SimNode(address, NatType.Public, 0);
	list[address] = node;
      }
      SimNode n0 = (SimNode) list.GetByIndex(0);
      for (int i = 1; i < 10; i++) {
	SimNode n = (SimNode) list.GetByIndex(i);
	n0.AddConnection(n, (i%2 == 0)?ConType.Near:ConType.Shortcut);
      }
      Assert.AreEqual(n0.ConnectionTable.GetConnections(ConType.Near).Count, 4);
      Assert.AreEqual(n0.ConnectionTable.GetConnections(ConType.Shortcut).Count, 5);
      Assert.AreEqual(n0.ConnectionTable.GetAllConnections().Count, 9);      

      //make sure all connections are sorted in n0 table
      ArrayList n0_con = n0.ConnectionTable.GetAllConnections();
      for (int i = 0; i < n0_con.Count; i++) {
	AHAddress target = (AHAddress) n0_con[i];
	SimNode n = (SimNode) list.GetByIndex(i+1);
	Assert.AreEqual(target, n.LocalAddress);
      }
      //connection table is sorted (fine);
      SimNode n5 = (SimNode) list.GetByIndex(5);
      int idx = n0.ConnectionTable.IndexOf(n5.LocalAddress);
      Assert.IsTrue(idx > 0);
      Assert.AreEqual(idx, 4);


      idx = n0.ConnectionTable.IndexOf(n0.LocalAddress);
      Assert.IsTrue(idx < 0);
      idx = ~idx;
      Assert.IsTrue(idx == 0);

      for (int trials = 0; trials < 100; trials++) {
	AHAddress test_address = new AHAddress(new RNGCryptoServiceProvider());
	SimNode test_node = new SimNode(test_address, NatType.Public, 0);
	list[test_address] = test_node;
	int test_idx = list.IndexOfKey(test_address);
	//address of this newly generated address
	idx = n0.ConnectionTable.IndexOf(test_address);
	Assert.IsTrue(idx < 0);
	idx = ~idx;
	if (test_idx == 0) {
	  Assert.IsTrue(idx == 0);	
	} else {
	  Assert.IsTrue(idx == test_idx - 1);
	}
	list.Remove(test_address);
      }

      //do some unit tests for LeftInclusiveCount and RightInclusiveCount;
      for (int i = 1; i < 10; i++) {
	Assert.AreEqual(i-1, n0.ConnectionTable.LeftInclusiveCount(n0.LocalAddress, (AHAddress) list.GetKey(i)));
	//Console.WriteLine(n0.ConnectionTable.RightInclusiveCount(n0.LocalAddress, (AHAddress) list.GetKey(i)));	
	Assert.AreEqual(list.Count - i - 1, n0.ConnectionTable.RightInclusiveCount(n0.LocalAddress, (AHAddress) list.GetKey(i)));


      }

      

    }
  }
  #endif
}
