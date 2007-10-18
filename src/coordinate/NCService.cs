using System;
using System.IO;
using System.Collections;

#if NC_NUNIT
using System.Security.Cryptography;
using System.Collections.Specialized;
using NUnit.Framework;
#endif

using Brunet;
namespace Brunet.Coordinate {
  public class NCService {
    //the object server that accepts sample requests
    public class NCServer {
      private NCService _local_service;
      public NCServer(NCService local) {
	_local_service = local;
      }
      public Hashtable EchoVivaldiState() {
#if NC_DEBUG
	if (_local_service.Node != null) {
	  Console.Error.WriteLine("[NCService] {0} EchoVivaldiState() method invoked.", _local_service.Node.Address);
	}
#endif
	//get snapshot of the local vivaldi state
	VivaldiState v_state = _local_service.State;
	
	//
	// Hashtable containing the Vivaldi state.
	//
	Hashtable ht = new Hashtable();
	//error
	ht["error"] = v_state.WeightedError;
	//local coordinates
	Hashtable ht_position = new Hashtable();
	ht_position["side"] = new ArrayList(v_state.Position.Side);
	ht_position["height"] = v_state.Position.Height;
	ht["position"] = ht_position;
#if NC_DEBUG
	if (_local_service.Node != null) {
	  Console.Error.WriteLine("[NCService] {0} EchoVivaldiState() returning.", _local_service.Node.Address);
	}
#endif	
	return ht;
      }
    }
    
    protected NCServer _server;
    public NCServer Server {
      get {
	return _server;
      }
    }

    public class TrialState {
      public Address TargetAddress;
      public Edge TargetEdge;
      public Channel Queue;
      public DateTime Start;
      public int NumSamples;
      public static readonly int MIN_SAMPLES = 2;
    }
    protected TrialState _current_trial_state = null;

    public class VivaldiState {
      //current weighted error
      public double WeightedError;
      //our current position estimate
      public Point Position;
      //EWMA of movements, dont think this is being used currently
      public double DistanceDelta;
    }

    protected VivaldiState _vivaldi_state;
    public VivaldiState State {
      get {
	lock(_sync) {
	  if (_vivaldi_state == null) {
	    return null;
	  }
	  VivaldiState v_state = new VivaldiState();
	  v_state.Position  = 
	    new Point(_vivaldi_state.Position.Side, _vivaldi_state.Position.Height);
	  v_state.WeightedError = _vivaldi_state.WeightedError;
	  v_state.DistanceDelta = _vivaldi_state.DistanceDelta;
	  return v_state;
	}
      }
    }

    private static Hashtable _nc_service_table = new Hashtable();
    //every 20 seconds get a new sample for latency
    private static readonly int SAMPLE_INTERVAL = 20;

    //sample not valid beyond 5 seconds
    protected static readonly long SAMPLE_EXPIRATION = 5;
    
    //maximum latency value to consider a sample 
    protected static readonly double MAX_RTT = 5;
    
    protected object _sync;
    protected Node _node;
    public Node Node {
      get { return _node; }
    }
    protected RpcManager _rpc;
    protected DateTime _last_sample_instant;
    protected DateTime _last_update_file_instant;


    /** Vivaldi related stuff. */
    protected static readonly double DAMPENING_FRACTION = 0.25f;
    protected static readonly double ERROR_FRACTION = 0.25f;
    protected static readonly double INITIAL_WEIGHTED_ERROR = 1.0f;
    
    
    //latency samples from neighbors
    protected Hashtable _samples;

    public NCService() {
      _sync = new object();
      _node = null;
      _server = new NCServer(this);
      _last_sample_instant = DateTime.MinValue;
      _last_update_file_instant = DateTime.MinValue;
      _samples = new Hashtable();
      _current_trial_state = new TrialState();
      _vivaldi_state = new VivaldiState();
      _vivaldi_state.WeightedError = INITIAL_WEIGHTED_ERROR;
      _vivaldi_state.Position = new Point();
      _vivaldi_state.DistanceDelta = 0.0f;
    }

