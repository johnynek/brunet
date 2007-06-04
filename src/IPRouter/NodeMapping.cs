using System;
using System.Net;
using Brunet;
using System.Collections;
using System.Threading;

namespace Ipop {
  public class NodeMapping {
    public IPAddress ip;
    public string netmask, nodeAddress, password, ipop_namespace;
    public byte [] mac;
    public BrunetTransport brunet;
    private Thread trackerThread;

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

    public void UpdateTracker() {
      while(true) {
        while(true) {
          try {
            DhtOp.Put("iprouter_tracker", brunet.brunetNode.Address.ToString() +
                "|" + ip.ToString(), null, 7200, brunet.dht);
            break;
          }
          catch(Exception) {
            Thread.Sleep(10000);
          }
        }
        Thread.Sleep(1000*60*60);
      }
    }
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