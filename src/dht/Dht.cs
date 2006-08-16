using System;
using System.Collections;
using System.Security.Cryptography;

using Brunet;
using Brunet.Dht;

namespace Brunet.Dht {	

  public class Dht {

    //lock for the Dht
    protected object _sync;
    //the we are attached to
    protected RpcManager _rpc;
    //node we are associated with
    protected Node _node = null;

    //table server
    protected TableServer _table;

    //keep track of our current neighbors
    protected AHAddress _left_addr = null;
    protected AHAddress _right_addr = null;


    protected class TransferState {
      protected object _sync;
      protected RpcManager _rpc;
      protected AHAddress _our_addr;
      protected AHAddress _target;
      protected Hashtable _key_list;
      

      protected BlockingQueue _driver_queue = null;
      protected IEnumerator _entry_enumerator = null;

      protected TransferCompleteCallback _tcb = null;
      
      public TransferState(RpcManager rpcman, AHAddress our_addr, AHAddress target, Hashtable key_list) 
      {
	_sync = new object();
	_rpc = rpcman;
	_our_addr = our_addr;
	_target = target;
	_key_list = key_list;
	_entry_enumerator = GetEntryEnumerator();
      }

      public IEnumerator GetEntryEnumerator() {
	foreach (byte[] k in _key_list.Keys) {
	  ArrayList values = (ArrayList) _key_list[k];
	  foreach (Entry e in values) {
	    yield return e;
	  }
	}
	//finally all keys have been transferred
      }

      public void StartTransfer(TransferCompleteCallback tcb) {
	_tcb = tcb;
	lock(_sync) {
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Getting the next value to transfer.", _our_addr);
#endif

	  if (_entry_enumerator.MoveNext()) {
	    Entry e = (Entry) _entry_enumerator.Current;
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Found a value. Making an RPC call to: {1}",
			      _our_addr,
			      _target);
#endif
	    TimeSpan t_span = e.EndTime - DateTime.Now;
	    //Console.WriteLine("Endtime: {0}, Current: {1}", e.EndTime, DateTime.Now);
	    //Console.WriteLine("TTL for transferred value: {0}", (int) t_span.TotalSeconds);

	    _driver_queue = _rpc.Invoke(_target, "dht.Put", e.Key, 
					(int) t_span.TotalSeconds, 
					e.Password, 
					e.Data);
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Found a value. Returning non-blocking RPC from: {1}",
			      _our_addr, _target);
#endif
	    _driver_queue.EnqueueEvent += new EventHandler(NextTransfer);
	  } else {
	    //we are done with the transfer
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Finished transferring all values to: {1}", _our_addr, _target);
#endif
	    if (_tcb != null) {
#if DHT_DEBUG
	      Console.WriteLine("[DhtLogic] {0}: Now do a sequence of deletes on us. ", _our_addr);
#endif
	      _tcb(this, _key_list);
	    }
	  }
	}
      }
      
