// Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida
// For license, see the file LICENSE in the root directory of this software.

using Brunet.Concurrent;
using Brunet.Services.Dht;
using Brunet.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brunet.Simulator.Tasks {
  /// <summary>Used to perform a DhtPut from a specific node.</summary>
  public class DhtPut : Task {
    public readonly Node Node;
    public readonly MemBlock Key;
    public readonly MemBlock Value;
    public readonly int Ttl;
    public DhtPutResult Result { get { return _result; } }
    protected DhtPutResult _result;

    public DhtPut(Node node, MemBlock key, MemBlock value, int ttl,
        EventHandler finished) : base(finished)
    {
      Node = node;
      Key = key;
      Value = value;
      Ttl = ttl;
      _result = null;
    }

    override public void Start()
    {
      Channel returns = new Channel();
      returns.CloseEvent += delegate(object o, EventArgs ea) {
        try {
          bool success = (bool) returns.Dequeue();
          _result = new DhtPutResult(success, null);
        } catch (Exception e) {
          _result = new DhtPutResult(false, e);
        } finally {
          Finished();
        }
      };
      Dht dht = new Dht(Node, 3, 20);
      dht.AsyncPut(Key, Value, Ttl, returns);
    }

    public class DhtPutResult {
      public readonly bool Successful;
      public readonly Exception Exception;

      public DhtPutResult(bool successful, Exception exception)
      {
        Successful = successful;
        Exception = exception;
      }
    }

    override public string ToString()
    {
      StringBuilder sb = new StringBuilder();
      sb.Append("DhtPut State: ");
      if(Done) {
        sb.Append("Complete\n");
        if(Result.Successful) {
          sb.Append("\tPut Successful");
        } else {
          sb.Append("\tPut Failed with Exception: ");
          sb.Append(Result.Exception.ToString());
        }
      } else {
        sb.Append("In progress.");
      }
      return sb.ToString();
    }
  }
}
