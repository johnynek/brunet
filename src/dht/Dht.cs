using System;
using System.Text;
using System.Collections;
using System.Security.Cryptography;
using System.Threading;

using Brunet;
using Brunet.Dht;

namespace Brunet.Dht {	
  public class DhtException: Exception {
    public DhtException(string message): base(message) {}
  }

  public class Dht {
    //we checkpoint the state of DHT every 1000 seconds.
    private static readonly int _CHECKPOINT_INTERVAL = 1000;
    private DateTime _next_checkpoint;

    //lock for the Dht
    protected object _sync;
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

    //keep track of our current neighbors
    protected AHAddress _left_addr = null;
    protected AHAddress _right_addr = null;

    public Dht(Node node, EntryFactory.Media media) {
      _sync = new object();
      _node = node;
      //activated not until we acquire left and right connections
      _dhtactivated = false;

      //we initially do not have eny structured neighbors to start
      _left_addr = _right_addr = null;

      //initialize the EntryFactory
      EntryFactory ef = EntryFactory.GetInstance(node, media);
      _table = new TableServer(ef, node);

      //get an instance of RpcManager for the node
      _rpc = RpcManager.GetInstance(node);

      //register the table with the RpcManagers
      _rpc.AddHandler("dht", _table);

      lock(_sync) {
        node.ConnectionTable.ConnectionEvent += 
          new EventHandler(ConnectHandler);

        node.ConnectionTable.DisconnectionEvent +=
          new EventHandler(DisconnectHandler);

        node.ConnectionTable.StatusChangedEvent +=
          new EventHandler(StatusChangedHandler);
      }

      DEGREE = 1;
      MAJORITY = 1;
      DELAY = 60000;
    }

    public Dht(Node node, EntryFactory.Media media, int degree) :
      this(node, media){
      this.DEGREE = (int) System.Math.Pow(2, degree);
      this.MAJORITY = DEGREE / 2 + 1;
    }

    public Dht(Node node, EntryFactory.Media media, int degree, int delay) :
      this(node, media, degree){
      DELAY = delay * 1000;
    }

    public BlockingQueue PrimitivePut(byte[] key, int ttl, byte[] data) {
      if (!_dhtactivated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }

      byte[][] b = MapToRing(key);
      Address target = new AHAddress(b[0]);

      AHSender s = new AHSender(_rpc.Node, target);
      BlockingQueue q = new BlockingQueue();
      _rpc.Invoke(s, q, "dht.Put", b[0], ttl, data);
      return q;
    }

    public BlockingQueue PrimitiveCreate(byte[] key, int ttl, byte[] data) {
      if (!_dhtactivated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }

      byte[][] b = MapToRing(key);
      Address target = new AHAddress(b[0]);
      AHSender s = new AHSender(_rpc.Node, target);
      BlockingQueue q = new BlockingQueue();
      _rpc.Invoke(s, q, "dht.Create", b[0], ttl, data);
      return q;
    }

    public BlockingQueue PrimitiveGet(byte[] key, int maxbytes, byte[] token) {
      if (!_dhtactivated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }

      byte[][] b = MapToRing(key);
      Address target = new AHAddress(b[0]);

      AHSender s = new AHSender(_rpc.Node, target);
      BlockingQueue q = new BlockingQueue();
      _rpc.Invoke(s, q, "dht.Get", b[0], maxbytes, token);
      return q;
    }

    /** Below are all the Create methods, they rely on a unique put   *
     * this returns true if it succeeded or an exception if it didn't */

    public BlockingQueue AsCreate(byte[] key, byte[] value, int ttl) {
      return AsPut(key, value, ttl, true);
    }

    public BlockingQueue AsCreate(string key, byte[] value, int ttl) {
      byte[] keyb = GetHashedKey(key);
      return AsCreate(keyb, value, ttl);
    }

    public BlockingQueue AsCreate(string key, string value, int ttl) {
      byte[] keyb = GetHashedKey(key);
      byte[] valueb = Encoding.UTF8.GetBytes(value);
      return AsCreate(keyb, valueb, ttl);
    }

    public bool Create(byte[] key, byte[] value, int ttl) {
      return Put(key, value, ttl, true);
    }

    public bool Create(string key, byte[] value, int ttl) {
      byte[] keyb = GetHashedKey(key);
      return Create(keyb, value, ttl);
    }

    public bool Create(string key, string value, int ttl) {
      byte[] keyb = GetHashedKey(key);
      byte[] valueb = Encoding.UTF8.GetBytes(value);
      return Create(keyb, valueb, ttl);
    }

    /** Below are all the Get methods */

    public BlockingQueue AsGet(string key) {
      byte[] keyb = GetHashedKey(key);
      return AsGet(keyb);
    }

    public BlockingQueue AsGet(byte[] key) {
      BlockingQueue queue = new BlockingQueue();
      object []data = new object[2];
      data[0] = key;
      data[1] = queue;
      ThreadPool.QueueUserWorkItem(new WaitCallback(Get), data);
      return queue;
    }

