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

//#define RPC_DEBUG
//#define DAVID_ASYNC_INVOKE
using System;
using System.IO;
using System.Collections;
using System.Reflection;
using System.Threading;
using Brunet.Collections;
using Brunet.Util;
using Brunet.Concurrent;

namespace Brunet.Messaging {
public interface IRpcHandler {

  /**
   * When you're done with the method call use:
   * RpcManager.SendResult(request_state, result);
   * @param caller the ISender that sends to the point that made the RPC call
   * @param method the part after the first "." in the method call
   * @param arguments a list of arguments passed
   * @param request_state used to send the response via RpcManager.SendResult
   */
  void HandleRpc(ISender caller, string method, IList arguments, object request_state);

}

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

  /** If the Result in an Exception, throw it, otherwise do nothing
   */
  public void AssertNotException() {
    //If result is an exception, we throw here:
    if( _result is Exception ) { throw (Exception)_result; }
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
   * This is a ISender that can send to the point that
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
      AssertNotException();
      return _result;
    }
  }
}
	
/**
 * This makes RPC over Brunet easier
 */
public class RpcManager : IReplyHandler, IDataHandler {
 
  protected class RpcRequestState {
    public Channel Results;
    public ISender RpcTarget;
  }

  protected object _sync;
  protected ReqrepManager _rrman;
  ///Holds a cache of method string names to MethodInfo
  protected readonly Cache _method_cache;
  protected const int CACHE_SIZE = 128;
  
  //Here are the methods that don't want the return_path
  protected Hashtable _method_handlers;

#if DAVID_ASYNC_INVOKE
  protected BlockingQueue _rpc_command;
  protected Thread _rpc_thread;
#endif

  /**
   * This is the "standard" RpcHandler for a set of objects.
   */
  protected class ReflectionRpcHandler : IRpcHandler {
    protected readonly RpcManager _rpc;
    protected readonly object _handler;
    protected readonly Type _type;
    protected readonly Cache _method_cache;
    protected readonly object _sync;
    protected bool _use_sender;

    public ReflectionRpcHandler(RpcManager rpc, object handler, bool use_sender) {
      _rpc = rpc;
      _handler = handler;
      _type = _handler.GetType();
      _use_sender = use_sender;
      _sync = new object();
      //Cache the 10 most used methods:
      _method_cache = new Cache(10);
    }

    public void HandleRpc(ISender caller, string methname, IList arguments, object request_state) {
      MethodInfo mi = null;
      /*
       * Lookup this method name in our table.
       * This uses a cache, so it should be fast
       * after the first time
       */
      lock( _sync ) {
        mi = (MethodInfo) _method_cache[methname];
        if( mi == null ) {
          mi = _type.GetMethod(methname);
          _method_cache[ methname ] = mi;
        }
      }
      
      if( _use_sender ) {
        arguments = new ArrayList(arguments);
        arguments.Add( caller );
      }
      object[] arg_array = new object[ arguments.Count ];
      arguments.CopyTo(arg_array, 0);
      //Console.Error.WriteLine("About to call: {0}.{1} with args",handler, mname);
      //foreach(object arg in pa) { Console.Error.WriteLine("arg: {0}",arg); }
      //make the following happen asynchronously in a separate thread
      //build an invocation record for the call
      Object result = null;
      try {
  #if RPC_DEBUG
        Console.Error.WriteLine("[RpcServer: {0}] Invoking method: {1}", _rrman.Info, mi);
  #endif
        result = mi.Invoke(_handler, arg_array);
      } catch(ArgumentException argx) {
  #if RPC_DEBUG
        Console.Error.WriteLine("[RpcServer: {0}] Argument exception. {1}", _rrman.Info, mi);
  #endif
        result = new AdrException(-32602, argx);
      }
      catch(TargetParameterCountException argx) {
  #if RPC_DEBUG
        Console.Error.WriteLine("[RpcServer: {0}] Parameter count exception. {1}", _rrman.Info, mi);
  #endif
        result = new AdrException(-32602, argx);
      }
      catch(TargetInvocationException x) {
  #if RPC_DEBUG
        Console.Error.WriteLine("[RpcServer: {0}] Exception thrown by method: {1}, {2}", _rrman.Info, mi, x.InnerException.Message);
  #endif
        if( x.InnerException is AdrException ) {
          result = x.InnerException;
        }
        else {
          result = new AdrException(-32608, x.InnerException);
        }
      }
      catch(Exception x) {
  #if RPC_DEBUG
        Console.Error.WriteLine("[RpcServer: {0}] General exception. {1}", _rrman.Info, mi);
  #endif
        result = x;
      }
      finally {
        _rpc.SendResult(request_state, result);
      }
    }
  }

