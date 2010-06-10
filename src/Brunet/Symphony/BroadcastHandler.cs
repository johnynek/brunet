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
  /// <summary>Handles incoming broadcasts, returns the data for further
  /// processing by the node and forwards it to the next step, via a
  /// another BroadcastSender.</summary>
  public class BroadcastHandler : IDataHandler {
    /// <summary>The local node.</summary>
    public readonly StructuredNode Node;

    /// <summary>Creates a BroadcastHandler, don't forget to Subscribe to the
    /// Node.DemuxHandler with the PType -> BroadcastSender.PType.</summary>
    public BroadcastHandler(StructuredNode node)
    {
      Node = node;
    }

    public void HandleData(MemBlock data, ISender return_path, object state)
    {
      MemBlock user_data;
      // Parse BroadcastSender
      BroadcastSender bs = BroadcastSender.Parse(Node, data, out user_data);
      // Present the packet to the local handler
      BroadcastReceiver br = new BroadcastReceiver(bs);
      Node.HandleData(user_data, br, null);
      // Broadcast to the next hop
      bs.Send(user_data);
    }
  }
}
