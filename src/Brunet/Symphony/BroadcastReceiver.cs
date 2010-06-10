/*
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet.Connections;
using Brunet.Messaging;
using Brunet.Transport;
using Brunet.Util;

using System;
using System.Collections.Generic;

namespace Brunet.Symphony {
  /// <summary>Since HandleData requires a return path, this services as a
  /// means for the receiver to know where the data came from and aspects
  /// of where it is going.  Most of it is useful for evaluation.</summary>
  public class BroadcastReceiver : ISender {
    /// <summary>The local node.</summary>
    public readonly StructuredNode Node;
    /// <summary>The beginning (inclusive) of the range.</summary>
    public readonly AHAddress From;
    /// <summary>The originator of the broadcast.</summary>
    public readonly AHAddress Source;
    /// <summary>The ending (inclusive) of the range.</summary>
    public readonly AHAddress To;
    /// <summary>The count of nodes this node sent due to this broadcast.</summary>
    public int SentTo { get { return _bs.SentTo; } }
    public readonly int Hops;
    protected readonly ISender _sender;
    protected readonly BroadcastSender _bs;

    /// <summary>Stores data about this hop in a broadcast and provides a path
    /// back to the originating broadcaster.</summary>
    public BroadcastReceiver(BroadcastSender bs)
    {
      Node = bs.Node;
      From = bs.From;
      To = bs.To;
      Source = bs.Source;
      Hops = bs.Hops;
      _bs = bs;
      _sender = new AHSender(Node, Source,
          AHSender.DefaultTTLFor(Node.NetworkSize),
          AHHeader.Options.Exact);
    }

    public void Send(ICopyable data)
    {
      _sender.Send(data);
    }

    public string ToUri()
    {
      throw new NotImplementedException();
    }
  }
}
