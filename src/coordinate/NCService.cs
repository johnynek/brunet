/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida
                   Arijit Ganguly <aganguly@acis.ufl.edu> University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/
//#define NC_DEBUG

using System;
using System.IO;
using System.Net;
using System.Collections;
using System.Collections.Specialized;
using System.Threading;
using Brunet;

#if NC_NUNIT
using System.Security.Cryptography;
using System.Collections.Specialized;
using NUnit.Framework;
#endif

namespace Brunet.Coordinate {

  /** This class implements the Vivaldi network coordinates. 
   *  Each node periodically takes latency samples from its connections
   *  and adjusts its coordinates.
   */
  public class NCService : IRpcHandler {
    protected static readonly string _hostname = Dns.GetHostName();
    /**
     * This dispatches the particular methods this class provides
     */
    public void HandleRpc(ISender caller, string method, IList args, object req_state) {
      if( method == "Echo" ) {
        Echo();
        _rpc.SendResult(req_state, new object[]{});
      } 
      else if (method == "EchoVivaldiState" ) {
        Hashtable ht = EchoVivaldiState();
        _rpc.SendResult(req_state, ht);
      } 
      else if ( method == "ComputePathLatencyTo" ) {
        ComputePathLatencyTo( (AHAddress)AddressParser.Parse((string)args[0]), req_state);
      }
      else {
        throw new AdrException(-32601, "No Handler for method: " + method);
      }
    }

    /**
     * This method packages the current state of the network coordinates
     * into a Hashtable than can be sent back as Brunet-Rpc result.
     * @returns hashtable representing the network coordinate state. 
     */
    public Hashtable EchoVivaldiState() {
#if NC_DEBUG
      if (_node != null) {
        Console.Error.WriteLine("[NCService] {0} EchoVivaldiState() method invoked.", _node.Address);
      }
#endif
      //get snapshot of the local vivaldi state
      VivaldiState v_state = State;
      
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
      ht["hostname"] = _hostname;
#if NC_DEBUG
      if (_node != null) {
        Console.Error.WriteLine("[NCService] {0} EchoVivaldiState() returning.", _node.Address);
      }
#endif	
      return ht;
    }

    /**
     * dummy method to help another node determine latency to us.
     */
    public void Echo() {
      return;
    }

    /**
     * Recursive function to compute the latency of an Rpc call
     * by accumulating measured latencies of individual hops.
     * @param target address of the target
     */
    public void ComputePathLatencyTo(AHAddress a, object req_state) {
      /*
       * First find the Connection pointing to the node closest to dest, if
       * there is one closer than us
       */
      
      ConnectionTable tab = _node.ConnectionTable;
      ConnectionList structs = tab.GetConnections(ConnectionType.Structured);
      Connection next_closest = structs.GetNearestTo((AHAddress) _node.Address, a);
      //Okay, we have the next closest:
      ListDictionary my_entry = new ListDictionary();
      my_entry["node"] = _node.Address.ToString();
      if( next_closest != null ) {
        my_entry["next_latency"] = GetMeasuredLatency(next_closest.Address);
        my_entry["next_contype"] = next_closest.ConType;
        Channel result = new Channel();
        //We only want one result, so close the queue after we get the first
        result.CloseAfterEnqueue();
        result.CloseEvent += delegate(object o, EventArgs args) {
          Channel q = (Channel)o;
          if( q.Count > 0 ) {
            try {
              RpcResult rres = (RpcResult)q.Dequeue();
              IList l = (IList) rres.Result;
              ArrayList results = new ArrayList( l.Count + 1);
              results.Add(my_entry);
              results.AddRange(l);
              _rpc.SendResult(req_state, results);
            }
            catch(Exception x) {
              string m = String.Format("<node>{0}</node> trying <connection>{1}</connection> got <exception>{2}</exception>", _node.Address, next_closest, x);
              Exception nx = new Exception(m);
              _rpc.SendResult(req_state, nx);
            }
          }
            else {
              //We got no results.
              IList l = new ArrayList(1);
              l.Add( my_entry );
              _rpc.SendResult(req_state, l);
            }
        };
        _rpc.Invoke(next_closest.Edge, result, "ncserver.ComputePathLatencyTo", a.ToString());
      }
      else {
        //We are the end of the line, send the result:
        ArrayList l = new ArrayList();
        l.Add(my_entry);
        _rpc.SendResult(req_state, l);  
      }
    }

