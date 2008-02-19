/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;
using Brunet.Dht;
using System;
using System.Collections;

namespace Ipop {
  public partial class DhtServiceClient {
    public static ISoapDht GetSoapDhtClient() {
      return GetSoapDhtClient(64221);
    }
    public static ISoapDht GetSoapDhtClient(int port) {
      return (ISoapDht)Activator.GetObject(typeof(ISoapDht),
              "http://127.0.0.1:" + port + "/sd.rem");
    }
  }

  public interface ISoapDht : IDht {
    IBlockingQueue GetAsBlockingQueue(string key);
  }

  /**
   * Dht client side operations
   */
  public class DhtClientOp {
    public delegate bool PutOp(string key, string value,int ttl);
    public delegate DhtGetResult[] GetOp(string key);

    private IDht _dht;

    public DhtClientOp(IDht dht) {
      this._dht = dht;
    }

    public IAsyncResult BeginGetWithCallback(string key, AsyncCallback acb, object state) {
      GetOp op = new GetOp(this._dht.Get);
      IAsyncResult ar = op.BeginInvoke(key, acb, state);
      return ar;
    }

    public IAsyncResult BeginPutWithCallback(string key, string value, int ttl, AsyncCallback acb, object state) {
      PutOp op = new PutOp(this._dht.Put);
      IAsyncResult ar = op.BeginInvoke(key, value, ttl, acb, state);
      return ar;
    }

    public IAsyncResult BeginCreateWithCallback(string key, string value, int ttl, AsyncCallback acb, object state) {
      PutOp op = new PutOp(this._dht.Create);
      IAsyncResult ar = op.BeginInvoke(key, value, ttl, acb, state);
      return ar;
    }
  }

  /**
   * Asynchronous BlockingQueue operations
   */
  public class BlockingQueueOp {
    public delegate object DequeueOp(int millisec, out bool timedout);

    private IBlockingQueue _bq;

    public BlockingQueueOp(IBlockingQueue bq) {
      this._bq = bq;
    }

    public IAsyncResult BeginDequeueOp(int millisec, out bool timedout, AsyncCallback acb, object state) {
      DequeueOp op = new DequeueOp(this._bq.Dequeue);
      IAsyncResult ar = op.BeginInvoke(millisec, out timedout, acb, state);
      return ar;
    }
  }
}