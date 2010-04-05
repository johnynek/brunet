/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System.Collections;
using System.Collections.Specialized;
using Brunet.Util;
using Brunet.Connections;
using Brunet.Concurrent;

using Brunet.Messaging;
namespace Brunet.Symphony {

/**
 * This class handles trace methods to debug and measure the network.
 * Anything that has to do with taking measurements using RPC calls
 * should probably be in this class
 */
public class TraceRpcHandler : IRpcHandler {
  protected readonly Node _node;
  protected readonly RpcManager _rpc;
  /**
   * This constructor DOES NOT add the handler,
   * you have to do that.
   *
   * @param n the Node this Handler is for
   */
  public TraceRpcHandler(Node n) {
    _node = n;
    _rpc = n.Rpc;
  }
  /**
   * This dispatches the particular methods this class provides
   */
  public void HandleRpc(ISender caller, string method, IList args, object req_state) {
    if( method == "GetRttTo" ) {
      ISender dest = new AHGreedySender(_node, AddressParser.Parse((string)args[0]));
      EchoSendHandler esh = new EchoSendHandler(_node, dest, req_state);
      //This will be garbage collected after the request is done:
      esh.SendEchoRequest();
    } else if ( method == "GetRouteTo" ) {
      DoTraceRouteTo( (AHAddress)AddressParser.Parse((string)args[0]), req_state);
    } else if ( method == "RecursiveCall" ) {
      RecursiveCall(args, req_state);
    }
    else {
      throw new AdrException(-32601, "No Handler for method: " + method);
    }
  }

  /**
   * This is a recursive function over the network
   * It helps to build an IList of IDictionary types that give the address
   * of each node in the path, and the connection to the next closest node if
   * there is one, otherwise no next.
   */
  protected void DoTraceRouteTo(AHAddress a, object req_state) {
    /*
     * First find the Connection pointing to the node closest to dest, if
     * there is one closer than us
     */

    ConnectionTable tab = _node.ConnectionTable;
    ConnectionList structs = tab.GetConnections(ConnectionType.Structured);
    Connection next_closest = structs.GetNearestTo((AHAddress) _node.Address, a);
    //Okay, we have the next closest:
    ListDictionary my_entry = new ListDictionary();
    my_entry["node"] = _node.Address.ToString();
    if( next_closest != null ) {
      my_entry["next_con"] = next_closest.ToString();
      Channel result = new Channel();
      //We only want one result, so close the queue after we get the first
      result.CloseAfterEnqueue();
      result.CloseEvent += delegate(object o, EventArgs args) {
        Channel q = (Channel)o;
        if( q.Count > 0 ) {
          try {
            RpcResult rres = (RpcResult)q.Dequeue();
            IList l = (IList) rres.Result;
            ArrayList results = new ArrayList( l.Count + 1);
            results.Add(my_entry);
            results.AddRange(l);
            _rpc.SendResult(req_state, results);
          }
          catch(Exception x) {
            string m = String.Format("<node>{0}</node> trying <connection>{1}</connection> got <exception>{2}</exception>", _node.Address, next_closest, x);
            Exception nx = new Exception(m);
            _rpc.SendResult(req_state, nx);
          }
        }
        else {
          //We got no results.
          IList l = new ArrayList(1);
          l.Add( my_entry );
          _rpc.SendResult(req_state, l);
        }
      };
      _rpc.Invoke(next_closest.Edge, result, "trace.GetRouteTo", a.ToString());
    }
    else {
      //We are the end of the line, send the result:
      ArrayList l = new ArrayList();
      l.Add(my_entry);
      _rpc.SendResult(req_state, l);  
    }
  }
  /**
   * This is a recursive function over the network
   * It helps do a link-reliable procedure call on the
   * on the overlay network.
   */
  protected void RecursiveCall(IList margs, object req_state) {
    //first argument is the target node.
    AHAddress a = (AHAddress) AddressParser.Parse( (string) margs[0]);
    /*
     * First find the Connection pointing to the node closest to dest, if
     * there is one closer than us
     */

    ConnectionTable tab = _node.ConnectionTable;
    ConnectionList structs = tab.GetConnections(ConnectionType.Structured);
    Connection next_closest = structs.GetNearestTo((AHAddress) _node.Address, a);
    //Okay, we have the next closest:
    if( next_closest != null ) {
      Channel result = new Channel();
      //We only want one result, so close the queue after we get the first
      result.CloseAfterEnqueue();
      result.CloseEvent += delegate(object o, EventArgs args) {
        Channel q = (Channel)o;
        if( q.Count > 0 ) {
          try {
            RpcResult rres = (RpcResult)q.Dequeue();
            _rpc.SendResult(req_state, rres.Result);
          }
          catch(Exception x) {
            string m = String.Format("<node>{0}</node> trying <connection>{1}</connection> got <exception>{2}</exception>", _node.Address, next_closest, x);
            Exception nx = new Exception(m);
            _rpc.SendResult(req_state, nx);
          }
        }
        else {
          //We got no results.
          _rpc.SendResult(req_state, null);
        }
      };
      object [] new_args = new object[margs.Count];
      margs.CopyTo(new_args, 0);
      _rpc.Invoke(next_closest.Edge, result, "trace.RecursiveCall", new_args);
    }
    else {
      //We are the end of the line, send the result:
      //Console.Error.WriteLine("Doing a local invocation");
      Channel result = new Channel();
      result.CloseAfterEnqueue();
      result.CloseEvent += delegate(object o, EventArgs args) {
        Channel q = (Channel)o;
        if( q.Count > 0 ) {
          try {
            //Console.Error.WriteLine("Got result.");
            RpcResult rres = (RpcResult)q.Dequeue();
            _rpc.SendResult(req_state, rres.Result);
          }
          catch(Exception x) {
            string m = String.Format("<node>{0}</node> local invocation got <exception>{1}</exception>", _node.Address, x);
            Exception nx = new Exception(m);
            _rpc.SendResult(req_state, nx);
          }
        }
        else {
          //We got no results.
          _rpc.SendResult(req_state, null);
        }        
      };

      string method_name = (string) margs[1];
      object [] new_args = new object[margs.Count - 2];
      margs.RemoveAt(0);//extract destination address
      margs.RemoveAt(0); //extract method name
      margs.CopyTo(new_args, 0);
      //Console.Error.WriteLine("Calling method: {0}, args_count: {1}", method_name, new_args.Length);
      //for (int i = 0; i < new_args.Length; i++) {
      //Console.Error.WriteLine(new_args[i]);
      //}
      _rpc.Invoke(_node, result, method_name, new_args);
    }
    
  }
  

