using System;
using System.Collections;
using System.IO;

using Brunet;

#if NC_LOG
using log4net;
using log4net.Config;
#endif



namespace Brunet.Coordinate {
  public class NCService {

#if NC_LOG
    private static readonly log4net.ILog _log =
    log4net.LogManager.GetLogger(System.Reflection.MethodBase.
				 GetCurrentMethod().DeclaringType);
#endif


    private static Hashtable _nc_service_table = new Hashtable();

    //every 20 seconds get a new sample for latency
    private static readonly int SAMPLE_INTERVAL = 20;

    //update coordinate on file interval (60 seconds)
    private static readonly int UPDATE_FILE_INTERVAL = 60;
    
    //sample not valid beyond 15 minutes
    protected static readonly long SAMPLE_EXPIRATION = 900;

    
    public class NCServer {

      private NCService _local_service;
      public NCServer(NCService local) {
	_local_service = local;
      }
      
      public Hashtable EchoVivaldiState() {

#if NC_DEBUG
	Console.WriteLine("[NCService] {0} EchoVivaldiState() method invoked.", _local_service.Address);
#endif
	
	Address addr = _local_service.Address;
	VivaldiState v_state = _local_service.State;
	
	//
	// hashtable containing the Vivaldi state.
	//
	
	Hashtable ht = new Hashtable();
	byte[] b = new byte[Address.MemSize];
	addr.CopyTo(b);
	ht["neighbor"] = b; 
	ht["error"] = v_state.WeightedError;


	//local coordinates
	Hashtable ht_position = new Hashtable();
	ht_position["side"] = new ArrayList(v_state.Position.Side);
	ht_position["height"] = v_state.Position.Height;
	ht["position"] = ht_position;
#if NC_DEBUG
	Console.WriteLine("[NCService] {0} EchoVivaldiState() returning.", _local_service.Address);
#endif	
	return ht;
      }
    }
    
    protected NCServer _server;
    
    protected object _sync;
    public object SyncRoot {
      get {
	return _sync;
      }
    }
    
    protected Node _node;
    public Address Address {
      get {
	return _node.Address;
      }
    }
    protected RpcManager _rpc;
    protected DateTime _last_sample_instant;
    protected DateTime _last_update_file_instant;

    /** Vivaldi related stuff. */
    protected static readonly float DAMPENING_FRACTION = 0.25f;
    protected static readonly float ERROR_FRACTION = 0.25f;
    protected static readonly float INITIAL_WEIGHTED_ERROR = 1.0f;
    
    
    //latency samples from neighbors
    protected Hashtable _samples;

    public class TrialState {
      public Address Target;
      public Channel Queue;
      public DateTime Start;
      public int NumSamples;
      public static readonly int MIN_SAMPLES = 2;
    }

    protected TrialState _current_trial_state = null;

    public class VivaldiState {
      //current weighted error
      public float WeightedError;
      //our current position estimate
      public Point Position;
      //EWMA of movements, dont think this is being used currently
      public float DistanceDelta;
    }
    protected VivaldiState _vivaldi_state;
    public VivaldiState State {
      get {
	lock(_sync) {
	  if (_vivaldi_state == null) {
	    return null;
	  }
	  VivaldiState v_state = new VivaldiState();
	  v_state.Position  = new Point();
	  v_state.Position.Assign(_vivaldi_state.Position);
	  v_state.WeightedError = _vivaldi_state.WeightedError;
	  v_state.DistanceDelta = _vivaldi_state.DistanceDelta;
	  return v_state;
	}
      }
    }

    protected static string _coordinate_cache_dir = "coordinates";
    protected static string _coordinate_cache_file = "coordinates.log";

    private NCService(Node node) {
      _sync = new object();
      _node = node;
      _last_sample_instant = DateTime.MinValue;
      _last_update_file_instant = DateTime.MinValue;
      _samples = new Hashtable();
      _current_trial_state = new TrialState();
      _vivaldi_state = new VivaldiState();
      _vivaldi_state.WeightedError = INITIAL_WEIGHTED_ERROR;
      _vivaldi_state.Position = new Point();
      _vivaldi_state.DistanceDelta = 0.0f;

      lock(_sync) {
	_rpc = RpcManager.GetInstance(node);      
	_server = new NCServer(this);
	_rpc.AddHandler("ncserver", _server);
	_node.HeartBeatEvent += new EventHandler(GetSample);
      }
    }