      public void NextTransfer(Object o, EventArgs args) {
	lock(_sync) {
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Finished transferring a value. Will try to pick next.", _our_addr);
#endif
	  BlockingQueue q =  (BlockingQueue) o;
	  
	  //unregister any future enqueue events
	  q.EnqueueEvent -= new EventHandler(NextTransfer);
	  
	  //close the queue
	  q.Close();
	  
	  //initiate next transfer
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Getting the next value to transfer to: {1}.", _our_addr, _target);
#endif
	  
	  if (_entry_enumerator.MoveNext()) {
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Found a value. Making an RPC call to: {1}",
			      _our_addr,
			      _target);
#endif
	    Entry e = (Entry) _entry_enumerator.Current;
	    TimeSpan t_span = e.EndTime - DateTime.Now;
	    _driver_queue = _rpc.Invoke(_target, "dht.Put", e.Key, 
					(int) t_span.TotalSeconds,
					e.Password, 
					e.Data);
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Found a value. Returning non-blocking RPC from: {1}",
			      _our_addr, _target);
#endif

	    _driver_queue.EnqueueEvent += new EventHandler(NextTransfer);
	  } else {
	    //we are done with the transfer
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Finished transferring all values to: {1} ", _our_addr, _target);
#endif
	    if (_tcb != null) {
#if DHT_DEBUG
	      Console.WriteLine("[DhtLogic] {0}: Now do a sequence of deletes on us. ", _our_addr);
#endif
	      _tcb(this, _key_list);
	    }
	  }
	}
      }
      public void InterruptTransfer() {
	lock(_sync) {
	  //it is possible that the driver queue is still not ready
	  if (_driver_queue != null) {
	    _driver_queue.EnqueueEvent -= new EventHandler(NextTransfer);
	    _driver_queue.Close();
	  }
	}
      }
      //all the keys have
    }
    
    protected delegate void TransferCompleteCallback (TransferState state, Hashtable key_list);


    protected TransferState _left_transfer_state = null;
    protected TransferState _right_transfer_state = null;
    
    public Address LeftAddress {
      get {
	return _left_addr;
      }
    }
    public Address RightAddress {
      get {
	return _right_addr;
      }
    }
    public Address Address {
      get {
	return _node.Address;
      }
    }
    public int Count {
      get {
	return _table.GetCount();
      }
    }
    public Dht(Node node) {
      _sync = new object();
      
      _node = node;
      //we initially do not have eny structured neighbors to start
      _left_addr = _right_addr = null;

      node.ConnectionTable.ConnectionEvent += 
	new EventHandler(ConnectHandler);

      node.ConnectionTable.DisconnectionEvent += 
	new EventHandler(DisconnectHandler);

      //get an instance of ReqrepManager
      ReqrepManager rrman = ReqrepManager.GetInstance(node);
      
      //create a new rpc manager
      _rpc = new RpcManager(rrman);
      
      _table = new TableServer(node);
      //register the table with the RpcManagers
      _rpc.AddHandler("dht", _table);

    }

    protected Address GetInvocationTarget(byte[] key) {
      HashAlgorithm hashAlgo = HashAlgorithm.Create();
      byte[] hash = hashAlgo.ComputeHash(key);
      hash[Address.MemSize -1] &= 0xFE;
      Address target = new AHAddress(new BigInteger(hash));
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invocation target: {0}", target);
#endif
      return target;
    }
    
    public BlockingQueue Put(byte[] key, int ttl, string hashed_password, byte[] data) {
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invoking a Dht::Put()");
#endif
      Address target = GetInvocationTarget(key);

#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Doing an RPC-invoke..");
#endif      
      //we now know the invocation target
      BlockingQueue q = _rpc.Invoke(target, "dht.Put", key, ttl, hashed_password, data);
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Returning a blocking queue..");
#endif
      return q;
      //RpcResult res = q.Dequeue() as RpcResult;
      //return Convert.ToInt32(res.Result);
    }

    public BlockingQueue Create(byte[] key, int ttl, string hashed_password, byte[] data) {
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invoking a Dht::Create()");
#endif
      Address target = GetInvocationTarget(key);
      
      //we now know the invocation target
      BlockingQueue q = _rpc.Invoke(target, "dht.Create", key, ttl, hashed_password, data);
      return q;
      //RpcResult res = q.Dequeue() as RpcResult;
      //return Convert.ToBoolean(res.Result);
    }
    
    public BlockingQueue Get(byte[] key, int maxbytes, byte[] token) {
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invoking a Dht::Get()");
#endif
      Address target = GetInvocationTarget(key);
      
      //we now know the invocation target
      BlockingQueue q = _rpc.Invoke(target, "dht.Get", key, maxbytes, token);
      return q;
      //RpcResult res = q.Dequeue() as RpcResult;
      //IList data_list = res.Result as IList;
      //return data_list;
    }
    public BlockingQueue Delete(byte[] key, string password)
    {  
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invoking a Dht::Delete()");
#endif
      Address target = GetInvocationTarget(key);
      
      //we now know the invocation target
      BlockingQueue q = _rpc.Invoke(target, "dht.Delete", key, password);
      return q;
    }
    
    protected void ConnectHandler(object contab, EventArgs eargs) 
    {
      ConnectionEventArgs args = (ConnectionEventArgs)eargs;
      Connection new_con = args.Connection;
      //AHAddress new_addr = new_con.Address as AHAddress;
      
      AHAddress our_addr = _node.Address as AHAddress;
      
#if DHT_DEBUG
      Console.WriteLine("[DhtLogic] {0}: Acquired a new connection to {1}.", our_addr, new_con.Address);
#endif 
      
      //first mke sure that it is a new StructuredConnection
      if (new_con.MainType != ConnectionType.Structured) {
#if DHT_DEBUG
	Console.WriteLine("[DhtLogic] {0}: Not a structured connection, but {1}, ignore!", our_addr, new_con.ConType);
#endif 
	return;
      }
      
      //now comes the important part
      ConnectionTable con_table = _node.ConnectionTable;
      AHAddress new_left_addr = null;
      AHAddress new_right_addr = null;

      lock(_sync) {
      lock(con_table.SyncRoot) {//lock the connection table
	try {
	  Connection new_left_con = con_table.GetLeftStructuredNeighborOf(our_addr);
	  new_left_addr = new_left_con.Address as AHAddress;
	} catch (Exception e) {
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Error getting left neighbor information. ", our_addr);
#endif	  
	}
	try {
	  Connection new_right_con = con_table.GetRightStructuredNeighborOf(our_addr);
	  new_right_addr = new_right_con.Address as AHAddress;
	} catch(Exception e) {
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Error getting right neighbor information. ", our_addr);
#endif	  
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
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Connected to my first left neighbor: {1}", 
			    our_addr, new_left_addr);
#endif
	  if (_left_transfer_state == null) {
	    //pass on some keys to him now
	    _left_transfer_state = new TransferState(_rpc, our_addr, new_left_addr,
						     _table.GetKeysToLeft(our_addr, new_left_addr));
	    _left_transfer_state.StartTransfer(null);
	    
	  } else {
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Just acquired first left neighbor. " + 
			      "Still an existing transfer state (Error).", our_addr);
#endif
	  }
	} else if (new_left_addr != null && !new_left_addr.Equals(_left_addr)) {
	  //its a changed left neighbor
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: New left  neighbor: {1}", our_addr, new_left_addr);
#endif
	  if (_left_transfer_state != null) {
	    _left_transfer_state.InterruptTransfer();
	  }
	  _left_transfer_state = new TransferState(_rpc, our_addr, new_left_addr,
						   _table.GetKeysToLeft(new_left_addr, _left_addr));
	  _left_transfer_state.StartTransfer(TransferCompleteHandler);
	  //we also have to initiate a deletion of extra keys (for later).
	}
	
	_left_addr = new_left_addr;
	



	//2. check if the right neighbpor has changed
	if (new_right_addr != null && _right_addr == null) {
	  //acquired a right neighbor
	  //share some keys with him
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Connected to my first right neighbor: {1}", our_addr, new_right_addr);
#endif
	  if (_right_transfer_state == null) {
	    //pass on some keys to him now
	    _right_transfer_state = new TransferState(_rpc, our_addr, new_right_addr,
						     _table.GetKeysToRight(our_addr, new_right_addr));
	    _right_transfer_state.StartTransfer(null);
	    
	  } else {
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Just acquired first right neighbor. " + 
			      "Still an existing transfer state (Error).", our_addr);
#endif
	  }
	  
	} else if (new_right_addr != null && !new_right_addr.Equals(_right_addr)) {
	  //its a changed right neighbor
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: New right  neighbor: {1}", 
			    our_addr, new_right_addr);
