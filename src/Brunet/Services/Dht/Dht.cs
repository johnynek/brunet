/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Diagnostics;
using System.Text;
using System.Collections;
using System.Security.Cryptography;
using System.Threading;

using Brunet;
using Brunet.Concurrent;
using Brunet.Util;
using Brunet.Messaging;
using Brunet.Symphony;
/**
\namespace Brunet.Services.Dht 
\brief Provides Distributed data storage services using the Brunet P2P
infrastructure.
*/
namespace Brunet.Services.Dht {
  /**
  <summary>Exception generated from the Dht should use this exception</summary>
  */
  public class DhtException: Exception {
    public DhtException(string message): base(message) {}
  }

  public class DhtPutException: DhtException {
    public readonly int Total;
    public readonly int Positive;
    public readonly int Negative;

    public DhtPutException(int total, int positive, int negative) :
      base(string.Format("Operation failed Total/Positive/Negative: {0}/{1}/{2}", total, positive, negative))
    {
      Total = total;
      Negative = negative;
      Positive = positive;
    }
  }

  /// <summary>This class provides a client interface to the dht, the servers only
  /// work together on a neighboring basis but not on a whole system basis.  It is
  /// up to the client to provide fault tolerance.  This class does it by naive
  /// replication.</summary>
  /// <remarks>This class implements the fault tolerant portion of the Dht by using
  /// naive replication as well as fixing holes in the dht.  A hole occurs when
  /// enough data results are received during a get to confirm existence, but not
  /// all the results returned a value.  In the case of a hole, a put will be used
  /// to place the data back to seal the hole.</remarks>
  public class Dht : IDht {
    /// <summary>The log enabler for the dht.</summary>
    public static BooleanSwitch DhtLog = new BooleanSwitch("Dht", "Log for Dht!");
    /// <summary>Lock for the dht put/get state tables.</summary>
    protected object _sync;
    /// <summary>The RpcManager to perform transactions through.</summary>
    protected RpcManager _rpc;
    /// <summary>The node to provide services for.</summary>
    public readonly Node Node;
    /// <summary>How many replications are made.</summary>
    public readonly int DEGREE;
    /// <summary>How long to wait for synchronous results.</summary>
    public readonly int DELAY;
    /// <summary>floor(DEGREE/2) + 1, the amount of positive results for a
    /// successful operation</summary>
    public readonly int MAJORITY;
    /// <summary>The state table for asynchronous puts.</summary>
    protected volatile Hashtable _adps_table = new Hashtable();
    /// <summary>The state table for asynchronos gets.</summary>
    protected volatile Hashtable _adgs_table = new Hashtable();
    /// <summary>True unless node is in offline, leaving, or disconnected</summary>
    protected bool _online;
    public bool Online { get { return _online; } }
    public string Name { get { return Node.Address.ToString(); } }

    /// <summary>A default Dht client provides a DEGREE of 1 and a sychronous wait
    /// time of up to 60 seconds.</summary>
    /// <param name ="node">The node to provide service for.</param>
    public Dht(Node node) {
      Node = node;
      _rpc = node.Rpc;
      _sync = new object();
      StateChangeHandler(Node, Node.ConState);
      Node.StateChangeEvent += StateChangeHandler;
      DEGREE = 1;
      MAJORITY = 1;
      DELAY = 60000;
    }

    /// <summary>Allows the user to specify the power of two of degrees to use.
    /// That is if degree=n, DEGREE for the dht is 2^n.</summary>
    /// <param name="node">The node to provide service for.</param>
    /// <param name="degree">n where DEGREE=2^n amount of replications to perform.
    /// </param>
    public Dht(Node node, int degree) : this(node) {
      this.DEGREE = (int) System.Math.Pow(2, degree);
      this.MAJORITY = (DEGREE / 2) + 1;
    }

    /// <summary>Allows the user to specify the power of two of degrees to use.
    /// That is if degree=n, DEGREE for the dht is 2^n.</summary>
    /// <param name="node">The node to provide service for.</param>
    /// <param name="degree">n where DEGREE=2^n amount of replications to
    /// perform.</param>
    /// <param name="delay">User specified delay for synchronous calls in
    /// seconds.</param>
    public Dht(Node node, int degree, int delay) : this(node, degree) {
      DELAY = delay * 1000;
    }

