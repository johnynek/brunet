/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2006,2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

  public RpcResult(ISender ret_path, object res) {
    _ret_path = ret_path;
    _result = res;
  }

  public RpcResult(ISender ret_path, object res, ReqrepManager.Statistics stats) {
    _ret_path = ret_path;
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
  protected ISender _ret_path;
  /**
   * This is a ISender that can send to the Node that
   * sent this result.
   */
  public ISender ResultSender { get { return _ret_path; } }

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
public class RpcManager : IReplyHandler, IDataHandler {
 
  protected class RpcRequestState {
    public BlockingQueue result_queue;
  }
 
  protected Hashtable _method_handlers;
  protected object _sync;
  protected ReqrepManager _rrman;
        
  protected RpcManager(ReqrepManager rrm) {

    _method_handlers = new Hashtable();
    _sync = new Object();
    
    _rrman = rrm;
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
      node.GetTypeSource( PType.Protocol.Rpc ).Subscribe(rpc, node);
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
   * Allows to unregister existing handlers.
   */
  public void RemoveHandler(string name)
  {
    lock( _sync ) {
      _method_handlers.Remove(name);
    }
  }

  /**
   * Implements the IReplyHandler (also provides some light-weight statistics)
   */
  public bool HandleReply(ReqrepManager man, ReqrepManager.ReqrepType rt,
			  int mid, PType prot, MemBlock payload, ISender ret_path,
			  ReqrepManager.Statistics statistics, object state)
  {
    object data = AdrConverter.Deserialize(payload);
    RpcRequestState rs = (RpcRequestState) state;
    BlockingQueue bq = rs.result_queue;

    if (!bq.Closed) {
      RpcResult res = new RpcResult(ret_path, data, statistics);
      bq.Enqueue(res);
    }
    //Keep listening unless the queue is closed
    return (!bq.Closed);
  }

  /**
   * When requests come in this handles it
   */
  public void HandleData(MemBlock payload, ISender ret_path, object state)
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
      RpcMethodInvokeDelegate inv_dlgt = this.RpcMethodInvoke;
      inv_dlgt.BeginInvoke(ret_path, mi, handler, pa.ToArray(), 
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
      Console.Error.WriteLine("[RpcServer: {0}] Something failed even before invocation began: {1}",
                     _rrman.Node.Address, exception);
#endif
      MemoryStream ms = new MemoryStream();
      AdrConverter.Serialize(exception, ms);
      ret_path.Send( new CopyList( PType.Protocol.Rpc, MemBlock.Reference( ms.ToArray() ) ) );
    }
  }
  
  /**
   * When an error comes in, this handles it
   */
  public void HandleError(ReqrepManager man, int message_number,
                   ReqrepManager.ReqrepError err, ISender ret_path, object state)
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
      RpcResult res = new RpcResult(ret_path, x);
      bq.Enqueue(res);
    }
  }

  /**
   * This is how you invoke a method on a remote host.
   * Results are put into the BlockingQueue.
   *
   * When a result comes back, we put and RpcResult into the Queue.
   * When you have enough responses, Close the queue (please).
   * 
   */
  public BlockingQueue Invoke(ISender target,
                              string method,
                              params object[] args)
  {
    //build state for the RPC call
    RpcRequestState rs = new RpcRequestState();
    rs.result_queue = new BlockingQueue();

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
    ICopyable rrpayload = new CopyList( PType.Protocol.Rpc, MemBlock.Reference(ms.ToArray()) ); 
    _rrman.SendRequest(target, ReqrepManager.ReqrepType.Request, rrpayload, this, rs);
    return rs.result_queue;
  }
  
  protected void RpcMethodInvoke(ISender ret_path, MethodInfo mi, Object handler, 
				 Object[] param_list) {
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
      ret_path.Send( new CopyList( PType.Protocol.Rpc, MemBlock.Reference( ms.ToArray() ) ) );
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
  protected delegate void RpcMethodInvokeDelegate(ISender return_path, MethodInfo mi, 
						  Object handler, 
						  Object[] param_list);
}
}
