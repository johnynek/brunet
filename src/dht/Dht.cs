using System;
using System.Text;
using System.Collections;
using System.Security.Cryptography;

#if DHT_LOG
using log4net;
using log4net.Config;
#endif

using Brunet;
using Brunet.Dht;

namespace Brunet.Dht {	
  public class DhtException: Exception {
    public DhtException(string message): base(message) {}
  }

  public class Dht {
#if DHT_LOG
    private static readonly log4net.ILog _log =
    log4net.LogManager.GetLogger(System.Reflection.MethodBase.
				 GetCurrentMethod().DeclaringType);
#endif

    //we checkpoint the state of DHT every 1000 seconds.
    private static readonly int _CHECKPOINT_INTERVAL = 1000;

    //when should next checkpoint be done.
    private DateTime _next_checkpoint;
    
    //lock for the Dht
    protected object _sync;
    //the we are attached to
    protected RpcManager _rpc;
    //node we are associated with
    protected Node _node = null;

    //flag indicating if we are ready to perform the DHT logic
    protected bool _activated = false;
    public bool Activated {
      get {
	return _activated;
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

      public bool ToDelete {
	get {
	  return _to_delete;
	}
      }

      protected AHAddress _target;
      public AHAddress Target {
	get {
	  return _target;
	}
      }
      
      protected Hashtable _key_list;
      

      protected BlockingQueue _driver_queue = null;
      protected IEnumerator _entry_enumerator = null;

      protected TransferCompleteCallback _tcb = null;
      
      public TransferState(RpcManager rpcman, AHAddress our_addr, AHAddress target, Hashtable key_list,
			   bool to_delete) 
      {
	_sync = new object();
	_rpc = rpcman;
	_our_addr = our_addr;
	_target = target;
	_key_list = key_list;
	_to_delete = to_delete;
	_entry_enumerator = GetEntryEnumerator();
#if DHT_DEBUG
	Console.WriteLine("[DhtLogic] {0}: Creating a new transfer state to: {1}, # of keys: {2}, to_delete: {3}. ", 
			  _our_addr, _target, _key_list.Count, _to_delete);
#endif

      }

      public IEnumerator GetEntryEnumerator() {
	foreach (MemBlock k in _key_list.Keys) {
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
	  Console.WriteLine("[DhtLogic] {0}: StartTransfer. Getting the next value to transfer.", 
			    _our_addr);
#endif

	  if (_entry_enumerator.MoveNext()) {
	    Entry e = (Entry) _entry_enumerator.Current;
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Found a value. Making an Put() on key: {1} call to target: {2}",
			      _our_addr,
			      Base32.Encode(e.Key),
			      _target);
#endif
	    TimeSpan t_span = e.EndTime - DateTime.Now;
	    //Console.WriteLine("Endtime: {0}, Current: {1}", e.EndTime, DateTime.Now);
	    //Console.WriteLine("TTL for transferred value: {0}", (int) t_span.TotalSeconds);

	    _driver_queue = _rpc.InvokeNode(_target, "dht.Put", e.Key, 
					(int) t_span.TotalSeconds, 
					e.Password, 
					e.Data);
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Returning non-blocking Put() call to: {1}",
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
	  Console.WriteLine("[DhtLogic] {0}: NextTransfer.Finished transferring a value. ", _our_addr);
#endif
	  BlockingQueue q =  (BlockingQueue) o;
	  try {
	    RpcResult res = q.Dequeue() as RpcResult;
	    q.Close();
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Return value from transfer: {1}.", _our_addr, res.Result);
#endif

	  } catch (Exception e) {
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Return of Put() was an exception: {1}", _our_addr, e);
#endif
	  }
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Now see if there is another key to transfer. ", _our_addr);
#endif	  
	  
	  //unregister any future enqueue events
	  q.EnqueueEvent -= new EventHandler(NextTransfer);
	  
	  //close the queue
	  q.Close();
	  
	  //initiate next transfer
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Getting the next value to transfer to: {1}.", _our_addr, _target);
#endif
	  
	  if (_entry_enumerator.MoveNext()) {
	    Entry e = (Entry) _entry_enumerator.Current;
	    TimeSpan t_span = e.EndTime - DateTime.Now;
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Found a value. Making a Put() on key: {1} call to target: {2}",
			      _our_addr,
			      Base32.Encode(e.Key),
			      _target);
#endif

	    _driver_queue = _rpc.InvokeNode(_target, "dht.Put", e.Key, 
					(int) t_span.TotalSeconds,
					e.Password, 
					e.Data);
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: . Returning non-blocking Put() call to: {1}",
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
    public Hashtable All {
      get {
	return _table.GetAll();
      }
    }
    
    public Dht(Node node, EntryFactory.Media media) {
      _sync = new object();
      _node = node;
      //activated not until we acquire a connection
      _activated = false;

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
	
	//node.HeartBeatEvent += new EventHandler(CheckpointHandler);
      }
    }

    public static byte[] MapToRing(byte[] key) {
      HashAlgorithm hashAlgo = HashAlgorithm.Create();
      byte[] hash = hashAlgo.ComputeHash(key);
      Address.SetClass(hash, AHAddress._class);
      return hash;
    }
    
    public BlockingQueue Put(byte[] key, int ttl, string hashed_password, byte[] data) {
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invoking a Dht::Put()");
#endif
      if (!_activated) {
#if DHT_DEBUG
	Console.WriteLine("[DhtClient] Not yet activated. Throwing exception!");
#endif	
	throw new DhtException("DhtClient: Not yet activated.");
      }

      byte[] b = MapToRing(key);
      Address target = new AHAddress(b);
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invocation target: {0}", target);
#endif


#if DHT_LOG
      _log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::InvokePut::::" +
		 + Base32.Encode(b) + "::::" + target);
#endif
      //we now know the invocation target
      BlockingQueue q = _rpc.Invoke(target, "dht.Put", b, ttl, hashed_password, data);
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Returning a blocking queue..");
#endif
      return q;
    }

    public BlockingQueue Create(byte[] key, int ttl, string hashed_password, byte[] data) {
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invoking a Dht::Create()");
#endif

      if (!_activated) {
#if DHT_DEBUG
	Console.WriteLine("[DhtClient] Not yet activated. Throwing exception!");
#endif	
	throw new DhtException("DhtClient: Not yet activated.");
      }
      
      byte[] b = MapToRing(key);
      Address target = new AHAddress(b);
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invocation target: {0}", target);
#endif

#if DHT_LOG
      _log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::InvokeCreate::::" +
		 + Base32.Encode(b) + "::::" + target);
#endif
      
      //we now know the invocation target
      BlockingQueue q = _rpc.Invoke(target, "dht.Create", b, ttl, hashed_password, data);
      return q;
    }

    public BlockingQueue Recreate(byte[] key, int ttl, string hashed_password, byte[] data) {
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invoking a Dht::Recreate()");
#endif

      if (!_activated) {
#if DHT_DEBUG
	Console.WriteLine("[DhtClient] Not yet activated. Throwing exception!");
#endif	
	throw new DhtException("DhtClient: Not yet activated.");
      }
      byte[] b= MapToRing(key);
      Address target = new AHAddress(b);
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invocation target: {0}", target);
#endif


#if DHT_LOG
      _log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::InvokeRecreate::::" +
		 + Base32.Encode(b) + "::::" + target);
#endif
      
      //we now know the invocation target
      BlockingQueue q = _rpc.Invoke(target, "dht.Recreate", b, ttl, hashed_password, data);
      return q;
    }
    
