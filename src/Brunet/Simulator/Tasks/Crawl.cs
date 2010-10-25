// Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida
// For license, see the file LICENSE in the root directory of this software.

using Brunet.Concurrent;
using Brunet.Messaging;
using Brunet.Symphony;
using Brunet.Security.PeerSec.Symphony;
using System;
using System.Text;
using System.Collections;

namespace Brunet.Simulator.Tasks {
  /// <summary>Helps performing a live crawl on the Simulator</summary>
  public class Crawl : Task {
    public readonly int Expected;
    public readonly Node Node;
    public readonly SymphonySecurityOverlord Bso;

    /// <summary>All nodes were queried (assuming no churn).</summary>
    public bool Success { get { return _crawled.Count == Expected; } }
    /// <summary>Left <=> Right edge agreement.</summary>
    public int Consistency { get { return _consistency; } }
    protected int _consistency;

    protected Hashtable _crawled;
    protected Address _first_left;
    protected Address _previous;

    public Crawl(Node node, int expected, SymphonySecurityOverlord bso,
        EventHandler finished) : base(finished)
    {
      Expected = expected;
      Bso = bso;
      Node = node;
      _crawled = new Hashtable(expected);
    }

    override public void Start()
    {
      base.Start();
      CrawlNext(Node.Address);
    }

    /// <summary> Handles the result of a crawl query.</summary>
    protected void CrawlHandler(object o, EventArgs ea)
    {
      Address addr = Node.Address;
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

        if(current.Equals(_first_left) && Node.Address.Equals(next)) {
          _consistency++;
        }

        _previous = current;
        addr = next;
      } catch {}
      CrawlNext(addr);
    }

    /// <summary> Attempts to crawl the next address.</summary>
    protected void CrawlNext(Address addr)
    {
      bool finished = false;
      if(_crawled.ContainsKey(addr)) {
        finished = true;
      } else {
        _crawled.Add(addr, true);
        try {
          ISender sender = null;
          if(Bso != null) {
            sender = Bso.GetSecureSender(addr);
          } else {
            sender = new AHGreedySender(Node, addr);
          }

          Channel q = new Channel(1);
          q.CloseEvent += CrawlHandler;
          Node.Rpc.Invoke(sender, q, "sys:link.GetNeighbors");
        } catch {
          finished = true;
        }
      }

      if(finished) {
        Finished();
      }
    }

    override public string ToString()
    {
      StringBuilder sb = new StringBuilder();
      sb.Append("Crawl State: ");
      if(Done) {
        sb.Append("Complete");
        sb.Append("\n\tTime taken: ");
        sb.Append(TimeTaken);
        sb.Append("\n\tNodes crawled: ");
        sb.Append(_crawled.Count);
        sb.Append("\n\tConsistency: ");
        sb.Append((_consistency * 1.0) / (_crawled.Count * 1.0));
      } else {
        sb.Append("In progress.");
      }
      return sb.ToString();
    }
  }
}
