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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Collections;
using System.Collections.Specialized;
using System.Threading;
using Brunet;
using Brunet.Concurrent;
using Brunet.Connections;
using Brunet.Util;
using Brunet.Transport;

#if NC_NUNIT
using System.Security.Cryptography;
using NUnit.Framework;
#endif

using Brunet.Messaging;
using Brunet.Symphony;
namespace Brunet.Services.Coordinate {

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
        //Console.Error.WriteLine("[NCService] {0} EchoVivaldiState() method invoked.", _node.Address);
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
        //Console.Error.WriteLine("[NCService] {0} EchoVivaldiState() returning.", _node.Address);
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
     * state of out connections. We record the state of an active trial in this
     * data structure. 
     */ 
    public class TrialState {
      public static readonly int MIN_LATENCY_SAMPLES = 1;
      protected static readonly Random _rand = new Random();
      public delegate void TrialFinishedCallback(TrialState trial, DateTime instant, Address target_address, 
                                                 Hashtable state_result,
                                                 double latency); 
      //local node
      protected readonly Node _node;
      //target address
      protected readonly Address _target_address;
      //edge over which we do the sampling
      protected readonly Edge _target_edge;
      //vivaldi state as retrieved from the remote node
      protected WriteOnce<Hashtable> _state_result;
      //callback function when finished
      TrialFinishedCallback _callback;
      //number of samples taken in this trial
      protected int _num_samples;
      protected int _started;

      public TrialState(Node n, TrialState.TrialFinishedCallback cb) {
        _node = n;
        _callback = cb;
        ArrayList con_list = new ArrayList();
        foreach (Connection con in _node.ConnectionTable.GetConnections(ConnectionType.Structured))
        {
          if (con.Edge is Tunnel.TunnelEdge) {
            continue;
          }
          con_list.Add(con);
        }

        if (con_list.Count == 0) {
          throw new Exception("Cannot initialize a trial state (No usable structured connections).");
        }
        
        Connection target = (Connection) con_list[_rand.Next(con_list.Count)];
        _target_address = target.Address;
        _target_edge = target.Edge;
        _state_result = new WriteOnce<Hashtable>();
        _num_samples = 0;
        _started = 0;
      }
      
      /* Start the current trial.*/
      public void Start() {
        int already_started = Interlocked.CompareExchange(ref _started, 1, 0);
        if (already_started == 1) {
          return;
        }
#if NC_DEBUG        
        Console.Error.WriteLine("[NCService] {0} Starting trial to: {1}.", _node.Address, _target_address);
#endif
        //retrieve the vivaldi state
        GetVivaldiState(); 
      }
        
      /** 
       * Step 1. Get vivaldi state for the current trial.
       * @param state information associated with the node we are going to sample. 
       */
      protected void GetVivaldiState() {
#if NC_DEBUG        
        Console.Error.WriteLine("[NCService] {0} Requesting vivaldi state from: {1}.", _node.Address, _target_address);
#endif
        Channel q = new Channel(1);
        q.CloseEvent += delegate(object o, EventArgs args) {
          Channel nc_q = (Channel) o;
          if (nc_q.Count > 0) {
            try {
              RpcResult result = nc_q.Dequeue() as RpcResult;
              _state_result.Value =  (Hashtable) result.Result;
            } catch(Exception) {}
          }
          
          //if got  something valid, move on to Step 2.
          if (_state_result.Value != null) {
            GetLatencySample();
          } 
          else {
            //do nothing, looks like things are not going well with this trial
            //termination
#if NC_DEBUG        
            Console.Error.WriteLine("[NCService] {0} Rpc (GetVivaldiState) failed: {1}.", _node.Address, _target_address);
#endif
          }
        };
        
        try {
          _node.Rpc.Invoke(_target_edge, q, "ncserver.EchoVivaldiState", new object[]{});
        } catch(Exception) {}
      }
      
      

