/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

#define RPC_DEBUG
using System;
using System.IO;
using System.Collections;
using System.Reflection;

namespace Brunet {


/**
 * This class holds Rpc results and the packet that carried them.
 */
public class RpcResult {

  public RpcResult(Packet p, object res) {
    _packet = p;
    _result = res;
  }

  public RpcResult(Packet p, object res, ReqrepManager.Statistics stats) {
    _packet = p;
    _result = res;
    _statistics = stats;
  }

  //statistical information from the ReqreplyManager
  protected ReqrepManager.Statistics _statistics;
  public ReqrepManager.Statistics Statistics {
    get {
      return _statistics;
    }
  }
  protected Packet _packet;
  /**
   * This is the packet that carried the result.  Check this
   * if you want some header, address, ttl information, etc...
   */
  public Packet ResultPacket { get { return _packet; } }

  protected object _result;
  /**
   * Here is the object which is the result of the RPC call.
   * If it is an exception, accessing this property will throw
   * an exception.
   */
  public object Result {
    get {
      //If result is an exception, we throw here:
      if( _result is Exception ) { throw (Exception)_result; }
      return _result;
    }
  }
  
}
	
/**
 * This makes RPC over Brunet easier
 */
public class RpcManager : IReplyHandler, IRequestHandler {
 
  protected class InvocationRecord {
    private ReqrepManager _manager;
    private object _request;
    public ReqrepManager ReqRepManager {
      get {
	return _manager;
      }
    }
    public Object Request {
      get {
	return _request;
      }
    }
    public InvocationRecord(ReqrepManager man, Object req)
    {
      _manager = man;
      _request = req;
    }
  }
  /** Our Rpc system can make following types of calls.
   */
  public enum RpcRequestType: byte 
  {
    RpcKey = 1, //an invocation on P2P key (inexact invocation); in this case we expect two distinct replies back
    RpcNode = 2 //an invocation on a P2P node  (exact invocation)
  }

  protected class RpcRequestState {
    public RpcRequestType type;
    public BlockingQueue result_queue;
    public Address first_target;
  }
 
  protected Hashtable _method_handlers;
  protected Hashtable _method_packet_handlers;
  protected object _sync;
  protected ReqrepManager _rrman;
        
  protected RpcManager(ReqrepManager rrm) {

    _method_handlers = new Hashtable();
    _method_packet_handlers = new Hashtable();
    _sync = new Object();
    
    _rrman = rrm;
    //Listen for RPC messages
    rrm.Bind("rpc", this);
  }
  /** static hashtable to keep track of RpcManager objects. */
  protected static Hashtable _rpc_table = new Hashtable();
  /** 
   * Static method to create RpcManager objects
   * @param node The node we work for
   */
  public static RpcManager GetInstance(Node node) {
    lock(_rpc_table) {
      //check if there is already an instance object for this node
      if (_rpc_table.ContainsKey(node)) {
	return (RpcManager) _rpc_table[node];
      }
      //in case no instance exists, create one
      RpcManager rpc  = new RpcManager(ReqrepManager.GetInstance(node)); 
      _rpc_table[node] = rpc;
      return rpc;
    }
  }
   
  /**
   * When a method is called with "name.meth"
   * we look up the object with name "name"
   * and invoke the method "meth".
   * @param handler the object to handle the RPC calls
   * @param name the name exposed for this object.  RPC calls to "name."
   * come to this object.
   */
  public void AddHandler(string name, object handler)
  {
    lock( _sync ) {
      _method_handlers.Add(name, handler);
    }
  }
  /**
   * Allows to unregister existing handlers and register.
   */
  public void RemoveHandler(string name)
  {
    lock( _sync ) {
      _method_handlers.Remove(name);
    }
  }

  /**
   * This is the same as AddHandler, except
   * the last parameter of the method must
   * take a Packet
   *
   * Note that *ALL* methods must accept a Packet object
   * as their last method parameter in this case.  This
   * is for cases where we might want access to the sender
   * address, ttl, etc..
   * @param handler the object to handle the RPC calls
   * @param name the name exposed for this object.  RPC calls to "name."
   * come to this object.
   */
  public void AddHandlerP(string name, object handler)
  {
    lock( _sync ) {
      _method_packet_handlers.Add(name, handler);
    }
  }
  