    /**
     * Network coordinates require periodic sampling of latency and coordinate
     * state of out connections. We record the current trial in this
     * data structure. 
     */ 
    public class TrialState {
      //address
      public Address TargetAddress;
      //edge over which we do the test
      public Edge TargetEdge;
      //result queue
      public Channel Queue;
      //vivaldi state as retrieved from the remote node
      public object StateResult;
      //start time of sampling
      public DateTime Start;
      //number of samples taken in this trial
      public int NumSamples;
      public static readonly int MIN_SAMPLES = 1;
    }
    protected TrialState _current_trial_state = null;

    //local vivaldi state
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
	    new Point(_vivaldi_state.Position);
	  v_state.WeightedError = _vivaldi_state.WeightedError;
	  v_state.DistanceDelta = _vivaldi_state.DistanceDelta;
	  return v_state;
	}
      }
    }

    /**
     * Returns latency to a neighbor.
     * @param address of the neighbor
     * @returns measured latency
     */
    public double GetMeasuredLatency(Address o_neighbor) {
      lock(_sync) {
	if (_samples.ContainsKey(o_neighbor)) {
	  Sample s = (Sample) _samples[o_neighbor];
          return s.GetSample();
	} else {
          return -1.0;
	}
      }
    }

    //every 60 seconds write out current position to a file.
    protected static readonly int CHECKPOINT_INTERVAL = 60;
    protected static readonly string _checkpoint_file = Path.Combine("/tmp", "vivaldi");
    protected static Hashtable _nc_service_table = new Hashtable();
    protected static Point _checkpointed_position = null;
    protected static Thread _checkpoint_thread = null;

    //every 10 seconds get a new sample for latency.
    protected static readonly int SAMPLE_INTERVAL = 10;

    //sample not valid beyond 1800 seconds
    protected static readonly long SAMPLE_EXPIRATION = 1800;
    
    //maximum latency value to consider a sample 
    //protected static readonly double MAX_RTT = 5.0;
    
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
      _last_sample_instant = DateTime.MinValue;
      _last_update_file_instant = DateTime.MinValue;
      _samples = new Hashtable();
      _current_trial_state = new TrialState();
      _vivaldi_state = new VivaldiState();
      _vivaldi_state.WeightedError = INITIAL_WEIGHTED_ERROR;
      _vivaldi_state.Position = new Point();
      _vivaldi_state.DistanceDelta = 0.0f;
    }

    /** 
     * Installs the network coordinate service on a given node. 
     * NCService instance can be installed on atmost one node. 
     * Each node is allowed to have only one NCService instance. 
     * @param node node for installing the service instance. 
     */
    public void Install(Node node) {
      Point init_position = null;
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
	if (_checkpoint_thread == null) {
	  //we are just starting up.
	  InitializeVivaldiPosition();
	  _checkpoint_thread = new Thread(CheckpointThread);
	  _checkpoint_thread.Start();
	}
	init_position = new Point(_checkpointed_position);
      }

#if NC_DEBUG
      Console.Error.WriteLine("[NCService] {0} Starting an instance of NCService", node.Address);
