using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;
using Brunet;
using Brunet.Dht;
using System.Collections;
using System.Threading;

namespace Ipop {
  public class NodeMapping : IRpcHandler {
    public IPAddress ip;
    public string netmask, nodeAddress, password, ipop_namespace, geo_loc;
    public byte [] mac;
    public BrunetTransport brunet;
    private Thread trackerThread;
    private RpcManager _rpc;

    public NodeMapping() {
      ip = null;
      netmask = null;
      mac = null;
      brunet = null;
      nodeAddress = null;
      password = null;
      ipop_namespace = null;
      trackerThread = new Thread(UpdateTracker);
      trackerThread.Start();
    }

    /**
     * Separate from ctor because we don't directly set all values in there
     */
    public void AddAsRpcHandler() {
      _rpc = RpcManager.GetInstance(brunet.brunetNode);
      _rpc.AddHandler("ipop", this);
    }

    public IDictionary GetState() {
      Hashtable ht = new Hashtable();
      ht.Add("ipop_namespace", ipop_namespace == null ? string.Empty : ipop_namespace);
      ht.Add("ip_address", ip == null ? string.Empty : ip.ToString());
      ht.Add("netmask", netmask == null ? string.Empty : netmask);
      return ht;
    }

    public string addr() {
      string server = "www.geobytes.com";
      int port = 80;
      Regex lat = new Regex("<td align=\"right\">Latitude.+\r\n.+");
      Regex lon = new Regex("<td align=\"right\">Longitude.+\r\n.+");
      Regex num = new Regex("\\-{0,1}\\d+.\\d+");
      Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
      s.Connect(server, port);
      string request = "GET /IpLocator.htm HTTP/1.0\r\nHost: netgeo.caida.org\r\nUser-Agent:  None\r\n\r\n";
      byte[] bs = Encoding.ASCII.GetBytes(request);
      s.Send(bs, bs.Length, 0);
      string page = String.Empty;
      byte[] br = new byte[256];
      int bytes = 0;
      do {
        bytes = s.Receive(br, br.Length, 0);
        page += Encoding.ASCII.GetString(br, 0, bytes);
      } while (bytes > 0);
      Match latm = lat.Match(page);
      Match lonm = lon.Match(page);
      if(latm.Success && lonm.Success) {
        latm = num.Match(latm.Value);
        lonm = num.Match(lonm.Value);
        if(latm.Success && lonm.Success) {
          latm = num.Match(latm.Value);
          lonm = num.Match(lonm.Value);
          return latm.Value + ", " + lonm.Value;
        }
      }
      return ",";
    }

    public void UpdateTracker() {
      int count = 0;
      int restart = (new Random()).Next(168);
      while(true) {
        if(count == 0 || geo_loc.Equals(", "))
          geo_loc = addr();
        else if(count == restart)
          count = 0;
        else
          count++;

        while(true) {
          bool result = false;
          try {
            result = brunet.dht.Put("iprouter_tracker", brunet.brunetNode.Address.ToString() +
                "|" + ip.ToString() + "|" + geo_loc, 7200);
          }
          catch(Exception) {;}
          if(result) {
            break;
          }
          else {
            Thread.Sleep(10000);
          }
        }
        Thread.Sleep(1000*60*60);
      }
    }

    #region IRpcHandler Members

    public void HandleRpc(ISender caller, string method, IList arguments, object request_state) {
      if(_rpc == null) {
        //In case it's called by local objects without initializing _rpc
        throw new InvalidOperationException("This method has to be called from Rpc");
      }
      if (method == "GetState") {
        IDictionary dic = this.GetState();
        _rpc.SendResult(request_state, dic);
      }
    }

    #endregion
  }

//  We need a thread to remove a node mapping if our use of it expires -- that is it isn't used for a certain period of time
  public class NodeMappings {
    private ArrayList nodeMappings;

    public NodeMappings() {
      nodeMappings = new ArrayList();
    }

    public void AddNodeMapping(NodeMapping node) {
      nodeMappings.Add(node);
    }

    public NodeMapping GetNode(IPAddress ip) {
      foreach (NodeMapping node in nodeMappings) {
        if(node.ip.Equals(ip))
          return node;
      }
      return null;
    }

    public bool RemoveNodeMapping(IPAddress ip) {
      for (int i = 0; i < nodeMappings.Count; i++) {
        if(((NodeMapping) nodeMappings[i]).ip.Equals(ip)) {
          nodeMappings.Remove(i);
          return true;
        }
      }
      return false;
    }
  }
}