  public RpcManager(ReqrepManager rrm) {
    _sync = new Object();
    _rrman = rrm;
    _method_cache = new Cache(CACHE_SIZE);
    _method_handlers = new Hashtable();

#if DAVID_ASYNC_INVOKE
    _rpc_command = new BlockingQueue();
    _rpc_thread = new Thread(RpcCommandRun);
    _rpc_thread.IsBackground = true;
    _rpc_thread.Start();
#endif
  }

  /**
   * When a method is called with "name.meth"
   * we look up the object with name "name"
   * and invoke the method "meth".
   * @param handler the object to handle the RPC calls, if
   *        this is not an IRpcHandler, then method calls are looked up
   *        using .Net reflection: anything after the first "." will be
   *        used to look up the method.  If this is an IRpcHandler, the
   *        Handle method will be called when 
   * @param name_space the name exposed for this object. 
   *        RPC calls to "<name_space>.<method>"
   * come to this object.
   */
  public void AddHandler(string name_space, object handler)
  {
    lock( _sync ) {
      IRpcHandler h = handler as IRpcHandler;
      if( h == null ) {
        h = new ReflectionRpcHandler(this, handler, false);
      }
      _method_handlers.Add(name_space, h);
      _method_cache.Clear();
    }
  }
  /**
   * Allows to unregister existing handlers.
   */
  public void RemoveHandler(string name)
  {
    lock( _sync ) {
      _method_handlers.Remove(name);
      _method_cache.Clear();
    }
  }
  /**
   * When a method is called with "name.meth"
   * we look up the object with name "name"
   * and invoke the method "meth".
   * The method's last parameter MUST be an ISender object
   *
   * @param handler the object to handle the RPC calls
   * @param name the name exposed for this object.  RPC calls to "name."
   * come to this object.
   */
  public void AddHandlerWithSender(string name, object handler)
  {
    lock( _sync ) {
      IRpcHandler h = new ReflectionRpcHandler(this, handler, true);
      _method_handlers.Add(name, h);
      _method_cache.Clear();
    }
  }

  /**
   * Implements the IReplyHandler (also provides some light-weight statistics)
   */
  public bool HandleReply(ReqrepManager man, ReqrepManager.ReqrepType rt,
			  int mid, PType prot, MemBlock payload, ISender ret_path,
			  ReqrepManager.Statistics statistics, object state)
  {
    RpcRequestState rs = (RpcRequestState) state;
    //ISender target = rs.RpcTarget;
    Channel bq = rs.Results;
    if( bq != null ) {
      object data = AdrConverter.Deserialize(payload);
      RpcResult res = new RpcResult(ret_path, data, statistics);
      bq.Enqueue(res);
      //Keep listening unless the queue is closed
      return (!bq.Closed);
    }
    else {
      //If they didn't even pass us a queue, I guess they didn't want to
      //listen too long
      return false;
    }
  }

  /**
   * When requests come in this handles it
   */
  public void HandleData(MemBlock payload, ISender ret_path, object state)
  {
    Exception exception = null; 
#if RPC_DEBUG
    Console.Error.WriteLine("[RpcServer: {0}] Getting method invocation request at: {1}.",
                     _rrman.Info, DateTime.Now);
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
                     _rrman.Info, methname);
#endif
      
      /*
       * Lookup this method name in our table.
       * This uses a cache, so it should be fast
       * after the first time
       */
      IRpcHandler handler = null;
      string mname = null;
      lock( _sync ) {
        object[] info = (object[]) _method_cache[methname];
        if( info == null ) {
          int dot_idx = methname.IndexOf('.');
          if( dot_idx == -1 ) {
            throw new AdrException(-32601, "No Handler for method: " + methname);
          }
          string hname = methname.Substring(0, dot_idx);
          //Skip the '.':
          mname = methname.Substring(dot_idx + 1);

          handler = (IRpcHandler)_method_handlers[ hname ];
          if( handler == null ) {
            //No handler for this.
            throw new AdrException(-32601, "No Handler for method: " + methname);
          }
          info = new object[2];
          info[0] = handler;
          info[1] = mname;
          _method_cache[ methname ] = info;
        }
        else {
          handler = (IRpcHandler)info[0];
          mname = (string)info[1];
        }
      }