    /// <summary>We're online once we are seeking connections, we're offline
    /// once Disconnect has been called.</summary>
    protected void StateChangeHandler(Node n, Node.ConnectionState state) {
      lock(_sync) {
        if(state == Node.ConnectionState.Leaving ||
            state == Node.ConnectionState.Disconnected ||
            state == Node.ConnectionState.Offline) {
          _online = false;
        } else {
          _online = true;
        }
      }
    }


    public void AsyncCreate(MemBlock key, MemBlock value, int ttl, Channel returns) {
      AsyncPut(key, value, ttl, returns, true);
    }

    public bool Create(MemBlock key, MemBlock value, int ttl) {
      return Put(key, value, ttl, true);
    }

    public Hashtable[] Get(MemBlock key) {
      BlockingQueue returns = new BlockingQueue();
      AsyncGet(key, returns);
      ArrayList values = new ArrayList();
      while(true) {
        // Still a chance for Dequeue to execute on an empty closed queue 
        // so we'll do this instead.
        try {
          Hashtable hash = (Hashtable) returns.Dequeue();
          values.Add(hash);
        }
        catch (Exception) {
          break;
        }
      }
      return (Hashtable[]) values.ToArray(typeof(Hashtable));
    }

    /// <remarks>This starts the get process by sending dht.Get to all the remote
    /// end points that contain the key we're looking up.  The next step is
    /// is when the results are placed in the channel and GetEnqueueHandler is
    /// called or GetCloseHandler is called.  This means the get needs to be
    /// stateful, that information is stored in the _adgs_table.</remarks>
    public void AsyncGet(MemBlock key, Channel returns) {
      if(!_online) {
        throw new DhtException("The Node is (going) offline, DHT is offline.");
      }
      // create a GetState and map in our table map its queues to it
      // so when we get a GetHandler we know which state to load
      AsDhtGetState adgs = new AsDhtGetState(returns);
      Channel[] q = new Channel[DEGREE];
      lock(_adgs_table.SyncRoot) {
        for (int k = 0; k < DEGREE; k++) {
          Channel queue = new Channel(1);
          _adgs_table[queue] = adgs;
          q[k] = queue;
        }
      }

      // Setting up our Channels
      for (int k = 0; k < DEGREE; k++) {
        Channel queue = q[k];
        queue.EnqueueEvent += this.GetEnqueueHandler;
        queue.CloseEvent += this.GetCloseHandler;
        adgs.queueMapping[queue] = k;
      }

      // Sending off the request!
      adgs.brunet_address_for_key = MapToRing(key);
      for (int k = 0; k < DEGREE; k++) {
        Address target = new AHAddress(adgs.brunet_address_for_key[k]);
        AHSender s = new AHGreedySender(Node, target);
        // 1024 is in there for backwards compatibility
        _rpc.Invoke(s, q[k], "dht.Get", adgs.brunet_address_for_key[k], 1024, null);
      }
    }

