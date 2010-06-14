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

using Brunet;
using Brunet.Security;
using Brunet.Util;
using Brunet.Transport;
using System;
using System.Threading;

using Brunet.Messaging;
namespace Brunet.Security.Transport {
  ///<summary>Provides a Security Wrapper for generic Edges.  Basically binds a
  ///SecurityAssociation to an Edge, so that all traffic must traverse the
  ///SecurityAssociation.  This clsas is thread-safe.</summary>
  public class SecureEdge: WrapperEdge, IDataHandler {
    ///<summary>The Security provider.</summary>
    public readonly SecurityAssociation SA;

    public SecureEdge(Edge edge, SecurityAssociation sa):
      base(edge, false)
    {
      SA = sa;
    }

    ///<summary>Pushes the data to the SecurityAssociation who will send it
    ///over the underlying edge.</summary>
    public override void Send(ICopyable data) {
      if(IsClosed) {
        throw new EdgeClosedException("SecureEdge has been closed.");
      }
      try {
        SA.Send(data);
      } catch(Exception e) {
        throw new EdgeException(IsClosed, "Unable to send on SE", e);
      }
    }

    public override bool Close() {
      if(!base.Close()) {
        return false;
      }
      SA.Close("Edge closed!");
      return true;
    }

    public override string ToString() {
      return "SecureEdge: " + _edge.ToString();
    }
  }
}
