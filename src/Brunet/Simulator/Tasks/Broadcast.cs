// Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida
// For license, see the file LICENSE in the root directory of this software.

using Brunet.Messaging;
using Brunet.Symphony;
using Brunet.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Brunet.Simulator.Tasks {
  public class Broadcast: Task, IDataHandler {
    public readonly SimpleSource BH;
    public readonly Node Forwarder;
    public readonly int Forwarders;
    public List<BroadcastReceiver> Results;
    protected DateTime _estimated_time_left;
    protected BroadcastSender _bs;

    /// <summary>Performs a broadcast from the node using the specified amount
    /// of forwarders.</summary>
    public Broadcast(SimpleSource bh, Node forwarder, int forwarders,
        EventHandler finished) : base(finished)
    {
      BH = bh;
      Forwarder = forwarder;
      Forwarders = forwarders;
      Results = new List<BroadcastReceiver>();
    }

    public void HandleData(MemBlock data, ISender sender, object state)
    {
      _estimated_time_left = DateTime.UtcNow.AddSeconds(1);

      BroadcastReceiver br = sender as BroadcastReceiver; 
      Results.Add(br);
    }

    protected void Finished(object o)
    {
      if(_estimated_time_left > DateTime.UtcNow) {
        SimpleTimer.Enqueue(Finished, null, 1000, 0);
        return;
      }

      BH.Unsubscribe(this);
      Finished();
    }

    override public void Start()
    {
      base.Start();

      _estimated_time_left = DateTime.UtcNow.AddSeconds(1);
      SimpleTimer.Enqueue(Finished, null, 1000, 0);

      BH.Subscribe(this, null);
      _bs = new BroadcastSender(Forwarder as StructuredNode, Forwarders);
      _bs.Send(Simulator.SimBroadcastPType);
    }

    public void WriteResultsToDisk(string filename)
    {
      if(!Done) {
        return;
      }

      using(FileStream fs = new FileStream(filename, FileMode.Append)) {
        using(StreamWriter sw = new StreamWriter(fs)) {
          sw.WriteLine(_bs.SentTo + ", 0");
          foreach(BroadcastReceiver br in Results) {
            sw.WriteLine(br.SentTo + ", " + br.Hops);
          }
        }
      }
    }

    override public string ToString()
    {
      StringBuilder sb = new StringBuilder();
      sb.Append("Broadcast State: ");
      if(!Done) {
        sb.Append("In progress.");
        return sb.ToString();
      }

      int slowest = -1;
      List<int> sent_to = new List<int>();
      foreach(BroadcastReceiver br in Results) {
        sent_to.Add(br.SentTo);
        slowest = Math.Max(slowest, br.Hops);
      }

      sent_to.Add(_bs.SentTo);
      double avg = Utils.Average(sent_to);
      double std = Utils.StandardDeviation(sent_to, avg);

      sb.Append("Complete");
      sb.Append(String.Format("\n\tAverage: {0}, StdDev: {1}", avg, std));
      sb.Append(String.Format("\n\tHit: {0}, in: {1} ", Results.Count + 1, slowest));
      return sb.ToString();
    }
  }
}
