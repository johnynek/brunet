/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
//#define ARI_CTM_DEBUG

using System;
using System.Collections;
//using log4net;
namespace Brunet
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
      string contype = ctm_req.ConnectionType;
      Linker l = new Linker(_n, target.Address, target.Transports, contype, ctm_req.InitiatorAddress);
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
      return new ConnectToMessage(ctm_req.ConnectionType, _n.GetNodeInfo(8), neigh_array, ctm_req.InitiatorAddress);
    }
  }

}
