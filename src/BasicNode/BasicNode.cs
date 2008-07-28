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
using Brunet.Coordinate;

/**
\namespace Brunet::Applications Provides BasicNode and core features
necessary for running Brunet.Node in a simple complete manner.
Besides providing basic functionality, this namespace and class offer 
some extra features, such as the ability to cleanly shutdown via ctrl-c,
a user configured Rpc method for providing information during a crawl
(see Information), and the ability to determine the IP addresses on the
local machines and their associated names.
\brief Provides BasicNode which implements a simple Brunet P2P Node.
*/
namespace Brunet.Applications {
  /**
  <summary>BasicNode provides the core Brunet features in a deployable model
  the inputs are a xml config file called NodeConfig, which specifies which
  if any services to deploy.  Other projects should inherit this as their
  base class rather than implementing their own interfaces to Brunet.</summary>
  */
  public class BasicNode {
    /// <summary>The path to the NodeConfig.</summary>
    protected String _path;
    /// <summary>The NodeConfig that defines the Brunet.Node.</summary>
    protected NodeConfig _node_config;
    /// <summary>The Brunet.Node used to connect to the p2p network.</summary>
    protected StructuredNode _node;
    /// <summary>The Dht object used to participate in the dht.</summary>
    protected Dht _dht;
    /// <summary>The NCService object used for this node.</summary>
    protected NCService _ncservice;
    /// <summary>The DhtRpc service provider.</summary>
    protected DhtServer _ds;
    /// <summary>The XmlRpc service provider.</summary>
    protected XmlRpcManagerServer _xrm;
    /// <summary>The shutdown service provider.</summary>
    protected Shutdown _shutdown;
    /// <summary>Path to the node config (for updating it).</summary>
    protected string _node_config_path;
    /**  <summary>True if the node should reincarnate itself if Node.Connect
    exits or throws an exception</summary>*/
    protected bool _running;

    /**
    <summary>Loads a configuration file and creates a Node.Address if
    necessary.</summary>
    <param name="path">The path to a NodeConfig</param>
    <returns>Exits if NodeConfig is invalid.</returns>
    */
    public BasicNode(String path) {
      try {
        _node_config = Utils.ReadConfig<NodeConfig>(path);
      }
      catch {
        Console.WriteLine("Invalid or missing configuration file.");
        Environment.Exit(1);
      }

      _node_config_path = path;
      if(_node_config.NodeAddress == null) {
        _node_config.NodeAddress = (Utils.GenerateAHAddress()).ToString();
        Utils.WriteConfig(path, _node_config);
      }
      _running = true;
    }

    /**
    <summary>A constructor to be used only by sub-classes.  The goal here being
    that inheritors may want to implement their own subclass of config but
    BasicNode still needs to be configured and possibly write to the config
    file.  This gets around that problem!</summary>
    <param name="path">The Path of the NodeConfig in the second parameter
    </param>
    <param name="config">A NodeConfig inherited object.</param>
    */
    protected BasicNode(String path, NodeConfig config) {
      _node_config = config;
      if(_node_config.NodeAddress == null) {
        _node_config.NodeAddress = (Utils.GenerateAHAddress()).ToString();
        Utils.WriteConfig(path, _node_config);
      }
      _running = true;
    }

    /**
    <summary>This should be called by the Main after all the setup is done
    this passes control to the _node and won't return until the program is
    exiting.  (It is synchronous.)</summary>
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
        StartServices();
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
    <summary>Creates a Brunet.Node, the resulting node will be available in
    the class as _node.</summary>
    <remarks>The steps to creating a node are first constructing it with a
    namespace, optionally adding local ip addresses to bind to, specifying
    local end points, specifying remote end points, and finally registering
    the dht.</remarks>
    */
    public virtual void CreateNode() {
      AHAddress address = (AHAddress) AddressParser.Parse(_node_config.NodeAddress);
      _node = new StructuredNode(address, _node_config.BrunetNamespace);
      IEnumerable addresses = IPAddresses.GetIPAddresses(_node_config.DevicesToBind);

      Brunet.EdgeListener el = null;
      foreach(NodeConfig.EdgeListener item in _node_config.EdgeListeners) {
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
      if (_node_config.EnableVivaldi) {
        if(_node_config.NCServiceCheckpoint != null) {
          _ncservice = new NCService(_node, _node_config.NCServiceCheckpoint);
        } else {
          _ncservice = new NCService(_node);
        }

        if (_node_config.OptimizeShortcuts) {
          TargetSelector vs = new VivaldiTargetSelector(_node, _ncservice);
          _node.Sco.TargetSelector = vs;
        }
      }
      _dht = new Dht(_node, 3, 20);
    }

    /**
    <summary>Starts services such as shutdown, rpcdht, and xmlrpc.  If you wish
    to have your own shutdown path, edit OnExit instead of this.  This can be
    called multiple times without negative effect.</summary>
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
      }

      if(_node_config.XmlRpcManager != null && _node_config.XmlRpcManager.Enabled) {
        if(_xrm == null) {
          _xrm = new XmlRpcManagerServer(_node_config.XmlRpcManager.Port);
        }
        _xrm.Update(_node);
      }

      if(_node_config.EnableVivaldi) {
        _node.HeartBeatEvent += _ncservice.CheckpointHandler;
        NCService.CheckpointEvent += NCServiceCheckpoint;
      }
    }

    protected void NCServiceCheckpoint(object Point, EventArgs ea) {
      _node_config.NCServiceCheckpoint = Point.ToString();
      Utils.WriteConfig(_node_config_path, _node_config);
    }

    /**
    <summary>If you no longer want to serve due to inactivity or for whatever
    reason, this will be active until StartServices is called again.  This just
    stops serving the DhtRpc and XmlRpc services, but the ports remain open.
    </summary>
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
    <summary>This stops all services such as Xml and DhtRpc. Call this instead
    of SuspendServices if the node is shutting down or services are no longer 
    required and you would like to release the ports</summary>
    */
    public virtual void StopServices() {
      if(_ds != null) {
        _ds.Stop();
        _ds = null;
      }
      if(_xrm != null) {
        _xrm.Stop();
        _xrm = null;
      }
    }

    /**
    <summary>This method is registered as a delegate to Shutdown.OnExit and
    will be called when ctrl-c is pressed by the user.  This stops services,
    prevents the node from reincarnating, and then disconnects the node.
    </summary>
    */
    public virtual void OnExit() {
      StopServices();
      _running = false;
      _node.Disconnect();
    }

    /**
    <summary>Runs the BasicNode.  This should be implemented in all inherited
    classes.</summary>
    <remarks>
    <para>To execute this at a command-line using Mono:</para>
    <code>
    mono BasicNode.exe path/to/node_config
    </code>
    <para>To execute this at a command-line using Windows .NET:</para>
    <code>
    BasicNode.exe path/to/node_config
    </code>
    </remarks>
    <param name="args">The command line argument required is a path to a
    NodeConfig</param>
    */
    public static int Main(String[] args) {
      BasicNode node = new BasicNode(args[0]);
      node.Run();
      return 0;
    }
  }
}
