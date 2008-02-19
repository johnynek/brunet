/**
 * This file contains MBR Adapters for FDht and DhtOp
 * Protocols include Soap and XmlRpc
 */
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using Brunet.Dht;
using Brunet;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting;
using System.Security.Cryptography;
using CookComputing.XmlRpc;

namespace Ipop {
  public abstract class DhtAdapter : MarshalByRefObject, IDht {
    [NonSerialized]
    public Dht _dht;
    /* Use cache so that we don't experience a leak, this should be replaced 
     * with some timeout mechanism to support multiple interactions at the 
     * same time.
     */
    [NonSerialized]
    protected Cache _bqs = new Cache(100);

    public DhtAdapter(Dht dht) {
      this._dht = dht;
    }

    public DhtAdapter() { ;}

    public virtual bool Create(string key, string value, int ttl) {
      try {
        _dht.Create(key, value, ttl);
        return true;
      }
      catch {
        return false;
      }
    }

    public virtual bool Put(string key, string value, int ttl) {
      try {
        _dht.Put(key, value, ttl);
        return true;
      }
      catch {
        return false;
      }
    }

    public virtual DhtGetResult[] Get(string key) {
      return _dht.Get(key);
    }

    public virtual string BeginGet(string key) {
      BlockingQueue q  = new BlockingQueue();
      this._dht.AsGet(key, q);
      string tk = this.GenToken(key);
      this._bqs.Add(tk, q);
      return tk;
    }

    public virtual DhtGetResult ContinueGet(string token) {
      BlockingQueue q = (BlockingQueue)this._bqs[token];
      if(q == null) {
        throw new ArgumentException("Invalid token");
      }
      DhtGetResult dgr = null;
      try {
        dgr = (DhtGetResult)q.Dequeue();
      }
      catch {
        dgr = DhtGetResult.Empty();
        this._bqs.Remove(q);
      }
      return dgr;
    }

    public virtual void EndGet(string token) {
      BlockingQueue q = (BlockingQueue)this._bqs[token];
      if (q == null) {
        throw new ArgumentException("Invalid token");
      }
      else {
        q.Close();
        this._bqs.Remove(q);
      }
    }

    public abstract IDictionary GetDhtInfo();

    private string GenToken(string key) {
      RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
      byte[] token = new byte[20];
      provider.GetBytes(token);
      string res = string.Empty;
      for(int i = 0; i < token.Length; i++) {
        res += token[i].ToString();
      }
      string real_tk = key + ":" + res;
      return real_tk;
    }

    // This object is intended to stay in memory
    public override object InitializeLifetimeService() {
      ILease lease = (ILease)base.InitializeLifetimeService();
      if (lease.CurrentState == LeaseState.Initial) {
        lease.InitialLeaseTime = TimeSpan.Zero; //infinite lifetime
      }
      return lease;
      }
    }


  public class SoapDht : DhtAdapter, ISoapDht {
    public SoapDht(Dht dht) : base(dht) { }

    public IBlockingQueue GetAsBlockingQueue(string key) {
      BlockingQueue bq = new BlockingQueue();
      this._dht.AsGet(key, bq);
      BlockingQueueAdapter adpt = new BlockingQueueAdapter(bq);
      return adpt;
    }

    public override IDictionary GetDhtInfo() {
      Hashtable ht = new Hashtable();
      ht.Add("address", _dht.node.Address.ToString());
      return ht;
    }
  }

  /// <summary>
  /// Dht stub using XmlRpc protocol
  /// </summary>
  public class XmlRpcDht : DhtAdapter {
    public XmlRpcDht(Dht dht) : base(dht) { }

    [XmlRpcMethod]
    public override bool Create(string key, string value, int ttl) {
      return base.Create(key, value, ttl);
    }

    [XmlRpcMethod]
    public override bool Put(string key, string value, int ttl) {
      return base.Put(key, value, ttl);
    }

    [XmlRpcMethod]
    public override DhtGetResult[] Get(string key) {
      return base.Get(key);
    }

    [XmlRpcMethod]
    public override string BeginGet(string key) {
      return base.BeginGet(key);
    }

    [XmlRpcMethod]
    public override DhtGetResult ContinueGet(string token) {
      return base.ContinueGet(token);
    }

    [XmlRpcMethod]
    public override void EndGet(string token) {
      base.EndGet(token);
    }

    [XmlRpcMethod]
    public override IDictionary GetDhtInfo() {
      XmlRpcStruct xrs = new XmlRpcStruct();
      xrs.Add("address", _dht.node.Address.ToString());
      return xrs;
    }
  }
}