    public static void InstallNCService(Node node) {
      lock(_nc_service_table) {
	if (_nc_service_table.ContainsKey(node)) {
	  throw new Exception(" NCService already installed on node: " + node.ToString());
	}
	_nc_service_table[node] = new NCService(node);
#if NC_DEBUG
	Console.WriteLine("[NCService] {0} Installing an instance of NCService", node.Address);
#endif 
      }
    }

    protected void GetSampleFromTarget(Address target) {
      Channel q = new Channel();
      q.CloseAfterEnqueue();
      q.EnqueueEvent += new EventHandler(HandleSample);
      AHSender s = new AHSender(_node, target, AHPacket.AHOptions.Exact);
      _rpc.Invoke(s, q, "ncserver.EchoVivaldiState", new object[]{});
      _current_trial_state.Queue = q;
      _current_trial_state.NumSamples++;
    }
    
    protected void GetSample(object node, EventArgs args) {
      TimeSpan elapsed = DateTime.Now - _last_sample_instant;

      //
      // Check if too early to get a sample. 
      //
      
      if (elapsed.TotalSeconds < SAMPLE_INTERVAL) {
	return;
      }

      //
      // Pick a random target from the connection table.
      //

      Connection target = _node.ConnectionTable.GetRandom(ConnectionType.Structured);
      if (target == null) {
#if NC_DEBUG
	Console.WriteLine("[NCService] {0} No structured connections.", _node.Address);
#endif
	return;
      }

      DateTime now = DateTime.Now;
      _last_sample_instant = now;
      _current_trial_state.NumSamples = 0;
      _current_trial_state.Target = target.Address;
      _current_trial_state.Start = now;
      GetSampleFromTarget(target.Address);
    }
    
    protected void HandleSample(object o, EventArgs args) {
#if NC_DEBUG
      Console.WriteLine("[NCService] {0} getting a sample.", _node.Address);
#endif
      DateTime start;
      Channel q =  (Channel) o;
      lock(_sync) {
	if (q != _current_trial_state.Queue) {
	  Console.Error.WriteLine("[NCService] {0} incomplete sample (no start time).", _node.Address);	  	  
	  return;
	} 

	if (_current_trial_state.NumSamples <  TrialState.MIN_SAMPLES) {
	  GetSampleFromTarget(_current_trial_state.Target);
	  return;
	}
	start = _current_trial_state.Start;
      }

      DateTime end = DateTime.Now;
      
      RpcResult res = q.Dequeue() as RpcResult;

      //unregister any future enqueue events
      q.EnqueueEvent -= new EventHandler(HandleSample);
      
      if (res.Statistics != null) {
	if (res.Statistics.SendCount == 1) {
	  //only then we consider the sample
	  Hashtable ht = (Hashtable) res.Result;
	  byte[] b = (byte[]) ht["neighbor"];
	  
	  Address neighbor = new AHAddress(b);

	  //make sure we still have a connection to this guy
	  Connection c = _node.ConnectionTable.GetConnection(ConnectionType.Structured, neighbor);
	  if (c == null) {
	    Console.Error.WriteLine("[NCService] {0} Got a sample from someone we are not connected.", _node.Address);
	    return;
	  }
#if NC_DEBUG
	  Console.WriteLine("[NCService] {0} # of structured connections: {1}.", 
			    _node.Address, _node.ConnectionTable.Count(ConnectionType.Structured));
	  Console.WriteLine("sample TA: {0}", c.Edge.RemoteTA.ToString());
#endif
	  Hashtable ht_position = (Hashtable) ht["position"];
	  Point o_position = new Point();
	  o_position.Height = (float) ht_position["height"];
	  
	  o_position.Side = (float[]) ((ArrayList) ht_position["side"]).ToArray(typeof(float));

	  float o_weightedError = (float) ht["error"];
	  float o_rawLatency = (float) ((end - start).TotalMilliseconds);
	  
#if NC_LOG
	  string ss = _node.Address + "::::" + DateTime.UtcNow.Ticks + "::::Coordinates::::"; 
	  ss += (State.Position.Side[0] + "::::" + State.Position.Side[1] + "::::" + State.Position.Height + "::::");
	  ss += ("Error::::" + State.WeightedError);
	  _log.Debug(ss);
#endif
	  

	  ProcessSample(end, neighbor, o_position, o_weightedError, o_rawLatency);

// 	  if ((end - _last_update_file_instant).TotalSeconds > UPDATE_FILE_INTERVAL) {
// 	    UpdateToFile();
// 	    _last_update_file_instant = end;
// 	  }
	} else {
	  Console.Error.WriteLine("[NCService] {0} ignore sample (multiple sends).", _node.Address);	  
	}
      }
    }