    public void Install(Node node) {
      lock(_nc_service_table) {
	if (_nc_service_table.ContainsKey(node)) {
	  throw new Exception("An instance of NCService already running on node: " + node.ToString());
	}
	if (_node != null) {
	  throw new Exception("NCService already assigned to node: " + _node.ToString() + 
			      " (cannot re-assign).");
	}
	_nc_service_table[node] = this;
	_node = node;
      }

#if NC_DEBUG
      Console.Error.WriteLine("[NCService] {0} Starting an instance of NCService", node.Address);
#endif 

      lock(_sync) {
	_rpc = RpcManager.GetInstance(node);
	_rpc.AddHandler("ncserver", _server);
	_node.HeartBeatEvent += new EventHandler(GetSample);
      }
    }

    protected void GetSampleFromTarget(Channel queue, Edge target_edge) {
      queue.CloseAfterEnqueue();
      queue.EnqueueEvent += new EventHandler(HandleSample);
      _rpc.Invoke(target_edge, queue, "ncserver.EchoVivaldiState", new object[]{});
    }
    
    protected void GetSample(object node, EventArgs args) {
      DateTime now = DateTime.Now;
      lock(_sync) {
	TimeSpan elapsed = now - _last_sample_instant;
	//
	// Check if it is too early to get a sample. 
	//
	
	if (elapsed.TotalSeconds < SAMPLE_INTERVAL) {
	  return;
	}
	
	//
	// Pick a random target from the connection table.
	//
	
	Connection target = _node.ConnectionTable.GetRandom(ConnectionType.Structured);
	if (target == null || target.Edge is TunnelEdge) {
#if NC_DEBUG
	  Console.Error.WriteLine("[NCService] {0} No structured connections.", _node.Address);
#endif
	  return;
	}
	_last_sample_instant = now;
	_current_trial_state.TargetAddress = target.Address;
	_current_trial_state.TargetEdge = target.Edge;
	_current_trial_state.Start = now;
	_current_trial_state.Queue = new Channel();
	_current_trial_state.NumSamples = 1;
	GetSampleFromTarget(_current_trial_state.Queue, _current_trial_state.TargetEdge);
      }
    }
    
    protected void HandleSample(object o, EventArgs args) {
#if NC_DEBUG
      Console.Error.WriteLine("[NCService] {0} getting a sample.", _node.Address);
#endif
      DateTime start;
      Address neighbor = null;
      Channel q =  (Channel) o;
      lock(_sync) {
	if (q != _current_trial_state.Queue) {
	  Console.Error.WriteLine("[NCService] {0} incomplete sample (no start time).", _node.Address);	  	  
	  return;
	} 

	//
	// Check to see if sufficient samples are now available. 
	// 
	if (_current_trial_state.NumSamples <  TrialState.MIN_SAMPLES) {
	  _current_trial_state.Queue = new Channel();
	  _current_trial_state.NumSamples++;
	  _current_trial_state.Start = DateTime.Now;
	  GetSampleFromTarget(_current_trial_state.Queue, _current_trial_state.TargetEdge);
	  return;
	}
	//
	// Complete sample is now available.
	//
	neighbor = _current_trial_state.TargetAddress;
	start = _current_trial_state.Start;
      }

      DateTime end = DateTime.Now;
      RpcResult res = q.Dequeue() as RpcResult;
      if (res.Statistics.SendCount == 1) {
	double o_rawLatency = (double) ((end - start).TotalMilliseconds);
	if (o_rawLatency > MAX_RTT*1000) {
	  return;
	}
	
	//extract sample information
	Hashtable ht = (Hashtable) res.Result;
	Hashtable ht_position = (Hashtable) ht["position"];
	Point o_position = 
	  new Point((double[]) ((ArrayList) ht_position["side"]).ToArray(typeof(double)), (double) ht_position["height"]);

	double o_weightedError = (double) ht["error"];
	ProcessSample(end, neighbor, o_position, o_weightedError, o_rawLatency);
      } else {
	Console.Error.WriteLine("[NCService] {0} ignore sample (multiple sends).", _node.Address);
      }
    }
    
    /** Processing of a latency sample using Vivaldi network coordinate approach. 
     *  @param o_stamp timestamp
     *  @param neighbor neighbor node from where sample is received
     *  @param o_position position vector of neighbor
     *  @param o_weightedError at the neighbor
     *  @param o_rawLatency latency of the sample
     */