#endif 
      lock(_sync) {
	_vivaldi_state.Position = new Point(init_position);
	_rpc = RpcManager.GetInstance(node);
	_rpc.AddHandler("ncserver", this);
	_node.HeartBeatEvent += new EventHandler(GetNextSample);
	_node.StateChangeEvent += delegate(Node n, Node.ConnectionState s) {
	  if( s == Node.ConnectionState.Disconnected ) {
	    lock(_nc_service_table) {
	      Console.Error.WriteLine("Removing {0} from nc_service table", n.Address);
	      _nc_service_table.Remove(n);
	    }
	  }
	};
      }
    }

    /** 
     * Step 2. Get latency sample for the current trial.
     * @param state information associated with the node we are going to sample. 
     */
    protected void GetLatencySample(TrialState state) {
      state.Queue.CloseAfterEnqueue();
      state.Queue.EnqueueEvent += new EventHandler(HandleLatencySample);
      _rpc.Invoke(state.TargetEdge, state.Queue, "ncserver.Echo", new object[]{});
    }

    /** 
     * Step 1. Get vivaldi state for the current trial.
     * @param state information associated with the node we are going to sample. 
     */
    protected void GetVivaldiState(TrialState state) {
      state.Queue.CloseAfterEnqueue();
      state.Queue.EnqueueEvent += new EventHandler(HandleVivaldiState);
      _rpc.Invoke(state.TargetEdge, state.Queue, "ncserver.EchoVivaldiState", new object[]{});      
    }

    /** 
     * Initiate next trial. 
     * We do it in two steps:
     * Step 1. We first retrieve the network coordinate state of the remote node. 
     * Step 2. We take one or more latency samples.
     * Sometimes NATs are first on the first packet sent after a long time. 
     * This is why the second step gets the latency sample. 
     */
    protected void GetNextSample(object node, EventArgs args) {
      //
      // Pick a random target from the connection table.
      //
      Connection target = _node.ConnectionTable.GetRandom(ConnectionType.Structured);
      try {
	DateTime now = DateTime.Now;
	lock(_sync) {
	  TimeSpan elapsed = now - _last_sample_instant;
	  //
	  // Check if it is too early to get a sample. 
	  //
	  if (elapsed.TotalSeconds < SAMPLE_INTERVAL) {
	    return;
	  }
          
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
	  _current_trial_state.StateResult = null;
	  GetVivaldiState(_current_trial_state);
	}
      } catch(Exception e) {
	Console.Error.WriteLine(e);
      }
    }
    
    /**
     * Invoked when we have just retrieved the vivaldi-state of a remote node. 
     * Next step would be to take a latency sample to the same node. 
     */
    protected void HandleVivaldiState(object o, EventArgs args) {
#if NC_DEBUG
      Console.Error.WriteLine("[NCService] {0} Got remote vivaldi state.", _node.Address);
#endif
      //make sure that the trial is still on, and we havenot moved on to next sample
      Channel q =  (Channel) o;
      lock(_sync) {
	if (q != _current_trial_state.Queue) {
	  Console.Error.WriteLine("[NCService] {0} Too late now.", _node.Address);	  	  
	  return;
	} 
	_current_trial_state.Queue = new Channel();
	_current_trial_state.NumSamples = 1;
	_current_trial_state.Start = DateTime.Now;
	try {
	  RpcResult result = q.Dequeue() as RpcResult;
	  _current_trial_state.StateResult = result.Result;
	  GetLatencySample(_current_trial_state);
	} catch(Exception e) {
	  //invalid sample
	  Console.Error.WriteLine(e);
	  return;
	}
      }
    }

    /** 
     * Invoked when we get a latency sample for the current trial.
     * Makes sure we have sufficient number of samples.
     */
    protected void HandleLatencySample(object o, EventArgs args) {
#if NC_DEBUG
      Console.Error.WriteLine("[NCService] {0} Got a latency sample.", _node.Address);
#endif
      DateTime start;
      Address neighbor = null;
      object state_result = null;
      Channel q =  (Channel) o;
      lock(_sync) {
	//check to see if still valid.
	if (q != _current_trial_state.Queue) {
	  Console.Error.WriteLine("[NCService] {0} Too late now.", _node.Address);	  	  
	  return;
	} 

	//
	// Check to see if sufficient samples are now available. 
	// 
	if (_current_trial_state.NumSamples <  TrialState.MIN_SAMPLES) {
#if NC_DEBUG
	  Console.Error.WriteLine("Insufficient samples. Get some more.");
#endif
	  _current_trial_state.Queue = new Channel();
	  _current_trial_state.NumSamples++;
	  _current_trial_state.Start = DateTime.Now;
	  try {
	    GetLatencySample(_current_trial_state);
	  } catch(Exception e) {
	    Console.Error.WriteLine(e);
	  }
	  return;
	}

	//
	// Complete sample is now available.
	//
#if NC_DEBUG
	Console.Error.WriteLine("Complete sample.");
#endif
	neighbor = _current_trial_state.TargetAddress;
	start = _current_trial_state.Start;
	state_result = _current_trial_state.StateResult;
      }

      DateTime end = DateTime.Now;
      try {
	RpcResult res = q.Dequeue() as RpcResult;
	if (res.Statistics.SendCount > 1) {
	  Console.Error.WriteLine("[NCService] {0} ignore sample (multiple sends).", _node.Address);
	  return;
	}
	double o_rawLatency = (double) ((end - start).TotalMilliseconds);
	//if (o_rawLatency > MAX_RTT*1000) {
	//  return;
	//}

	//extract vivaldi state for the remote node.
	Hashtable ht =  (Hashtable) state_result;
	Hashtable ht_position = (Hashtable) ht["position"];
	Point o_position = 
	  new Point((double[]) ((ArrayList) ht_position["side"]).ToArray(typeof(double)), (double) ht_position["height"]);
	double o_weightedError = (double) ht["error"];
	string host = null;
	if (ht.Contains("hostname")) {
	  host = (string) ht["hostname"];
	}
	ProcessSample(end, host, neighbor, o_position, o_weightedError, o_rawLatency);
      } catch(Exception e) {
	Console.Error.WriteLine(e);
	return;
      }
    }
    
    /** Processing of a latency sample using Vivaldi network coordinate approach. 
     *  @param o_stamp timestamp
     *  @param o_host name of the host from which we got the sample
     *  @param o_neighbor neighbor node from where sample is received
     *  @param o_position position vector of neighbor
     *  @param o_weightedError at the neighbor
     *  @param o_rawLatency latency of the sample
     */

    public void ProcessSample(DateTime o_stamp, string o_host, Address o_neighbor, Point o_position, 
			      double o_weightedError, double o_rawLatency) {
      lock(_sync) {
	Sample sample = null;
	if (_samples.ContainsKey(o_neighbor)) {
	  sample = (Sample) _samples[o_neighbor];
	} else {
	  sample = new Sample();
	  _samples[o_neighbor] = sample;
	}

	sample.AddSample(o_stamp, o_rawLatency, o_position, o_weightedError);
	double o_latency = sample.GetSample();
	if (o_latency < 0.0) {
#if NC_DEBUG
	  Console.Error.WriteLine("Too few samples to consider.");
#endif
	  return;
	}

	Console.Error.WriteLine("Sample at: {0}, from: {1} {2}, position: {3}, error: {4}, raw latency: {5}, smooth latency: {6}", 
				o_stamp, o_host, o_neighbor, o_position, o_weightedError, o_rawLatency, o_latency);
	double o_distance = _vivaldi_state.Position.GetEucledianDistance(o_position);
	while (o_distance == 0) {
	  _vivaldi_state.Position.Bump();
	  o_distance = _vivaldi_state.Position.GetEucledianDistance(o_position);
	}

	Console.Error.WriteLine("Current position: {0}, distance: {1}", _vivaldi_state.Position, o_distance);
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
	if (_vivaldi_state.WeightedError > 1.0) {
	  _vivaldi_state.WeightedError = 1.0;
	} 
	if (_vivaldi_state.WeightedError < 0.0) {
	  _vivaldi_state.WeightedError = 0.0;
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
	  // valid sample.
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

	Console.Error.WriteLine("Updated position: {0}, distance: {1}", 
				_vivaldi_state.Position,  
				_vivaldi_state.Position.GetEucledianDistance(o_position));
      }
    }
    
    /**
     * Initialize vivaldi coordinates from a file. 
     */
    protected static void InitializeVivaldiPosition() {
      Point p = null;
      try {
	TextReader tr = new StreamReader(_checkpoint_file);
        //ignore first line; it has the timestamp
        tr.ReadLine();
	string s = tr.ReadLine().Trim();
	p = new Point(s);
	tr.Close();
      } catch {
	p = new Point();
      }
      lock(_nc_service_table) {
	_checkpointed_position = p;
      }
    }

    /**
     * Checkpoint vivaldi coordinates from a file. 
     */
    protected static void CheckpointThread() {
      do {
	System.Threading.Thread.Sleep(CHECKPOINT_INTERVAL*1000);
	//update the checkpointed position
	double min_error = 1.0;
	VivaldiState min_error_state = null;
	Hashtable ht = (Hashtable) _nc_service_table.Clone();
	foreach(NCService nc in ht.Values) {
	  VivaldiState vs = nc.State;
	  if (vs.WeightedError < min_error) {
	    min_error = vs.WeightedError;
	    min_error_state = vs;
	  }
	}

	if (min_error_state == null) {
	  Console.Error.WriteLine("No network coordinates to checkpoint, node instances: {0}, min_error: {1}", 
				  ht.Count, min_error);
	  continue;
	}
	
	lock(_nc_service_table) {
	  _checkpointed_position = min_error_state.Position;
	}
	
	try {
	  Console.Error.WriteLine("Checkpointing: {0}", min_error_state.Position);
	  TextWriter tw = new StreamWriter(_checkpoint_file);
          //also write the checkpoint time
          tw.WriteLine(DateTime.UtcNow.Ticks);
	  tw.WriteLine(min_error_state.Position);
	  tw.Close();
	} catch (Exception e) {
	  Console.Error.WriteLine(e);
	}
      } while (true);
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

      nc_service.ProcessSample(now + new TimeSpan(0, 0, 5), "local-test", addr_remote, 
			       new Point(new double[] {(double) 3.0, (double) 4.0}, 0),
			       (double) 0.9, (double)10.0); 

      nc_service.ProcessSample(now + new TimeSpan(0, 0, 6), "local-test",addr_remote1, 
			       new Point(new double[] {(double) 10.0, (double) 2.0}, 0),
			       (double) 0.9, (double)10.0); 


      nc_service.ProcessSample(now + new TimeSpan(0, 0, 7), "local-test",addr_remote, 
			       new Point(new double[] {(double) 3.0, (double) 4.0}, 0),
			       (double) 0.8, (double)12.0); 

      nc_service.ProcessSample(now + new TimeSpan(0, 0, 8), "local-test",addr_remote1, 
			       new Point(new double[] {(double) 10.0, (double) 2.0}, 0),
			       (double) 0.8, (double)12.0); 


      nc_service.ProcessSample(now + new TimeSpan(0, 0, 9), "local-test",addr_remote, 
			       new Point(new double[] {(double) 3.0, (double) 4.0}, 0),
			       (double)0.7, (double)13.0); 

      nc_service.ProcessSample(now + new TimeSpan(0, 0, 11), "local-test",addr_remote1, 
			       new Point(new double[] {(double) 10.0, (double) 2.0}, 0),
			       (double)0.7, (double)13.0); 


      nc_service.ProcessSample(now + new TimeSpan(0, 0, 12), "local-test",addr_remote, 
			       new Point(new double[] {(double) 3.0, (double) 4.0}, 0),
			       (double)0.6, (double)10.0); 

      nc_service.ProcessSample(now + new TimeSpan(0, 0, 13), "local-test",addr_remote1, 
			       new Point(new double[] {(double) 10.0, (double) 2.0}, 0),
			       (double)0.6, (double)10.0);       

      NCService.VivaldiState state = nc_service.State;
      Console.Error.WriteLine("position: {0}, error: {1}", state.Position, state.WeightedError);
    }

    [Test]
    public void TestSerialize() {
      NCService nc_service = new NCService();
      Hashtable ht1 = nc_service.EchoVivaldiState();
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