    /** Processing of a latency sample using Vivaldi network coordinate approach. 
     *  @param o_stamp timestamp
     *  @param neighbor neighbor node from where sample is received
     *  @param o_position position vector of neighbor
     *  @param o_weightedError at the neighbor
     *  @param o_rawLatency latency of the sample
     */
    public void ProcessSample(DateTime o_stamp, Address neighbor, Point o_position, float o_weightedError, float o_rawLatency) {
      lock(_sync) {
#if NC_DEBUG
	Console.WriteLine("[Sample] stamp: {0}, neighbor: {1}, position: {2}, error: {3}, latency: {4}", 
			  o_stamp, neighbor, o_position, o_weightedError, o_rawLatency);
#endif

#if NC_LOG
	Connection c = null;
	string ss = null;
	if (_node != null) {
	  c = _node.ConnectionTable.GetConnection(ConnectionType.Structured, neighbor);
	  ss = _node.Address + "::::" + DateTime.UtcNow.Ticks + "::::RawSample::::";
	  ss += (neighbor + "::::" + c.Edge.RemoteTA.ToString() + "::::" + o_rawLatency);
	  _log.Debug(ss);
	}
#endif



	Sample sample = null;
	if (_samples.ContainsKey(neighbor)) {
	  sample = (Sample) _samples[neighbor];
	} else {
	  sample = new Sample();
	  _samples[neighbor] = sample;
	}
	sample.AddSample(o_stamp, o_rawLatency, o_position, o_weightedError);
	float o_latency = sample.GetSample();
	if (o_latency < 0) {
	  return;
	}

#if NC_LOG
	if (_node != null) {
	  c = _node.ConnectionTable.GetConnection(ConnectionType.Structured, neighbor);
	  ss = _node.Address + "::::" + DateTime.UtcNow.Ticks + "::::Sample::::";
	  ss += (neighbor + "::::" + c.Edge.RemoteTA.ToString() + "::::" + o_rawLatency);
	  _log.Debug(ss);
	}
#endif

	float o_distance = _vivaldi_state.Position.GetEucledianDistance(o_position);
	while (o_distance == 0) {
	  _vivaldi_state.Position.Bump();
	  o_distance = _vivaldi_state.Position.GetEucledianDistance(o_position);
	}
	float o_relativeError = Math.Abs((o_distance - o_latency)/o_latency);
	float o_rawRelativeError = Math.Abs((o_distance - o_rawLatency)/o_rawLatency);
	float o_weight = _vivaldi_state.WeightedError/(_vivaldi_state.WeightedError + o_weightedError);
	float o_alphaWeightedError = ERROR_FRACTION * o_weight;
      
#if NC_DEBUG
	Console.WriteLine("o_distance: {0}", o_distance);
	Console.WriteLine("o_latency: {0}", o_latency);
	Console.WriteLine("o_relativeError (epsi): {0}", o_relativeError);
	Console.WriteLine("o_weight (w_s): {0}", o_weight);
	Console.WriteLine("my_weight (preupdate)): {0}", State.WeightedError);
	Console.WriteLine("alpha: {0}", o_alphaWeightedError);
	
#endif
	_vivaldi_state.WeightedError = (o_relativeError* o_alphaWeightedError) + _vivaldi_state.WeightedError*(1 - o_alphaWeightedError);
#if NC_DEBUG
	Console.WriteLine("my_weight (postupdate)): {0}", State.WeightedError);
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
	    //sample has expired
	    invalid_nodes.Add(node);
	  } else {
	    valid_nodes.Add(node);
	    if (o_oldestSample > n_sample.TimeStamp) {
	      o_oldestSample = n_sample.TimeStamp;
	    }
	  }
	}
      
	//get rid of invalid nodes
	for (int k = 0; k < invalid_nodes.Count; k++) {
	  Address node = (Address) invalid_nodes[k];
	  _samples.Remove(node);
	}
      
	float o_sampleWeightSum = 0.0f;
	for (int k = 0; k < valid_nodes.Count; k++) {
	  Address node = (Address) valid_nodes[k];
	  Sample n_sample = (Sample) _samples[node];	
	  o_sampleWeightSum += (float) (n_sample.TimeStamp - o_oldestSample).TotalSeconds;
	}
      
