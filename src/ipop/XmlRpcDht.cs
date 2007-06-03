
using System;
using CookComputing.XmlRpc;
using System.Runtime.Remoting;
using System.Text;
using System.Collections;
using Brunet;
using System.Diagnostics;
using Brunet.Dht;

namespace Ipop
{		
	/// <summary>
	/// Dht stub using XmlRpc protocol
	/// </summary>
    public class XmlRpcDht : MarshalByRefObject,IDht
	{			
        [NonSerialized]
        private FDht dht;

        public XmlRpcDht(FDht dht)
        {
            this.dht = dht;
        }
		
		[XmlRpcMethod] 
		public DhtGetResultItem[] Get(string key)
		{            
            Hashtable[] ht_res = DhtOp.Get(key, this.dht);
            ArrayList list = new ArrayList();
            foreach (Hashtable re in ht_res)
            {
                list.Add(new DhtGetResultItem(re));
            }
            return (DhtGetResultItem[])list.ToArray();
		}
		
		[XmlRpcMethod]
        public string Create(string key, string value, string password, int ttl) 
		{
            return DhtOp.Create(key, value, password, ttl, this.dht);
		}		
		
		[XmlRpcMethod]
        public string Put(string key, string value, string password, int ttl)
		{
            return DhtOp.Create(key, value, password, ttl, this.dht);
		}    
	}

    /// <summary>
    /// The XmlRpc interface for the Dht which is used on the client side
    /// </summary>
    public interface IXmlRpcDht : IDht, CookComputing.XmlRpc.IXmlRpcProxy
    {
        [XmlRpcMethod]
        new DhtGetResultItem[] Get(string key);
        [XmlRpcMethod]
        new string Create(string key, string value, string password, int ttl);
        [XmlRpcMethod]
        new string Put(string key, string value, string password, int ttl);
    }
}
