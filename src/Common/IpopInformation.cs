using Brunet;
using System;
using System.Collections;
using System.Threading;

namespace Ipop
{
  public class IpopInformation: IRpcHandler
  {
    private bool in_geoloc = false;
    private DateTime _last_called = DateTime.UtcNow - TimeSpan.FromHours(48);
    private RpcManager _rpc;
    private string geo_loc = ",";
    private readonly Hashtable info_base;
    public string ip;
    private StructuredNode _node;

    public IpopInformation(StructuredNode node, string type, 
                           string ipop_namespace, string brunet_namespace)
    {
      _node = node;
      _rpc = RpcManager.GetInstance(node);
      _rpc.AddHandler("ipop", this);
      info_base = new Hashtable();
      info_base.Add("type", type);
      info_base.Add("ipop_namespace", ipop_namespace);
      info_base.Add("brunet_namespace", brunet_namespace);
    }

    public void HandleRpc(ISender caller, string method, IList arguments, object request_state) {
      if(_rpc == null) {
        //In case it's called by local objects without initializing _rpc
        throw new InvalidOperationException("This method has to be called from Rpc");
      }
      object result = new InvalidOperationException("Invalid method");
      if(method.Equals("Information"))
        result = this.Information();
      _rpc.SendResult(request_state, result);
    }

    public IDictionary Information() {
      GetGeoLoc();
      Hashtable ht = new Hashtable(info_base);
      ht.Add("geo_loc", geo_loc);
      ht.Add("ip", ip);
      ht.Add("localips", _node.sys_link.GetLocalIPAddresses(null));
      ht.Add("neighbors", _node.sys_link.GetNeighbors(null));
      return ht;
    }

    public void GetGeoLoc() {
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

    public void GetGeoLocAsThread(object o) {
      string local_geo_loc = IPOP_Common.GetMyGeoLoc();
      if(!local_geo_loc.Equals(","))
        geo_loc = local_geo_loc;
      in_geoloc = false;
    }
  }
}