  public void RemoveHandlerP(string name)
  {
    lock( _sync ) {
      _method_packet_handlers.Remove(name);
    }
  }
  /**
   * Implements the IReplyHandler (also provides some light-weight statistics)
   */
  public bool HandleReply(ReqrepManager man, ReqrepManager.ReqrepType rt,
			  int mid,
			  string prot,
			  System.IO.MemoryStream payload, AHPacket packet,
			  ReqrepManager.Statistics statistics, object state)
  {
    //Here
    object data = AdrConverter.Deserialize(payload);
    RpcRequestState rs = (RpcRequestState) state;
    BlockingQueue bq = rs.result_queue;

    bool retval = false;
    bool enqueue_result = false;

    //now we check if the request is finished or not
    if (bq.Closed) {
      return false;
    }
    //check if it is a RpcNode we are done
    if (rs.type == RpcRequestType.RpcNode) {
      if (rs.first_target == null) {
	rs.first_target = packet.Source;
	enqueue_result = true;
	retval = false;
      } else {
	Console.Error.WriteLine("How did we get here (a second result for a RpcNode)");
	enqueue_result = false;
	retval = false;
      }
    }
    if (rs.type == RpcRequestType.RpcKey) {
      //otherwise we see if this is a distinct reply
      if (rs.first_target == null) {//first reply
	rs.first_target = packet.Source;
	enqueue_result = true;
	retval = true;
      } else if (rs.first_target.Equals(packet.Source)) {//same reply
	//we should not actually do an enqueue here
	enqueue_result = false;
	retval = true;
      } else {
	//enqueue this for sure
	enqueue_result = true;
	retval = false;
      }
    } 
#if RPC_DEBUG    
    Console.Error.WriteLine("enqueue_packet: {0}", enqueue_result);    
    Console.Error.WriteLine("continue listening: {0}", retval);
#endif

    if (enqueue_result) {
      RpcResult res = new RpcResult(packet, data, statistics);
      bq.Enqueue(res);
    }
    return retval;
  }

  /**
   * When requests come in this handles it
   */
  public void HandleRequest(ReqrepManager man, ReqrepManager.ReqrepType rt,
                   object req,
                   string prot,
                   System.IO.MemoryStream payload, AHPacket packet)
  {
    Exception exception = null; 
#if RPC_DEBUG
    Console.Error.WriteLine("[RpcServer: {0}] Getting method invocation request at: {1}.",
                     _rrman.Node.Address, DateTime.Now);
#endif
    try {
      object data = AdrConverter.Deserialize(payload);
      IList l = data as IList;

      if( l == null ) {
        //We could not cast the request into a list... so sad:
	throw new AdrException(-32600,"method call not a list");
      }
      
      string methname = (string)l[0];
#if RPC_DEBUG
      Console.Error.WriteLine("[RpcServer: {0}] Getting invocation request,  method: {1}",
                     _rrman.Node.Address, methname);
#endif

      string[] parts = methname.Split('.');

      string hname = parts[0];
      string mname = parts[1];
      
      object handler = null;
      ArrayList pa = (ArrayList)l[1];
      lock( _sync ) {
        if( _method_handlers.ContainsKey( hname ) ) {
          handler = _method_handlers[ hname ];
        }
        else if( _method_packet_handlers.ContainsKey( hname ) ) {
          handler = _method_packet_handlers[ hname ];
          pa.Add(packet);
        }
        else {
          //No handler for this.
          throw new AdrException(-32601, "No Handler for method: " + methname);
        }
      }
      //Console.Error.WriteLine("About to call: {0}.{1} with args",handler, mname);
      //foreach(object arg in pa) { Console.Error.WriteLine("arg: {0}",arg); }
      MethodInfo mi = handler.GetType().GetMethod(mname);
      //make the following happen asynchronously in a separate thread
      //build an invocation record for the call
      InvocationRecord inv = new InvocationRecord(man, req);
      RpcMethodInvokeDelegate inv_dlgt = new RpcMethodInvokeDelegate(RpcMethodInvoke);
      inv_dlgt.BeginInvoke(inv, mi, handler, pa.ToArray(), 
			   new AsyncCallback(RpcMethodFinish),
			   inv_dlgt);
      //we have setup an asynchronous invoke here
    }
    catch(ArgumentException argx) {
      exception = new AdrException(-32602, argx);
    }
    catch(TargetParameterCountException argx) {
      exception = new AdrException(-32602, argx);
    }
    catch(Exception x) {
      exception = x;
    }
    if (exception != null) {
      //something failed even before invocation began
#if RPC_DEBUG
      Console.Error.WriteLine("[RpcServer: {0}] Something failed even before invocation began.",
                     _rrman.Node.Address);
#endif
      MemoryStream ms = new MemoryStream();
      AdrConverter.Serialize(exception, ms);
      man.SendReply( req, ms.ToArray() );
    }
  }
  
  /**
   * When an error comes in, this handles it
   */
  public void HandleError(ReqrepManager man, int message_number,
                   ReqrepManager.ReqrepError err, object state)
  {
    Exception x = null;
    RpcRequestState rs = (RpcRequestState) state;
    BlockingQueue bq = rs.result_queue;
    switch(err) {
        case ReqrepManager.ReqrepError.NoHandler:
          x = new AdrException(-32601, "No RPC Handler on remote host");
          break;
        case ReqrepManager.ReqrepError.HandlerFailure:
          x = new AdrException(-32603, "The remote RPC System had a problem");
          break;
        case ReqrepManager.ReqrepError.Timeout:
          //In this case we close the BlockingQueue:
          bq.Close();
          break;
    }
    if( x != null ) {
      ///@todo We need to include the packet here too, that means changing the HandleError
      ///interface
      RpcResult res = new RpcResult(null, x);
      bq.Enqueue(res);
    }
  }