	for (int k = 0; k < valid_nodes.Count; k++) {
	  Address node = (Address) valid_nodes[k];
	  Sample n_sample = (Sample) _samples[node];

	  float s_distance = _vivaldi_state.Position.GetEucledianDistance(n_sample.Position);
	  while (s_distance == 0) {
	    _vivaldi_state.Position.Bump();
	    s_distance = _vivaldi_state.Position.GetEucledianDistance(n_sample.Position);	  
	  }
	  Point s_unitVector = _vivaldi_state.Position.GetDirection(n_sample.Position);
	  if (s_unitVector == null) {
	    s_unitVector = Point.GetRandomUnitVector();
	  }
	  float s_latency = n_sample.GetSample();
	
	  float s_weight = _vivaldi_state.WeightedError / (_vivaldi_state.WeightedError + n_sample.WeightedError);

	  float s_error = s_distance - s_latency;
	
	  float s_dampening = s_weight;
	
	  float s_sampleWeight = 1.0f;
	  if (o_sampleWeightSum > 0.0) {
	    s_sampleWeight = (float) (((n_sample.TimeStamp - o_oldestSample).TotalSeconds)/o_sampleWeightSum);
	  }
	  s_unitVector.Scale(s_error * s_dampening * s_sampleWeight);
	  o_force.Add(s_unitVector);
	  measurementsUsed++;
	}
	o_force.Height = -o_force.Height;
	o_force.Scale(DAMPENING_FRACTION);
      
	//update position
#if NC_DEBUG
	Console.WriteLine("force: {0}", o_force);
#endif
	_vivaldi_state.Position.Add(o_force);
	_vivaldi_state.Position.CheckHeight();
#if NC_DEBUG
	Console.WriteLine("position: {0}", _vivaldi_state.Position);
#endif
	//_vivaldi_state.DistanceDelta = (float) (0.05 * Math.Abs(o_force.Length()) + 0.95*_vivaldi_state.DistanceDelta);
      }
    }

//     protected void TryInitializeFromFile() {
//       lock(_sync) {
// 	string dir_name = _coordinate_cache_dir + "/" + _node.Address.ToString();
// 	if (!Directory.Exists(dir_name)) {
// 	  Directory.CreateDirectory(dir_name);
// 	  return;
// 	}
// 	string fname = dir_name + "/" + _coordinate_cache_file;
// 	  try {
// 	    FileStream fStream = new FileStream(fname, FileMode.Open, FileAccess.Read);
// 	    StreamReader br = new StreamReader(fStream);
// 	    //first line are the coordinates; assume 2 dimernsions
// 	    string s = br.ReadLine();
// 	    string[] ss = s.Split();
// 	    if (ss.Length != Point.DIMENSIONS) {
// 	      throw new Exception("Incorrect number of dimensions in euclidien space" );
// 	    }
// 	    float[] side = new float[Point.DIMENSIONS];
// 	    for (int i = 0; i < Point.DIMENSIONS; i++) {
// 	      side[i] = (float) Double.Parse(ss[i].Trim());
// 	    }
// 	    //next line is the height
// 	    s = br.ReadLine();
// 	    float height = (float) Double.Parse(s.Trim());
// 	    //next value is the error
// 	    s = br.ReadLine();
// 	    float error = (float) Double.Parse(s.Trim());
// 	    _vivaldi_state = new VivaldiState();
// 	    _vivaldi_state.Position = new Point();
// 	    _vivaldi_state.Position.Side = side;
// 	    _vivaldi_state.Position.Height = height;
// 	    _vivaldi_state.WeightedError = error;
// 	  } catch(Exception e) {
// 	    Console.Error.WriteLine(e);
// 	    _vivaldi_state = null;
// 	  }
//       }
//     }
//     protected void UpdateToFile() {
//       lock(_sync) {
// 	try {
// 	  string fname = _coordinate_cache_dir + "/" + _node.Address.ToString() + "/" + _coordinate_cache_file;
	  
// 	  FileStream fStream = new FileStream(fname, FileMode.Truncate);
// 	  StreamWriter sw = new StreamWriter(fStream);
	  
// 	  for (int i = 0; i < Point.DIMENSIONS; i++) {
// 	    sw.Write("{0} ", _vivaldi_state.Position.Side[i]);
// 	  }
// 	  sw.WriteLine();
// 	  sw.WriteLine("{0} ", _vivaldi_state.Position.Height);
// 	  sw.WriteLine("{0} ", _vivaldi_state.WeightedError);	
	  
// 	  //this is important; only on close are updates actually made in some cases
// 	  sw.Close();
// 	} catch (Exception e) {
// 	  Console.Error.WriteLine(e);	  
// 	}
//       }
//     }
  }
}
