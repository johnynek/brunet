/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Threading;

namespace Brunet.Tunnel {
  public interface ITunnelOverlap {
    void FindOverlap(TunnelTransportAddress tta, WaitCallback callback);
  }

  public class SimpleTunnelOverlap : ITunnelOverlap {
    protected Node _node;
    protected static readonly ArrayList _empty_array_list = new ArrayList(0);

    public SimpleTunnelOverlap(Node node)
    {
      _node = node;
    }

    public void FindOverlap(TunnelTransportAddress tta, WaitCallback callback)
    {
      ISender s = new AHGreedySender(_node, tta.Target);
      Channel chan = new Channel(1);
      Address addr = null;
      chan.CloseEvent += delegate(object o, EventArgs ea) {
        try {
          Hashtable ht = (chan.Dequeue() as RpcResult).Result as Hashtable;
          StatusMessage sm = new StatusMessage(ht);
          foreach(NodeInfo ni in sm.Neighbors) {
            if(ni.FirstTA == null || ni.FirstTA is TunnelTransportAddress) {
              continue;
            }

            addr = ni.Address;
            break;
          }
        } catch(Exception e) {
          Console.WriteLine(e);
        } finally {
          Console.WriteLine("Selected address: " + addr);
          callback(addr);
        }
      };

      StatusMessage req = new StatusMessage(ConnectionType.Structured, _empty_array_list);
      _node.Rpc.Invoke(s, chan, "sys:link.GetStatus", req.ToDictionary());
    }
  }
}
