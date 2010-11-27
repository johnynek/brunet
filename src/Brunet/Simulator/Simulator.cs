/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Threading;

using Brunet.Concurrent;
using Brunet.Collections;
using Brunet.Connections;
using Brunet.Messaging;
using Brunet.Security;
using Brunet.Security.Dtls;
using Brunet.Security.PeerSec;
using Brunet.Security.PeerSec.Symphony;
using Brunet.Security.Transport;
using Brunet.Services;
using Brunet.Services.Coordinate;
using Brunet.Services.Dht;
using Brunet.Simulator.Transport;
using Brunet.Symphony;
using Brunet.Transport;
using Brunet.Relay;
using Brunet.Util;

namespace Brunet.Simulator {
  public class NodeMapping {
    public IDht Dht;
    public RpcDhtProxy DhtProxy;
    public int ID;
    public NCService NCService;
    public Node Node;
    public PathELManager PathEM;
    public SecurityOverlord SO;
    public SymphonySecurityOverlord Sso;
  }

  public class Simulator {
    public int StartingNetworkSize;
    public SortedList<Address, NodeMapping> Nodes;
    public SortedList<int, NodeMapping> TakenIDs;

    public int CurrentNetworkSize;
    protected Random _rand;
    public readonly string BrunetNamespace;

    protected bool _start;
    protected readonly double _broken;
    protected readonly bool _pathing;
    protected readonly bool _secure_edges;
    protected readonly bool _secure_senders;
    protected readonly bool _dtls;
    public bool NCEnable;
    protected RSACryptoServiceProvider _se_key;
    protected Certificate _ca_cert;
    protected readonly Parameters _parameters;

    public static readonly PType SimBroadcastPType = new PType("simbcast");
    public readonly SimpleFilter SimBroadcastHandler;

    public Simulator(Parameters parameters) : this(parameters, true)
    {
    }

    protected Simulator(Parameters parameters, bool start)
    {
      SimulationTransportAddress.Enable();
      SimulationTransportAddressOther.Enable();
      _parameters = parameters;
      StartingNetworkSize = parameters.Size;
      CurrentNetworkSize = 0;
      Nodes = new SortedList<Address, NodeMapping>();
      TakenIDs = new SortedList<int, NodeMapping>();
      SimBroadcastHandler = new SimpleFilter();
      _rand = Node.SimulatorRandom;

      BrunetNamespace = "testing" + _rand.Next();
      _broken = parameters.Broken;
      _secure_edges = parameters.SecureEdges;
      _secure_senders = parameters.SecureSenders;
      _pathing = parameters.Pathing;
      _dtls = parameters.Dtls;
      if(_secure_edges || _secure_senders) {
        _se_key = new RSACryptoServiceProvider();
        byte[] blob = _se_key.ExportCspBlob(false);
        RSACryptoServiceProvider rsa_pub = new RSACryptoServiceProvider();
        rsa_pub.ImportCspBlob(blob);
        CertificateMaker cm = new CertificateMaker("United States", "UFL", 
            "ACIS", "David Wolinsky", "davidiw@ufl.edu", rsa_pub,
            "brunet:node:abcdefghijklmnopqrs");
        Certificate cert = cm.Sign(cm, _se_key);
        _ca_cert = cert;
      }

      if(parameters.LatencyMap != null) {
        SimulationEdgeListener.LatencyMap = parameters.LatencyMap;
      }

      if(start) {
        Start();
      }
    }

    protected void Start()
    {
      _start = true;
      for(int i = 0; i < _parameters.Size; i++) {
        AddNode();
      }

      TransportAddress broken_ta = TransportAddressFactory.CreateInstance("b.s://" + 0);
      for(int idx = 0; idx < Nodes.Count; idx++) {
        NodeMapping nm = Nodes.Values[idx];
        var tas = new List<TransportAddress>();
        int cidx = idx + 1;
        cidx = cidx == Nodes.Count ? 0 : cidx;
        tas.Add(Nodes.Values[cidx].Node.LocalTAs[0]);
        if(_broken != 0) {
          tas.Add(broken_ta);
        }
        nm.Node.RemoteTAs = tas;
      }
      foreach(NodeMapping nm in Nodes.Values) {
        nm.Node.Connect();
      }
      _start = false;
    }

    // The following are some helper functions