    /// <summary>This is called as a result of a successful retrieval of data
    /// from a remote end point and performs follow up gets for remaining values
    /// </summary>
    /// <remarks>This adds the results to the entry in the _adgs_table.  Once a
    /// value has been received by a majority of nodes, it is enqueued into the
    /// requestors returns channel.  If not all results were retrieved follow up
    /// gets are performed, this is determined by looking at the state of the
    /// token, a non-null token implies there are remaining results.</remarks>
    /// <param name="o">The channel used to store the results.</param>
    /// <param name="args">Unused.</param>
    public void GetEnqueueHandler(Object o, EventArgs args) {
      Channel queue = (Channel) o;
      // Looking up state
      AsDhtGetState adgs = (AsDhtGetState) _adgs_table[queue];

      if(adgs == null) {
        return;
      }

      int idx = (int) adgs.queueMapping[queue];
      // Test to see if we got any results and place them into results if necessary
      ISender sendto = null;
      MemBlock token = null;
      try {
        RpcResult rpc_reply = (RpcResult) queue.Dequeue();
        ArrayList result = (ArrayList) rpc_reply.Result;
        //Result may be corrupted
        if(result == null) {
          throw new Exception("Invalid result");
        }
        ArrayList values = (ArrayList) result[0];
        int remaining = (int) result[1];
        if(remaining > 0) {
          token = MemBlock.Reference((byte[]) result[2]);
          sendto = rpc_reply.ResultSender;
        }

        // Going through the return values and adding them to our
        // results, if a majority of our servers say a data exists
        // we say it is a valid data and return it to the caller
        foreach (Hashtable ht in values) {
          MemBlock mbVal = MemBlock.Reference((byte[]) ht["value"]);
          int count = 1;
          Hashtable res = null;
          lock(adgs.SyncRoot) {
            res = (Hashtable) adgs.results[mbVal];
            if(res == null) {
              res = new Hashtable();
              adgs.results[mbVal] = res;
              adgs.ttls[mbVal] = ht["ttl"];
            }
            else {
              adgs.ttls[mbVal] = (int) adgs.ttls[mbVal] + (int) ht["ttl"];
            }

            res[idx] = true;
            count = ((ICollection) adgs.results[mbVal]).Count;
          }
          if(count == MAJORITY) {
            ht["ttl"] = (int) adgs.ttls[mbVal] / MAJORITY;
            adgs.returns.Enqueue(ht);
          }
        }
      }
      catch (Exception) {
        sendto = null;
        token = null;
      }

    // We were notified that more results were available!  Let's go get them!
      if(token != null && sendto != null) {
        Channel new_queue = new Channel(1);
        lock(adgs.SyncRoot) {
          adgs.queueMapping[new_queue] = idx;
        }
        lock(_adgs_table.SyncRoot) {
          _adgs_table[new_queue] = adgs;
        }
        new_queue.EnqueueEvent += this.GetEnqueueHandler;
        new_queue.CloseEvent += this.GetCloseHandler;
        try {
          _rpc.Invoke(sendto, new_queue, "dht.Get", 
                    adgs.brunet_address_for_key[idx], token);
        }
        catch(Exception) {
          lock(adgs.SyncRoot) {
            adgs.queueMapping.Remove(new_queue);
          }
          lock(_adgs_table.SyncRoot) {
            _adgs_table.Remove(new_queue);
          }
          new_queue.EnqueueEvent -= this.GetEnqueueHandler;
          new_queue.CloseEvent -= this.GetCloseHandler;
        }
      }
    }

    /// <summary>This is called by the Get callbacks when all the results for a
    /// get have come in.  This looks at the results, finds holes, and does a
    /// follow up put to place the data back into the dht via GetFollowUp.
    /// </summary>
    /// <param name="o">The channel representing a specific get.</param>
    /// <param name="args">Unused.</param>
    protected void GetCloseHandler(object o, EventArgs args) {
      Channel queue = (Channel) o;
      queue.EnqueueEvent -= this.GetEnqueueHandler;
      queue.CloseEvent -= this.GetCloseHandler;
      // Looking up state
      AsDhtGetState adgs = (AsDhtGetState) _adgs_table[queue];

      if(adgs == null) {
        return;
      }

      int count = 0;
      lock(adgs.SyncRoot) {
        adgs.queueMapping.Remove(queue);
        count = adgs.queueMapping.Count;
      }
      lock(_adgs_table.SyncRoot) {
        _adgs_table.Remove(queue);
      }
      if(count == 0) {
        adgs.returns.Close();
        GetFollowUp(adgs);
      }
      else if(count < MAJORITY && !adgs.GotToLeaveEarly) {
        lock(adgs.SyncRoot) {
          if(!adgs.GotToLeaveEarly) {
            GetLeaveEarly(adgs);
          }
        }
      }
    }

    /// <summary>This helps us leave the Get early if we either have no results or
    /// our remaining results will not reach a majority due to too many nodes
    /// missing data.  This closes the clients returns queue.</summary>
    /// <param name="adgs">The AsDhtGetState to qualify for leaving early</param>
    protected void GetLeaveEarly(AsDhtGetState adgs) {
      int left = adgs.queueMapping.Count;
      // Maybe we can leave early
      bool got_all_values = true;
      foreach (DictionaryEntry de in adgs.results) {
        int val = ((Hashtable) de.Value).Count;
        if(val < MAJORITY && ((val + left) >= MAJORITY)) {
          got_all_values = false;
          break;
        }
      }

      // If we got to leave early, we must clean up
      if(got_all_values) {
        if(Dht.DhtLog.Enabled) {
          ProtocolLog.Write(Dht.DhtLog, String.Format(
            "GetLeaveEarly found:left:total = {0}:{1}:{2}", 
            adgs.results.Count, left, DEGREE));
        }
        adgs.returns.Close();
        adgs.GotToLeaveEarly = true;
      }
    }