      /** 
       * Step 2. Get latency sample for the current trial.
       * @param state information associated with the node we are going to sample. 
       */
      protected void GetLatencySample() {
#if NC_DEBUG        
        Console.Error.WriteLine("[NCService] {0} Requesting latency sample from: {1}.", _node.Address, _target_address);
#endif
        DateTime start = DateTime.UtcNow;
        Channel q = new Channel(1);
        q.CloseEvent += delegate(object o, EventArgs args) {
          Channel res_q = (Channel) o;
          DateTime stop = DateTime.UtcNow;
          bool do_consider = false;
          if (res_q.Count > 0 ) {
            try {
              RpcResult res = res_q.Dequeue() as RpcResult;
              if (res.Statistics.SendCount == 1) {
                do_consider = true;
              }
              else {
#if NC_DEBUG
                Console.Error.WriteLine("[NCService] {0} multiple sends for: {1}.", _node.Address, _target_address);
#endif
              }
            } catch(Exception) {}
          }
          
          if (do_consider) {
            double elapsed = (stop - start).TotalMilliseconds;
            int count = Interlocked.Increment(ref _num_samples);
#if NC_DEBUG        
            Console.Error.WriteLine("[NCService] {0} Got latency sample from: {1}, sample #: {2}.", 
                                    _node.Address, 
                                    _target_address,
                                    count);
#endif
            
            if (count >= TrialState.MIN_LATENCY_SAMPLES) {
              _callback(this, DateTime.UtcNow, _target_address, _state_result.Value, elapsed);
              //termination
            }
            else {
              // take another latency sample
              GetLatencySample();
            }
          }
          else {
            //do nothing, looks like things are not going well with this trial
            //termination
#if NC_DEBUG        
            Console.Error.WriteLine("[NCService] {0} Rpc (GetLatencySample) failed: {1}.", _node.Address, _target_address);
#endif
          }
        };
        
        try {
          _node.Rpc.Invoke(_target_edge, q, "ncserver.Echo", new object[]{});
        } catch (Exception) {}
      }
    }
    
    protected TrialState _current_trial = null;
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

    //every 10 seconds get a new sample for latency.
    protected static readonly int SAMPLE_INTERVAL = 10;

    //samples not valid beyond 1800 seconds
    protected static readonly long SAMPLE_EXPIRATION = 1800;
    
    //maximum latency value to consider a sample 
    //protected static readonly double MAX_RTT = 5.0;
    
    protected object _sync;
    protected Node _node;
    protected RpcManager _rpc;
    protected DateTime _last_sample_instant;

    /** Vivaldi related stuff. */
    protected static readonly double DAMPENING_FRACTION = 0.25f;
    protected static readonly double ERROR_FRACTION = 0.25f;
    protected static readonly double INITIAL_WEIGHTED_ERROR = 1.0f;
    
    //latency samples from neighbors
    protected Hashtable _samples;

    /** 
     * Installs the network coordinate service on a given node. 
     * NCService instance can be installed on atmost one node. 
     * Each node is allowed to have only one NCService instance. 
     * @param node node for installing the service instance. 
     * @param InitialPoint a starting place for the NCService to use
     */
    public NCService(Node node, Point InitialPoint) {
      _sync = new object();
      _node = null;
      _last_sample_instant = DateTime.MinValue;
      _samples = new Hashtable();
      _vivaldi_state = new VivaldiState();
      _vivaldi_state.WeightedError = INITIAL_WEIGHTED_ERROR;
      _vivaldi_state.Position = new Point();
      _vivaldi_state.DistanceDelta = 0.0f;

      if(InitialPoint == null) {
        InitialPoint = new Point();
      }
      _vivaldi_state.Position = InitialPoint;

      if(node != null) {
        _node = node;

#if NC_DEBUG
        Console.Error.WriteLine("[NCService] {0} Starting an instance of NCService.", node.Address);
#endif 

        lock(_sync) {
          _rpc = _node.Rpc;
          _rpc.AddHandler("ncserver", this);
          _node.HeartBeatEvent += GetNextSample;
        }
      }
    }