    /// <summary>This is an example of an
    public bool Complete(bool quiet)
    {
      DateTime start = DateTime.UtcNow;
      long ticks_end = start.AddHours(1).Ticks;
      bool success = false;
      while(DateTime.UtcNow.Ticks < ticks_end) {
        success = CheckRing(false);
        if(success) {
          break;
        }
        SimpleTimer.RunStep();
      }

      if(!quiet) {
        if(success) {
          Console.WriteLine("It took {0} to complete the ring", DateTime.UtcNow - start);
        } else {
          PrintConnections();
          PrintConnectionState();
          Console.WriteLine("Unable to complete ring.");
        }
      }

      return success;
    }

    public NodeMapping RandomNode()
    {
      return Nodes.Values[_rand.Next(0, Nodes.Count)];
    }

    /// <summary>Revoke a random node from a random node.</summary>
    public NodeMapping Revoke(bool log)
    {
      NodeMapping revoked = Nodes.Values[_rand.Next(0, Nodes.Count)];
      NodeMapping revoker = Nodes.Values[_rand.Next(0, Nodes.Count)];
      while(revoked != revoker) {
        revoker = Nodes.Values[_rand.Next(0, Nodes.Count)];
      }
 
      string username = revoked.Node.Address.ToString().Replace('=', '0');
      UserRevocationMessage urm = new UserRevocationMessage(_se_key, username);
      BroadcastSender bs = new BroadcastSender(revoker.Node as StructuredNode);
      bs.Send(new CopyList(BroadcastRevocationHandler.PType, urm));
      if(log) {
        Console.WriteLine("Revoked: " + revoked.Node.Address);
      }
      return revoked;
    }

    // The follow methods are used to add nodes to the current simulation

    /// <summary>Remove and return the next ID from availability.</summary>
    protected int TakeID()
    {
      int id = TakenIDs.Count;
      while(TakenIDs.ContainsKey(id)) {
        id = _rand.Next(0, Int32.MaxValue);
      }
      return id;
    }

    /// <summary>Generate a new unique address, there is potential for
    /// collissions when we make the address space small.</summary>
    protected AHAddress GenerateAddress()
    {
      byte[] addr = new byte[Address.MemSize];
      _rand.NextBytes(addr);
      Address.SetClass(addr, AHAddress._class);
      AHAddress ah_addr = new AHAddress(MemBlock.Reference(addr));
      if(Nodes.ContainsKey(ah_addr)) {
        ah_addr = GenerateAddress();
      }
      return ah_addr;
    }

    /// <summary>Return the SimulationEdgeListener.</summary>
    protected virtual EdgeListener CreateEdgeListener(int id)
    {
      TAAuthorizer auth = null;
      if(_broken != 0 && id > 0) {
        auth = new BrokenTAAuth(_broken);
      }

      return new SimulationEdgeListener(id, 0, auth, true);
    }

    /// <summary>Return a small list of random TAs.</summary>
    protected virtual List<TransportAddress> GetRemoteTAs()
    {
      var RemoteTAs = new List<TransportAddress>();
      for(int i = 0; i < 5 && i < TakenIDs.Count; i++) {
        int rid = TakenIDs.Keys[_rand.Next(0, TakenIDs.Count)];
        RemoteTAs.Add(TransportAddressFactory.CreateInstance("b.s://" + rid));
      }
      if(_broken != 0) {
        RemoteTAs.Add(TransportAddressFactory.CreateInstance("b.s://" + 0));
      }

      return RemoteTAs;
    }