  /**
   * Sends an Echo request and times how long it takes to get a response
   */
  protected class EchoSendHandler : IReplyHandler {
    
    /**
     * When we get the result with this request state
     */
    protected static readonly ICopyable PING_DATA =
            new CopyList( PType.Protocol.Echo, MemBlock.Reference( new byte[64] ) );
    protected RpcManager _rpc;
    protected readonly object _req_state;
    protected readonly ReqrepManager _rrman;
    protected readonly ISender _dest;
    protected DateTime _start_time;

    public EchoSendHandler(Node n, ISender dest, object req_state) {
      _rrman = n.Rrm;
      _rpc = n.Rpc;
      _req_state = req_state;
      _dest = dest;
    }
    
    /**
     * Note the time and send the request.  The reply will trigger the RPC
     * call to be finished.  The first error will also finish the call.
     */
    public void SendEchoRequest() {
      _start_time = DateTime.UtcNow;
      _rrman.SendRequest(_dest, ReqrepManager.ReqrepType.Request, PING_DATA, this, null);
    }

    public bool HandleReply(ReqrepManager man, ReqrepManager.ReqrepType rt,
                   int mid,
                   PType prot,
                   MemBlock payload, ISender returnpath,
                   ReqrepManager.Statistics statistics,
                   object state) {
      DateTime reply_time = DateTime.UtcNow;
      
      ListDictionary res_dict = new ListDictionary();
      AHSender ah_rp = returnpath as AHSender;
      if( ah_rp != null ) {
        res_dict["target"] = ah_rp.Destination.ToString();
      }
      //Here are the number of microseconds
      res_dict["musec"] = (int)( 1000.0 * ((reply_time - _start_time).TotalMilliseconds) );
      //Send the RPC result now;
      RpcManager my_rpc = System.Threading.Interlocked.Exchange(ref _rpc, null);
      if( my_rpc != null ) {
        //We have not sent any reply yet:
        my_rpc.SendResult(_req_state, res_dict);
      }
      return false;
    }
    public void HandleError(ReqrepManager man, int message_number,
                   ReqrepManager.ReqrepError err, ISender ret_path, object state) {
      _rrman.StopRequest(message_number, this);
      RpcManager my_rpc = System.Threading.Interlocked.Exchange(ref _rpc, null);
      if( my_rpc != null ) {
        //We have not sent any reply yet:
        my_rpc.SendResult( _req_state,
                    new Exception(String.Format("Error: {0} from: {1}", err, ret_path)));
      }
    }
  }

}

}
