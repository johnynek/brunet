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

/**
 * This file contains MBR Adapters for FDht and DhtOp
 * Protocols include Soap and XmlRpc
 */
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using Brunet.DistributedServices;
using Brunet;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting;
using System.Security.Cryptography;
using CookComputing.XmlRpc;

namespace Brunet.Rpc {
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

    public virtual bool Create(byte[] key, byte[] value, int ttl) {
      try {
        _dht.Create(MemBlock.Reference(key), MemBlock.Reference(value), ttl);
        return true;
      }
      catch {
        return false;
      }
    }

    public virtual bool Put(byte[] key, byte[] value, int ttl) {
      try {
        _dht.Put(MemBlock.Reference(key), MemBlock.Reference(value), ttl);
        return true;
      }
      catch {
        return false;
      }
    }

    public virtual IDictionary[] Get(byte[] key) {
      return (IDictionary[]) _dht.Get(MemBlock.Reference(key));
    }

    public virtual byte[] BeginGet(byte[] key) {
      BlockingQueue q  = new BlockingQueue();
      this._dht.AsGet(MemBlock.Reference(key), q);
      byte[] tk = GenToken(key);
      _bqs.Add(MemBlock.Reference(tk), q);
      return tk;
    }

    public virtual IDictionary ContinueGet(byte[] token) {
      MemBlock tk = MemBlock.Reference(token);
      BlockingQueue q = (BlockingQueue)this._bqs[tk];
      if(q == null) {
        throw new ArgumentException("Invalid token");
      }
      IDictionary res = null;
      try {
        res = (IDictionary) q.Dequeue();
      }
      catch {
        res = new Hashtable();
        _bqs.Remove(q);
      }
      return res;
    }

    public virtual void EndGet(byte[] token) {
      MemBlock tk = MemBlock.Reference(token);
      BlockingQueue q = (BlockingQueue)this._bqs[tk];
      if (q == null) {
        throw new ArgumentException("Invalid token");
      }
      else {
        q.Close();
        this._bqs.Remove(q);
      }
    }

    public abstract IDictionary GetDhtInfo();

    private byte[] GenToken(byte[] key) {
      RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
      byte[] token = new byte[20];
      provider.GetBytes(token);
      byte[] res = new byte[40];
      key.CopyTo(res, 0);
      token.CopyTo(res, 20);
      return res;
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

    public IBlockingQueue GetAsBlockingQueue(byte[] key) {
      BlockingQueue bq = new BlockingQueue();
      this._dht.AsGet(MemBlock.Reference(key), bq);
      BlockingQueueAdapter adpt = new BlockingQueueAdapter(bq);
      return adpt;
    }

    public override IDictionary GetDhtInfo() {
      Hashtable ht = new Hashtable();
      ht.Add("address", _dht.Node.Address.ToString());
      return ht;
    }
  }

  /// <summary>
  /// Dht stub using XmlRpc protocol
  /// </summary>
  public class XmlRpcDht : DhtAdapter {
    public XmlRpcDht(Dht dht) : base(dht) { }

    protected XmlRpcStruct IDictionaryToXmlRpcStruct(IDictionary dict) {
      XmlRpcStruct str = new XmlRpcStruct();
      foreach(DictionaryEntry de in dict) {
        str.Add(de.Key as string, de.Value);
      }
      return str;
    }

    [XmlRpcMethod]
    public override bool Create(byte[] key, byte[] value, int ttl) {
      return base.Create(key, value, ttl);
    }

    [XmlRpcMethod]
    public override bool Put(byte[] key, byte[] value, int ttl) {
      return base.Put(key, value, ttl);
    }

    [XmlRpcMethod]
    new public XmlRpcStruct[] Get(byte[] key) {
      IDictionary[] vals = base.Get(key);
      ArrayList output = new ArrayList();
      foreach(IDictionary val in vals) {
        output.Add(IDictionaryToXmlRpcStruct(val));
      }
      return (XmlRpcStruct[]) output.ToArray(typeof(XmlRpcStruct));
    }

    [XmlRpcMethod]
    public override byte[] BeginGet(byte[] key) {
      return base.BeginGet(key);
    }

    [XmlRpcMethod]
    new public XmlRpcStruct ContinueGet(byte[] token) {
      return IDictionaryToXmlRpcStruct(base.ContinueGet(token));
    }

    [XmlRpcMethod]
    public override void EndGet(byte[] token) {
      base.EndGet(token);
    }

    [XmlRpcMethod]
    public override IDictionary GetDhtInfo() {
      XmlRpcStruct xrs = new XmlRpcStruct();
      xrs.Add("address", _dht.Node.Address.ToString());
      return xrs;
    }
  }
}