  /**
   * This is how you invoke a method on a remote host.
   * Results are put into the BlockingQueue.
   *
   * When a result comes back, we put and RpcResult into the Queue.
   * 
   */
  public BlockingQueue Invoke(Address target,
                              string method,
                              params object[] args)
  {
    //build state for the RPC call
    RpcRequestState rs = new RpcRequestState();
    rs.type = RpcRequestType.RpcKey;
    rs.result_queue = new BlockingQueue();
    rs.first_target = null;

    ArrayList arglist = new ArrayList();
    arglist.AddRange(args);
    //foreach(object o in arglist) { Console.Error.WriteLine("arg: {0}",o); } 
    ArrayList rpc_call = new ArrayList();
    rpc_call.Add(method);
    rpc_call.Add(arglist);
    
    MemoryStream ms = new MemoryStream();
    AdrConverter.Serialize(rpc_call, ms);

#if RPC_DEBUG
    Console.Error.WriteLine("[RpcClient: {0}] Invoking method: {1} on target: {2}",
                     _rrman.Node.Address, method, target);
#endif
    
    _rrman.SendRequest(target, ReqrepManager.ReqrepType.Request,
                       "rpc", ms.ToArray(), this, rs);
    return rs.result_queue;
  }
  
  /**
   * This is how you invoke a method on a remote host (exact mode).
   * Results are put into the BlockingQueue.
   *
   * When a result comes back, we put and RpcResult into the Queue.
   * 
   */
  public BlockingQueue InvokeNode(Address target,
                              string method,
                              params object[] args)
  {
    //build state for the RPC call
    RpcRequestState rs = new RpcRequestState();
    rs.type = RpcRequestType.RpcNode;
    rs.result_queue = new BlockingQueue();
    rs.first_target = null;

    ArrayList arglist = new ArrayList();
    arglist.AddRange(args);
    //foreach(object o in arglist) { Console.Error.WriteLine("arg: {0}",o); } 
    ArrayList rpc_call = new ArrayList();
    rpc_call.Add(method);
    rpc_call.Add(arglist);
    
    MemoryStream ms = new MemoryStream();
    AdrConverter.Serialize(rpc_call, ms);

#if RPC_DEBUG
    Console.Error.WriteLine("[RpcClient: {0}] Invoking method: {1} on target: {2}",
                     _rrman.Node.Address, method, target);
#endif
    
    _rrman.SendExactRequest(target, ReqrepManager.ReqrepType.Request,
                       "rpc", ms.ToArray(), this, rs);
    return rs.result_queue;
  }

  protected void RpcMethodInvoke(InvocationRecord inv, MethodInfo mi, Object handler, 
				 Object[] param_list) {
    ReqrepManager man = inv.ReqRepManager;
    Object req = inv.Request;

    Object result = null;
    try {
#if RPC_DEBUG
      Console.Error.WriteLine("[RpcServer: {0}] Invoking method: {1}", _rrman.Node.Address, mi);
#endif
      result = mi.Invoke(handler, param_list);
    } catch(ArgumentException argx) {
#if RPC_DEBUG
      Console.Error.WriteLine("[RpcServer: {0}] Argument exception. {1}", _rrman.Node.Address, mi);
#endif
      result = new AdrException(-32602, argx);
    }
    catch(TargetParameterCountException argx) {
#if RPC_DEBUG
      Console.Error.WriteLine("[RpcServer: {0}] Parameter count exception. {1}", _rrman.Node.Address, mi);
#endif
      result = new AdrException(-32602, argx);
    }
    catch(TargetInvocationException x) {
#if RPC_DEBUG
      Console.Error.WriteLine("[RpcServer: {0}] Exception thrown by method: {1}, {2}", _rrman.Node.Address, mi, x.InnerException.Message);
#endif
      result = new AdrException(-32608, x.InnerException);
    }
    catch(Exception x) {
#if RPC_DEBUG
      Console.Error.WriteLine("[RpcServer: {0}] General exception. {1}", _rrman.Node.Address, mi);
#endif
      result = x;
    }
    finally {
      MemoryStream ms = new MemoryStream();
      AdrConverter.Serialize(result, ms);
      man.SendReply( req, ms.ToArray() );    
    }
  }
  
  protected void RpcMethodFinish(IAsyncResult ar) {
    RpcMethodInvokeDelegate  dlgt = (RpcMethodInvokeDelegate) ar.AsyncState;
    //call EndInvoke to do cleanup
    //ideally no exception should be thrown, since the delegate catches everything
    dlgt.EndInvoke(ar);
  }
  
  /** We need to do the method invocation in a thread from the thrread pool. 
   */
  protected delegate void RpcMethodInvokeDelegate(InvocationRecord inv, MethodInfo mi, 
						  Object handler, 
						  Object[] param_list);
}
}
