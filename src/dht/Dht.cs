using System;
using System.Text;
using System.Collections;
using System.Security.Cryptography;
using System.Threading;

using Brunet;

namespace Brunet.Dht {	
  public class DhtException: Exception {
    public DhtException(string message): base(message) {}
  }

  public class Dht {
    //lock for the Dht
    protected object _sync = new object();
    private RpcManager _rpc;
    public Node _node = null;
    public bool Activated { get { return _table.Activated; } }
    public bool debug { 
      get { return _table.debug ; }
      set { _table.debug = value; }
    }
    public readonly int DEGREE;
    public readonly int DELAY;
    public readonly int MAJORITY;
    public static readonly int MAX_BYTES = 1000;

    //table server
    protected TableServer _table;
    public int Count { get { return _table.Count; } }
    //Dht Get / Put States
    private volatile Hashtable _adps_table = new Hashtable();
    private volatile Hashtable _adgs_table = new Hashtable();

    //keep track of our current neighbors, we start with none
    protected AHAddress _left_addr = null, _right_addr = null;

    public Dht(Node node) {
      _node = node;
      //get an instance of RpcManager for the node
      _rpc = RpcManager.GetInstance(node);
      _table = new TableServer(node, _rpc);
      //register the table with the RpcManagers
      _rpc.AddHandler("dht", _table);
      // We default into a single pair of nodes for each data point
      DEGREE = 1;
      MAJORITY = 1;
      // 60 second delay for blocking calls
      DELAY = 60000;
    }

    public Dht(Node node, int degree) :
      this(node){
      this.DEGREE = (int) System.Math.Pow(2, degree);
      this.MAJORITY = DEGREE / 2 + 1;
    }

    // Delay from users point of view is in seconds
    public Dht(Node node, int degree, int delay) :
      this(node, degree){
      DELAY = delay * 1000;
    }

    public BlockingQueue[] PrimitivePut(MemBlock key, int ttl, MemBlock data) {
      return PrimitivePut(key, ttl, data, false);
    }

    public BlockingQueue[] PrimitiveCreate(MemBlock key, int ttl, MemBlock data) {
      return PrimitivePut(key, ttl, data, true);
    }

    public BlockingQueue[] PrimitivePut(MemBlock key, int ttl, MemBlock data, bool unique) {
      if (!Activated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }

      BlockingQueue[] q = new BlockingQueue[DEGREE];
      MemBlock[] b = MapToRing(key);

      for(int i = 0; i < DEGREE; i++) {
        Address target = new AHAddress(b[i]);
        AHSender s = new AHSender(_rpc.Node, target);
        q[i] = new BlockingQueue();
        _rpc.Invoke(s, q[i], "dht.Put", b[i], data, ttl, unique);
      }
      return q;
    }

    public BlockingQueue[] PrimitiveGet(MemBlock key, int maxbytes, MemBlock token) {
      if (!Activated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }

      BlockingQueue[] q = new BlockingQueue[DEGREE];
      MemBlock[] b = MapToRing(key);

      for(int i = 0; i < DEGREE; i++) {
        Address target = new AHAddress(b[i]);
        AHSender s = new AHSender(_rpc.Node, target);
        q[i] = new BlockingQueue();
        _rpc.Invoke(s, q[i], "dht.Get", b[i], maxbytes, token);
      }
      return q;
    }

    /* Below are all the Create methods, they rely on a unique put   *
     * this returns true if it succeeded or an exception if it didn't */

    public void AsCreate(MemBlock key, MemBlock value, int ttl, BlockingQueue returns) {
      AsPut(key, value, ttl, returns, true);
    }

    public void AsCreate(string key, MemBlock value, int ttl, BlockingQueue returns) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      AsCreate(keyb, value, ttl, returns);
    }

