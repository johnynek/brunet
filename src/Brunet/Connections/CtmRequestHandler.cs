/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
//#define ARI_CTM_DEBUG

using System;
using System.Collections;
using Brunet.Symphony;
namespace Brunet.Connections
{

  /**
   * When a ConnectToMessage *REQUEST* comes in, this object handles it.
   * Each node will have its own CtmRequestHandler.
   * When the CtmRequestHandler gets a ConnectToMessage, it creates a Linker
   * to link to the node that sent the message, and it sends a *RESPONSE*
   * ConnectToMessage back to the Node that made the request.
   *
   * The response will be handled by the Connector, which is the object which
   * initiates the Connection operations.
   *
   * @see Connector
   * @see Node
   * 
   */

  public class CtmRequestHandler
  {
    //private static readonly ILog _log = LogManager.GetLogger( typeof(CtmRequestHandler) );
    protected Node _n;
    /**
     */
    public CtmRequestHandler(Node n)
    {
      _n = n;
    }

    /**
     * This is a method for use with the RpcManager.  Remote
     * nodes can call the "sys:ctm.ConnectTo" method to 
     * reach this method
     */
    public IDictionary ConnectTo(IDictionary ht) {
      ConnectToMessage ctm_req = new ConnectToMessage(ht);
      //Console.Error.WriteLine("[{0}.ConnectTo({1})]", _n.Address, ctm_req);
      NodeInfo target = ctm_req.Target;
      if(_n.Address.Equals(target.Address)) {
        throw new Exception("Trying to connect to myself!");
      }
      string contype = ctm_req.ConnectionType;
      Linker l = new Linker(_n, target.Address, target.Transports, contype, ctm_req.Token);
      //Here we start the job:
      _n.TaskQueue.Enqueue( l );
      ConnectToMessage resp = GetCtmResponseTo(ctm_req);
      //Console.Error.WriteLine("[{0}.ConnectTo()->{1}]", _n.Address, resp);
      return resp.ToDictionary();
    }

    protected ConnectToMessage GetCtmResponseTo(ConnectToMessage ctm_req) {
      NodeInfo target = ctm_req.Target;
      
      //Send the 4 neighbors closest to this node:
      ArrayList nearest = _n.ConnectionTable.GetNearestTo( (AHAddress)target.Address, 4);
      //Now get these the NodeInfo objects for these:
      ArrayList neighbors = new ArrayList();
      foreach(Connection cons in nearest) {
        //No need to send the TA, since only the address is used
        NodeInfo neigh = NodeInfo.CreateInstance(cons.Address);
        neighbors.Add( neigh );
      }
      //Put these into an NodeInfo[]
      NodeInfo[] neigh_array = new NodeInfo[ neighbors.Count ];
      for(int i = 0; i < neighbors.Count; i++) {
        neigh_array[i] = (NodeInfo)neighbors[i];
      }
      return new ConnectToMessage(ctm_req.ConnectionType, _n.GetNodeInfo(12), neigh_array, ctm_req.Token);
    }
  }

}