    public BlockingQueue Get(byte[] key, int maxbytes, byte[] token) {
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invoking a Dht::Get()");
#endif
      if (!_activated) {
#if DHT_DEBUG
	Console.WriteLine("[DhtClient] Not yet activated. Throwing exception!");
#endif	
	throw new DhtException("DhtClient: Not yet activated.");
      }

      byte[] b = MapToRing(key);
      Address target = new AHAddress(b);
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invocation target: {0}", target);
#endif


#if DHT_LOG
      _log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::InvokeGet::::" +
		 + Base32.Encode(b) + "::::" + target);
#endif      
      //we now know the invocation target
      BlockingQueue q = _rpc.Invoke(target, "dht.Get", b, maxbytes, token);
      return q;
    }
    public BlockingQueue Delete(byte[] key, string password)
    {  
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invoking a Dht::Delete()");
#endif

      if (!_activated) {
#if DHT_DEBUG
	Console.WriteLine("[DhtClient] Not yet activated. Throwing exception!");
#endif	
	throw new DhtException("DhtClient: Not yet activated.");
      }

      byte[] b = MapToRing(key);
      Address target = new AHAddress(b);
#if DHT_DEBUG
      Console.WriteLine("[DhtClient] Invocation target: {0}", target);
#endif


#if DHT_LOG
      _log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::InvokeDelete::::" +
		 + Base32.Encode(b) + "::::" + target);
#endif
      
