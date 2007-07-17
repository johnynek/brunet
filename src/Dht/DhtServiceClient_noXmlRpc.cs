using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;
using Brunet.Dht;
using System;
using System.Collections;

namespace Ipop {
  public partial class DhtServiceClient {
    public static ISoapDht GetSoapDhtClient() {
      ISoapDht dht = (ISoapDht)Activator.GetObject(typeof(ISoapDht), "http://127.0.0.1:64221/sd.rem");
      return dht;
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