    public NCService(Node node, string InitialPoint):
      this(node, (InitialPoint == null ? null : new Point(InitialPoint)))
    {
    }

    public NCService(Node node):
      this(node, (Point) null)
    {
    }

    /// <summary>For testing purposes only</summary>
    public NCService():
      this(null, (Point) null)
    {
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
      DateTime now = DateTime.UtcNow;
      lock(_sync) {
        TimeSpan elapsed = now - _last_sample_instant;
        //
        // Check if it is too early to get a sample. 
        //
        if (elapsed.TotalSeconds < SAMPLE_INTERVAL) {
          return;
        }
        _last_sample_instant = now;
      }

      try {
        TrialState new_trial = new TrialState(_node, TrialComplete); 
        _current_trial = new_trial; 
        new_trial.Start();  
       /** The trial object will not get garbage collected while it is active. It will be registered
        *  with the queues during the course of its activity. 
        */
      } catch(Exception
#if NC_DEBUG
                        x) { //This silliness is so the compiler doesn't complain about x being unsed
        Console.Error.WriteLine("[NCService] {0}, {1}", _node.Address, x.Message);
#else 
                         ) {
#endif        
      }
    }
    
    protected void TrialComplete(TrialState trial, DateTime instant, Address target_address, 
                                 Hashtable state_result, double latency) {
#if NC_DEBUG
      Console.Error.WriteLine("[NCService] {0} Trial complete to: {1}.", _node.Address, target_address);
#endif
      if (_current_trial != trial) {
#if NC_DEBUG
        Console.Error.WriteLine("[NCService] {0} Too late now: {1}.", _node.Address, target_address);
#endif
        return;
      }
      
      Hashtable ht_position = (Hashtable) state_result["position"];
      Point position = 
        new Point((double[]) ((ArrayList) ht_position["side"]).ToArray(typeof(double)), (double) ht_position["height"]);
      double weighted_error = (double) state_result["error"];
      string host = (string) state_result["hostname"];
#if NC_DEBUG
      Console.Error.WriteLine("[NCService] {0} Trial result at: {1}, from: {2} {3}, position: {4}, error: {5}, latency: {6}", 
                              _node.Address, instant.Ticks, host, target_address, position, weighted_error, latency);
#endif
      ProcessSample(instant, host, target_address, position, weighted_error, latency);
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

#if NC_DEBUG
  Console.Error.WriteLine("Sample at: {0}, from: {1} {2}, position: {3}, error: {4}, raw latency: {5}, smooth latency: {6}", 
        o_stamp, o_host, o_neighbor, o_position, o_weightedError, o_rawLatency, o_latency);
#endif
  double o_distance = _vivaldi_state.Position.GetEucledianDistance(o_position);
  while (o_distance == 0) {
    _vivaldi_state.Position.Bump();
    o_distance = _vivaldi_state.Position.GetEucledianDistance(o_position);
  }

#if NC_DEBUG
  Console.Error.WriteLine("Current position: {0}, distance: {1}", _vivaldi_state.Position, o_distance);
#endif
  double o_relativeError = Math.Abs((o_distance - o_latency)/o_latency);
  //double o_rawRelativeError = Math.Abs((o_distance - o_rawLatency)/o_rawLatency);
  double o_weight = _vivaldi_state.WeightedError/(_vivaldi_state.WeightedError + o_weightedError);
  double o_alphaWeightedError = ERROR_FRACTION * o_weight;
      
#if NC_DEBUG
  Console.Error.WriteLine("o_distance: {0}", o_distance);
  Console.Error.WriteLine("o_latency: {0}", o_latency);
  Console.Error.WriteLine("o_relativeError (epsi): {0}", o_relativeError);
  Console.Error.WriteLine("o_weight (w_s): {0}", o_weight);
  Console.Error.WriteLine("my_weighted_error (preupdate)): {0}", _vivaldi_state.WeightedError);
  Console.Error.WriteLine("alpha: {0}", o_alphaWeightedError);
#endif

  _vivaldi_state.WeightedError = (o_relativeError* o_alphaWeightedError) + 
    _vivaldi_state.WeightedError*(1 - o_alphaWeightedError);

#if NC_DEBUG
  Console.Error.WriteLine("my_weighted_error (postupdate)): {0}", _vivaldi_state.WeightedError);
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

#if NC_DEBUG
  Console.Error.WriteLine("Updated position: {0}, distance: {1}", 
        _vivaldi_state.Position,  
        _vivaldi_state.Position.GetEucledianDistance(o_position));
#endif
      }//end of lock
    }
    
    /**
     * Checkpoint vivaldi coordinates from a file. 
     */
    public string GetCheckpoint() {
      return State.Position.ToString();
    }
  }

