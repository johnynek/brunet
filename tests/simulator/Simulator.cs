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

using Brunet.Concurrent;
using Brunet.Connections;
using Brunet.Messaging;
using Brunet.Security;
using Brunet.Security.Protocol;
using Brunet.Security.Transport;
using Brunet.Services;
using Brunet.Services.Coordinate;
using Brunet.Services.Dht;
using Brunet.Symphony;
using Brunet.Transport;
using Brunet.Tunnel;
using Brunet.Util;

namespace Brunet.Simulator {
  public class Simulator {
    public int StartingNetworkSize;
    public SortedList Nodes = new SortedList();
    protected SortedList TakenIDs = new SortedList();

    public int CurrentNetworkSize;
    protected Random _rand;
    public readonly string BrunetNamespace;
    public double Broken = 0;
    public int Seed {
      set {
        _rand = new Random(value);
        _seed = value;
      }
      get {
        return _seed;
      }
    }
    protected int _seed;

    public bool SecureEdges;
    public bool SecureSenders;
    public bool NCEnable;
    protected RSACryptoServiceProvider SEKey;
    protected Certificate CACert;

    public Simulator()
    {
      StartingNetworkSize = 0;
      CurrentNetworkSize = 0;
      Nodes = new SortedList();
      TakenIDs = new SortedList();

      _rand = new Random();
      BrunetNamespace = "testing" + _rand.Next();
      Broken = 0;
      SecureEdges = false;
      SecureSenders = false;
    }

    public void SecureStartup()
    {
      if(SEKey != null) {
        return;
      }
      SEKey = new RSACryptoServiceProvider();
      byte[] blob = SEKey.ExportCspBlob(false);
      RSACryptoServiceProvider rsa_pub = new RSACryptoServiceProvider();
      rsa_pub.ImportCspBlob(blob);
      CertificateMaker cm = new CertificateMaker("United States", "UFL", 
          "ACIS", "David Wolinsky", "davidiw@ufl.edu", rsa_pub,
          "brunet:node:abcdefghijklmnopqrs");
      Certificate cert = cm.Sign(cm, SEKey);
      CACert = cert;
    }

    public void Complete()
    {
      DateTime start = DateTime.UtcNow;
      long ticks_start = start.Ticks;
      long ticks_end = start.AddHours(1).Ticks;
      bool success = false;
      while(DateTime.UtcNow.Ticks < ticks_end) {
        success = CheckRing(false);
        if(success) {
          break;
        }
        SimpleTimer.RunStep();
      }
      AllToAll(false);
      if(success) {
        Console.WriteLine("It took {0} to complete the ring", DateTime.UtcNow - start);
      } else {
        Console.WriteLine("Unable to complete ring.");
      }
    }

    public void SimComplete()
    {
      while(!CheckRing(false)) {
        SimpleTimer.RunSteps(1000, false);
      }
    }

    public void AllToAll(bool secure)
    {
      AllToAllHelper a2ah = new AllToAllHelper(Nodes, secure);
      a2ah.Start();
      while(a2ah.Done == 0) {
        SimpleTimer.RunStep();
      }
    }

    public bool Crawl(bool log, bool secure)
    {
      NodeMapping nm = (NodeMapping) Nodes.GetByIndex(0);
      ProtocolSecurityOverlord bso = null;
      if(secure) {
        bso = nm.BSO;
      }

      CrawlHelper ch = new CrawlHelper(nm.Node, Nodes.Count, bso, log);
      ch.Start();
      while(ch.Done == 0) {
        SimpleTimer.RunStep();
      }

      return ch.Success;
    }

    /// <summary>Remove and return the next ID from availability.</summary>
    protected int TakeID()
    {
      int id = TakenIDs.Count;
      while(TakenIDs.Contains(id)) {
        id = _rand.Next(0, Int32.MaxValue);
      }
      return id;
    }