    public DhtGetResult[] Get(string key) {
      byte[] keyb = GetHashedKey(key);
      return Get(keyb);
    }

    public DhtGetResult[] Get(byte[] key) {
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

    /**  This is the get that does all the work, it is meant to be
     *   run as a thread */
    public void Get(object data) {
      object []data_array = (object[]) data;
      byte[] key = (byte[]) data_array[0];
      BlockingQueue allValues = (BlockingQueue) data_array[1];

      Hashtable allValuesCount = new Hashtable();
      int remaining = 0, last_count = DEGREE;
      byte [][]tokens = new byte[DEGREE][];
      bool multiget = false;

      byte[][] b = MapToRing(key);
      Address[] target = new Address[DEGREE];
      BlockingQueue[] q = new BlockingQueue[DEGREE];
      Address[] targets = new AHAddress[DEGREE];

      for (int k = 0; k < DEGREE; k++) {
        targets[k] = new AHAddress(MemBlock.Reference(b[k]));
        AHSender s = new AHSender(_rpc.Node, targets[k]);
        q[k] = new BlockingQueue();
        _rpc.Invoke(s, q[k], "dht.Get", b[k], MAX_BYTES, null);
      }

      ArrayList allQueues = new ArrayList();
      allQueues.AddRange(q);
      ArrayList queueMapping = new ArrayList();
      for(int i = 0; i < DEGREE; i++) {
        queueMapping.Add(i);
      }

      DateTime start = DateTime.UtcNow;
      while(allQueues.Count > 0) {
        if(last_count == allQueues.Count) {
          start = DateTime.UtcNow;
        }
        last_count = allQueues.Count;
        TimeSpan ts_timeleft = (DateTime.UtcNow - start);
        int time_left = DELAY - (int) ts_timeleft.TotalMilliseconds;
        time_left = (time_left > 0) ? time_left : 0;
        int idx = BlockingQueue.Select(allQueues, time_left);
        if(idx == -1) {
          break;
        }
        int real_idx = (int) queueMapping[idx];

        if(q[real_idx].Closed) {
          tokens[real_idx] = null;
        }
        else {
          ArrayList result;
          try {
              RpcResult rpc_reply = (RpcResult) q[real_idx].Dequeue();
              result = (ArrayList) rpc_reply.Result;
          }
          catch (Exception) {
            result = null;
          }
          //Result may be corrupted
          if (result != null && result.Count == 3) {
            ArrayList values = (ArrayList) result[0];
            remaining = (int) result[1];
            if(remaining > 0) {
              tokens[real_idx] = (byte[]) result[2];
            }
            else {
              tokens[real_idx] = null;
            }

            foreach (Hashtable ht in values) {
              MemBlock mbVal = MemBlock.Reference((byte[])ht["value"]);
              if(!allValuesCount.Contains(mbVal)) {
                allValuesCount[mbVal] = 1;
              }
              else {
                int count = ((int) allValuesCount[mbVal]) + 1;
                allValuesCount[mbVal] = count;
                if(count == MAJORITY) {
                  allValues.Enqueue(new DhtGetResult(ht));
                }
              }
            }
          }
        }

        q[real_idx].Close();
        if(tokens[real_idx] != null) {
          multiget = true;
          AHSender s = new AHSender(_rpc.Node, target[real_idx]);
          q[real_idx] = new BlockingQueue();
          _rpc.Invoke(s,q[real_idx], "dht.Get", b[real_idx], MAX_BYTES, tokens[real_idx]);
        }
        else {
          allQueues.RemoveAt(idx);
          queueMapping.RemoveAt(idx);
        }
        if(!multiget) {
          int left = allQueues.Count;
          if(left > MAJORITY - 1) {
            // Continue as normal
          }
          else if(allValuesCount.Count == 0) {
            // Not going to find anything
            start = DateTime.MinValue;
          }
          else {
            // Maybe we can leave early
            bool got_all_values = true;
            foreach (DictionaryEntry de in allValuesCount) {
              int val = (int) de.Value;
              if(val < MAJORITY && ((val + left) >= MAJORITY)) {
                got_all_values = false;
                break;
              }
            }
            if(got_all_values) {
              start = DateTime.MinValue;
            }
          }
        }
      }
      allValues.Close();
    }

    /** Below are all the Put methods, they use a non-unique put */

    public BlockingQueue AsPut(byte[] key, byte[] value, int ttl) {
      return AsPut(key, value, ttl, false);
    }

    public BlockingQueue AsPut(string key, byte[] value, int ttl) {
      byte[] keyb = GetHashedKey(key);
      return AsPut(keyb, value, ttl);
    }

    public BlockingQueue AsPut(string key, string value, int ttl) {
      byte[] keyb = GetHashedKey(key);
      byte[] valueb = Encoding.UTF8.GetBytes(value);
      return AsPut(keyb, valueb, ttl);
    }

    public bool Put(byte[] key, byte[] value, int ttl) {
      return Put(key, value, ttl, false);
    }

    public bool Put(string key, byte[] value, int ttl) {
      byte[] keyb = GetHashedKey(key);
      return Put(keyb, value, ttl);
    }

    public bool Put(string key, string value, int ttl) {
      byte[] keyb = GetHashedKey(key);
      byte[] valueb = Encoding.UTF8.GetBytes(value);
      return Put(keyb, valueb, ttl);
    }

    /** Since the Puts and Creates are the same from the client side, we merge them into a
    single put that if unique is true, it is a create, otherwise a put */

    public BlockingQueue AsPut(byte[] key, byte[] value, int ttl, bool unique) {
      BlockingQueue queue = new BlockingQueue();
      object []data = new object[5];
      data[0] = key;
      data[1] = value;
      data[2] = ttl;
      data[3] = unique;
      data[4] = queue;
      ThreadPool.QueueUserWorkItem(new WaitCallback(Put), data);
      return queue;
    }

    public bool Put(byte[] key, byte[] value, int ttl, bool unique) {
      BlockingQueue queue = new BlockingQueue();
      object []data = new object[5];
      data[0] = key;
      data[1] = value;
      data[2] = ttl;
      data[3] = unique;
      data[4] = queue;
      Put(data);
      return (bool) queue.Dequeue();
    }


    public void Put(object data) {
      object[] data_array = (object[]) data;
      byte[] key = (byte[]) data_array[0];
      byte[] value = (byte[]) data_array[1];
      int ttl = (int) data_array[2];
      bool unique = (bool) data_array[3];
      string funct = "dht.";
      if(unique) {
        funct += "Create";
      }
      else {
        funct += "Put";
      }
      BlockingQueue queue = (BlockingQueue) data_array[4];
      byte[][] b = MapToRing(key);

      bool rv = false;

      BlockingQueue[] q = new BlockingQueue[DEGREE];
      for (int k = 0; k < DEGREE; k++) {
        Address target = new AHAddress(MemBlock.Reference(b[k]));
        AHSender s = new AHSender(_rpc.Node, target);
        q[k] = new BlockingQueue();
        _rpc.Invoke(s, q[k], funct, b[k], ttl, value);
      }
      int pcount = 0, ncount = 0;
      // Special case cause I don't want to have to deal with extra logic
      if(MAJORITY == 1) {
        ncount = -1;
      }
      ArrayList allQueues = new ArrayList();
      allQueues.AddRange(q);

      DateTime start = DateTime.UtcNow;

      while(pcount < MAJORITY && ncount < MAJORITY - 1) {
        TimeSpan ts_timeleft = DateTime.UtcNow - start;
        int time_left = DELAY - (int) ts_timeleft.TotalMilliseconds;
        time_left = (time_left > 0) ? time_left: 0;

        int idx = BlockingQueue.Select(allQueues, time_left);
        bool result = false;
        if(idx == -1) {
          break;
        }

        if(!((BlockingQueue) allQueues[idx]).Closed) {
          try {
            RpcResult rpc_reply = (RpcResult) ((BlockingQueue) allQueues[idx]).Dequeue();
            result = (bool) rpc_reply.Result;
          }
          catch(Exception) {;} // Treat this as receiving a negative
        }

        if(result == true) {
          pcount++;
        }
        else {
          ncount++;
        }
        allQueues.RemoveAt(idx);
      }

      if(pcount >= MAJORITY) {
        rv = true;
      }

      foreach(BlockingQueue qclose in q) {
        qclose.Close();
      }
      queue.Enqueue(rv);
      queue.Close();
    }

    public byte[] GetHashedKey(string key) {
      byte[] keyb = Encoding.UTF8.GetBytes(key);
      HashAlgorithm algo = new SHA1CryptoServiceProvider();
      return algo.ComputeHash(keyb);
    }

    public byte[][] MapToRing(byte[] key) {
      HashAlgorithm hashAlgo = HashAlgorithm.Create();
      byte[] hash = hashAlgo.ComputeHash(key);

      //find targets which are as far apart on the ring as possible
      byte[][] target = new byte[DEGREE][];
      target[0] = hash;
      Address.SetClass(target[0], AHAddress._class);

      //add these increments to the base address
      BigInteger inc_addr = Address.Full/DEGREE;

      BigInteger curr_addr = new BigInteger(target[0]);
      for (int k = 1; k < target.Length; k++) {
        curr_addr = curr_addr + inc_addr;
        target[k] = Address.ConvertToAddressBuffer(curr_addr);
        Address.SetClass(target[k], AHAddress._class);
      }
      return target;
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
            _rpc.Invoke(_t_sender, _driver_queue, "dht.Put", e.Key,
                              (int) t_span.TotalSeconds, e.Data);
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
            _rpc.Invoke(_t_sender, _driver_queue, "dht.Put", e.Key,
                        (int) t_span.TotalSeconds, e.Data);
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
    public Hashtable All { get { return _table.GetAll(); } }

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
    public void Reset(EntryFactory.Media media) {
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

        EntryFactory ef = EntryFactory.GetInstance(_node, media);

        _table = new TableServer(ef, _node);

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
  }
}