      //we now know the invocation target
      BlockingQueue q = _rpc.Invoke(target, "dht.Delete", b, password);
      return q;
    }
    
    protected void ConnectHandler(object contab, EventArgs eargs) 
    {
      ConnectionEventArgs args = (ConnectionEventArgs)eargs;
      Connection new_con = args.Connection;
      //AHAddress new_addr = new_con.Address as AHAddress;
      
      AHAddress our_addr = _node.Address as AHAddress;
#if DHT_LOG
      string status = "StatusBegin";
      StatusMessage sm = new_con.Status;
      ArrayList arr = sm.Neighbors;
      foreach (NodeInfo n_info in arr) {
	AHAddress stat_addr = n_info.Address as AHAddress;
	status += ("::::" + stat_addr);
      }
      status += "::::StatusEnd";
      _log.Debug(our_addr + "::::" + DateTime.UtcNow.Ticks + "::::Connection::::" +
		 new_con.ConType + "::::" + new_con.Address + "::::" +
		 new_con.Edge.LocalTA.ToString() + "::::" + new_con.Edge.RemoteTA.ToString() + "::::" + status + "::::Connected::::" +
		 _node.IsConnected);
#endif

      
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
	//we need to check if we are ready for DhtLogic
	if (!_activated) {
	  if (_node.IsConnected ) {
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Activated (on connection) at time: {1}.", our_addr, DateTime.Now);
	    try {
	      Console.WriteLine("Activated left: {0}", con_table.GetLeftStructuredNeighborOf(our_addr));
	    } catch(Exception e) {
	      Console.WriteLine(e);
	    }
	    try {
	      Console.WriteLine("Activated right: {0}", con_table.GetRightStructuredNeighborOf(our_addr));
	    } catch(Exception e) {
	      Console.WriteLine(e);
	    } 
#endif	
	    _activated = true;
	  } 
	}

	try {
	  Connection new_left_con = con_table.GetLeftStructuredNeighborOf(our_addr);
	  new_left_addr = new_left_con.Address as AHAddress;
	} catch (Exception e) {
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Error getting left neighbor information. ", our_addr);
	  Console.WriteLine("[DhtLogic] {0}, exception: {1}", our_addr, e);
#endif	  
	}
	try {
	  Connection new_right_con = con_table.GetRightStructuredNeighborOf(our_addr);
	  new_right_addr = new_right_con.Address as AHAddress;
	} catch(Exception e) {
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Error getting right neighbor information. ", our_addr);
	  Console.WriteLine("[DhtLogic] {0}, exception: {1}", our_addr, e);
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
	    if(_activated) {
	      _left_transfer_state = new TransferState(_rpc, our_addr, new_left_addr,
						       _table.GetKeysToLeft(our_addr, new_left_addr), 
						       false);
	      _left_transfer_state.StartTransfer(TransferCompleteHandler);
	    } else {
#if DHT_DEBUG
	      Console.WriteLine("[DhtLogic] {0}: Not activated (don't do any transfers).", our_addr);
#endif
	    }
	    
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
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Interrupt an existing left transfer to: {1}. ", 
			      our_addr, _left_transfer_state.Target);
#endif
	    _left_transfer_state.InterruptTransfer();
	  }
	  if (_activated) {
	    _left_transfer_state = new TransferState(_rpc, our_addr, new_left_addr,
						     _table.GetKeysToLeft(new_left_addr, _left_addr), 
						     true);
	    _left_transfer_state.StartTransfer(TransferCompleteHandler);
	  } else {
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Not activated (don't do any transfers).", our_addr);
#endif
	  }
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
	    if (_activated) {
	      //pass on some keys to him now
	      _right_transfer_state = new TransferState(_rpc, our_addr, new_right_addr,
							_table.GetKeysToRight(our_addr, new_right_addr), 
							false);
	      _right_transfer_state.StartTransfer(TransferCompleteHandler);
	    } else {
#if DHT_DEBUG
	      Console.WriteLine("[DhtLogic] {0}: Not activated (don't do any transfers).", our_addr);
#endif
	    }
	    
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
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Interrupt an existing right transfer to: {1}. ", 
			      our_addr, _right_transfer_state.Target);