    public void ProcessSample(DateTime o_stamp, Address neighbor, Point o_position, 
			      double o_weightedError, double o_rawLatency) {
      lock(_sync) {
#if NC_DEBUG
	Console.WriteLine("[Sample] at: {0}, from: {1}, position: {2}, error: {3}, latency: {4}", 
				o_stamp, neighbor, o_position, o_weightedError, o_rawLatency);
#endif

	Sample sample = null;
	if (_samples.ContainsKey(neighbor)) {
	  sample = (Sample) _samples[neighbor];
	} else {
	  sample = new Sample();
	  _samples[neighbor] = sample;
	}
	sample.AddSample(o_stamp, o_rawLatency, o_position, o_weightedError);
	double o_latency = sample.GetSample();
	if (o_latency < 0.0) {
#if NC_DEBUG
	  Console.Error.WriteLine("Too few samples to consider.");
#endif
	  return;
	}

	double o_distance = _vivaldi_state.Position.GetEucledianDistance(o_position);
	while (o_distance == 0) {
	  _vivaldi_state.Position.Bump();
	  o_distance = _vivaldi_state.Position.GetEucledianDistance(o_position);
	}
	double o_relativeError = Math.Abs((o_distance - o_latency)/o_latency);
	double o_rawRelativeError = Math.Abs((o_distance - o_rawLatency)/o_rawLatency);
	double o_weight = _vivaldi_state.WeightedError/(_vivaldi_state.WeightedError + o_weightedError);
	double o_alphaWeightedError = ERROR_FRACTION * o_weight;
      
#if NC_DEBUG
	Console.Error.WriteLine("o_distance: {0}", o_distance);
	Console.Error.WriteLine("o_latency: {0}", o_latency);
	Console.Error.WriteLine("o_relativeError (epsi): {0}", o_relativeError);
	Console.Error.WriteLine("o_weight (w_s): {0}", o_weight);
	Console.Error.WriteLine("my_weighted_error (preupdate)): {0}", State.WeightedError);
	Console.Error.WriteLine("alpha: {0}", o_alphaWeightedError);
#endif

	_vivaldi_state.WeightedError = (o_relativeError* o_alphaWeightedError) + 
	  _vivaldi_state.WeightedError*(1 - o_alphaWeightedError);

#if NC_DEBUG
	Console.Error.WriteLine("my_weighted_error (postupdate)): {0}", State.WeightedError);
#endif
	if (_vivaldi_state.WeightedError > 1.0f) {
	  _vivaldi_state.WeightedError = 1.0f;
	} 
	if (_vivaldi_state.WeightedError < 0.0f) {
	  _vivaldi_state.WeightedError = 0.0f;
	}
      
	Point o_force = new Point();
	int measurementsUsed = 0;
	DateTime o_oldestSample = o_stamp;
      
	ArrayList valid_nodes = new ArrayList();
	ArrayList invalid_nodes = new ArrayList();
	//only consider nodes we have heard from in "near" past
	foreach(Address node in _samples.Keys) {
	  Sample n_sample = (Sample) _samples[node];
	  if ((o_stamp - n_sample.TimeStamp).TotalSeconds > SAMPLE_EXPIRATION) {
	    //
	    // Invalidate node.
	    //
	    invalid_nodes.Add(node);
	    continue;
	  }
	  
	  if (n_sample.GetSample() < 0) {
	    //
	    // Neither valid nor invalid.
	    //
#if NC_DEBUG
	    Console.Error.WriteLine("Neither valid nor invalid.");
#endif
	    continue;
	  }
	  
	  //
	  // Valid node.
	  //
	  valid_nodes.Add(node);
	  if (o_oldestSample > n_sample.TimeStamp) {
	    o_oldestSample = n_sample.TimeStamp;
	  }
	}
      
	//get rid of invalid nodes.
	for (int k = 0; k < invalid_nodes.Count; k++) {
	  Address node = (Address) invalid_nodes[k];
#if NC_DEBUG
	  Console.Error.WriteLine("Removing samples from node: {0}", node);
#endif
	  _samples.Remove(node);
	}

#if NC_DEBUG
	Console.Error.WriteLine("Initiating force computation.");
#endif   
      
	double o_sampleWeightSum = 0.0f;
	for (int k = 0; k < valid_nodes.Count; k++) {
	  Address node = (Address) valid_nodes[k];
	  Sample n_sample = (Sample) _samples[node];	
	  o_sampleWeightSum += (double) (n_sample.TimeStamp - o_oldestSample).TotalSeconds;
	}

#if NC_DEBUG
	Console.Error.WriteLine("Oldest sample: {0}", o_oldestSample);
	Console.Error.WriteLine("Sample weight sum: {0}", o_sampleWeightSum);
#endif   

   	for (int k = 0; k < valid_nodes.Count; k++) {
	  Address node = (Address) valid_nodes[k];
	  Sample n_sample = (Sample) _samples[node];
	  
#if NC_DEBUG
	  Console.Error.WriteLine("current position: {0}", _vivaldi_state.Position);
#endif
	  double s_distance = _vivaldi_state.Position.GetEucledianDistance(n_sample.Position);
	  while (s_distance == 0) {
	    _vivaldi_state.Position.Bump();
	    s_distance = _vivaldi_state.Position.GetEucledianDistance(n_sample.Position);	  
	  }

	  Point s_unitVector = _vivaldi_state.Position.GetDirection(n_sample.Position);
	  if (s_unitVector == null) {
	    s_unitVector = Point.GetRandomUnitVector();
	  }

	  double s_latency = n_sample.GetSample();
	  double s_dampening = _vivaldi_state.WeightedError / (_vivaldi_state.WeightedError + n_sample.WeightedError);
	  double s_error = s_distance - s_latency;
	  double s_sampleWeight = 1.0f;
	  double s_sampleNewness = (double) (n_sample.TimeStamp - o_oldestSample).TotalSeconds;
	  if (o_sampleWeightSum > 0.0) {
	    s_sampleWeight = s_sampleNewness/o_sampleWeightSum;
	  }

#if NC_DEBUG
	  Console.Error.WriteLine("Force component: {0}", k);
	  Console.Error.WriteLine("Unit vector: {0}", s_unitVector);	  
	  Console.Error.WriteLine("s_distance: {0}", s_distance);
	  Console.Error.WriteLine("s_latency: {0}", s_latency);
	  Console.Error.WriteLine("s_error: {0}", s_error);
	  Console.Error.WriteLine("s_weighted_error (ws): {0}", n_sample.WeightedError);
	  Console.Error.WriteLine("s_dampening (ws): {0}", s_dampening);
	  Console.Error.WriteLine("s_sampleNewness: {0}", s_sampleNewness);
	  Console.Error.WriteLine("s_sampleWeight: {0}", s_sampleWeight);
#endif

	  s_unitVector.Scale(s_error * s_dampening * s_sampleWeight);
#if NC_DEBUG
	  Console.Error.WriteLine("s_force: {0}", s_unitVector);
#endif 
	  o_force.Add(s_unitVector);
	  measurementsUsed++;
	}

#if NC_DEBUG
	Console.Error.WriteLine("force (pre-scaling): {0}", o_force);
#endif
	o_force.Height = -o_force.Height;
	o_force.Scale(DAMPENING_FRACTION);
      

#if NC_DEBUG
	Console.Error.WriteLine("force (post-scaling): {0}", o_force);
#endif
	_vivaldi_state.Position.Add(o_force);
	_vivaldi_state.Position.CheckHeight();
#if NC_DEBUG
	Console.Error.WriteLine("position: {0}", _vivaldi_state.Position);
#endif
      }
    }
  }
#if NC_NUNIT
  [TestFixture]
  public class NCTester {  
    [Test]
    public void TestSample() {
      Sample sample = new Sample();
      Assert.IsTrue(sample.GetSample() < 0.0);
      sample.AddSample(DateTime.Now, (double) 100.0, new Point(), (double) 0.001);
      sample.AddSample(DateTime.Now, (double) 100.0, new Point(), (double) 0.002);
      sample.AddSample(DateTime.Now, (double) 100.0, new Point(), (double) 0.003);
      sample.AddSample(DateTime.Now, (double) 100.0, new Point(), (double) 0.004);
      Assert.IsTrue(sample.GetSample() > 0.0);

    }
    [Test]
    public void TestPoint() {
      Point p1 = new Point(new double[] {(double) 3.0, (double) 4.0}, 0);
      Assert.IsTrue(p1.Length() > 4.9 && p1.Length() < 5.1);
      Point p2 = new Point(new double[] {(double) 6.0, (double) 8.0}, 0);
      double d = p1.GetEucledianDistance(p2);
      Assert.IsTrue(d > 4.9 && d < 5.1);
      Point uv = p1.GetDirection(p2);
      Assert.IsTrue(uv.Length() > 0.9 && uv.Length() < 1.1);
      p1.Add(p2);
      Assert.IsTrue(p1.Side[0] > 8.9 && p1.Side[0] < 9.1);
      Assert.IsTrue(p1.Side[1] > 11.9 && p1.Side[1] < 12.1);

      p2.Scale((double) 0.5);
      Assert.IsTrue(p2.Side[0] > 2.9 && p2.Side[0] < 3.1);
      Assert.IsTrue(p2.Side[1] > 3.9 && p2.Side[1] < 4.1);

      Point p = new Point(p2.Side, p2.Height);
      Assert.IsTrue(p.Equals(p2));

      p2.Scale((double)2.0);
      Assert.IsTrue(!p.Equals(p2));
    }