      ArrayList pa = (ArrayList)l[1];
#if DAVID_ASYNC_INVOKE
      object[] odata = new object[4];
      odata[0] = handler;
      odata[1] = ret_path;
      odata[2] = mname;
      odata[3] = pa;
      _rpc_command.Enqueue(odata);
#else
      handler.HandleRpc(ret_path, mname, pa, ret_path);
#endif
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
                     _rrman.Info, exception);
#endif
      using( MemoryStream ms = new MemoryStream() ) { 
        AdrConverter.Serialize(exception, ms);
        ret_path.Send( new CopyList( PType.Protocol.Rpc, MemBlock.Reference( ms.ToArray() ) ) );
      }
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
    Channel bq = rs.Results;
    switch(err) {
        case ReqrepManager.ReqrepError.NoHandler:
          x = new AdrException(-32601, "No RPC Handler on remote host");
          break;
        case ReqrepManager.ReqrepError.HandlerFailure:
          x = new AdrException(-32603, "The remote RPC System had a problem");
          break;
        case ReqrepManager.ReqrepError.Timeout:
          //In this case we close the Channel:
          if( bq != null ) { bq.Close(); }
          break;
        case ReqrepManager.ReqrepError.Send:
          //We had some problem sending, but ignore it for now
          break;
    }
    if( x != null && (bq != null) ) {
      RpcResult res = new RpcResult(ret_path, x);
      bq.Enqueue(res);
    }
  }

  /**
   * This is how you invoke a method on a remote host.
   * Results are put into the Channel.
   * 
   * If you want to have an Event based approach, listen to the EnqueueEvent
   * on the Channel you pass for the results.  That will be fired
   * immediately from the thread that gets the result.
   *
   * When a result comes back, we put and RpcResult into the Channel.
   * When you have enough responses, Close the queue (please).  The code
   * will stop sending requests after the queue is closed.  If you never close
   * the queue, this will be wasteful of resources.
   *
   * @param target the sender to use when making the RPC call
   * @param q the Channel into which the RpcResult objects will be placed.
   *            q may be null if you don't care about the response.
   * @param method the Rpc method to call
   *
   * @throw Exception if we cannot send the request for some reason.
   */
  virtual public void Invoke(ISender target, Channel q, string method,
                              params object[] args)
  {
    //build state for the RPC call
    RpcRequestState rs = new RpcRequestState();
    rs.Results = q;
    rs.RpcTarget = target;

    object[] rpc_call = new object[2];
    rpc_call[0] = method;
    if( args != null ) {
      rpc_call[1] = args;
    }
    else {
      //There are no args, which we represent as a zero length list
      rpc_call[1] = new object[0];
    }
    
    AdrCopyable req_copy = new AdrCopyable(rpc_call);
#if RPC_DEBUG
    Console.Error.WriteLine("[RpcClient: {0}] Invoking method: {1} on target: {2}",
                     _rrman.Info, method, target);
#endif
    ICopyable rrpayload = new CopyList( PType.Protocol.Rpc, req_copy ); 
    int reqid = _rrman.SendRequest(target, ReqrepManager.ReqrepType.Request,
                                   rrpayload, this, rs);
  
    //Make sure we stop this request when the queue is closed.
    if( q != null ) {
      try {
        q.CloseEvent += delegate(object qu, EventArgs eargs) {
          _rrman.StopRequest(reqid, this);
       };
      }
      catch {
        if(q.Closed) {
          _rrman.StopRequest(reqid, this);
        }
        else {
          throw;
        }
      }
    }
  }

#if DAVID_ASYNC_INVOKE
  protected void RpcCommandRun() {
    while(true) {
      try {
        object[] data = (object[]) _rpc_command.Dequeue();
        IRpcHandler handler = (IRpcHandler) data[0];
        ISender ret_path = (ISender) data[1];
        string methname = (string) data[2];
        IList param_list = (IList) data[3];
        handler.HandleRpc(ret_path, methname, param_list, ret_path);
      }
      catch (Exception x) {
        Console.Error.WriteLine("Exception in RpcCommandRun: {0}", x);
      }
    }
  }

#endif
  /**
   * This is used to send a result from an IRpcHandler
   * @param request_state this is passed to the IRpcHandler
   * @param result the result of the RPC call
   */
  public virtual void SendResult(object request_state, object result) {
    ISender ret_path = (ISender)request_state;
    AdrCopyable r_copy = new AdrCopyable(result);
    ret_path.Send( new CopyList( PType.Protocol.Rpc, r_copy ) );
  }
}
}