#endif
	    _right_transfer_state.InterruptTransfer();
	  }
	  if (_activated) {
	    _right_transfer_state = new TransferState(_rpc, our_addr, new_right_addr,
						      _table.GetKeysToRight(new_right_addr, _right_addr), 
						      true);
	    _right_transfer_state.StartTransfer(TransferCompleteHandler);
	  } else {
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Not activated (don't do any transfers).", our_addr);
#endif
	  }
	}
	_right_addr = new_right_addr;
      } //release lock on the connection table
      }//release out own lock
    }


    protected void DisconnectHandler(object contab, EventArgs eargs) 
    {
      ConnectionEventArgs cargs = eargs as ConnectionEventArgs;
      Connection old_con = cargs.Connection;
      AHAddress our_addr = _node.Address as AHAddress;

#if DHT_LOG
      _log.Debug(our_addr + "::::" + DateTime.UtcNow.Ticks + "::::Disconnection::::" +
		 old_con.ConType + "::::" + old_con.Address + "::::" +
		 old_con.Edge.LocalTA.ToString() + "::::" + old_con.Edge.RemoteTA.ToString() + "::::Connected::::"  +
		 _node.IsConnected);
#endif


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
	//we need to check if we can Put() our keys away.
	//we only do that if we have sufficient number of connections
	if (!_activated) {
	  if (_node.IsConnected ) {
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Activated (on disconnection) at time: {1}.", our_addr, DateTime.Now);
	    try {
	      Console.WriteLine("Activated left: {0}", con_table.GetLeftStructuredNeighborOf(our_addr));
	    } catch(Exception e) {
	      Console.WriteLine(e);
	    }
	    try {
	      Console.WriteLine("Activated right: {0}", con_table.GetRightStructuredNeighborOf(our_addr));
	    } catch(Exception e) {
	      Console.WriteLine(e);
	    }
#endif	
	    _activated = true;
	  } 
	}
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
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Interrupt an existing left transfer to: {1}. ", 
			      our_addr, _left_transfer_state.Target);
