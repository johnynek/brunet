/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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
using System;

namespace Brunet.Transport
{

  /**
   * This a derived class from the base class Edge.
   * It is designed for the sole purpose of testing the AHRoutingTable
   * The only functionality is that it has the local and remote TransportAddress
   */

  public class FakeEdge : Edge
  {

    private TransportAddress local_add;
    private TransportAddress remote_add;
    private readonly TransportAddress.TAType _tatype;

    public FakeEdge(TransportAddress local, TransportAddress remote) :
      this(local, remote, TransportAddress.TAType.Tcp)
    {
    }

    public FakeEdge(TransportAddress local, TransportAddress remote, TransportAddress.TAType tatype)
    {
      _tatype = tatype;
      local_add = local;
      remote_add = remote;
    }

    public override TransportAddress LocalTA
    {
      get
      {
        return local_add;
      }
    }
    public override TransportAddress RemoteTA
    {
      get
      {
        return remote_add;
      }
    }

    public override TransportAddress.TAType TAType
    {
      get
      {
        return _tatype;
      }
    }
  }

}