#endif

	  if (_right_transfer_state != null) {
	    _right_transfer_state.InterruptTransfer();
	  }
	  _right_transfer_state = new TransferState(_rpc, our_addr, new_right_addr,
						     _table.GetKeysToRight(new_right_addr, _right_addr));
	  _right_transfer_state.StartTransfer(TransferCompleteHandler);


	}
	_right_addr = new_right_addr;

      } //release lock on the connection table
      }
    }


    protected void DisconnectHandler(object contab, EventArgs eargs) 
    {
      ConnectionEventArgs cargs = eargs as ConnectionEventArgs;
      Connection old_con = cargs.Connection;
      AHAddress our_addr = _node.Address as AHAddress;


#if DHT_DEBUG
      Console.WriteLine("[DhtLogic] {0}: Lost a connection to: {1}.", our_addr, old_con.Address);
#endif 


      //first mke sure that it is a new StructuredConnection
      if (old_con.MainType != ConnectionType.Structured) {
#if DHT_DEBUG
	Console.WriteLine("[DhtLogic] {0} Not a structured connection, but {1}, ignore!", our_addr, old_con.ConType);
#endif 
	return;
      }
      ConnectionTable con_table = _node.ConnectionTable;

      AHAddress new_left_addr = null;
      AHAddress new_right_addr = null;
      
      lock(_sync) {
      lock(con_table.SyncRoot) {  //lock the connection table
	try {
	  Connection new_left_con = con_table.GetLeftStructuredNeighborOf(our_addr);
	  new_left_addr = new_left_con.Address as AHAddress;
	} catch (Exception e) {
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0} Error getting left neighbor information. ", our_addr);
#endif	  
	}	
	try {
	  Connection new_right_con = con_table.GetRightStructuredNeighborOf(our_addr);
	  new_right_addr = new_right_con.Address as AHAddress;
	} catch(Exception e) {
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0} Error getting right neighbor information. ", our_addr);
#endif	  
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
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Lost my only left neighbor: {1}", our_addr, _left_addr);
#endif
	  //there is nothing that we can do, it just went away.
	  if (_left_transfer_state != null) {
	    _left_transfer_state.InterruptTransfer();
	    _left_transfer_state = null;
	  }
	} else if (new_left_addr != null && !new_left_addr.Equals(_left_addr)) {
	  //its a changed left neighbor
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: New left  neighbor: {1}", our_addr, new_left_addr);
#endif
	  if (_left_transfer_state != null) {
	    _left_transfer_state.InterruptTransfer();
	  }
	  _left_transfer_state = new TransferState(_rpc, our_addr, new_left_addr,
						   _table.GetKeysToLeft(our_addr, _left_addr));
	  _left_transfer_state.StartTransfer(null);
	}
		   
	_left_addr = new_left_addr;
	

		   
	//2. check if the right neighbpor has changed
	if (new_right_addr == null && _right_addr != null) {
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Lost my only right neighbor: {1}", our_addr, _right_addr);
#endif
	  //nothing that we can do, the guy just went away.
	  if (_right_transfer_state != null) {
	    _right_transfer_state.InterruptTransfer();
	    _right_transfer_state = null;
	  }
	} else if (new_right_addr != null && !new_right_addr.Equals(_right_addr)) {
	  //its a changed right neighbor
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: New right  neighbor: {1}", our_addr, new_right_addr);
#endif
	  if (_right_transfer_state != null) {
	    _right_transfer_state.InterruptTransfer();
	  }
	  _right_transfer_state = new TransferState(_rpc, our_addr, new_right_addr,
						    _table.GetKeysToRight(our_addr, _right_addr));
	  _right_transfer_state.StartTransfer(null);
	}
	_right_addr = new_right_addr;
		   
      } //release the lock on connection table
      }
    }

    private void TransferCompleteHandler(TransferState state, Hashtable key_list) {
      //we also have to make sure that this transfer of keys is still valid
      lock(_sync) {
	AHAddress our_addr = _node.Address as AHAddress;
	//make sure that this transfer is still valid
	if (state == _left_transfer_state || state == _right_transfer_state) {
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: # of keys to delete: {1}", our_addr, key_list.Keys.Count);
#endif	  
	  _table.AdminDelete(key_list);
	} else {//otherwise this transfer is no longer valid
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Illegal transfer state. No actual deletion.");
#endif	  
	}
      }
    }
  } 
}