#endif
	    _left_transfer_state.InterruptTransfer();
	    _left_transfer_state = null;
	  }
	} else if (new_left_addr != null && !new_left_addr.Equals(_left_addr)) {
	  //its a changed left neighbor
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: New left  neighbor: {1}", our_addr, new_left_addr);
#endif
	  if (_left_transfer_state != null) {
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Interrupt an existing left transfer to: {1}. ", 
			      our_addr, _left_transfer_state.Target);
#endif
	    _left_transfer_state.InterruptTransfer();
	  }
	  if (_activated) {
	    _left_transfer_state = new TransferState(_rpc, our_addr, new_left_addr,
						     _table.GetKeysToLeft(our_addr, _left_addr), 
						     false);
	    _left_transfer_state.StartTransfer(TransferCompleteHandler);
	  } else {
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Not activated (don't do any transfers).", our_addr);
#endif
	  }
	}
		   
	_left_addr = new_left_addr;
	

		   
	//2. check if the right neighbpor has changed
	if (new_right_addr == null && _right_addr != null) {
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Lost my only right neighbor: {1}", our_addr, _right_addr);
#endif
	  //nothing that we can do, the guy just went away.
	  if (_right_transfer_state != null) {
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Interrupt an existing right transfer to: {1}. ", 
			      our_addr, _right_transfer_state.Target);
#endif
	    _right_transfer_state.InterruptTransfer();
	    _right_transfer_state = null;
	  }
	} else if (new_right_addr != null && !new_right_addr.Equals(_right_addr)) {
	  //its a changed right neighbor
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: New right  neighbor: {1}", our_addr, new_right_addr);
#endif
	  if (_right_transfer_state != null) {
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Interrupt an existing right transfer to: {1}. ", 
			      our_addr, _right_transfer_state.Target);
#endif
	    _right_transfer_state.InterruptTransfer();
	  }
	  if (_activated) {
	    _right_transfer_state = new TransferState(_rpc, our_addr, new_right_addr,
						      _table.GetKeysToRight(our_addr, _right_addr), 
						      false);
	    _right_transfer_state.StartTransfer(TransferCompleteHandler);
	  }else {
#if DHT_DEBUG
           Console.WriteLine("[DhtLogic] {0}: Not activated (don't do any transfers).", our_addr);
#endif
          }
	  
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
	if (state == _left_transfer_state ) { 
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: # of keys to delete: {1}", our_addr, key_list.Keys.Count);
#endif	  
	  if (state.ToDelete) {
	    _table.AdminDelete(key_list);
	  }
	  //we also have to reset the transfer state
	  _left_transfer_state = null;
	} else if (state == _right_transfer_state) {
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: # of keys to delete: {1}", our_addr, key_list.Keys.Count);
#endif	  
	  if (state.ToDelete) {
	    _table.AdminDelete(key_list);	  
	  }
	  _right_transfer_state = null;
	} else {//otherwise this transfer is no longer valid
#if DHT_DEBUG
	  Console.WriteLine("[DhtLogic] {0}: Illegal transfer state. No actual deletion.");
#endif	  
	}
      }
    }
    /** 
     *  This method Checkpoints the table state periodically.
     *  (Still an incomplete implementation!)
     */
    public void CheckpointHandler(object node, EventArgs eargs) {
      lock(_sync) {
	if (DateTime.Now > _next_checkpoint) {
	  
	  TimeSpan interval = new TimeSpan(0,0,0,0, _CHECKPOINT_INTERVAL);
	  _next_checkpoint = DateTime.Now + interval; 
	}
      }    
    }
    protected void StatusChangedHandler(object contab, EventArgs eargs) {
      ConnectionEventArgs args = (ConnectionEventArgs)eargs;
      Connection new_con = args.Connection;

      ConnectionTable con_table = _node.ConnectionTable;
      AHAddress our_addr = _node.Address as AHAddress;
      lock(con_table.SyncRoot) {  //lock the connection table
	if (!_activated) {
	  if (_node.IsConnected ) {
#if DHT_DEBUG
	    Console.WriteLine("[DhtLogic] {0}: Activated (on status change) at time: {1}.", our_addr, DateTime.Now);
	    try {
	      Console.WriteLine("Activated left: {0}", con_table.GetLeftStructuredNeighborOf(our_addr));
	    } catch(Exception e) {
	      Console.WriteLine(e);
	    } try {
	      Console.WriteLine("Activated right: {0}", con_table.GetRightStructuredNeighborOf(our_addr));
	    } catch(Exception e) {
	      Console.WriteLine(e);
	    }
#endif	
	    _activated = true;
	  } 
	}
      }
// #if DHT_LOG
//       string status = "StatusBegin";
//       StatusMessage sm = new_con.Status;
//       ArrayList arr = sm.Neighbors;
//       foreach (NodeInfo n_info in arr) {
// 	AHAddress stat_addr = n_info.Address as AHAddress;
// 	status += ("::::" + stat_addr);
//       }
//       status += "::::StatusEnd";
//       _log.Debug(our_addr + "::::" + DateTime.UtcNow.Ticks + "::::StatusChanged::::" +
// 		 new_con.ConType + "::::" + new_con.Address + "::::" +
// 		 new_con.Edge.LocalTA.ToString() + "::::" + new_con.Edge.RemoteTA.ToString() + "::::" + status + "::::Connected::::" +
// 		 _node.IsConnected);
// #endif
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
