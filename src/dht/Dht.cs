using System;
using System.Text;
using System.Collections;
using System.Security.Cryptography;

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
    protected RpcManager _rpc;
    protected Node _node = null;
    protected bool _dhtactivated = false;
    public bool Activated {
      get {
        return _dhtactivated;
      }
    }

    //table server
    protected TableServer _table;

    //keep track of our current neighbors
    protected AHAddress _left_addr = null;
    protected AHAddress _right_addr = null;


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
            TimeSpan t_span = e.EndTime - DateTime.Now;
            _driver_queue = new BlockingQueue();
            _driver_queue.EnqueueEvent += new EventHandler(NextTransfer);
            _rpc.Invoke(_t_sender, _driver_queue, "dht.Put", e.Key,
                              (int) t_span.TotalSeconds, e.Password, e.Data);
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
            TimeSpan t_span = e.EndTime - DateTime.Now;
            _driver_queue = new BlockingQueue();
            _driver_queue.EnqueueEvent += new EventHandler(NextTransfer);
            _rpc.Invoke(_t_sender, _driver_queue, "dht.Put", e.Key,
                        (int) t_span.TotalSeconds, e.Password, e.Data);
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

    public Dht(Node node, EntryFactory.Media media) {
      _sync = new object();
      _node = node;
      //activated not until we acquire a connection
      _dhtactivated = false;

      //we initially do not have eny structured neighbors to start
      _left_addr = _right_addr = null;

      //initialize the EntryFactory
      EntryFactory ef = EntryFactory.GetInstance(node);
      ef.SetMedia(media);
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
    }

    public static MemBlock MapToRing(byte[] key) {
      HashAlgorithm hashAlgo = HashAlgorithm.Create();
      byte[] hash = hashAlgo.ComputeHash(key);
      Address.SetClass(hash, AHAddress._class);
      return MemBlock.Reference(hash);
    }

    public BlockingQueue Put(byte[] key, int ttl, string hashed_password, byte[] data) {
      if (!_dhtactivated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }

      MemBlock b = MapToRing(key);
      Address target = new AHAddress(b);

      AHSender s = new AHSender(_rpc.Node, target);
      BlockingQueue q = new BlockingQueue();
      _rpc.Invoke(s, q, "dht.Put", b, ttl, hashed_password, data);
      return q;
    }

    public BlockingQueue Create(byte[] key, int ttl, string hashed_password, byte[] data) {
      if (!_dhtactivated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }

      MemBlock b = MapToRing(key);
      Address target = new AHAddress(b);
      AHSender s = new AHSender(_rpc.Node, target);
      BlockingQueue q = new BlockingQueue();
      _rpc.Invoke(s, q, "dht.Create", b, ttl, hashed_password, data);
      return q;
    }

    public BlockingQueue Get(byte[] key, int maxbytes, byte[] token) {
      if (!_dhtactivated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }

      MemBlock b = MapToRing(key);
      Address target = new AHAddress(b);

      AHSender s = new AHSender(_rpc.Node, target);
      BlockingQueue q = new BlockingQueue();
      _rpc.Invoke(s, q, "dht.Get", b, maxbytes, token);
      return q;
    }

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
        if (DateTime.Now > _next_checkpoint) {
          TimeSpan interval = new TimeSpan(0,0,0,0, _CHECKPOINT_INTERVAL);
          _next_checkpoint = DateTime.Now + interval;
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

        EntryFactory ef = EntryFactory.GetInstance(_node);
        ef.SetMedia(media);

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
