using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.Remoting;
using CookComputing.XmlRpc;
using Brunet;

namespace Ipop {  
  public abstract class AbstractBlockingQueueAdapter : MarshalByRefObject, IBlockingQueue {
    [NonSerialized]
    protected BlockingQueue _bq;

    public readonly string Uri;

    public AbstractBlockingQueueAdapter(BlockingQueue bq) {
      this._bq = bq;
      this.Uri = this.GetRandomEncodedUri();
      System.Diagnostics.Debug.WriteLine(string.Format("Marshalling to Uri {0}", this.Uri));
      RemotingServices.Marshal(this, this.Uri);      
    }

    private string GetRandomEncodedUri() {
      RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
      byte[] bUri = new byte[100];
      provider.GetBytes(bUri);
      return Base32.Encode(bUri);
    }

    public abstract object Dequeue();
    public abstract object Dequeue(int millisec, out bool timedout);
    public abstract void Close();
  }

  public class SoapBlockingQueueAdapter : AbstractBlockingQueueAdapter {
    public SoapBlockingQueueAdapter(BlockingQueue bq) : base(bq) { }

    public override object Dequeue() {
      return this._bq.Dequeue();
    }

    public override object Dequeue(int millisec, out bool timedout) {
      return this._bq.Dequeue(millisec, out timedout);
    }

    public override void Close() {            
      _bq.Close();
      RemotingServices.Disconnect(this);
    }
  }

  public class XmlRpcBlockingQueueAdapter : AbstractBlockingQueueAdapter {
    public XmlRpcBlockingQueueAdapter(BlockingQueue bq) : base(bq) { }

    [XmlRpcMethod]
    public override object Dequeue() {      
      return this._bq.Dequeue();
    }

    [XmlRpcMethod]
    public override object Dequeue(int millisec, out bool timedout) {
      return this._bq.Dequeue(millisec, out timedout);
    }

    [XmlRpcMethod]
    public override void Close() {
      _bq.Close();
      RemotingServices.Disconnect(this);
    }
  }

}
