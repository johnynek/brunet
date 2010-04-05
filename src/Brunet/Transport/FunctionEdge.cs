/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com> University of Florida

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

using System;
using System.Threading;
using System.Collections;

namespace Brunet.Transport
{

  /**
  * A Edge which does its transport locally
  * by calling a method on the other edge
  *
  * This Edge is for debugging purposes on
  * a single machine in a single process.
  */

  public class FunctionEdge : Edge
  {

    protected readonly int _l_id;
    protected readonly int _r_id;

    public FunctionEdge(IEdgeSendHandler s, int local_id, int remote_id, bool is_in) : base(s, is_in)
    {
      _l_id = local_id;
      _r_id = remote_id;
    }

    protected FunctionEdge _partner;
    public FunctionEdge Partner
    {
      get
      {
        return _partner;
      }
      set
      {
        Interlocked.Exchange<FunctionEdge>(ref _partner, value);
      }
    }


    public override TransportAddress.TAType TAType
    {
      get
      {
        return TransportAddress.TAType.Function;
      }
    }

    public int ListenerId {
      get { return _l_id; }
    }

    protected TransportAddress _local_ta;
    public override TransportAddress LocalTA
    {
      get {
        if ( _local_ta == null ) {
          _local_ta = TransportAddressFactory.CreateInstance("brunet.function://localhost:"
                                    + _l_id.ToString());
        }
        return _local_ta;
      }
    }
    protected TransportAddress _remote_ta;
    public override TransportAddress RemoteTA
    {
      get {
        if ( _remote_ta == null ) {
          _remote_ta = TransportAddressFactory.CreateInstance("brunet.function://localhost:"
                                    + _r_id.ToString());
        }
        return _remote_ta;
      }
    }
  }
}