    protected AHAddress GenerateAddress()
    {
      byte[] addr = new byte[Address.MemSize];
      _rand.NextBytes(addr);
      Address.SetClass(addr, AHAddress._class);
      AHAddress ah_addr = new AHAddress(MemBlock.Reference(addr));
      if(Nodes.Contains(ah_addr)) {
        ah_addr = GenerateAddress();
      }
      return ah_addr;
    }

    // Adds a node to the pool
    public virtual Node AddNode()
    {
      return AddNode(TakeID(), GenerateAddress());
    }

    public virtual Node AddNode(int id, AHAddress address)
    {
      StructuredNode node = PrepareNode(id, address);
      node.Connect();
      CurrentNetworkSize++;
      return node;
    }

    protected virtual EdgeListener CreateEdgeListener(int id)
    {
      TAAuthorizer auth = null;
      if(Broken != 0 && id > 0) {
        auth = new BrokenTAAuth(Broken);
      }

      return new SimulationEdgeListener(id, 0, auth, true);
    }

    protected virtual ArrayList GetRemoteTAs()
    {
      ArrayList RemoteTAs = new ArrayList();
      for(int i = 0; i < 5 && i < TakenIDs.Count; i++) {
        int rid = (int) TakenIDs.GetByIndex(_rand.Next(0, TakenIDs.Count));
        RemoteTAs.Add(TransportAddressFactory.CreateInstance("b.s://" + rid));
      }
      if(Broken != 0) {
        RemoteTAs.Add(TransportAddressFactory.CreateInstance("b.s://" + 0));
      }

      return RemoteTAs;
    }

    protected virtual StructuredNode PrepareNode(int id, AHAddress address)
    {
      if(TakenIDs.Contains(id)) {
        throw new Exception("ID already taken");
      }

      StructuredNode node = new StructuredNode(address, BrunetNamespace);

      NodeMapping nm = new NodeMapping();
      TakenIDs[id] = nm.ID = id;
      nm.Node = node;
      Nodes.Add((Address) address, nm);

      EdgeListener el = CreateEdgeListener(nm.ID);

      if(SecureEdges || SecureSenders) {
        byte[] blob = SEKey.ExportCspBlob(true);
        RSACryptoServiceProvider rsa_copy = new RSACryptoServiceProvider();
        rsa_copy.ImportCspBlob(blob);

        CertificateMaker cm = new CertificateMaker("United States", "UFL", 
          "ACIS", "David Wolinsky", "davidiw@ufl.edu", rsa_copy,
          address.ToString());
        Certificate cert = cm.Sign(CACert, SEKey);

        CertificateHandler ch = new CertificateHandler();
        ch.AddCACertificate(CACert.X509);
        ch.AddSignedCertificate(cert.X509);

        ProtocolSecurityOverlord so = new ProtocolSecurityOverlord(node, rsa_copy, node.Rrm, ch);
        so.Subscribe(node, null);
        node.GetTypeSource(SecurityOverlord.Security).Subscribe(so, null);
        nm.BSO = so;
        node.HeartBeatEvent += so.Heartbeat;
      }

      if(SecureEdges) {
        node.EdgeVerifyMethod = EdgeVerify.AddressInSubjectAltName;
        el = new SecureEdgeListener(el, nm.BSO);
      }

      node.AddEdgeListener(el);

      node.RemoteTAs = GetRemoteTAs();

      ITunnelOverlap ito = null;
      if(NCEnable) {
        nm.NCService = new NCService(node, new Point());
// My evaluations show that when this is enabled the system sucks
//        (node as StructuredNode).Sco.TargetSelector = new VivaldiTargetSelector(node, ncservice);
        ito = new NCTunnelOverlap(nm.NCService);
      } else {
        ito = new SimpleTunnelOverlap();
      }

      if(Broken != 0) {
        el = new Tunnel.TunnelEdgeListener(node, ito);
        node.AddEdgeListener(el);
      }
      // Enables Dht data store
      new TableServer(node);
      return node;
    }

