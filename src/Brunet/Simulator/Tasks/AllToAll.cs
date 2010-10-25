// Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida
// For license, see the file LICENSE in the root directory of this software.

using Brunet.Concurrent;
using Brunet.Messaging;
using Brunet.Symphony;
using Brunet.Security.PeerSec.Symphony;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brunet.Simulator.Tasks {
  /// <summary>Helps performing a live AllToAll metrics on the Simulator</summary>
  public class AllToAll : Task {
    public readonly SortedList<Address, NodeMapping> Nodes;
    public readonly bool Secure;
    public readonly List<int> LatenciesInMs;

    protected long _waiting_on;
    protected long _start_time;

    public AllToAll(SortedList<Address, NodeMapping> nodes, bool secure,
        EventHandler finished) : base(finished)
    {
      Nodes = nodes;
      Secure = secure;

      LatenciesInMs = new List<int>();
      _waiting_on = 0;
      _start_time = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
    }

    protected void Callback(object o, EventArgs ea)
    {
      Channel q = o as Channel;
      try {
        RpcResult res = (RpcResult) q.Dequeue();
        int result = (int) res.Result;
        if(result != 0) {
          throw new Exception(res.Result.ToString());
        }

        long end_time = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        long latency = end_time - _start_time;
        LatenciesInMs.Add((int) latency);
      } catch(Exception e) {
        Console.WriteLine(e);
      }

      if(--_waiting_on == 0) {
        Finished();
      }
    }

    override public void Start() {
      base.Start();
      foreach(NodeMapping nm_from in Nodes.Values) {
        foreach(NodeMapping nm_to in Nodes.Values) {
          if(nm_from == nm_to) {
            continue;
          }

          ISender sender = null;
          if(Secure) {
            sender = nm_from.Sso.GetSecureSender(nm_to.Node.Address);
          } else {
            sender = new AHGreedySender(nm_from.Node, nm_to.Node.Address);
          }

          Channel q = new Channel(1);
          q.CloseEvent += Callback;
          try {
            nm_from.Node.Rpc.Invoke(sender, q, "sys:link.Ping", 0);
            _waiting_on++;
          } catch {
            _waiting_on--;
          }
        }
      }
    }

    override public string ToString()
    {
      StringBuilder sb = new StringBuilder();
      sb.Append("AllToAll State: ");
      if(Done) {
        double avg = Utils.Average(LatenciesInMs);
        double std = Utils.StandardDeviation(LatenciesInMs, avg);

        sb.Append("Complete");
        sb.Append("\n\tTime taken: ");
        sb.Append(TimeTaken);
        sb.Append("\n\tSuccessful measurements: ");
        sb.Append(LatenciesInMs.Count);
        sb.Append("\n\tAverage: ");
        sb.Append(avg);
        sb.Append("\n\tStandard Deviation: ");
        sb.Append(std);
      } else {
        sb.Append("In progress.");
      }
      return sb.ToString();
    }
  }
}
