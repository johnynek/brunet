// Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida
// For license, see the file LICENSE in the root directory of this software.

using Brunet.Concurrent;
using Brunet.Services.Dht;
using Brunet.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Brunet.Simulator.Tasks {
  /// <summary>Used to perform a DhtGet from a specific node.</summary>
  public class DhtGet : Task {
    public readonly Queue<MemBlock> Results;
    public readonly Node Node;
    public readonly MemBlock Key;
    protected readonly EventHandler _enqueue;

    public DhtGet(Node node, MemBlock key, EventHandler enqueue,
        EventHandler close) : base(close)
    {
      Node = node;
      Key = key;
      _enqueue = enqueue;
      Results = new Queue<MemBlock>();
    }

    override public void Start()
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
        Finished();
      };

      Dht dht = new Dht(Node, 3, 20);
      dht.AsyncGet(Key, returns);
    }

    override public string ToString()
    {
      StringBuilder sb = new StringBuilder();
      sb.Append("DhtGet State: ");
      if(Done) {
        sb.Append("Complete\n");
        sb.Append("\tResults found: ");
        sb.Append(Results.Count);
      } else {
        sb.Append("In progress.");
      }
      return sb.ToString();
    }
  }
}