    public void RemoveNode(Node node, bool cleanly) {
      NodeMapping nm = (NodeMapping) Nodes[node.Address];
      if(cleanly) {
        node.Disconnect();
      } else {
        node.Abort();
      }
      TakenIDs.Remove(nm.ID);
      Nodes.Remove(node.Address);
      CurrentNetworkSize--;
    }

    // removes a node from the pool
    public void RemoveNode(bool output, bool cleanly) {
      int index = _rand.Next(0, Nodes.Count);
      NodeMapping nm = (NodeMapping) Nodes.GetByIndex(index);
      if(output) {
        Console.WriteLine("Removing: " + nm.Node.Address);
      }
      if(cleanly) {
        nm.Node.Disconnect();
      } else {
        nm.Node.Abort();
      }
      TakenIDs.Remove(nm.ID);
      Nodes.RemoveAt(index);
      CurrentNetworkSize--;
    }

    /// <summary>Performs a crawl of the network using the ConnectionTable of
    /// each node.</summary>
    public bool CheckRing(bool log)
    {
      return FindMissing(log).Count == 0;
    }

    public List<AHAddress> FindMissing(bool log)
    {
      if(log) {
        Console.WriteLine("Checking ring...");
      }

      Dictionary<AHAddress, bool> found = new Dictionary<AHAddress, bool>();
      Address start_addr = (Address) Nodes.GetKeyList()[0];
      Address curr_addr = start_addr;
      int count = 0;

      while(count < Nodes.Count) {
        found[curr_addr as AHAddress] = true;
        Node node = ((NodeMapping) Nodes[curr_addr]).Node;
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
          Console.WriteLine("Hop {2}\t Address {0}\n\t Connection to left {1}\n", curr_addr, con, count);
        }
        Address next_addr = con.Address;

        Connection lc = null;
        try {
          Node tnode = ((NodeMapping)Nodes[next_addr]).Node;
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
        count++;
        if(curr_addr.Equals(start_addr)) {
          break;
        }
      }

      List<AHAddress> missing = new List<AHAddress>();
      if(count == Nodes.Count) {
        if(log) {
          Console.WriteLine("Ring properly formed!");
        }
      } else {
        ICollection keys = Nodes.Keys;
        foreach(AHAddress addr in keys) {
          if(!found.ContainsKey(addr)) {
            missing.Add(addr);
          }
        }
      }

      return missing;
    }