    protected virtual StructuredNode PrepareNode(int id, AHAddress address)
    {
      if(TakenIDs.ContainsKey(id)) {
        throw new Exception("ID already taken");
      }

      StructuredNode node = new StructuredNode(address, BrunetNamespace);

      NodeMapping nm = new NodeMapping();
      nm.ID = id;
      TakenIDs[id] = nm;
      nm.Node = node;
      Nodes.Add((Address) address, nm);

      EdgeListener el = CreateEdgeListener(nm.ID);

      if(_secure_edges || _secure_senders) {
        byte[] blob = _se_key.ExportCspBlob(true);
        RSACryptoServiceProvider rsa_copy = new RSACryptoServiceProvider();
        rsa_copy.ImportCspBlob(blob);

        string username = address.ToString().Replace('=', '0');
        CertificateMaker cm = new CertificateMaker("United States", "UFL", 
          "ACIS", username, "davidiw@ufl.edu", rsa_copy,
          address.ToString());
        Certificate cert = cm.Sign(_ca_cert, _se_key);

        CertificateHandler ch = null;
        if(_dtls) {
          ch = new OpenSslCertificateHandler();
        } else {
          ch = new CertificateHandler();
        }
        ch.AddCACertificate(_ca_cert.X509);
        ch.AddSignedCertificate(cert.X509);

        if(_dtls) {
          nm.SO = new DtlsOverlord(rsa_copy, ch, PeerSecOverlord.Security);
        } else {
          nm.Sso = new SymphonySecurityOverlord(node, rsa_copy, ch, node.Rrm);
          nm.SO = nm.Sso;
        }

        var brh = new BroadcastRevocationHandler(_ca_cert, nm.SO);
        node.GetTypeSource(BroadcastRevocationHandler.PType).Subscribe(brh, null);
        ch.AddCertificateVerification(brh);
        nm.SO.Subscribe(node, null);
        node.GetTypeSource(PeerSecOverlord.Security).Subscribe(nm.SO, null);
      }

      if(_pathing) {
        nm.PathEM = new PathELManager(el, nm.Node);
        nm.PathEM.Start();
        el = nm.PathEM.CreatePath();
        PType path_p = PType.Protocol.Pathing;
        nm.Node.DemuxHandler.GetTypeSource(path_p).Subscribe(nm.PathEM, path_p);
      }

      if(_secure_edges) {
        node.EdgeVerifyMethod = EdgeVerify.AddressInSubjectAltName;
        el = new SecureEdgeListener(el, nm.SO);
      }

      node.AddEdgeListener(el);

      if(!_start) {
        node.RemoteTAs = GetRemoteTAs();
      }

      IRelayOverlap ito = null;
      if(NCEnable) {
        nm.NCService = new NCService(node, new Point());
// My evaluations show that when this is enabled the system sucks
//        (node as StructuredNode).Sco.TargetSelector = new VivaldiTargetSelector(node, ncservice);
        ito = new NCRelayOverlap(nm.NCService);
      } else {
        ito = new SimpleRelayOverlap();
      }

      if(_broken != 0) {
        el = new Relay.RelayEdgeListener(node, ito);
        if(_secure_edges) {
          el = new SecureEdgeListener(el, nm.SO);
        }
        node.AddEdgeListener(el);
      }

      BroadcastHandler bhandler = new BroadcastHandler(node as StructuredNode);
      node.DemuxHandler.GetTypeSource(BroadcastSender.PType).Subscribe(bhandler, null);
      node.DemuxHandler.GetTypeSource(SimBroadcastPType).Subscribe(SimBroadcastHandler, null);

      // Enables Dht data store
      new TableServer(node);
      nm.Dht = new Dht(node, 3, 20);
      nm.DhtProxy = new RpcDhtProxy(nm.Dht, node);
      return node;
    }

    ///<summary>Add a new (random) node to the simulation.</summary>
    public virtual Node AddNode()
    {
      return AddNode(TakeID(), GenerateAddress());
    }

    ///<summary>Add a new specific node to the simulation.</summary>
    public virtual Node AddNode(int id, AHAddress address)
    {
      if(TakenIDs.ContainsKey(id)) {
        throw new Exception("ID already taken");
      }

      StructuredNode node = PrepareNode(id, address);
      if(!_start) {
        node.Connect();
      }
      CurrentNetworkSize++;
      return node;
    }

    // The next set of methods handle the removal of nodes from the simulation

    // removes a node from the pool
    public void RemoveNode(Node node, bool cleanly, bool output) {
      NodeMapping nm = Nodes[node.Address];
      if(output) {
        Console.WriteLine("Removing: " + nm.Node.Address);
      }
      if(cleanly) {
        node.Disconnect();
      } else {
        node.Abort();
      }
      TakenIDs.Remove(nm.ID);
      Nodes.Remove(node.Address);
      if(_pathing) {
        nm.PathEM.Stop();
      }
      CurrentNetworkSize--;
    }

