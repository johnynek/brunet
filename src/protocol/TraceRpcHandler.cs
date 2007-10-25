/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

using System;
using System.Collections;
using System.Collections.Specialized;

namespace Brunet {

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
    _rpc = RpcManager.GetInstance(n);
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
    } else {
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
     * First find the Connection pointing to the node closest to a, if
     * there is one closer than us
     */
    ConnectionTable tab = _node.ConnectionTable;
    Connection next_closest = null;
    lock( tab.SyncRoot ) {
      next_closest = tab.GetConnection(ConnectionType.Structured, a);
      if( next_closest == null ) {
        //a is not the table:
        Connection right = tab.GetRightStructuredNeighborOf(a);
        Connection left = tab.GetLeftStructuredNeighborOf(a);
        BigInteger my_dist = ((AHAddress)_node.Address).DistanceTo(a).abs();
        BigInteger ld = ((AHAddress)left.Address).DistanceTo(a).abs();
        BigInteger rd = ((AHAddress)right.Address).DistanceTo(a).abs();
        if( (ld < rd) && (ld < my_dist) ) {
          next_closest = left;
        }
        if( (rd < ld) && (rd < my_dist) ) {
          next_closest = right;
        }
      }
    }
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
      _rpc = RpcManager.GetInstance(n);
      _rrman = ReqrepManager.GetInstance(n);
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
