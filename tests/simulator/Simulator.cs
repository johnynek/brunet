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
using Brunet.Util;
using Brunet.Security;
using Brunet.Security.Protocol;
using Brunet.Security.Transport;

namespace Brunet.Simulator {
  public class Simulator {
    public int StartingNetworkSize = 10;
    protected SortedList Nodes = new SortedList();
    protected SortedList TakenPorts = new SortedList();

    public int CurrentNetworkSize = 0;
    protected Random _rand = new Random();
    public readonly string BrunetNamespace;
    public double Broken = 0;

    public bool SecureEdges;
    public bool SecureSenders;
    protected RSACryptoServiceProvider SEKey;
    protected Certificate CACert;

    public Simulator()
    {
      StartingNetworkSize = 10;
      Nodes = new SortedList();
      TakenPorts = new SortedList();
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
      LinkProtocolState.EdgeVerifyMethod = EdgeVerify.AddressInSubjectAltName;
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

    protected int TakePort()
    {
      int port = TakenPorts.Count;
      while(TakenPorts.Contains(port)) {
        port = _rand.Next(0, 65535);
      }
      return port;
    }

    // adds a node to the pool
    public void AddNode(bool output) {
      AHAddress address = new AHAddress(new RNGCryptoServiceProvider());
      Node node = new StructuredNode(address, BrunetNamespace);
      NodeMapping nm = new NodeMapping();
      nm.Node = node;
      Nodes.Add((Address) address, nm);

      nm.Port = TakePort();

      TAAuthorizer auth = null;
      if(Broken != 0) {
        auth = new BrokenTAAuth(Broken);
      }

      EdgeListener el = new SimulationEdgeListener(nm.Port, 0, auth, true);

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
        el = new SecureEdgeListener(el, nm.BSO);
      }

      node.AddEdgeListener(el);

      if(Broken != 0) {
        el = new TunnelEdgeListener(node);
        node.AddEdgeListener(el);
      }

      ArrayList RemoteTAs = new ArrayList();
      for(int i = 0; i < 5 && i < TakenPorts.Count; i++) {
        int rport = (int) TakenPorts.GetByIndex(_rand.Next(0, TakenPorts.Count));
        RemoteTAs.Add(TransportAddressFactory.CreateInstance("brunet.function://127.0.0.1:" + rport));
      }
      node.RemoteTAs = RemoteTAs;

      TakenPorts[nm.Port] = nm.Port;

      if(output) {
        Console.WriteLine("Adding: " + nm.Node.Address);
      }
      node.Connect();
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
      TakenPorts.Remove(nm.Port);
      Nodes.RemoveAt(index);
    }

    /// <summary>Performs a crawl of the network using the ConnectionTable of
    /// each node.</summary>
    public bool CheckRing()
    {
      Console.WriteLine("Checking ring...");
      Address start_addr = (Address) Nodes.GetKeyList()[0];
      Address curr_addr = start_addr;

      for (int i = 0; i < Nodes.Count; i++) {
        Node node = ((NodeMapping) Nodes[curr_addr]).Node;
        ConnectionTable con_table = node.ConnectionTable;

        Connection con = null;
        try {
          con = con_table.GetLeftStructuredNeighborOf((AHAddress) curr_addr);
        }
        catch {}

        if(con == null) {
          Console.WriteLine("Found no connection.");
          return false;
        }
        Console.WriteLine("Hop {2}\t Address {0}\n\t Connection to left {1}\n", curr_addr, con, i);
        Address next_addr = con.Address;

        if (next_addr == null) {
          Console.WriteLine("Found no connection.");
          return false;
        }

        Connection lc = null;
        try {
          Node tnode = ((NodeMapping)Nodes[next_addr]).Node;
          lc = tnode.ConnectionTable.GetRightStructuredNeighborOf((AHAddress) next_addr);
        }
        catch {}

        if( (lc == null) || !curr_addr.Equals(lc.Address)) {
          Address left_addr = lc.Address;
          Console.WriteLine(curr_addr + " != " + left_addr);
          Console.WriteLine("Right had edge, but left has no record of it!\n{0} != {1}", con, lc);
          return false;
        }
        else if(next_addr.Equals(start_addr) && i != Nodes.Count -1) {
          Console.WriteLine("Completed circle too early.  Only {0} nodes in the ring.",
                            (i + 1));
          return false;
        }
        curr_addr = next_addr;
      }

      if(start_addr.Equals(curr_addr)) {
        Console.WriteLine("Ring properly formed!");
        return true;
      }
      return false;
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

    /// <summary>Disconnects all the nodes in the simulator.</summary>
    public void Disconnect()
    {
      foreach(DictionaryEntry de in Nodes) {
        Node node = ((NodeMapping) de.Value).Node;
        node.Disconnect();
      }
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

      public void Callback(object o, EventArgs ea) {
        Channel q = o as Channel;
        try {
          RpcResult res = (RpcResult) q.Dequeue();
          int result = (int) res.Result;
          if(result != 0) {
            throw new Exception(res.Result.ToString());
          }

          _total_latency += (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond) - _start_time;
        } catch(Exception e) {
          Console.WriteLine(e);
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
              lock(_sync) {
                _count++;
                _waiting_on++;
              }
            } catch(Exception e) {
              Console.WriteLine(e);
            }
          }
        }
      }
    }
  }

  public class NodeMapping {
    public int Port;
    public Node Node;
    public ProtocolSecurityOverlord BSO;
  }

  public class BrokenTAAuth : TAAuthorizer {
    double _prob;
    Hashtable _allowed;
    object _sync;
    Random _rand;

    public BrokenTAAuth(double probability) {
      _prob = probability;
      _allowed = new Hashtable();
      _sync = new object();
      _rand = new Random();
    }

    public override TAAuthorizer.Decision Authorize(TransportAddress a) {
      int port = ((IPTransportAddress) a).Port;
      lock(_sync) {
        if(!_allowed.Contains(port)) {
          if(_rand.NextDouble() > _prob) {
            _allowed[port] = TAAuthorizer.Decision.Allow;
          } else {
            _allowed[port] = TAAuthorizer.Decision.Deny;
          }
        }
      }
      return (TAAuthorizer.Decision) _allowed[port];
    }
  }
}