    public void RemoveNode(bool cleanly, bool output) {
      int index = _rand.Next(0, Nodes.Count);
      NodeMapping nm = Nodes.Values[index];
      RemoveNode(nm.Node, cleanly, output);
    }

    // These methods assist with determining state of the overlay

    static public bool AreConnected(Node node0, Node node1)
    {
      Address addr0 = node0.Address, addr1 = node1.Address;
      bool connected = node0.ConnectionTable.GetConnection(ConnectionType.Structured, addr1) != null;
      connected &= node1.ConnectionTable.GetConnection(ConnectionType.Structured, addr0) != null;
      return connected;
    }

    /// <summary>Performs a crawl of the network using the ConnectionTable of
    /// each node.</summary>
    public bool CheckRing(bool log)
    {
      return FindMissing(log).Count == 0;
    }

    /// <summary>Returns a list of missing nodes, while crawling the simulation.
    /// This is an example of a PassiveTask.</summary>
    public List<AHAddress> FindMissing(bool log)
    {
      if(log) {
        Console.WriteLine("Checking ring...");
      }

      Dictionary<AHAddress, bool> found = new Dictionary<AHAddress, bool>();
      if(Nodes.Count == 0) {
        return new List<AHAddress>(0);
      }
      Address start_addr = Nodes.Keys[0];
      Address curr_addr = start_addr;

      while(found.Count < Nodes.Count) {
        found[curr_addr as AHAddress] = true;
        Node node = Nodes[curr_addr].Node;
        ConnectionTable con_table = node.ConnectionTable;

        Connection con = null;
        try {
          con = con_table.GetLeftStructuredNeighborOf((AHAddress) curr_addr);
        } catch {
          if(log) {
            Console.WriteLine("Found no connection.");
          }
          break;
        }

        if(log) {
          Console.WriteLine("Hop {2}\t Address {0}\n\t Connection to left {1}\n", curr_addr, con, found.Count);
        }
        Address next_addr = con.Address;

        Connection lc = null;
        try {
          Node tnode = Nodes[next_addr].Node;
          lc = tnode.ConnectionTable.GetRightStructuredNeighborOf((AHAddress) next_addr);
        } catch {}

        if( (lc == null) || !curr_addr.Equals(lc.Address)) {
          if(log) {
            if(lc != null) {
              Console.WriteLine(curr_addr + " != " + lc.Address);
            }
            Console.WriteLine("Right had edge, but left has no record of it!\n{0} != {1}", con, lc);
          }
          break;
        }
        curr_addr = next_addr;
        if(curr_addr.Equals(start_addr)) {
          break;
        }
      }

      List<AHAddress> missing = new List<AHAddress>();
      if(found.Count == Nodes.Count) {
        if(log) {
          Console.WriteLine("Ring properly formed!");
        }
      } else {
        foreach(AHAddress addr in Nodes.Keys) {
          if(!found.ContainsKey(addr)) {
            missing.Add(addr);
          }
        }
      }

      if(found.Count < CurrentNetworkSize) {
        // A node must be registered, but uncreated
        missing.Add(default(AHAddress));
      }
      return missing;
    }

    // The following method print out the current state of the simulation

    /// <summary>Prints all the connections for the nodes in the simulator.</summary>
    public void PrintConnections()
    {
      foreach(NodeMapping nm in Nodes.Values) {
        Node node = nm.Node;
        PrintConnections(node);
        Console.WriteLine("==============================================================");
      }
    }

    public void PrintConnections(Node node) {
      IEnumerable ie = node.ConnectionTable.GetConnections(ConnectionType.Structured);
      Console.WriteLine("Connections for Node: " + node.Address);
      foreach(Connection c in ie) {
        Console.WriteLine(c);
      }
    }

    public void PrintConnectionState()
    {
      int count = 0;
      foreach(NodeMapping nm in Nodes.Values) {
        Node node = nm.Node;
        Console.WriteLine(node.Address + " " + node.ConState);
        count = node.IsConnected ? count + 1 : count;
      }
      Console.WriteLine("Connected: " + count);
    }

    /// <summary>Disconnects all the nodes in the simulator.</summary>
    public void Disconnect()
    {
      SimulationEdgeListener.Clear();
      foreach(NodeMapping nm in Nodes.Values) {
        Node node = nm.Node;
        node.Disconnect();
      }
      Nodes.Clear();
    }
  }
}
