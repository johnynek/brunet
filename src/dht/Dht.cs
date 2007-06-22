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
    //we checkpoint the state of DHT every 1000 seconds.
    private static readonly int _CHECKPOINT_INTERVAL = 1000;
    private DateTime _next_checkpoint;

    //lock for the Dht
    protected object _sync = new object();
    private RpcManager _rpc;
    public Node _node = null;
    private bool _dhtactivated = false;
    public bool Activated { get { return _dhtactivated; } }
    public readonly int DEGREE;
    public readonly int DELAY;
    public readonly int MAJORITY;
    public static readonly int MAX_BYTES = 1000;

    //table server
    protected TableServer _table;

    //Dht Get / Put States
    private volatile Hashtable _adps_table = new Hashtable();
    private volatile Hashtable _adgs_table = new Hashtable();

    //keep track of our current neighbors, we start with none
    protected AHAddress _left_addr = null, _right_addr = null;

    public Dht(Node node) {
      _node = node;
      //activated not until we acquire left and right connections
      _dhtactivated = false;
      //get an instance of RpcManager for the node
      _rpc = RpcManager.GetInstance(node);
      _table = new TableServer(node, _rpc);
      //register the table with the RpcManagers
      _rpc.AddHandler("dht", _table);
      //we need to update our collection of data everytime our neighbors change
      lock(_sync) {
        node.ConnectionTable.ConnectionEvent += 
          new EventHandler(ConnectHandler);

        node.ConnectionTable.DisconnectionEvent +=
          new EventHandler(DisconnectHandler);

        node.ConnectionTable.StatusChangedEvent +=
          new EventHandler(StatusChangedHandler);
      }

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
      if (!_dhtactivated) {
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
      if (!_dhtactivated) {
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

    public BlockingQueue AsCreate(MemBlock key, MemBlock value, int ttl) {
      return AsPut(key, value, ttl, true);
    }

    public BlockingQueue AsCreate(string key, MemBlock value, int ttl) {
      MemBlock keyb = GetHashedKey(key);
      return AsCreate(keyb, value, ttl);
    }

    public BlockingQueue AsCreate(string key, string value, int ttl) {
      MemBlock keyb = GetHashedKey(key);
      MemBlock valueb = MemBlock.Reference(Encoding.UTF8.GetBytes(value));
      return AsCreate(keyb, valueb, ttl);
    }

    public bool Create(MemBlock key, MemBlock value, int ttl) {
      return Put(key, value, ttl, true);
    }

    public bool Create(string key, MemBlock value, int ttl) {
      MemBlock keyb = GetHashedKey(key);
      return Create(keyb, value, ttl);
    }

    public bool Create(string key, string value, int ttl) {
      MemBlock keyb = GetHashedKey(key);
      MemBlock valueb = MemBlock.Reference(Encoding.UTF8.GetBytes(value));
      return Create(keyb, valueb, ttl);
    }

    /* Below are all the Get methods */

    public BlockingQueue AsGet(string key) {
      MemBlock keyb = GetHashedKey(key);
      return AsGet(keyb);
    }

    public DhtGetResult[] Get(string key) {
      MemBlock keyb = GetHashedKey(key);
      return Get(keyb);
    }

    public DhtGetResult[] Get(MemBlock key) {
      BlockingQueue queue = AsGet(key);
      ArrayList allValues = new ArrayList();
      while(true) {
        // Still a chance for Dequeue to execute on an empty closed queue 
        // so we'll do this instead.
        try {
          DhtGetResult dgr = (DhtGetResult) queue.Dequeue();
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
    public BlockingQueue AsGet(MemBlock key) {
      if (!_dhtactivated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }

      // create a GetState and map in our table map its queues to it
      // so when we get a GetHandler we know which state to load
      AsDhtGetState adgs = new AsDhtGetState();
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
      return adgs.returns;
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
        if(queue.Closed) {
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
            adgs.results.Clear();
          }
          else {
              GetLeaveEarly(adgs);
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
            object o_count = null;
            int count = 1;
            lock(adgs.results) {
              o_count = adgs.results[mbVal];
              if(o_count != null) {
                count = (int) o_count + 1;
              }
              adgs.results[mbVal] = count;
            }
            if(count == MAJORITY) {
              adgs.returns.Enqueue(new DhtGetResult(ht));
            }
          }
        }
        catch (Exception) {
          sendto = null;
          token = null;
        }

        // We were notified that more results were available!  Let's go get them!
        if(token != null && sendto != null) {
          queue = new BlockingQueue();
          lock(adgs.queueMapping) {
            adgs.queueMapping[queue] = idx;
          }
          lock(_adgs_table) {
            _adgs_table[queue] = adgs;
          }
          queue.EnqueueEvent += this.GetHandler;
          queue.CloseEvent += this.GetHandler;
          _rpc.Invoke(sendto, queue, "dht.Get", 
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
      if (left >= MAJORITY) {
        return;
      }
      // Maybe we can leave early
      bool got_all_values = true;
      lock(adgs.results) {
        foreach (DictionaryEntry de in adgs.results) {
          int val = (int) de.Value;
          if(val < MAJORITY && ((val + left) >= MAJORITY)) {
            got_all_values = false;
            break;
          }
        }
      }

      // If we got to leave early, we must clean up
      if(got_all_values) {
        BlockingQueue [] queues = new BlockingQueue[adgs.queueMapping.Count];
        lock(adgs.queueMapping) {
          int i = 0;
          foreach(DictionaryEntry de in adgs.queueMapping) {
            queues[i++] = (BlockingQueue) de.Key;
          }
        }
        for(int i = 0; i < queues.Length; i++) {
          BlockingQueue q = queues[i];
          q.CloseEvent -= this.GetHandler;
          q.EnqueueEvent -= this.GetHandler;
          q.Close();
        }
        lock(_adgs_table) {
          lock(adgs.queueMapping) {
            for(int i = 0; i < queues.Length; i++) {
              adgs.queueMapping.Remove(queues[i]);
              _adgs_table.Remove(queues[i]);
            }
          }
        }
        adgs.returns.Close();
        adgs.results.Clear();
      }
    }

    /// @todo need to implement a put on failed gets (iff a majority occurs)

    /** Below are all the Put methods, they use a non-unique put */

    public BlockingQueue AsPut(MemBlock key, MemBlock value, int ttl) {
      return AsPut(key, value, ttl, false);
    }

    public BlockingQueue AsPut(string key, MemBlock value, int ttl) {
      MemBlock keyb = GetHashedKey(key);
      return AsPut(keyb, value, ttl);
    }

    public BlockingQueue AsPut(string key, string value, int ttl) {
      MemBlock keyb = GetHashedKey(key);
      MemBlock valueb = MemBlock.Reference(Encoding.UTF8.GetBytes(value));
      return AsPut(keyb, valueb, ttl);
    }

    public bool Put(MemBlock key, MemBlock value, int ttl) {
      return Put(key, value, ttl, false);
    }

    public bool Put(string key, MemBlock value, int ttl) {
      MemBlock keyb = GetHashedKey(key);
      return Put(keyb, value, ttl);
    }

    public bool Put(string key, string value, int ttl) {
      MemBlock keyb = GetHashedKey(key);
      MemBlock valueb = MemBlock.Reference(Encoding.UTF8.GetBytes(value));
      return Put(keyb, valueb, ttl);
    }

    /** Since the Puts and Creates are the same from the client side, we merge them into a
    single put that if unique is true, it is a create, otherwise a put */

    public bool Put(MemBlock key, MemBlock value, int ttl, bool unique) {
      return (bool) AsPut(key, value, ttl, unique).Dequeue();
    }

    public BlockingQueue AsPut(MemBlock key, MemBlock value, int ttl, bool unique) {
      if (!_dhtactivated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }

      AsDhtPutState adps = new AsDhtPutState();

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
      return adps.returns;
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
          queue.Close();
          return;
        }


        // Well it was closed, shouldn't have happened, but we'll do garbage collection
        if(queue.Closed) {
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
          queue.CloseEvent -= this.PutHandler;
          queue.EnqueueEvent -= this.PutHandler;
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
        catch (Exception) {;}
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

    public MemBlock GetHashedKey(string key) {
      byte[] keyb = Encoding.UTF8.GetBytes(key);
      HashAlgorithm algo = new SHA1CryptoServiceProvider();
      return MemBlock.Reference(algo.ComputeHash(keyb));
    }

    public MemBlock[] MapToRing(byte[] key) {
      HashAlgorithm hashAlgo = HashAlgorithm.Create();
      byte[] hash = hashAlgo.ComputeHash(key);

      //find targets which are as far apart on the ring as possible
      MemBlock[] targets = new MemBlock[DEGREE];
      Address.SetClass(hash, AHAddress._class);
      targets[0] = hash;

      //add these increments to the base address
      BigInteger inc_addr = Address.Full/DEGREE;

      BigInteger curr_addr = new BigInteger(targets[0]);
      for (int k = 1; k < targets.Length; k++) {
        curr_addr = curr_addr + inc_addr;
        byte[] target = Address.ConvertToAddressBuffer(curr_addr);
        Address.SetClass(target, AHAddress._class);
        targets[k] = target;
      }
      return targets;
    }


    protected class TransferState {
      protected object _sync;
      protected RpcManager _rpc;
      protected AHAddress _our_addr;

      protected bool _to_delete = false;
      public bool ToDelete { get { return _to_delete; } }

      protected AHAddress _target;
      public AHAddress Target { get { return _target; } }

      protected Hashtable _key_list;
      protected ISender _t_sender;
      protected BlockingQueue _driver_queue = null;
      protected IEnumerator _entry_enumerator = null;

      protected TransferCompleteCallback _tcb = null;

      public TransferState(RpcManager rpcman, AHAddress our_addr, AHAddress target, 
                           Hashtable key_list, bool to_delete) {
        _sync = new object();
        _rpc = rpcman;
        _our_addr = our_addr;
        _target = target;
        _key_list = key_list;
        _to_delete = to_delete;
        _t_sender = new AHExactSender(rpcman.Node, target);
        _entry_enumerator = GetEntryEnumerator();
      }

      // Transfer all keys
      public IEnumerator GetEntryEnumerator() {
        foreach (MemBlock k in _key_list.Keys) {
          ArrayList values = (ArrayList) _key_list[k];
          foreach (Entry e in values) {
            yield return e;
          }
        }
      }

      public void StartTransfer(TransferCompleteCallback tcb) {
        _tcb = tcb;
        lock(_sync) {
          if (_entry_enumerator.MoveNext()) {
            Entry e = (Entry) _entry_enumerator.Current;
            TimeSpan t_span = e.EndTime - DateTime.UtcNow;
            _driver_queue = new BlockingQueue();
            _driver_queue.EnqueueEvent += new EventHandler(NextTransfer);
            _rpc.Invoke(_t_sender, _driver_queue, "dht.PutHandler", e.Key, e.Value,
                              (int) t_span.TotalSeconds, false);
          }
          else {
            if (_tcb != null) {
              _tcb(this, _key_list);
            }
          }
        }
      }

      public void NextTransfer(Object o, EventArgs args) {
        lock(_sync) {
          BlockingQueue q =  (BlockingQueue) o;
          try {
            q.Dequeue();
            q.Close();
          }
          catch(Exception) {;}
          //unregister any future enqueue events
          q.EnqueueEvent -= new EventHandler(NextTransfer);
          q.Close();
          //initiate next transfer
          if (_entry_enumerator.MoveNext()) {
            Entry e = (Entry) _entry_enumerator.Current;
            TimeSpan t_span = e.EndTime - DateTime.UtcNow;
            _driver_queue = new BlockingQueue();
            _driver_queue.EnqueueEvent += new EventHandler(NextTransfer);
            _rpc.Invoke(_t_sender, _driver_queue, "dht.PutHandler", e.Key, e.Value,
                        (int) t_span.TotalSeconds, false);
          }
          else {
            if (_tcb != null) {
              _tcb(this, _key_list);
            }
          }
        }
      }

      //it is possible that the driver queue is still not ready
      public void InterruptTransfer() {
        BlockingQueue to_close = null;
        lock(_sync) {
          if (_driver_queue != null) {
            _driver_queue.EnqueueEvent -= new EventHandler(NextTransfer);
            to_close = _driver_queue;
          }
        }
        if( to_close != null ) {
          to_close.Close();
        }
      }
    }

    protected delegate void TransferCompleteCallback (TransferState state, Hashtable key_list);
    protected TransferState _left_transfer_state = null;
    protected TransferState _right_transfer_state = null;
    public Address LeftAddress { get { return _left_addr; } }
    public Address RightAddress { get { return _right_addr; } }
    public Address Address { get { return _node.Address; } }
    public int Count { get { return _table.GetCount(); } }

    protected void ConnectHandler(object contab, EventArgs eargs) 
    {
      ConnectionEventArgs args = (ConnectionEventArgs)eargs;
      Connection new_con = args.Connection;
      //AHAddress new_addr = new_con.Address as AHAddress;

      AHAddress our_addr = _node.Address as AHAddress;
      //first mke sure that it is a new StructuredConnection
      if (new_con.MainType != ConnectionType.Structured) {
        return;
      }

      ConnectionTable con_table = _node.ConnectionTable;
      AHAddress new_left_addr = null;
      AHAddress new_right_addr = null;

      lock(_sync) {
        lock(con_table.SyncRoot) {//lock the connection table
          //we need to check if we are ready for DhtLogic
          if (!_dhtactivated) {
            if (_node.IsConnected ) {
              _dhtactivated = true;
            }
          }

          try {
          Connection new_left_con = con_table.GetLeftStructuredNeighborOf(our_addr);
          new_left_addr = new_left_con.Address as AHAddress;
          }
          catch (Exception) { // Error getting left neighbor information
          }

          try {
            Connection new_right_con = con_table.GetRightStructuredNeighborOf(our_addr);
            new_right_addr = new_right_con.Address as AHAddress;
          }
          catch(Exception) { // Error getting right neighbor information
          }


          /** Here;s the algorithm we plan to use. 
            *  A---C and later becomes A---B---C.
            *  A transfers to B [B, C] and then gets rid of these set of keys.
            *  C transfers to B [A, B] and then gets rid of these keys. 
            */

          //1. test of the left neighbor has changed

          if (new_left_addr != null && _left_addr == null) {
            //acquired a left neighbor
            //share some keys with him
            if (_left_transfer_state == null) {
              //pass on some keys to him now
              if(_dhtactivated) {
                _left_transfer_state = new TransferState(_rpc, our_addr, new_left_addr,
                          _table.GetKeysToLeft(our_addr, new_left_addr), false);
                _left_transfer_state.StartTransfer(TransferCompleteHandler);
              }
            }
          }
          else if (new_left_addr != null && !new_left_addr.Equals(_left_addr)) {
            //its a changed left neighbor
            if (_left_transfer_state != null) {
              _left_transfer_state.InterruptTransfer();
            }
            if (_dhtactivated) {
              _left_transfer_state = new TransferState(_rpc, our_addr, new_left_addr,
                            _table.GetKeysToLeft(new_left_addr, _left_addr), true);
              _left_transfer_state.StartTransfer(TransferCompleteHandler);
            }
          }

          _left_addr = new_left_addr;

          //2. check if the right neighbpor has changed
          if (new_right_addr != null && _right_addr == null) {
            //acquired a right neighbor
            //share some keys with him
            if (_right_transfer_state == null) {
              if (_dhtactivated) {
                //pass on some keys to him now
                _right_transfer_state = new TransferState(_rpc, our_addr, new_right_addr,
                              _table.GetKeysToRight(our_addr, new_right_addr), false);
                _right_transfer_state.StartTransfer(TransferCompleteHandler);
              }
            }
          } else if (new_right_addr != null && !new_right_addr.Equals(_right_addr)) {
            //its a changed right neighbor
            if (_right_transfer_state != null) {
              _right_transfer_state.InterruptTransfer();
            }
            if (_dhtactivated) {
              _right_transfer_state = new TransferState(_rpc, our_addr, new_right_addr,
                            _table.GetKeysToRight(new_right_addr, _right_addr), true);
              _right_transfer_state.StartTransfer(TransferCompleteHandler);
            }
          }
          _right_addr = new_right_addr;
        } //release lock on the connection table
      }//release out own lock
    }


    protected void DisconnectHandler(object contab, EventArgs eargs) {
      ConnectionEventArgs cargs = eargs as ConnectionEventArgs;
      Connection old_con = cargs.Connection;
      AHAddress our_addr = _node.Address as AHAddress;

      //first make sure that it is a new StructuredConnection
      if (old_con.MainType != ConnectionType.Structured) {
        return;
      }
      ConnectionTable con_table = _node.ConnectionTable;

      AHAddress new_left_addr = null;
      AHAddress new_right_addr = null;

      lock(_sync) {
        lock(con_table.SyncRoot) {  //lock the connection table
          //we need to check if we can Put() our keys away.
          //we only do that if we have sufficient number of connections
          if (!_dhtactivated) {
            if (_node.IsConnected ) {
              _dhtactivated = true;
            }
          }
          try {
            Connection new_left_con = con_table.GetLeftStructuredNeighborOf(our_addr);
            new_left_addr = new_left_con.Address as AHAddress;
          }
          catch (Exception) { // Error getting left neighbor information
          }

          try {
            Connection new_right_con = con_table.GetRightStructuredNeighborOf(our_addr);
            new_right_addr = new_right_con.Address as AHAddress;
          }
          catch(Exception) { // Error getting right neighbor information
          }

          /** Here;s the algorithm we plan to use. 
          *  Its A----B----C and later it becomes: A-----C
          *  A is missing [B,C]
          *  C is missing [A,B]
          *  C transfers  to A:  [B,C]
          *  A transfers to A: [A,B]
          *  There is no deletion of keys as well. 
          */

          if (new_left_addr == null && _left_addr != null) {
            //there is nothing that we can do, it just went away.
            if (_left_transfer_state != null) {
              _left_transfer_state.InterruptTransfer();
              _left_transfer_state = null;
            }
          } else if (new_left_addr != null && !new_left_addr.Equals(_left_addr)) {
            //its a changed left neighbor
            if (_left_transfer_state != null) {
              _left_transfer_state.InterruptTransfer();
            }
            if (_dhtactivated) {
              _left_transfer_state = new TransferState(_rpc, our_addr, new_left_addr,
                                                      _table.GetKeysToLeft(our_addr, _left_addr), 
                                                      false);
              _left_transfer_state.StartTransfer(TransferCompleteHandler);
            }
          }
          _left_addr = new_left_addr;

          //2. check if the right neighbpor has changed
          if (new_right_addr == null && _right_addr != null) {
            //nothing that we can do, the guy just went away.
            if (_right_transfer_state != null) {
              _right_transfer_state.InterruptTransfer();
              _right_transfer_state = null;
            }
          }
          else if (new_right_addr != null && !new_right_addr.Equals(_right_addr)) {
            //its a changed right neighbor
            if (_right_transfer_state != null) {
              _right_transfer_state.InterruptTransfer();
            }
            if (_dhtactivated) {
              _right_transfer_state = new TransferState(_rpc, our_addr, new_right_addr,
                              _table.GetKeysToRight(our_addr, _right_addr), false);
              _right_transfer_state.StartTransfer(TransferCompleteHandler);
            }
            else {
            }
          }
          _right_addr = new_right_addr;
        } //release the lock on connection table
      }
    }

    private void TransferCompleteHandler(TransferState state, Hashtable key_list) {
      //we also have to make sure that this transfer of keys is still valid
      lock(_sync) {
        //make sure that this transfer is still valid
        if (state == _left_transfer_state ) {
          if (state.ToDelete) {
            _table.AdminDelete(key_list);
          }
          //we also have to reset the transfer state
          _left_transfer_state = null;
        }
        else if (state == _right_transfer_state) {
          if (state.ToDelete) {
            _table.AdminDelete(key_list);
          }
          _right_transfer_state = null;
        }
      }
    }

    /** 
     *  This method Checkpoints the table state periodically.
     *  (Still an incomplete implementation!)
     *  I guess we're still not doing checkpoints
     */
    public void CheckpointHandler(object node, EventArgs eargs) {
      lock(_sync) {
        if (DateTime.UtcNow > _next_checkpoint) {
          TimeSpan interval = new TimeSpan(0,0,0,0, _CHECKPOINT_INTERVAL);
          _next_checkpoint = DateTime.UtcNow + interval;
        }
      }
    }

    /**
     * This method checks to see if the node is connected and activates 
     * the Dht if it is.
     */
    protected void StatusChangedHandler(object contab, EventArgs eargs) {
      ConnectionTable con_table = _node.ConnectionTable;
      lock(con_table.SyncRoot) {  //lock the connection table
        if (!_dhtactivated) {
          if (_node.IsConnected ) {
            _dhtactivated = true;
          }
        }
      }
    }

    /** 
     * Useful for debugging , code save 30 minutes of my time atleast.
     * Warning: This method should not be used at all.
     */
    public void Reset() {
      lock(_sync) {
        //unsubscribe the disconnection
        _node.ConnectionTable.ConnectionEvent -= 
          new EventHandler(ConnectHandler);
        _node.ConnectionTable.DisconnectionEvent -= 
          new EventHandler(DisconnectHandler);
        _node.ConnectionTable.StatusChangedEvent -=
          new EventHandler(StatusChangedHandler);

        if (_left_transfer_state != null) {
          _left_transfer_state.InterruptTransfer();
          _left_transfer_state = null;
        }
        if (_right_transfer_state != null) {
          _right_transfer_state.InterruptTransfer();
          _right_transfer_state.InterruptTransfer();
        }
        _left_addr = _right_addr = null;

        _table = new TableServer(_node, _rpc);

        //register a new TableServer with RpcManager
        _rpc.RemoveHandler("dht");
        _rpc.AddHandler("dht", _table);

        _node.ConnectionTable.ConnectionEvent += 
          new EventHandler(ConnectHandler);
        _node.ConnectionTable.DisconnectionEvent += 
          new EventHandler(DisconnectHandler);
        _node.ConnectionTable.StatusChangedEvent +=
          new EventHandler(StatusChangedHandler);
      } //end of lock
    }

    protected class AsDhtPutState {
      public Hashtable queueMapping = new Hashtable();
      public object pcount = 0, ncount = 0;
      public BlockingQueue returns = new BlockingQueue();

    }

    protected class AsDhtGetState {
      public Hashtable queueMapping = new Hashtable();
      public Hashtable results = new Hashtable();
      public BlockingQueue returns = new BlockingQueue();
      public MemBlock[] brunet_address_for_key;
    }
  }
}
