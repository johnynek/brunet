/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using System;
using System.Collections;
using System.Threading;

namespace Brunet.Applications {
  /**
   * This class provides a Rpc to be used during crawls of the network.  It
   * provides base informatoin such as geographical coordinates, local end
   * points, the type of node, and neighbors.  There is also support for
   * user's to put their own data in the return value.
   */
  public class Information: IRpcHandler {
    private bool in_geoloc = false;
    private DateTime _last_called = DateTime.UtcNow - TimeSpan.FromHours(48);
    private RpcManager _rpc;
    private String geo_loc = ",";
    public Hashtable UserData; /**< Put all node unique data inside this hashtable! */
    protected readonly StructuredNode _node;
    protected readonly String _type;

    public Information(StructuredNode node, String type) {
      UserData = new Hashtable();
      _type = type;
      _node = node;
      _rpc = RpcManager.GetInstance(node);
      _rpc.AddHandler("Information", this);
    }

    public void HandleRpc(ISender caller, String method, IList arguments, object request_state) {
      if(_rpc == null) {
        //In case it's called by local objects without initializing _rpc
        throw new InvalidOperationException("This method has to be called from Rpc");
      }
      object result = new InvalidOperationException("Invalid method");
      if(method.Equals("Info"))
        result = this.Info();
      _rpc.SendResult(request_state, result);
    }

    /**
     * Call this method by Rpc during crawl!
     * @return Returns the dictionary containing the information for the node.
     */
    public IDictionary Info() {
      GetGeoLoc();
      Hashtable ht = new Hashtable(UserData);
      ht.Add("type", _type);
      ht.Add("geo_loc", geo_loc);
      ht.Add("localips", _node.sys_link.GetLocalIPAddresses(null));
      ht.Add("neighbors", _node.sys_link.GetNeighbors(null));
      return ht;
    }

    /**
     * Call this if you want the Geographical lookup to occur in another
     * thread.
     */
    protected void GetGeoLoc() {
      DateTime now = DateTime.UtcNow;
      if((now - _last_called > TimeSpan.FromDays(7) || geo_loc.Equals(","))) {
        lock(geo_loc) {
          if(in_geoloc) {
            return;
          }
          in_geoloc = true;
        }
        ThreadPool.QueueUserWorkItem(GetGeoLocAsThread, null);
      }
    }

    /**
     * Sets the geographical location of the running node.
     */
    protected void GetGeoLocAsThread(object o) {
      String local_geo_loc = Utils.GetMyGeoLoc();
      if(!local_geo_loc.Equals(",")) {
        geo_loc = local_geo_loc;
      }
      in_geoloc = false;
    }
  }
}