    /// <summary>Prints all the connections for the nodes in the simulator.</summary>
    public void PrintConnections()
    {
      foreach(DictionaryEntry de in Nodes) {
        Node node = ((NodeMapping)de.Value).Node;
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
      foreach(DictionaryEntry de in Nodes) {
        Node node = ((NodeMapping)de.Value).Node;
        Console.WriteLine(node.Address + " " + node.ConState);
        count = node.IsConnected ? count + 1 : count;
      }
      Console.WriteLine("Connected: " + count);
    }

    /// <summary>Disconnects all the nodes in the simulator.</summary>
    public void Disconnect()
    {
      foreach(DictionaryEntry de in Nodes) {
        Node node = ((NodeMapping) de.Value).Node;
        node.Disconnect();
      }
      Nodes.Clear();
    }

    /// <summary>Helps performing a live crawl on the Simulator</summary>
    protected class CrawlHelper {
      protected int _count;
      protected Hashtable _crawled;
      protected DateTime _start;
      protected Node _node;
      protected int _done;
      public int Done { get { return _done; } }
      protected int _consistency;
      protected bool _log;
      protected Address _first_left;
      protected Address _previous;
      public bool Success { get { return _crawled.Count == _count; } }
      protected ProtocolSecurityOverlord _bso;

      public CrawlHelper(Node node, int count, ProtocolSecurityOverlord bso, bool log) {
        _count = count;
        _node = node;
        Interlocked.Exchange(ref _done, 0);
        _crawled = new Hashtable(count);
        _log = log;
        _bso = bso;
      }

      protected void CrawlNext(Address addr) {
        bool finished = false;
        if(_log && _crawled.Count < _count) {
          Console.WriteLine("Current address: " + addr);
        }
        if(_crawled.Contains(addr)) {
          finished = true;
        } else {
          _crawled.Add(addr, true);
          try {
            ISender sender = null;
            if(_bso != null) {
              sender = _bso.GetSecureSender(addr);
            } else {
              sender = new AHGreedySender(_node, addr);
            }

            Channel q = new Channel(1);
            q.CloseEvent += CrawlHandler;
            _node.Rpc.Invoke(sender, q, "sys:link.GetNeighbors");
          } catch(Exception e) {
            if(_log) {
              Console.WriteLine("Crawl failed" + e);
            }
            finished = true;
          }
        }

        if(finished) {
          Interlocked.Exchange(ref _done, 1);
          if(_log) {
            Console.WriteLine("Crawl stats: {0}/{1}", _crawled.Count, _count);
            Console.WriteLine("Consistency: {0}/{1}", _consistency, _crawled.Count);
            Console.WriteLine("Finished in: {0}", (DateTime.UtcNow - _start));
          }
        }
      }

      public void Start() {
        _start = DateTime.UtcNow;
        CrawlNext(_node.Address);
      }

      protected void CrawlHandler(object o, EventArgs ea) {
        Address addr = _node.Address;
        Channel q = (Channel) o;
        try {
          RpcResult res = (RpcResult) q.Dequeue();
          Hashtable ht = (Hashtable) res.Result;

          Address left = AddressParser.Parse((String) ht["left"]);
          Address next = AddressParser.Parse((String) ht["right"]);
          Address current = AddressParser.Parse((String) ht["self"]);
          if(left.Equals(_previous)) {
            _consistency++;
          } else if(_previous == null) {
            _first_left = left;
          }

          if(current.Equals(_first_left) && _node.Address.Equals(next)) {
            _consistency++;
          }

          _previous = current;
          addr = next;
        } catch(Exception e) {
          if(_log) {
            Console.WriteLine("Crawl failed due to exception...");
            Console.WriteLine(e);
          }
        }
        CrawlNext(addr);
      }
    }

    /// <summary>Helps performing a live AllToAll metrics on the Simulator</summary>
    protected class AllToAllHelper {
      protected long _total_latency;
      protected long _count;
      protected SortedList _nodes;
      protected int _done;
      protected long _waiting_on;
      public int Done { get { return _done; } }
      protected long _start_time;
      protected object _sync;
      protected bool _secure;

      public AllToAllHelper(SortedList nodes, bool secure) {
        _nodes = nodes;
        _count = 0;
        _total_latency = 0;
        _waiting_on = 0;
        _start_time = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        _done = 0;
        _sync = new object();
        _secure = secure;
      }

      protected void Callback(object o, EventArgs ea) {
        Channel q = o as Channel;
        try {
          RpcResult res = (RpcResult) q.Dequeue();
          int result = (int) res.Result;
          if(result != 0) {
            throw new Exception(res.Result.ToString());
          }

          _total_latency += (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond) - _start_time;
        } catch {
//        } catch(Exception e) {
//          Console.WriteLine(e);
        }
        if(Interlocked.Decrement(ref _waiting_on) == 0) {
          Interlocked.Exchange(ref _done, 1);
          Console.WriteLine("Performed {0} tests on {1} nodes", _count, _nodes.Count);
          Console.WriteLine("Latency avg: {0}", _total_latency / _count);
          DateTime start = new DateTime(_start_time * TimeSpan.TicksPerMillisecond);
          Console.WriteLine("Finished in: {0}", (DateTime.UtcNow - start));
        }
      }

      public void Start() {
        foreach(NodeMapping nm_from in _nodes.Values) {
          foreach(NodeMapping nm_to in _nodes.Values) {
            if(nm_from == nm_to) {
              continue;
            }

            ISender sender = null;
            if(_secure) {
              sender = nm_from.BSO.GetSecureSender(nm_to.Node.Address);
            } else {
              sender = new AHGreedySender(nm_from.Node, nm_to.Node.Address);
            }

            Channel q = new Channel(1);
            q.CloseEvent += Callback;
            try {
              nm_from.Node.Rpc.Invoke(sender, q, "sys:link.Ping", 0);
              _count++;
              _waiting_on++;
            } catch {
//            } catch(Exception e) {
//              Console.WriteLine(e);
            }
          }
        }
      }
    }

    /// <summary>Used to perform a DhtPut from a specific node.</summary>
    protected class DhtPut {
      public bool Done { get { return _done; } }
      protected bool _done;
      protected readonly Node _node;
      protected readonly MemBlock _key;
      protected readonly MemBlock _value;
      protected readonly int _ttl;
      protected readonly EventHandler _callback;
      public bool Successful { get { return _successful; } }
      protected bool _successful;

      public DhtPut(Node node, MemBlock key, MemBlock value, int ttl, EventHandler callback)
      {
        _node = node;
        _key = key;
        _value = value;
        _ttl = ttl;
        _callback = callback;
        _successful = false;
      }

      public void Start()
      {
        Channel returns = new Channel();
        returns.CloseEvent += delegate(object o, EventArgs ea) {
          try {
            _successful = (bool) returns.Dequeue();
          } catch {
          }

          _done = true;
          if(_callback != null) {
            _callback(this, EventArgs.Empty);
          }
        };
        Dht dht = new Dht(_node, 3, 20);
        dht.AsyncPut(_key, _value, _ttl, returns);
      }
    }

    /// <summary>Used to perform a DhtGet from a specific node.</summary>
    protected class DhtGet {
      public bool Done { get { return _done; } }
      protected bool _done;
      public Queue<MemBlock> Results;
      public readonly Node Node;
      protected readonly MemBlock _key;
      protected readonly EventHandler _enqueue;
      protected readonly EventHandler _close;

      public DhtGet(Node node, MemBlock key, EventHandler enqueue, EventHandler close)
      {
        Node = node;
        _key = key;
        _enqueue = enqueue;
        _close = close;
        Results = new Queue<MemBlock>();
      }

      public void Start()
      {
        Channel returns = new Channel();
        returns.EnqueueEvent += delegate(object o, EventArgs ea) {
          while(returns.Count > 0) {
            Hashtable result = null;
            try {
              result = returns.Dequeue() as Hashtable;
            } catch {
              continue;
            }

            byte[] res = result["value"] as byte[];
            if(res != null) {
              Results.Enqueue(MemBlock.Reference(res));
            }
          }
          if(_enqueue != null) {
            _enqueue(this, EventArgs.Empty);
          }
        };

        returns.CloseEvent += delegate(object o, EventArgs ea) {
          if(_close != null) {
            _close(this, EventArgs.Empty);
          }
          _done = true;
        };

        Dht dht = new Dht(Node, 3, 20);
        dht.AsyncGet(_key, returns);
      }
    }
  }

  public class NodeMapping {
    public int ID;
    public Node Node;
    public ProtocolSecurityOverlord BSO;
    public NCService NCService;
  }

  /// <summary> Randomly breaks all edges to remote entity.</summary>
  public class BrokenTAAuth : TAAuthorizer {
    double _prob;
    Hashtable _allowed;
    Random _rand;

    public BrokenTAAuth(double probability) {
      _prob = probability;
      _allowed = new Hashtable();
      _rand = new Random();
    }

    public override TAAuthorizer.Decision Authorize(TransportAddress a) {
      int id = ((SimulationTransportAddress) a).ID;
      if(id == 0) {
        return TAAuthorizer.Decision.Allow;
      }

      if(!_allowed.Contains(id)) {
        if(_rand.NextDouble() > _prob) {
          _allowed[id] = TAAuthorizer.Decision.Allow;
        } else {
          _allowed[id] = TAAuthorizer.Decision.Deny;
        }
      }

      return (TAAuthorizer.Decision) _allowed[id];
    }
  }
}