    [Test]
    public void TestService() {
      NCService nc_service = new NCService();
      DateTime now = DateTime.Now;
      Address addr_remote = new AHAddress(new RNGCryptoServiceProvider());
      Address addr_remote1 = new AHAddress(new RNGCryptoServiceProvider());

      nc_service.ProcessSample(now + new TimeSpan(0, 0, 5), addr_remote, 
			       new Point(new double[] {(double) 3.0, (double) 4.0}, 0),
			       (double) 0.9, (double)10.0); 

      nc_service.ProcessSample(now + new TimeSpan(0, 0, 6), addr_remote1, 
			       new Point(new double[] {(double) 10.0, (double) 2.0}, 0),
			       (double) 0.9, (double)10.0); 


      nc_service.ProcessSample(now + new TimeSpan(0, 0, 7), addr_remote, 
			       new Point(new double[] {(double) 3.0, (double) 4.0}, 0),
			       (double) 0.8, (double)12.0); 

      nc_service.ProcessSample(now + new TimeSpan(0, 0, 8), addr_remote1, 
			       new Point(new double[] {(double) 10.0, (double) 2.0}, 0),
			       (double) 0.8, (double)12.0); 


      nc_service.ProcessSample(now + new TimeSpan(0, 0, 9), addr_remote, 
			       new Point(new double[] {(double) 3.0, (double) 4.0}, 0),
			       (double)0.7, (double)13.0); 

      nc_service.ProcessSample(now + new TimeSpan(0, 0, 11), addr_remote1, 
			       new Point(new double[] {(double) 10.0, (double) 2.0}, 0),
			       (double)0.7, (double)13.0); 


      nc_service.ProcessSample(now + new TimeSpan(0, 0, 12), addr_remote, 
			       new Point(new double[] {(double) 3.0, (double) 4.0}, 0),
			       (double)0.6, (double)10.0); 

      nc_service.ProcessSample(now + new TimeSpan(0, 0, 13), addr_remote1, 
			       new Point(new double[] {(double) 10.0, (double) 2.0}, 0),
			       (double)0.6, (double)10.0);       

      NCService.VivaldiState state = nc_service.State;
      Console.Error.WriteLine("position: {0}, error: {1}", state.Position, state.WeightedError);
    }

    [Test]
    public void TestSerialize() {
      NCService nc_service = new NCService();
      Hashtable ht1 = nc_service.Server.EchoVivaldiState();
      MemoryStream ms = new MemoryStream();
      int serialized = AdrConverter.Serialize(ht1, ms);
      byte[] buf = ms.ToArray();
      Assert.AreEqual(serialized, buf.Length, "Buffer length same as written");
      ms.Seek(0, SeekOrigin.Begin);
      object o = AdrConverter.Deserialize(ms);
      Hashtable ht =  o as Hashtable;
      Assert.IsTrue(ht != null);
      Hashtable ht_position = (Hashtable) ht["position"];
      Point o_position = 
	new Point((double[]) ((ArrayList) ht_position["side"]).ToArray(typeof(double)), (double) ht_position["height"]);
	  
      double o_weightedError = (double) ht["error"];
      // 
      // Make sure that the values obtained match the orgininal NC state.
      //

      NCService.VivaldiState state = nc_service.State;

      Assert.IsTrue(o_position.Equals(state.Position));
      Assert.IsTrue(o_weightedError.Equals(state.WeightedError));
    }
  }
#endif
}