    public void AsCreate(string key, string value, int ttl, BlockingQueue returns) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      MemBlock valueb = MemBlock.Reference(Encoding.UTF8.GetBytes(value));
      AsCreate(keyb, valueb, ttl, returns);
    }

    public bool Create(MemBlock key, MemBlock value, int ttl) {
      return Put(key, value, ttl, true);
    }

    public bool Create(string key, MemBlock value, int ttl) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      return Create(keyb, value, ttl);
    }

    public bool Create(string key, string value, int ttl) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      MemBlock valueb = MemBlock.Reference(Encoding.UTF8.GetBytes(value));
      return Create(keyb, valueb, ttl);
    }

    /* Below are all the Get methods */

    public void AsGet(string key, BlockingQueue returns) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      AsGet(keyb, returns);
    }

    public DhtGetResult[] Get(string key) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      return Get(keyb);
    }

    public DhtGetResult[] Get(MemBlock key) {
      BlockingQueue returns = new BlockingQueue();
      AsGet(key, returns);
      ArrayList allValues = new ArrayList();
      while(true) {
        // Still a chance for Dequeue to execute on an empty closed queue 
        // so we'll do this instead.
        try {
          DhtGetResult dgr = (DhtGetResult) returns.Dequeue();
          allValues.Add(dgr);
        }
        catch (Exception) {
          break;
        }
      }
      return (DhtGetResult []) allValues.ToArray(typeof(DhtGetResult));
    }

    /*  This is the get that does all the work, it is meant to be
     *   run as a thread */
    public void AsGet(MemBlock key, BlockingQueue returns) {
      if (!Activated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }

      // create a GetState and map in our table map its queues to it
      // so when we get a GetHandler we know which state to load
      AsDhtGetState adgs = new AsDhtGetState(returns);
      BlockingQueue[] q = new BlockingQueue[DEGREE];
      lock(_adgs_table) {
        for (int k = 0; k < DEGREE; k++) {
          BlockingQueue queue = new BlockingQueue();
          _adgs_table[queue] = adgs;
          q[k] = queue;
        }
      }

      // Setting up our BlockingQueues
      lock(adgs) {
        for (int k = 0; k < DEGREE; k++) {
          BlockingQueue queue = q[k];
          queue.EnqueueEvent += this.GetHandler;
          queue.CloseEvent += this.GetHandler;
          adgs.queueMapping[queue] = k;
        }
      }

      // Sending off the request!
      adgs.brunet_address_for_key = MapToRing(key);
      for (int k = 0; k < DEGREE; k++) {
        Address target = new AHAddress(adgs.brunet_address_for_key[k]);
        AHSender s = new AHSender(_rpc.Node, target, AHPacket.AHOptions.Greedy);
        _rpc.Invoke(s, q[k], "dht.Get", adgs.brunet_address_for_key[k], MAX_BYTES, null);
      }
    }

    /* Here we receive a BlockingQueue, use it to look up our state, process the results,
     * and update our state as necessary
     */

    public void GetHandler(Object o, EventArgs args) {
      BlockingQueue queue = (BlockingQueue) o;
      lock(queue) {
        // Looking up state
        AsDhtGetState adgs = (AsDhtGetState) _adgs_table[queue];

        if(adgs == null) {
          queue.Close();
          return;
        }

        // If we get here we either were closed by the remote rpc or we finished our get
        if(queue.Closed && queue.Count == 0) {
          int count = 0;
          lock(adgs.queueMapping) {
            adgs.queueMapping.Remove(queue);
            count = adgs.queueMapping.Count;
          }
          lock(_adgs_table) {
            _adgs_table.Remove(queue);
          }
          queue.EnqueueEvent -= this.GetHandler;
          queue.CloseEvent -= this.GetHandler;
          if(count == 0) {
            adgs.returns.Close();
            GetFollowUp(adgs);
          }
          else {
            if(count < MAJORITY && !(bool) adgs.GotToLeaveEarly) {
              lock(adgs.GotToLeaveEarly) {
                if(!(bool) adgs.GotToLeaveEarly) {
                  GetLeaveEarly(adgs);
                }
              }
            }
          }
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
          ArrayList values = (ArrayList) result[0];
          int remaining = (int) result[1];
          if(remaining > 0) {
            token = (byte[]) result[2];
            sendto = rpc_reply.ResultSender;
          }

          // Going through the return values and adding them to our
          // results, if a majority of our servers say a data exists
          // we say it is a valid data and return it to the caller
          foreach (Hashtable ht in values) {
            MemBlock mbVal = (byte[]) ht["value"];
            int count = 1;
            Hashtable res = null;
            lock(adgs.results) {
              res = (Hashtable) adgs.results[mbVal];
              if(res == null) {
                res = new Hashtable();
                adgs.results[mbVal] = res;
              }
              res[idx] = true;
              count = ((ICollection) adgs.results[mbVal]).Count;
            }
            if(count == MAJORITY) {
              adgs.returns.Enqueue(new DhtGetResult(ht));
              adgs.ttls[res] = ht["ttl"];
            }
          }
        }
        catch (Exception) {
          sendto = null;
          token = null;
        }

      // We were notified that more results were available!  Let's go get them!
        if(token != null && sendto != null) {
          BlockingQueue new_queue = new BlockingQueue();
          lock(adgs.queueMapping) {
            adgs.queueMapping[new_queue] = idx;
          }
          lock(_adgs_table) {
            _adgs_table[new_queue] = adgs;
          }
          new_queue.EnqueueEvent += this.GetHandler;
          new_queue.CloseEvent += this.GetHandler;
          _rpc.Invoke(sendto, new_queue, "dht.Get", 
                      adgs.brunet_address_for_key[idx], MAX_BYTES, token);
        }
      }
      queue.Close();
    }

    /* This helps us leave the Get early if we either have no results or
    * our remaining results will not reach a majority due to too many nodes
    * missing data
    */
    private void GetLeaveEarly(AsDhtGetState adgs) {
      int left = adgs.queueMapping.Count;
      // Maybe we can leave early
      bool got_all_values = true;
      lock(adgs.results) {
        foreach (DictionaryEntry de in adgs.results) {
          int val = ((Hashtable) de.Value).Count;
          if(val < MAJORITY && ((val + left) >= MAJORITY)) {
            got_all_values = false;
            break;
          }
        }
      }

      // If we got to leave early, we must clean up
      if(got_all_values) {
        adgs.returns.Close();
        adgs.GotToLeaveEarly = true;
      }
    }

    private void GetFollowUp(AsDhtGetState adgs) {
      foreach (DictionaryEntry de in adgs.results) {
        Hashtable res = (Hashtable) de.Value;
        if(res.Count < MAJORITY || res.Count == DEGREE) {
          res.Clear();
          continue;
        }
        MemBlock value = (MemBlock) de.Key;
        int ttl = (int) adgs.ttls[res];
        for(int i = 0; i < DEGREE; i++) {
          if(!res.Contains(i)) {
            MemBlock key = adgs.brunet_address_for_key[i];
            BlockingQueue queue = new BlockingQueue();
            Address target = new AHAddress(key);
            AHSender s = new AHSender(_rpc.Node, target, AHPacket.AHOptions.Greedy);
            _rpc.Invoke(s, queue, "dht.Put", key, value, ttl, false);
          }
        }
        res.Clear();
      }
      adgs.ttls.Clear();
      adgs.results.Clear();
    }

    /** Below are all the Put methods, they use a non-unique put */

    public void AsPut(MemBlock key, MemBlock value, int ttl, BlockingQueue returns) {
      AsPut(key, value, ttl, returns, false);
    }

    public void AsPut(string key, MemBlock value, int ttl, BlockingQueue returns) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      AsPut(keyb, value, ttl, returns);
    }

    public void AsPut(string key, string value, int ttl, BlockingQueue returns) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      MemBlock valueb = MemBlock.Reference(Encoding.UTF8.GetBytes(value));
      AsPut(keyb, valueb, ttl, returns);
    }

    public bool Put(MemBlock key, MemBlock value, int ttl) {
      return Put(key, value, ttl, false);
    }

    public bool Put(string key, MemBlock value, int ttl) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      return Put(keyb, value, ttl);
    }

    public bool Put(string key, string value, int ttl) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      MemBlock valueb = MemBlock.Reference(Encoding.UTF8.GetBytes(value));
      return Put(keyb, valueb, ttl);
    }

    /** Since the Puts and Creates are the same from the client side, we merge them into a
    single put that if unique is true, it is a create, otherwise a put */

    public bool Put(MemBlock key, MemBlock value, int ttl, bool unique) {
      BlockingQueue returns = new BlockingQueue();
      AsPut(key, value, ttl, returns, unique);
      return (bool) returns.Dequeue();
    }

    public void AsPut(MemBlock key, MemBlock value, int ttl, BlockingQueue returns, bool unique) {
      if (!Activated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }

      AsDhtPutState adps = new AsDhtPutState(returns);

      MemBlock[] brunet_address_for_key = MapToRing(key);
      BlockingQueue[] q = new BlockingQueue[DEGREE];
      lock(_adps_table) {
        for (int k = 0; k < DEGREE; k++) {
          BlockingQueue queue = new BlockingQueue();
          _adps_table[queue] = adps;
          q[k] = queue;
        }
      }

      lock(adps) {
        for (int k = 0; k < DEGREE; k++) {
          BlockingQueue queue = q[k];
          queue.EnqueueEvent += this.PutHandler;
          queue.CloseEvent += this.PutHandler;
          adps.queueMapping[queue] = k;
        }
      }

      for (int k = 0; k < DEGREE; k++) {
        Address target = new AHAddress(brunet_address_for_key[k]);
        AHSender s = new AHSender(_rpc.Node, target, AHPacket.AHOptions.Greedy);
        _rpc.Invoke(s, q[k], "dht.Put", brunet_address_for_key[k], value, ttl, unique);
      }
    }

    /* We receive an BlockingQueue use it to map to our state and update the 
     * necessary, we'll get this even after a user has received his value, so
     * that we can ensure all places in the ring actually get the data.  Should
     * timeout after 5 minutes though!
     */
    public void PutHandler(Object o, EventArgs args) {
      BlockingQueue queue = (BlockingQueue) o;
      lock(queue) {
        // Get our mapping
        AsDhtPutState adps = (AsDhtPutState) _adps_table[queue];
        if(adps == null) {
          queue.CloseEvent -= this.PutHandler;
          queue.EnqueueEvent -= this.PutHandler;
          queue.Close();
          return;
        }


        // Well it was closed, shouldn't have happened, but we'll do garbage collection
        if(queue.Closed) {
          queue.CloseEvent -= this.PutHandler;
          queue.EnqueueEvent -= this.PutHandler;
          lock(_adps_table) {
            _adps_table.Remove(queue);
          }
          int count = 0;
          lock(adps.queueMapping) {
            adps.queueMapping.Remove(queue);
            count = adps.queueMapping.Count;
          }
          if(count == 0) {
            adps.pcount = null;
            adps.ncount = null;
            if(!adps.returns.Closed) {
              adps.returns.Enqueue(false);
              adps.returns.Close();
            }
          }
          return;
        }

        /* Check out results from our request and update the overall results
        * send a message to our client if we're done!
        */
        bool timedout, result = false;
        try {
          RpcResult rpcResult = (RpcResult) queue.Dequeue(0, out timedout);
          result = (bool) rpcResult.Result;
        }
        catch (Exception) {}
        if(result) {
          // Once we get pcount to a majority, we ship off the result
          lock(adps.pcount) {
            int count = (int) adps.pcount + 1;
            if(count == MAJORITY) {
              adps.returns.Enqueue(true);
              adps.returns.Close();
            }
            adps.pcount = count;
          }
        }
        else {
          lock(adps.ncount) {
            /* Once we get to ncount to 1 less than a majority, we ship off the
            * result, because we can't get pcount equal to majority any more!
            */
            int count = (int) adps.ncount + 1;
            if(count == MAJORITY - 1 || 1 == DEGREE) {
              adps.returns.Enqueue(false);
              adps.returns.Close();
            }
            adps.ncount = count;
          }
        }
      }
      queue.Close();
    }

    /* Get the hash of the first key and add 1/DEGREE * Address space
     * to each successive key
     */
    public MemBlock[] MapToRing(byte[] key) {
      MemBlock[] targets = new MemBlock[DEGREE];
      // Setup the first key
      HashAlgorithm algo = new SHA1CryptoServiceProvider();
      byte[] target = algo.ComputeHash(key);
      Address.SetClass(target, AHAddress._class);
      targets[0] = MemBlock.Reference(target);

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

    protected class AsDhtPutState {
      public Hashtable queueMapping = new Hashtable();
      public object pcount = 0, ncount = 0;
      public BlockingQueue returns;

      public AsDhtPutState(BlockingQueue returns) {
        this.returns = returns;
      }
    }

    protected class AsDhtGetState {
      public object GotToLeaveEarly = false;
      public Hashtable ttls = new Hashtable();
      public Hashtable queueMapping = new Hashtable();
      public Hashtable results = new Hashtable();
      public BlockingQueue returns;
      public MemBlock[] brunet_address_for_key;

      public AsDhtGetState(BlockingQueue returns) {
        this.returns = returns;
      }
    }
  }
}