    /// <summary>Restores any of the Dht results that don't return all their
    /// values.  We only get here at the end of a Dht return operation.</summary>
    /// <remarks>This analyzes the holes and fills them in individually.  This only
    /// fills holes where there was a positive result (MAJORITY of results
    /// received).</remarks>
    /// <param name="adgs">The AsDhtGetState to analyze for follow up.</param>
    protected void GetFollowUp(AsDhtGetState adgs) {
      foreach (DictionaryEntry de in adgs.results) {
        if(de.Value == null || de.Key == null) {
          continue;
        }

        Hashtable res = (Hashtable) de.Value;
        if(res.Count < MAJORITY || res.Count == DEGREE) {
          if(res.Count < MAJORITY) {
            if(Dht.DhtLog.Enabled) {
              ProtocolLog.Write(Dht.DhtLog, String.Format(
                "Failed get count:total = {0}:{1}", res.Count, DEGREE));
            }
          }
          res.Clear();
          continue;
        }
        MemBlock value = (MemBlock) de.Key;

        int ttl = (int) adgs.ttls[value] / res.Count;
        if(Dht.DhtLog.Enabled) {
          ProtocolLog.Write(Dht.DhtLog, String.Format(
            "Doing follow up put count:total = {0}:{1}", res.Count, DEGREE));
        }
        for(int i = 0; i < DEGREE; i++) {
          if(!res.Contains(i)) {
            MemBlock key = adgs.brunet_address_for_key[i];
            Channel queue = new Channel();
            Address target = new AHAddress(key);
            AHSender s = new AHGreedySender(Node, target);
            try {
             _rpc.Invoke(s, queue, "dht.Put", key, value, ttl, false);
            }
            catch(Exception) {}
          }
        }
        res.Clear();
      }
      adgs.ttls.Clear();
      adgs.results.Clear();
    }

    public void AsyncPut(MemBlock key, MemBlock value, int ttl, Channel returns) {
      AsyncPut(key, value, ttl, returns, false);
    }

    public bool Put(MemBlock key, MemBlock value, int ttl) {
      return Put(key, value, ttl, false);
    }

    /// <summary>This is the sychronous version of the generic Put used by both
    /// the Put and Create methods.  The use of the unique variable
    /// differentiates the two.  Returns true if successful or an exception if
    /// there are network errors in adding the entry, creates also fail if a
    /// previous entry exists.</summary>
    /// <param name="key">The index to store the value at.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="ttl">The dht lease time for the key:value pair.</param>
    /// <param name="unique">True to do a create, false otherwise.</param>
    /// <returns>True if success, exception on fail</returns>
    public bool Put(MemBlock key, MemBlock value, int ttl, bool unique) {
      BlockingQueue returns = new BlockingQueue();
      AsyncPut(key, value, ttl, returns, unique);
      object result = returns.Dequeue();
      try {
        return (bool) result;
      }
      catch {
        throw (Exception) result;
      }
    }


    /// <summary>This is the generic Put that is used by both the regular Put
    /// and Create methods.  The use of the unique variable differentiates the
    /// two.  This is asynchronous.  Results are stored in the Channel returns.
    /// Creates and Puts return true if successful or exception if there are
    /// network errors in adding the entry, creates also fail if a previous
    /// entry exists.  The work of determining success is handled in
    /// PutEnqueueHandler and PutCloseHandler.</summary>
    /// <param name="key">The index to store the value at.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="ttl">The dht lease time for the key:value pair.</param>
    /// <param name="returns">The Channel where the result will be placed.</param>
    /// <param name="unique">True to do a create, false otherwise.</param>
    public void AsyncPut(MemBlock key, MemBlock value, int ttl, Channel returns, bool unique) {
      if(!_online) {
        throw new DhtException("The Node is (going) offline, DHT is offline.");
      }
      AsDhtPutState adps = new AsDhtPutState(returns);

      MemBlock[] brunet_address_for_key = MapToRing(key);
      Channel[] q = new Channel[DEGREE];
      lock(_adps_table.SyncRoot) {
        for (int k = 0; k < DEGREE; k++) {
          Channel queue = new Channel(1);
          queue.CloseEvent += this.PutCloseHandler;
          _adps_table[queue] = adps;
          q[k] = queue;
        }
      }

      for (int k = 0; k < DEGREE; k++) {
        Address target = new AHAddress(brunet_address_for_key[k]);
        AHSender s = new AHGreedySender(Node, target);
        _rpc.Invoke(s, q[k], "dht.Put", brunet_address_for_key[k], value, ttl, unique);
      }
    }

