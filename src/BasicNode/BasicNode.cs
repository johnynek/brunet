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
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Threading;
using System.Net;

using Brunet;
using Brunet.Coordinate;
using Brunet.DistributedServices;
using Brunet.Rpc;
using Brunet.Security;
using Brunet.Security.Protocol;
using Brunet.Security.Transport;
using Brunet.Tunnel;

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
  /// <summary>BasicNode provides the core Brunet features in a deployable model
  /// the inputs are a xml config file called NodeConfig, which specifies which
  /// if any services to deploy.  Other projects should inherit this as their
  /// base class rather than implementing their own interfaces to Brunet.</summary>
  public class BasicNode {
    /// <summary>The path to the NodeConfig.</summary>
    protected String _path;
    /// <summary>The NodeConfig that defines the Brunet.Node.</summary>
    protected NodeConfig _node_config;
    /// <summary>Path to the node config (for updating it).</summary>
    protected string _node_config_path;

    /// <summary>Contains the StructuredNode and other Brunet features.</summary>
    protected ApplicationNode _app_node;

    /// <summary>The XmlRpc service provider.</summary>
    protected XmlRpcManagerServer _xrm;

    /// <summary>The shutdown service provider.</summary>
    public Shutdown Shutdown { get { return _shutdown; } }
    protected Shutdown _shutdown;

    /// <summary>True if the node should reincarnate itself if Node.Connect
    /// exits or throws an exception</summary>
    protected bool _running;

    protected Dictionary<string, PathELManager> _type_to_pem;
    protected Random _rand;

    /// <summary>Prepares a BasicNode.</summary>
    /// <param name="node_config">A node config object.</param>
    public BasicNode(NodeConfig node_config)
    {
      _node_config = node_config;
      _running = true;
      _shutdown = Shutdown.GetShutdown();
      _shutdown.OnExit += OnExit;

      _type_to_pem = new Dictionary<string, PathELManager>();
      _rand = new Random();
    }

    /// <summary>This should be called by the Main after all the setup is done
    /// this passes control to the _node and won't return until the program is
    /// exiting.  (It is synchronous.)</summary>
    public virtual void Run()
    {
      int sleep = 60, sleep_min = 60, sleep_max = 3600;
      DateTime start_time = DateTime.UtcNow;

      // Keep creating new nodes no matter what!
      while(_running) {
        ApplicationNode node = CreateNode(_node_config);
        _app_node = node;

        new Information(node.Node, "BasicNode");
        Console.WriteLine("Starting at {0}, {1} is connecting to {2}.",
            DateTime.UtcNow, node.Node.Address, node.Node.Realm);

        node.Node.DisconnectOnOverload = true;
        start_time = DateTime.UtcNow;
        node.Node.Connect();

        if(!_running) {
          break;
        }

        // Assist in garbage collection
        if(_xrm != null) {
          _xrm.Remove(node.Node);
        }
        node = null;
        _app_node = null;

        DateTime now = DateTime.UtcNow;
        Console.WriteLine("Going to sleep for {0} seconds. Current time is: {1}", sleep, now);
        Thread.Sleep(sleep * 1000);

        if(now - start_time < TimeSpan.FromSeconds(sleep_max)) {
          sleep *= 2;
          sleep = (sleep > sleep_max) ? sleep_max : sleep;
        } else {
          sleep /= 2;
          sleep = (sleep < sleep_min) ? sleep_min : sleep;
        }
      }
    }

    /// <summary>Creates an ApplicationNode and prepares it for connection to
    /// the overlay.  For historical reasons it is linked to _node, _dht,
    /// _rpc_dht, and _bso.</summary>
    public virtual ApplicationNode CreateNode(NodeConfig node_config) {
      // Get a Node ID for the new Node
      AHAddress address = null;
      try {
        address = (AHAddress) AddressParser.Parse(node_config.NodeAddress);
      } catch {
        address = Utils.GenerateAHAddress();
      }

      // Create the Node state
      StructuredNode node = new StructuredNode(address, node_config.BrunetNamespace);
      IEnumerable addresses = IPAddresses.GetIPAddresses(node_config.DevicesToBind);

      ProtocolSecurityOverlord pso = null;
      // Enable Security if requested
      if(node_config.Security.Enabled) {
        if(node_config.Security.SelfSignedCertificates) {
          SecurityPolicy.SetDefaultSecurityPolicy(SecurityPolicy.DefaultEncryptor,
              SecurityPolicy.DefaultAuthenticator, true);
        }

        byte[] blob = null;
        using(FileStream fs = File.Open(node_config.Security.KeyPath, FileMode.Open)) {
          blob = new byte[fs.Length];
          fs.Read(blob, 0, blob.Length);
        }

        RSACryptoServiceProvider rsa_private = new RSACryptoServiceProvider();
        rsa_private.ImportCspBlob(blob);

        CertificateHandler ch = new CertificateHandler(node_config.Security.CertificatePath);
        pso = new ProtocolSecurityOverlord(node, rsa_private, node.Rrm, ch);
        pso.Subscribe(node, null);

        node.GetTypeSource(SecurityOverlord.Security).Subscribe(pso, null);
        node.HeartBeatEvent += pso.Heartbeat;

        // A hack to enable a test for security that doesn't require each peer
        // to exchange certificates
        if(node_config.Security.TestEnable) {
          blob = rsa_private.ExportCspBlob(false);
          RSACryptoServiceProvider rsa_pub = new RSACryptoServiceProvider();
          rsa_pub.ImportCspBlob(blob);
          CertificateMaker cm = new CertificateMaker("United States", "UFL", 
              "ACIS", "David Wolinsky", "davidiw@ufl.edu", rsa_pub,
              "brunet:node:abcdefghijklmnopqrs");
          Certificate cacert = cm.Sign(cm, rsa_private);

          cm = new CertificateMaker("United States", "UFL", 
              "ACIS", "David Wolinsky", "davidiw@ufl.edu", rsa_pub,
              address.ToString());
          Certificate cert = cm.Sign(cacert, rsa_private);
          ch.AddCACertificate(cacert.X509);
          ch.AddSignedCertificate(cert.X509);
        }
      }

      // Add Dht
      new TableServer(node);
      IDht dht = new Dht(node, 3, 20);
      RpcDhtProxy dht_proxy = new RpcDhtProxy(dht, node);

      // Setup Vivaldi if requested
      ITunnelOverlap ito = null;
      NCService ncservice = null;
      if(node_config.NCService.Enabled) {
        ncservice = new NCService(node, node_config.NCService.Checkpoint);

        if (node_config.NCService.OptimizeShortcuts) {
          node.Ssco.TargetSelector = new VivaldiTargetSelector(node, ncservice);
        }
        ito = new NCTunnelOverlap(ncservice);
      } else {
        ito = new SimpleTunnelOverlap();
      }

      // Create the ApplicationNode
      ApplicationNode app_node = new ApplicationNode(node, dht, dht_proxy, ncservice, pso);

      // Add Edge listeners
      Brunet.EdgeListener el = null;
      foreach(NodeConfig.EdgeListener item in node_config.EdgeListeners) {
        el = CreateEdgeListener(item, app_node, addresses);
        if(node_config.Security.SecureEdgesEnabled) {
          el = new SecureEdgeListener(el, pso);
        }
        node.AddEdgeListener(el);
      }

      // Create the tunnel and potentially wrap it in a SecureEL
      el = new Tunnel.TunnelEdgeListener(node, ito);
      if(node_config.Security.SecureEdgesEnabled) {
        node.EdgeVerifyMethod = EdgeVerify.AddressInSubjectAltName;
        el = new SecureEdgeListener(el, pso);
      }
      node.AddEdgeListener(el);

      ArrayList RemoteTAs = null;
      if(node_config.RemoteTAs != null) {
        RemoteTAs = new ArrayList();
        foreach(String ta in node_config.RemoteTAs) {
          RemoteTAs.Add(TransportAddressFactory.CreateInstance(ta));
        }
        node.RemoteTAs = RemoteTAs;
      }

      // Add XmlRpc
      if(node_config.XmlRpcManager.Enabled) {
        if(_xrm == null) {
          _xrm = new XmlRpcManagerServer(node_config.XmlRpcManager.Port);
        }
        _xrm.Add(node, GetXmlRpcUri(app_node));
        new RpcDht(dht, node);
      }

      return app_node;
    }

    protected virtual string GetXmlRpcUri(ApplicationNode appnode)
    {
      return appnode.Node.Address.ToString();
    }


    /// <summary>Given an EdgeListener info, attempts to find a PathEL, if one is not
    /// found, creates a base EL and wraps it with a PathEL.</summary>
    protected EdgeListener CreateEdgeListener(NodeConfig.EdgeListener el_info,
        ApplicationNode node, IEnumerable addresses)
    {
      PathELManager pem = null;
      if(!_type_to_pem.TryGetValue(el_info.type, out pem)) {
        pem = new PathELManager(CreateBaseEdgeListener(el_info, node, addresses), node.Node);
        pem.Start();
        _type_to_pem[el_info.type] = pem;
      }

      EdgeListener el = pem.CreatePath();
      PType path_p = PType.Protocol.Pathing;
      node.Node.DemuxHandler.GetTypeSource(path_p).Subscribe(pem, path_p);
      return el;
    }

    /// <summary>Given a EdgeListener info and a list of addresses to advertise,
    /// returns an EdgeListener.</summary>
    protected EdgeListener CreateBaseEdgeListener(NodeConfig.EdgeListener el_info,
        ApplicationNode node, IEnumerable addresses)
    {
      EdgeListener el = null;
      int port = el_info.port;
      if(el_info.type == "tcp") {
        try {
          el = new TcpEdgeListener(port, addresses);
        } catch {
          el = new TcpEdgeListener(0, addresses);
        }
      } else if(el_info.type == "udp") {
        try {
          el = new UdpEdgeListener(port, addresses);
        } catch {
          el = new UdpEdgeListener(0, addresses);
        }
      } else if(el_info.type == "function") {
        port = port == 0 ? (new Random()).Next(1024, 65535) : port;
        el = new FunctionEdgeListener(port, 0, null);
      } else {
        throw new Exception("Unrecognized transport: " + el_info.type);
      }
      return el;
    }

    /// <summary>All nodes are disconnected?  Stop the PathEL.</summary>
    protected virtual void StopPem(DateTime now)
    {
      ApplicationNode appnode = _app_node;
      bool stop = appnode == null;

      if(!stop) {
        stop = appnode.Node.ConState == Node.ConnectionState.Disconnected;
      }

      if(stop) {
        foreach(PathELManager pem in _type_to_pem.Values) {
          pem.Stop();
        }
      }
    }

    /**
    <summary>This method is registered as a delegate to Shutdown.OnExit and
    will be called when ctrl-c is pressed by the user.  This stops services,
    prevents the node from reincarnating, and then disconnects the node.
    </summary>
    */
    public virtual void OnExit() {
      _running = false;

      if(_xrm != null) {
        _xrm.Stop();
        _xrm = null;
      }

      ApplicationNode appnode = _app_node;
      _app_node = null;

      NCService ncservice = null;
      if(appnode != null) {
        appnode.Node.Disconnect();
        ncservice = appnode.NCService;
      }

      if(ncservice != null && _node_config.NCService.Checkpointing) {
        string checkpoint = ncservice.GetCheckpoint();
        string prev_cp = _node_config.NCService.Checkpoint;
        string empty_cp = (new Point()).ToString();
        if(!checkpoint.Equals(prev_cp) && !checkpoint.Equals(empty_cp))
        {
          _node_config.NCService.Checkpoint = checkpoint;
          _node_config.WriteConfig();
        }
      }

      Brunet.Util.FuzzyTimer.Instance.DoEvery(StopPem, 500, 500);
    }
  }
}
