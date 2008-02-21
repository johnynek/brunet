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

using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Threading;
using System.Net;

using Brunet.DistributedServices;
using Brunet.Rpc;
using Brunet;

namespace Brunet.Node {
  public class BasicNode {
    protected String _path;
    protected NodeConfig _node_config;
    protected StructuredNode _node;
    protected Dht _dht;
    protected DhtServer _ds;
    protected XmlRpcManagerServer _xrm;
    protected Shutdown _shutdown;
    protected bool _running;

    /**
     * Typical constructor, it loads up the config
     * @param path the path to a NodeConfig
     * @return Exits if NodeConfig is invalid
     */
    public BasicNode(String path) {
      try {
        _node_config = NodeConfigHandler.Read(path);
      }
      catch {
        Console.WriteLine("Invalid or missing configuration file.");
        Environment.Exit(1);
      }

      if(_node_config.NodeAddress == null) {
        _node_config.NodeAddress = (Utils.GenerateAHAddress()).ToString();
        NodeConfigHandler.Write(path, _node_config);
      }
      _running = true;
    }

    /**
     * This should be called by the Main after all the setup is done
     * this passes control to the _node and won't return until the program
     * is exiting.  (It is synchronous.)
     */
    public virtual void Run() {
      int sleep = 60, sleep_min = 60, sleep_max = 3600;
      DateTime start_time = DateTime.UtcNow;
      // Keep creating new nodes no matter what!
      while(_running) {
        CreateNode();
        new Information(_node, "BasicNode");
        Console.Error.WriteLine("I am connected to {0} as {1}.  Current time is {2}.",
                                _node.Realm, _node.Address.ToString(), DateTime.UtcNow);
        _node.DisconnectOnOverload = true;
        start_time = DateTime.UtcNow;
        _node.Connect();
        SuspendServices();
        if(!_running) {
          break;
        }
        // Assist in garbage collection
        DateTime now = DateTime.UtcNow;
        Console.Error.WriteLine("Going to sleep for {0} seconds. Current time is: {1}", sleep, now);
        Thread.Sleep(sleep * 1000);
        if(now - start_time < TimeSpan.FromSeconds(sleep_max)) {
          sleep *= 2;
          sleep = (sleep > sleep_max) ? sleep_max : sleep;
        }
        else {
          sleep /= 2;
          sleep = (sleep < sleep_min) ? sleep_min : sleep;
        }
      }
    }

    /**
     * Creates a node, the resulting node will be available in the class as _node.
     * @see MultiNode for some cool things done with this, best to leave this
     * alone and tweak @see Run()
     */
    public virtual void CreateNode() {
      AHAddress address = (AHAddress) AddressParser.Parse(_node_config.NodeAddress);
      _node = new StructuredNode(address, _node_config.BrunetNamespace);

      IEnumerable addresses = null;
      if(_node_config.DevicesToBind != null) {
        addresses = IPAddresses.GetIPAddresses(_node_config.DevicesToBind);
      }

      Brunet.EdgeListener el = null;
      foreach(EdgeListener item in _node_config.EdgeListeners) {
        int port = item.port;
        if (item.type =="tcp") {
          try {
            el = new TcpEdgeListener(port, addresses);
          }
          catch {
            el = new TcpEdgeListener(0, addresses);
          }
        }
        else if (item.type == "udp") {
          try {
            el = new UdpEdgeListener(port, addresses);
          }
          catch {
            el = new UdpEdgeListener(0, addresses);
          }
        }
        else {
          throw new Exception("Unrecognized transport: " + item.type);
        }
        _node.AddEdgeListener(el);
      }
      el = new TunnelEdgeListener(_node);
      _node.AddEdgeListener(el);

      ArrayList RemoteTAs = null;
      if(_node_config.RemoteTAs != null) {
        RemoteTAs = new ArrayList();
        foreach(String ta in _node_config.RemoteTAs) {
          RemoteTAs.Add(TransportAddressFactory.CreateInstance(ta));
        }
        _node.RemoteTAs = RemoteTAs;
      }

      _dht = new Dht(_node, 3, 20);
      StartServices();
    }

    /**
     * Starts services such as shutdown, rpcdht, and xmlrpc.  It is in users
     * best interest to just reimplement OnExit rather than tweak with this.
     */
    public virtual void StartServices() {
      _shutdown = Shutdown.GetShutdown();
      if(_shutdown != null) {
        _shutdown.OnExit += OnExit;
      }

      if(_node_config.RpcDht != null && _node_config.RpcDht.Enabled) {
        if(_ds == null) {
          _ds = new DhtServer(_node_config.RpcDht.Port);
        }
        _ds.Update(_dht);

        if(_node_config.XmlRpcManager != null && _node_config.XmlRpcManager.Enabled) {
          if(_xrm == null) {
            _xrm = new XmlRpcManagerServer(_node_config.XmlRpcManager.Port);
          }
          _xrm.Update(_node);
        }
      }
    }

    /**
     * If you no longer want to serve due to inactivity or for whatever reason,
     * this will be active until StartServices is called again.
     */
    public virtual void SuspendServices() {
      if(_ds != null) {
        _ds.Stop();
      }
      if(_xrm != null) {
        _xrm.Suspend();
      }
    }

    /**
     * After you're sure you don't want XmlRpc or DhtRpc(Soap) call this.
     * Calling this followed by changing the configured ports and then calling
     * StartServices will also let you change your port and what not.
     */
    public virtual void StopServices() {
      if(_ds != null) {
        _ds.Stop();
      }
      if(_xrm != null) {
        _xrm.Stop();
      }
    }

    /**
     * This method should be registered as a delegate to Shutdown.OnExit.
     * Override this if you need different features.
     */
    public virtual void OnExit() {
      StopServices();
      _running = false;
      _node.Disconnect();
    }

    /**
     * Runs the BasicNode
     * @param args the only argument is the path to an NodeConfig
     */
    public static int Main(String[] args) {
      BasicNode node = new BasicNode(args[0]);
      node.Run();
      return 0;
    }
  }
}