    /// <summary>Uses the channel to determine which Put this is processing.
    /// Returns true if we've received a MAJORITY of votes or an exception if a
    /// enough negative results come in.  The returns are enqueued to the users
    /// returns Channel.<summary>
    /// <param name="o">The channel used by put.</param>
    /// <param name="args">Unused.</param>
    public void PutCloseHandler(Object o, EventArgs args) {
      Channel queue = (Channel) o;
      // Get our mapping
      AsDhtPutState adps = (AsDhtPutState) _adps_table[queue];
      if(adps == null) {
        return;
      }

      lock(_adps_table.SyncRoot) {
        _adps_table.Remove(queue);
      }

      /* Check out results from our request and update the overall results
      * send a message to our client if we're done!
      */
      bool result = false;
      try {
        RpcResult rpcResult = (RpcResult) queue.Dequeue();
        result = (bool) rpcResult.Result;
      }
      catch (Exception) {}

      if(result) {
        // Once we get pcount to a majority, we ship off the result
        if(Interlocked.Increment(ref adps.pcount) == MAJORITY) {
          adps.returns.Enqueue(true);
          adps.returns.Close();
        }
      }
      else {
        // Once we get to ncount to 1 less than a majority, we ship off the
        // result, because we can't get pcount equal to majority any more!
        if(Interlocked.Increment(ref adps.ncount) == MAJORITY - 1 || 1 == DEGREE) {
          adps.returns.Enqueue(new DhtPutException(DEGREE, adps.pcount, adps.ncount));
          adps.returns.Close();
        }
      }
    }

    /// <summary>Get the hash of the first key and add 1/DEGREE * Address space
    /// to each successive key.  The results are the positions in the ring where
    /// the data should be stored.</summary>
    /// <param name="key">The key to index.</param>
    public MemBlock[] MapToRing(byte[] key) {
      MemBlock[] targets = new MemBlock[DEGREE];
      // Setup the first key
      HashAlgorithm algo = new SHA1CryptoServiceProvider();
      byte[] target = algo.ComputeHash(key);
      Address.SetClass(target, AHAddress._class);
      targets[0] = MemBlock.Reference(target, 0, Address.MemSize);

      // Setup the rest of the keys
      BigInteger inc_addr = Address.Full/DEGREE;
      BigInteger curr_addr = new BigInteger(targets[0]);
      for (int k = 1; k < targets.Length; k++) {
        curr_addr = curr_addr + inc_addr;
        target = Address.ConvertToAddressBuffer(curr_addr);
        Address.SetClass(target, AHAddress._class);
        targets[k] = target;
      }
      return targets;
    }

    /// <summary>Stores the state used for asynchronous puts.</summary>
    protected class AsDhtPutState {
      public object SyncRoot = new object();
      public int pcount = 0, ncount = 0;
      public Channel returns;

      public AsDhtPutState(Channel returns) {
        this.returns = returns;
      }
    }

    /// <summary>Stores the state used for asynchronous gets.</summary>
    protected class AsDhtGetState {
      public object SyncRoot = new object();
      public bool GotToLeaveEarly = false;
      public Hashtable ttls = new Hashtable();
      public Hashtable queueMapping = new Hashtable();
      public Hashtable results = new Hashtable();
      public Channel returns;
      public MemBlock[] brunet_address_for_key;

      public AsDhtGetState(Channel returns) {
        this.returns = returns;
      }
    }
  }
}