#if NC_NUNIT
  [TestFixture]
  public class NCTester {  
    [Test]
    public void TestSample() {
      Sample sample = new Sample();
      Assert.IsTrue(sample.GetSample() < 0.0);
      sample.AddSample(DateTime.UtcNow, (double) 100.0, new Point(), (double) 0.001);
      sample.AddSample(DateTime.UtcNow, (double) 100.0, new Point(), (double) 0.002);
      sample.AddSample(DateTime.UtcNow, (double) 100.0, new Point(), (double) 0.003);
      sample.AddSample(DateTime.UtcNow, (double) 100.0, new Point(), (double) 0.004);
      Assert.IsTrue(sample.GetSample() > 0.0);

    }
    [Test]
    public void TestPoint() {
      Point p1 = new Point(new double[] {(double) 3.0, (double) 4.0}, 0);
      Assert.IsTrue(p1.Length() == 5.0);
      Point p2 = new Point(new double[] {(double) 6.0, (double) 8.0}, 0);
      double d = p1.GetEucledianDistance(p2);
      Assert.IsTrue(d == 5.0);
      Point uv = p1.GetDirection(p2);
      Assert.IsTrue(uv.Length() == 1.0);
      p1.Add(p2);
      Assert.IsTrue(p1.Side[0] == 9.0);
      Assert.IsTrue(p1.Side[1] == 12.0);

      p2.Scale((double) 0.5);
      Assert.IsTrue(p2.Side[0] == 3.0);
      Assert.IsTrue(p2.Side[1] == 4.0);

      Point p = new Point(p2.Side, p2.Height);
      Assert.IsTrue(p.Equals(p2));

      p2.Scale((double)2.0);
      Assert.IsTrue(!p.Equals(p2));
    }

    [Test]
    public void TestService() {
      NCService nc_service = new NCService();
      DateTime now = DateTime.UtcNow;
      Address addr_remote = new AHAddress(new RNGCryptoServiceProvider());
      Address addr_remote1 = new AHAddress(new RNGCryptoServiceProvider());
      Address addr_remote2 = new AHAddress(new RNGCryptoServiceProvider());

      nc_service.ProcessSample(now + new TimeSpan(0, 0, 5), "local-test", addr_remote, 
             new Point(new double[] {(double) 3.0, (double) 4.0}, 0),
             (double) 0.9, (double)10.0); 
      NCService.VivaldiState state = nc_service.State;
      

      nc_service.ProcessSample(now + new TimeSpan(0, 0, 6), "local-test",addr_remote1, 
             new Point(new double[] {(double) 10.0, (double) 2.0}, 0),
             (double) 0.9, (double)10.0); 

      nc_service.ProcessSample(now + new TimeSpan(0, 0, 6), "local-test",addr_remote2, 
             new Point(new double[] {(double) 5.0, (double) 6.0}, 0),
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

      state = nc_service.State;
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
      Assert.AreEqual(o_weightedError, state.WeightedError);
    }
  }
#endif
}
