using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.Remoting;
using CookComputing.XmlRpc;
using Brunet;
using Brunet.Dht;

namespace Ipop {  
  public class BlockingQueueAdapter : MarshalByRefObject, IBlockingQueue {
    /**
     * Adaptee
     */
    [NonSerialized]
    protected BlockingQueue _bq;

    public BlockingQueueAdapter(BlockingQueue bq) {
      this._bq = bq;
    }

    public object Dequeue() {
      return this._bq.Dequeue();
    }
    public object Dequeue(int millisec, out bool timedout) {
      return this._bq.Dequeue(millisec, out timedout);
    }
    public void Close() {
      this._bq.Close();
    }

    public object Peek() {
      return this._bq.Peek();
    }

    public object Peek(int millisec, out bool timedout) {
      return this._bq.Peek(millisec, out timedout);
    }

    public void Enqueue(object o) {
      this._bq.Enqueue(o);
    }

    public bool Contains(object o) {
      return this._bq.Contains(o);
    }

    public void Clear() {
      this._bq.Clear();
    }
  }
}