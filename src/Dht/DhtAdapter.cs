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
using System.Runtime.Remoting;
using System.Security.Cryptography;
using CookComputing.XmlRpc;

namespace Ipop {
  public class DhtAdapter : MarshalByRefObject, IDht {
    [NonSerialized]
    protected FDht _dht;
    [NonSerialized]
    protected DhtOp _dhtOp;
    [NonSerialized]
    protected Hashtable _bqs = new Hashtable();

    public DhtAdapter(FDht dht) {
      this._dht = dht;
      this._dhtOp = new DhtOp(_dht);
    }

    public DhtAdapter() { ;}

    public virtual string Create(string key, string value, string password, int ttl) {
      return _dhtOp.Create(key, value, password, ttl);
    }

    public virtual string Put(string key, string value, string password, int ttl) {
      return _dhtOp.Put(key, value, password, ttl);
    }

    public virtual DhtGetResult[] Get(string key) {
      return _dhtOp.Get(key);
    }

    public virtual string BeginGet(string key) {
      BlockingQueue q  = this._dhtOp.AsGet(key);
      string tk = this.GenToken(key);
      this._bqs.Add(tk, q);
      return tk;
    }

    /**
     * Wait for at most 1 second to see what can be got.
     * If nothing in the queue, just return an empty array
     * If the token if incorrect, throw exception
     */
    public virtual DhtGetResult[] ContinueGet(string token) {
      List<DhtGetResult> dgrs = new List<DhtGetResult>();
      BlockingQueue q = (BlockingQueue)this._bqs[token];
      if(q == null) {
        throw new ArgumentException("Invalid token");
      }            
      while (true) {
        try {
          bool timedout = false;
          DhtGetResult dgr = (DhtGetResult)q.Dequeue(1000, out timedout);
          if (!timedout) {
            dgrs.Add(dgr);
          } else {
            break;
          }
        } catch (Exception) {
          break;
        }
      }
      
      //list could be empty here
      return dgrs.ToArray();
    }

    public virtual void EndGet(string token) {
      BlockingQueue q = (BlockingQueue)this._bqs[token];
      if (q == null) {
        throw new ArgumentException("Invalid token");
      } else {
        q.Close();
        this._bqs.Remove(q);
      }
    }

    private string GenToken(string key) {
      RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
      byte[] token = new byte[50];
      provider.GetBytes(token);
      string real_tk = key + ":" + Encoding.UTF8.GetString(token);
      return real_tk;
    }
  }


  public class SoapDht : DhtAdapter, ISoapDht {
    public SoapDht(FDht dht) : base(dht) { }

    public IBlockingQueue GetAsBlockingQueue(string key) {
      BlockingQueue bq = this._dhtOp.AsGet(key);
      BlockingQueueAdapter adpt = new BlockingQueueAdapter(bq);
      return adpt;
    }
  }

  /// <summary>
  /// Dht stub using XmlRpc protocol
  /// </summary>
  public class XmlRpcDht : DhtAdapter {
    public XmlRpcDht(FDht dht) : base(dht) { }

    [XmlRpcMethod]
    public override string Create(string key, string value, string password, int ttl) {
      return base.Create(key, value, password, ttl);
    }

    [XmlRpcMethod]
    public override string Put(string key, string value, string password, int ttl) {
      return base.Put(key, value, password, ttl);
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
    public override DhtGetResult[] ContinueGet(string token) {
      return base.ContinueGet(token);
    }

    [XmlRpcMethod]
    public override void EndGet(string token) {
      base.EndGet(token);
    }
  }
}